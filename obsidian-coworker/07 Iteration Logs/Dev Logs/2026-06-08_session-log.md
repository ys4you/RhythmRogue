# RhythmRogue Dev Log, 2026-06-08

A handoff log for the next chat. This session was the response to Playtest 1, four fixes (after-retry damage, an on-death crash, battle-start music + scroll-in, and miss timing) plus a small inspector tweak, and then a design-led feature off the same playtest: the guard mechanic that finally gives enemy notes teeth. Read the 2026-06-06 log first for the flat-HP, Gameplay settings tab, and constant-velocity scroll work this sits on top of.

---

## Quick status

- All bug-fixing off the first external playtest. Four things changed: the after-retry invincibility bug, the on-death crash, start-of-battle behaviour (music is now immediate and the opening notes scroll in), and miss timing (misses now register at the hit line, not a beat later). BattleManager's intro/end delays are now inspector sliders.
- The user confirmed gameplay feels right in-engine after these: damage works on a second run, battle-start music is immediate with notes sliding in, and misses land on the beat you missed. Treat all four as validated.
- The 2026-06-06 work (flat HP, Gameplay tab, constant-velocity scroll) and this session's four fixes plus both logs were committed and pushed. Still uncommitted: the guard mechanic (DamageConfig + DamagePipeline), the guard HUD indicator (BattleUI), and the scroll-speed cap raise (ScrollSpeedSetting). Commit messages for all three were provided in chat.
- NEW FEATURE this session: the guard mechanic, the first answer to the tester's "the enemy just exists." The enemy's auto-played notes now damage the player, but only while the guard is down: the guard starts up, a Miss drops it, and the next successful hit restores it. It has a HUD indicator (top-left GUARDED/EXPOSED badge, sprite-ready) so the state is legible. Built and wired into BattleScene; not yet validated in-engine for feel or balance-tuned. Full sections below.
- Remaining Playtest 1 backlog, not yet touched: the flat difficulty curve (Cultist feels like Acolyte, then Zealot is a wall), hold-note hit feedback, and the enemy/boss reading as decorative. Hit juice is still the big feel lever (already-built stack, see 06-06 log).

### Machine paths (call Filesystem:list_allowed_directories first each session)
- Laptop (active this session): C:\PersonalProjects\RhythmRogue
- Desktop: C:\Users\Computerpilot\Documents\Github\RhythmRogue
- Claude CANNOT run git or the terminal on the user's machine. The user commits, pushes, and runs the Python generator.

---

## Source: Playtest 1

A casual rhythm player ran the build. The base loop worked and the vibe landed; per-enemy music and the event mystery were the standouts, and they said they would replay and would pick it over FNF. The actionable items, ranked: a blocking after-retry bug, opening notes popping onto the receptor instead of scrolling in, hold notes giving no feedback, misses registering too late, a flat difficulty curve with a cliff at Zealot, and font legibility. This session cleared the first, second, and fourth of those, plus an on-death crash found while fixing them. The rest carry forward.

---

## BUG: no damage after a retry (was blocking)

**Symptom:** start a run, die, start another run, the player is invincible. Confirmed fixed in-engine ("I can get damage now in 2nd run").

**Cause.** HealthComponent has an `_isDead` latch: once HP hits 0, `TakeDamage` early-returns and `IsAlive` is false. PlayerHealth.ResetForNewRun calls `SetMaxHP(_maxHP, fillToMax: true)`, which refilled `_currentHP` to full but never cleared `_isDead`. So a fresh run after a death began at full HP yet still flagged dead, and both downstream guards bailed: DamagePipeline.ApplyPlayerDamage returns early on `!IsAlive`, and TakeDamage itself no-ops while `_isDead`. Net result, the player could not be damaged.

**Fix.** `Scripts/Battle/HealthComponent.cs`, in `SetMaxHP`, added `if (_currentHP > 0) _isDead = false;`. The invariant is that you cannot be dead with positive HP, so refilling revives. This fixes ResetForNewRun automatically and also protects any enemy pool that gets reused. A clarifying comment went on PlayerHealth.ResetForNewRun.

---

## BUG: crash on death (collection modified)

**Symptom:** an InvalidOperationException, "Collection was modified," on the miss that kills the player.

**Cause.** `NoteHighway.DespawnPassedNotes` fired `OnNoteMissedEvent` inside a `foreach` over `_activeNotes`. On the fatal miss that event ran synchronously all the way through: miss -> player death -> BattleManager.OnPlayerDied -> EndBattle -> the Playing-phase exit -> `_highway.ClearAllNotes()`, which calls `_activeNotes.Clear()` mid-enumeration, so the next loop step threw.

**Fix.** Never fire events while enumerating. DespawnPassedNotes classifies in a scan pass with no events, then fires in a second pass and recycles in a third, both using index loops that recheck `.Count` each iteration so a mid-loop teardown (ClearAllNotes emptying the lists) stops cleanly instead of double-recycling. This three-pass shape is now part of the miss-timing rework below; the re-entrancy safety is the load-bearing part, keep it.

---

## BATTLE START: music is immediate, opening notes scroll in

**Symptom:** "some notes appear and dont drop from off screen" at the start of a fight; the first notes sat on the receptor instead of falling into it.

**Cause.** The chart's first notes sat at the song's real beats, which start near beat 0, and the assembler's `leadInBeats` was metadata only (it padded TotalBeats but never moved a note or delayed audio). So at song start `currentBeat` was about 0 and a beat-0 note was positioned right at the receptor.

**Dead end, do NOT retry.** The first attempt was an audio count-in: delay the song and let the beat clock run negative so notes scrolled in during the silence. The user rejected it, the music has to start the instant the fight does. The whole count-in was reverted: Conductor.Play is back to immediate, IConductor back to two args, and UpdateTiming's pre-start early-out is restored.

**Fix that shipped (the FNF model).** The song plays immediately, and the runway comes from the chart having a short note-free intro: the assembler does not place notes before a lead-in beat, so the song's opening bars play while the first notes scroll down from off-screen. Notes after the intro stay at their real beats, so the chart still tracks the song's structure; only the opening onsets are dropped.
- `Scripts/Battle/PatternAssembler.cs`: `leadInBeats` is threaded into FillSide and StampFragment, and StampFragment skips any note whose beat is below the lead-in (judged off the head beat).
- `Scripts/Battle/ChartProvider.cs`: new `ComputeLeadInBeats(bpm)`. The runway is how long a note takes to cross the off-screen spawn distance at the current scroll speed, in beats: `ceil((DefaultSpawnAheadUnits / UnitsPerSecond) * bpm / 60)`. It is passed into Assemble. Scroll-speed dependent on purpose (a slower scroll needs a longer runway); about 3.6s at the default 5 u/s, and constant in real time across BPM.
- `Scripts/Battle/HighwayBase.cs`: exposes `public const float DefaultSpawnAheadUnits = 18f` (the `_spawnAheadUnits` field defaults to it) so ChartProvider can size the lead-in to the same distance notes spawn at. The temporary NoteTravelSeconds getter from the count-in attempt is gone.
- `Scripts/Core/Conductor/Conductor.cs` and `IConductor.cs`: reverted to immediate playback.

**Knobs.** If the intro feels long, lower `ComputeLeadInBeats` (or `_spawnAheadUnits`, which shortens every note's approach). The silent enemy-reveal pause before the song is `_introDelay` on BattleManager, now an inspector slider (below); set it to 0 for the song to start the very instant the fight does. The old lane-shape `ShapeAssembler` fallback path did NOT get the lead-in; enemies run through the pattern path so it does not matter in practice, but note it if that path is ever revived.

---

## MISS TIMING: misses register at the hit line

**Symptom:** "misses get known too late," the combo break and damage felt disconnected from the note you missed.

**Cause.** A note was only flagged missed once it had fallen `_beatsDespawnBehind` (2 beats) past the receptor, and the miss event fired there. At 120 BPM that is about a second after the note crossed the hit line, by which point it is well below the line or off-screen, so the combo break and the player damage landed late.

**Fix.** The miss judgment is decoupled from the despawn. A note is a confirmed miss the instant its hit window closes, `_missWindowMs` (110ms) past its head beat, and the miss fires there, at the line. The note keeps scrolling and is still recycled at the old 2-beat distance, so nothing changes visually; only the judgment moved earlier.
- `Scripts/Battle/NoteHighway.cs`: new `_missWindowMs` serialized field (default 110, under a Miss Timing header) and a `MissWindowBeats` helper that converts it per-frame (the ms window is BPM-independent reaction time). DespawnPassedNotes now marks newly-missed notes into a `_newlyMissed` list in the scan pass and fires them in the event pass; despawn still keys off `_beatsDespawnBehind`. Hold notes are now missed off their HEAD beat (you cannot start a hold once its head is past the window); previously an unhit hold missed off its end beat plus 2 beats, even later. ClearAllNotes clears the new list too, so the re-entrancy safety holds.
- Accuracy totals are unchanged: every unhit note still produces exactly one miss, only the moment it registers moved.

**Watch:** `_missWindowMs` (NoteHighway) and `_maxHitWindowMs` (NoteMatcher) are separate fields, both 110. If you ever retune the hit window, change both, or a note could vanish as a miss while NoteMatcher would still accept the press. Easy follow-up to wire the highway to read NoteMatcher's value so they cannot drift.

---

## Inspector: BattleManager timing sliders

`_introDelay` and `_endDelay` on BattleManager are now `[Range(0, 5)]` sliders with accurate tooltips, under the existing Timing header. `_introDelay` is the silent pause after the enemy appears and before the song; its old tooltip described a "song intro section" that no longer exists, so it now reads that 0 starts the song the instant the fight does. Both were already serialized; this just makes them easy to drag and documents them.

---

## ENEMY THREAT: the guard mechanic

This one is a feature, not a Playtest 1 fix, but it comes from the same tester note that the enemy "just exists." The root cause, found by reading the code: the EnemyHighway already auto-plays its notes as pure spectacle (receptor flash, no input, no damage), so the player was only ever fighting the chart, never the enemy. The enemy was a difficulty knob with a health bar.

**Decision.** Give enemy notes teeth through a "guard" model, chosen over call-and-response (too FNF, both turns are the same verb) and over dodge/defence phases (taking the attacking verb away reads as more stressful, not more fun). The guard keeps the player always attacking; the enemy's notes become a fair, telegraphed threat layered on top.

**How it behaves.**
- The player starts every battle guarded. The opening enemy notes always glance off, before the player has hit anything.
- A real Miss drops the guard. While it is down, each enemy note that reaches its receptor deals `enemyNoteDamage` (default 2) to the player.
- The next successful hit (Bad/Good/Perfect) raises the guard again, closing the window. Only a Miss drops it, so gaps in the player's own chart are safe: an empty stretch cannot restore the guard, but it cannot drop it either.

**Why it is fair (the bar it was held to).** Every enemy hit was telegraphed by the note scrolling in; the out is the main verb (hit your next note); the cost is small and self-correcting. The critical case the user raised, an enemy that holds the first notes of a battle, is handled by starting the guard UP: the player can never take damage they had no chance to prevent, including at the very top of the fight. Pair it with the charting habit of keeping the enemy quiet in the opening section as defence in depth.

**Implementation.** All in the damage path, nothing new spun up.
- `Scripts/Data/DamageConfig.cs`: new `enemyNoteDamage` field (default 2; set 0 to disable enemy attacks). This is the one balance dial.
- `Scripts/Battle/DamagePipeline.cs`: owns the guard. It already subscribed to judgments, held PlayerHealth, and had god mode plus the OnDamageDealt feedback event, so it was the cohesive home and enemy-note damage routes through the same path (god mode and damage juice cover it for free). A Miss calls SetGuard(false); any successful hit calls SetGuard(true). It subscribes to `EnemyHighway.OnAutoHit` and, only while the guard is down, applies `enemyNoteDamage` to the player. The guard resets up in OnEnable (battle scene objects are fresh per fight). State is exposed via `GuardUp` and `OnGuardChanged` for a future HUD piece.

**Wiring (done this session).** DamagePipeline has a new serialized `_enemyHighway` field; the scene's EnemyHighway is assigned to it on the DamagePipeline GameObject in BattleScene. If it is ever left empty the pipeline logs a warning on battle start and enemy notes go back to harmless.

**Pending on this feature.**
- In-engine validation of the feel and tuning of `enemyNoteDamage`: it stacks across a dense enemy phrase, so 2 is a cautious start and may want nudging once it is felt. God mode suppresses enemy-note damage, so test with it off.
- The guard now has a HUD indicator (next section), so it is legible. The combo counter remains a secondary proxy.

---

## GUARD HUD: the indicator

The guard had no visible state at first (only the combo break and the HP tick hinted at it), so it got a dedicated readout in BattleUI. It reads `DamagePipeline.GuardUp` / `OnGuardChanged`, which BattleUI already held a reference for, so there was no new wiring.

- Default (text mode): a small badge, bright gold **GUARDED** that flips to a dim amber **EXPOSED** the instant a Miss drops the guard, popping once on the transition to pull the eye. It shows GUARDED from the first frame even at zero combo, which is the start-guarded state the combo counter could not convey.
- Placement: top-left corner, mirroring the score top-right. It was first placed bottom-left by the HP, but the 200px text box overlapped the leftmost lane receptor, so it moved to the clear top-left. A compact icon could sit bottom-left in the margin if ever wanted; that is a one-line position change in `CreateUI`.
- Sprite-ready: BattleUI has optional `_guardUpSprite` / `_guardDownSprite` fields (Guard header). Assign BOTH and it switches to a swapping shield icon (intact vs broken) and hides the text box; leave either empty and the text is the fallback. The user is considering drawing two shields (intact + cracked); import them Point filter, no compression, on the warm palette, no red.

`Scripts/Battle/BattleUI.cs` only.

---

## SCROLL SPEED: cap raised

The player wanted faster scroll than the old ceiling allowed. `ScrollSpeedSetting.MaxSpeed` went 16 -> 40 u/s. The Gameplay tab field, the clamp, and the highways all read this const, so the higher ceiling propagates with no UI change (the field hint now reads "2 to 40 u/s"). Default (5) and min (2) unchanged. Safe by design: scroll speed is a constant velocity and notes still spawn a fixed distance out, so a higher value just moves them faster over the same runway, no pop-in. `Scripts/Data/ScrollSpeedSetting.cs` (plus a stale comment fixed in SettingsPanel).

---

## Pending / TODO (carry into next chat)

1. **Commit + push.** Three uncommitted changes from this session: the guard mechanic (DamageConfig.enemyNoteDamage, DamagePipeline guard state + EnemyHighway subscription), the guard HUD indicator (BattleUI), and the scroll-speed cap raise (ScrollSpeedSetting). Commit messages for each were given in chat; ask Claude for fresh ones if needed.
2. **Difficulty curve** is the top remaining Playtest 1 item: Cultist felt the same as Acolyte, then Zealot was a wall. Now that scroll speed and miss timing are fixed, that flat-then-cliff shape is the clearest signal and it is mostly tuning (DifficultyProfile, per-enemy markerDifficulty / density), not new systems.
3. **Hold-note feedback** (Playtest 1 bug 3) and the **hit juice** pass go together; the juice stack is already built (see 06-06 log item 4: palette off the no-red warm scheme, wiring check, SFX clips, tuning).
4. **Enemy / boss aliveness:** the guard mechanic (above), now with its HUD indicator, is the first concrete step; the enemy actually threatens the player and the state is legible. Still open: tuning enemyNoteDamage in-engine, deciding on the shield sprites, and the bigger one, making the boss feel interactive (Undertale-like was the strongest future signal).
5. **Protect:** per-enemy music and the event mystery were the strongest positives; do not regress them.
6. Carried from 06-06: retire the lane-shape path, per-note accent tags, Windows build, get to 3 testers.

---

## Architecture reminders (new or changed this session; 06-06 and earlier still hold)

- **The enemy damages the player only through the guard.** EnemyHighway auto-plays its notes (display only); DamagePipeline subscribes to its OnAutoHit and applies DamageConfig.enemyNoteDamage to the player only while the guard is down. The guard starts up each battle (reset in OnEnable), drops on a Miss, and is restored by the next successful hit. Enemy-note damage routes through DamagePipeline so god mode and the OnDamageDealt juice cover it. Requires the scene's EnemyHighway wired on the DamagePipeline GameObject. Guard state is on GuardUp / OnGuardChanged.
- **The guard's HUD readout lives in BattleUI**, driven by DamagePipeline.OnGuardChanged / GuardUp (BattleUI already references the pipeline, no extra wiring). Text mode by default (GUARDED gold / EXPOSED amber, top-left); assigning both `_guardUpSprite` and `_guardDownSprite` switches it to a shield icon and hides the text.
- **Scroll speed is capped at 40 u/s** (ScrollSpeedSetting.MaxSpeed), min 2, default 5. The Gameplay field, clamp, and highways all read these consts; change the const, not the UI. It is a constant world-units/second velocity, BPM-independent.
- **Misses fire when the hit window closes, not at despawn.** NoteHighway flags a note missed `_missWindowMs` past its head beat and fires there; the note despawns later at `_beatsDespawnBehind`. Judgment and despawn are separate. Hold misses key off the head beat.
- **The chart owns the opening runway, not the audio.** The song plays immediately; PatternAssembler leaves the first `ComputeLeadInBeats` beats note-free so the opening notes scroll in over the song's intro. Do NOT reintroduce an audio count-in, the user rejected delayed music. Lead-in is scroll-speed dependent and sized to `HighwayBase.DefaultSpawnAheadUnits`.
- **HealthComponent invariant: not dead while HP > 0.** SetMaxHP clears the `_isDead` latch whenever HP ends positive, which is what makes ResetForNewRun actually revive.
- **Never fire events while enumerating `_activeNotes`.** A fatal miss tears the battle down synchronously (death -> EndBattle -> ClearAllNotes). DespawnPassedNotes classifies first, then fires and recycles via index loops that recheck Count. Keep that shape.
- Standing reminders unchanged: SOLID, ScriptableObject configs, seeded determinism, code-generated UI (no prefabs), two-layer events, GameLog (Info/Warn/Error), Input System Only (Keyboard.current, no legacy UnityEngine.Input), beat detection and scroll are visual-only and never affect judgment, RhythmRogue.Core.Audio.AudioSettings must be fully qualified, warm 8-color palette with NO red. Working style: terse, iterative, tests in-engine, prefers direct edits and that Claude picks a direction and provides commit messages.
