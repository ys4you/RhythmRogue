# RhythmRogue Dev Log, 2026-06-06

A handoff log for the next chat. Covers everything done this session. Read this first. For the human-feel pattern assembler, calibration fix, and lane transforms, see the 2026-06-05 log; this session builds on top of that validated baseline.

---

## Quick status

- Three things shipped this session: enemy HP was reworked from note-scaled to flat authored values, a new Gameplay settings tab was added (scroll direction + scroll speed), and scroll speed was changed from a BPM-relative multiplier to a constant BPM-independent velocity.
- Up/down scroll is confirmed working in-engine by the user. The flat-HP rework and the constant-velocity scroll change landed after that and need a recompile plus a play to confirm feel. All three are code-complete and reasoned to compile.
- NOT committed. Commit message owed once the user confirms it compiles and feels right.
- Next big lever is still hit juice, but with an important correction from this session: the juice stack is already largely built (see TODO), so that work is palette + wiring + SFX + tuning, not building from scratch.

### Machine paths (call `Filesystem:list_allowed_directories` first each session)
- Laptop (active this session): `C:\PersonalProjects\RhythmRogue`
- Desktop: `C:\Users\Computerpilot\Documents\Github\RhythmRogue`
- Claude CANNOT run git or the terminal on the user's machine. The user commits, pushes, and runs the Python generator.

---

## FLAT ENEMY HP (replaces note-scaled HP)

**Symptoms that drove this:** "when enemies have 1 HP it still feels like ages until dead" and "even when enemy has 0 HP it does not die instantly." Earlier in the arc the opposite was true (enemies died in the first few seconds), which is what had pushed HP to note-scaling in the first place. Note-scaling overcorrected.

**Diagnosis.** The death logic was already correct: `HealthComponent.TakeDamage` fires `OnDeath` the moment HP hits 0, and `BattleManager` ends the battle immediately. The real causes were pool size and presentation. Note-scaled HP (`notes x 8`, x1.5 for bosses) created huge pools, so a single hit moved the bar a fraction of a percent (on a boss, one Perfect at full combo is about 0.4% of the bar), and the enemy bar had no number, so a 12% sliver read as "basically dead" when it was still many notes away. That gap between "looks dead" and "is dead" was the "ages at 1 HP" feeling, and "0 HP" was really the bar looking empty at a few percent.

**Changes:**
- `Scripts/Data/EnemyData.cs` , added an explicit `public bool isBoss` field and changed `IsBoss => isBoss`. The old `IsBoss => maxHP >= 250` heuristic is gone. That heuristic is what made a high-HP normal enemy register as a boss (see bug below).
- `Scripts/Battle/BattleManager.cs` , `InitializeBattle` now uses `int enemyHP = Mathf.Max(1, _currentEnemy.maxHP)` plus elite scaling. Removed `ComputeEnemyHP`, the `_enemyHpPerNote` field, and the `_bossHpMultiplier` field. Added `LogKillTiming()` (called from `OnEnemyDied`), which logs the beat and percent-of-song at the kill plus the enemy's maxHP. That is the tuning instrument: aim for the kill to land around 80% for an average player.
- `Scripts/Battle/BattleUI.cs` , the enemy HP bar now shows a real number centered on it (`current/max`), mirroring the player's readout, so empty now reads as dead.
- Enemy assets (`Assets/_Project/SCO/Enemy/Enemies_data/`): Acolyte 300, Cultist 450, Zealot 550, High Priest 1000, with `isBoss: 1` only on High Priest. These were `maxHP: 0` on three of four on disk (the 550 the user set on Zealot lived only in memory), which under flat HP would have clamped to 1 HP and died instantly, so setting real values was required.

**Bug fixed (the "Zealot became a boss"):** the user set Zealot's maxHP to 550 in the Inspector, which tripped the old `maxHP >= 250` heuristic, so Zealot showed the boss label and got the 1.5x boss HP multiplier on top of its note-scaled pool. That is why 300+ notes barely dented it. The explicit `isBoss` flag plus removing the multiplier fixes both.

**Tuning guidance.** Damage is roughly 7 per note for a casual player who breaks combo and about 12 to 14 holding combo, so the starting HP values give roughly 25 to 45 notes for the Acolyte up to 80 to 140 for the boss. Watch the kill-% log and aim around 80%. Values lean deliberately short because 300+ notes felt far too long. If an enemy ends too early, raise its maxHP; if the song ends with the enemy alive (which is a loss), lower it.

**Two things to watch.** First, smaller pools make every offense relic swing much harder, since a flat damage bonus is now a real slice of the bar, so the damage and combo relics will likely need toning down once HP is locked. Second, flat HP means a fight lasts a fixed number of notes but a variable number of seconds, so a fast, dense song (Zealot on Crash Runner) feels shorter in real time than the Acolyte at the same HP. Nudge a fast enemy's HP up if it ends too quickly.

---

## GAMEPLAY SETTINGS TAB (new)

A fourth Settings tab, ordered Audio | Gameplay | Controls | Display. It lives in the shared `SettingsPanel`, so the main menu and the in-run pause menu both get it with no duplication.

- **Scroll Direction** , a toggle showing "Down" or "Up." The highways already re-read `ScrollDirectionSetting` every frame (even while paused), so flipping this updates an in-progress battle the instant it changes, and the menu setting carries into the next battle. The up/down rendering was already fully built in the highways (mirror around the camera, live note re-placement); the only thing missing was UI to change it.
- **Scroll Speed** , a typed float field, not a slider (the user found the slider fiddly to land on an exact value). Commits on Enter or focus-loss, clamps to range, and reflects the stored value back so junk or out-of-range input visibly corrects itself. Accepts both comma and dot decimals for the NL locale. Moved here off the Audio tab.
- Added `HighlightTab` and `WireTabRow` private helpers in `SettingsPanel` to centralize the now-four-tab coloring and navigation instead of repeating it per tab.

Files: `Scripts/UI/SettingsPanel.cs`. The panel is fully code-generated, so there is no scene wiring to do.

---

## CONSTANT-VELOCITY SCROLL SPEED (changed semantics)

**Symptom:** "speed does differ, but I want it to go on how many units per second it moves." The old scroll speed was a BPM-relative multiplier: travel was `beatHeight x multiplier` world units per beat, so the actual on-screen speed was `that x BPM/60` units per second. Since every enemy has a different BPM, the same setting scrolled at a different real speed per song.

**Change.** Scroll speed is now a constant velocity in world units per second, BPM-independent (the "CMod" model from osu!mania / Etterna). The highway derives units-per-beat from it: `EffectiveBeatHeight => UnitsPerSecond x 60 / CurrentBpm`, so the on-screen speed comes out the same regardless of tempo.

- `Scripts/Data/ScrollSpeedSetting.cs` , the property is now `UnitsPerSecond` (renamed from `Multiplier`). Default 5, range 2 to 16, step 0.5, with `Min`/`Max` getters. It uses a NEW PlayerPrefs key (`RhythmRogue_ScrollUnitsPerSecond`) on purpose, because the old key stored a 0.5 to 3.0 multiplier that would load as a nonsensical velocity. Existing users reset cleanly to the default.
- `Scripts/Battle/HighwayBase.cs` , new `EffectiveBeatHeight`, a `CurrentBpm` helper (safe fallback of 120 before the conductor starts, to avoid divide-by-zero), and a `SpawnAheadBeats` helper. The spawn lead changed from a fixed number of beats (`_beatsShownInAdvance`, 8) to a fixed WORLD DISTANCE (`_spawnAheadUnits`, 18). With a constant velocity, a beat-based lead would make notes pop in on-screen at low speeds / high BPM; a fixed distance prevents that and makes read time (distance / speed) consistent. The old `_beatHeight` field is kept but no longer sets speed (legacy, marked as such).
- `Scripts/Battle/NoteHighway.cs` and `Scripts/Battle/EnemyHighway.cs` , both spawn loops now use `currentBeat + SpawnAheadBeats`.
- `Scripts/UI/SettingsPanel.cs` , the Gameplay field reads/writes `UnitsPerSecond`, shows the value as e.g. "5.0", and the placeholder reads the live range ("2 to 16 u/s").
- `Scripts/Tests/ChartGymController.cs` (dev tool) , updated its `_scrollSpeed` range/tooltip and its references from `Multiplier` to `UnitsPerSecond`.

**Fairness is untouched.** `NoteMatcher` judges purely on beat/time (`currentBeat` vs `note.Data.BeatPosition`, converted to ms via `SecPerBeat`), with zero reference to world position. Scroll speed and direction are visual only and cannot affect judgment. This was confirmed by reading NoteMatcher before changing positions.

**Reference point for re-tuning:** under the old system the user's "1.0x" was landing around 2.8 u/s on the Acolyte up to about 6 u/s on the Zealot, so the new default of 5 sits in that range and will read a touch faster on slow songs, about the same on fast ones.

**Caveat for the future, not relevant now:** if a boss ramps BPM mid-song, the constant-velocity formula recomputes each frame, so pixel speed stays constant (intended), but already-spawned notes re-space in the single frame the BPM changes. The current four enemies have fixed BPM, so this does not apply yet.

---

## Pending / TODO (carry into next chat)

1. **Recompile in Unity and verify:** all four enemies show their new HP and only High Priest reads as a boss; the Gameplay tab shows and works; scroll feels right at the new units/sec (it now scrolls the same speed on every enemy); the kill-% log lands near 80%.
2. **Commit + push** (message owed: flat HP, explicit isBoss, enemy HP number + kill log, the Gameplay settings tab, constant-velocity scroll speed, this log).
3. **Tune:** per-enemy `maxHP` toward an ~80% kill, scroll speed by feel, then rebalance the offense relics for the now-smaller HP pools.
4. **Hit juice** is the next big feel lever, but reassessed this session: `HitFeedback.cs`, `HitParticles.cs`, and `ReceptorAnimator.cs` are already a complete, well-built stack (floating judgments, per-judgment particles, camera shake, receptor beat-pulse / press-pop / hit-glow, SFX routing). The real work is (a) palette: the juice hardcodes saturated arcade colors including red, which is off-brand for the no-red warm palette, and the colors are duplicated across three files; (b) confirm it is wired in BattleScene (several SerializeField refs); (c) the hit SFX are routed but silent until clips are added to the SfxLibrary; (d) tune intensities. Hitstop is genuinely absent but is the most questionable addition for a rhythm game, so treat it as optional.
5. **Retire the lane-shape path** (`LaneShape`, `ShapeAssembler`) and move `ShapeTag` to its own file once PatternAssembler is fully trusted (`NotePattern` depends on `ShapeTag`, which still lives in `LaneShape.cs`).
6. **Deferred chart refinements:** per-note accent tags (jumps on crashes, holds on sustains), more sparse-tier fragments, a per-fragment "no rotate" flag if any rotated fragment feels harder.
7. **Recurring:** cut a Windows build, get to 3 testers. The user plans to playtest with siblings; the watch-don't-coach guidance from this session applies.

---

## Architecture reminders (new or changed this session; older ones in prior logs still hold)

- **Enemy HP is flat authored `EnemyData.maxHP`** (plus elite scaling), NOT derived from note count. maxHP is the real value now, not a fallback.
- **Boss-ness is the explicit `EnemyData.isBoss` flag** (plus `NodeType.Boss` in a real run). NEVER infer boss from HP; that heuristic caused a real bug.
- **Scroll speed is a constant velocity in world units per second** (`ScrollSpeedSetting.UnitsPerSecond`), BPM-independent. The highway converts it with `x 60 / BPM`. Spawn lead is a fixed world distance (`_spawnAheadUnits`), not a beat count. The old `_beatHeight` no longer sets speed.
- **Highways self-sync scroll direction AND speed every frame, even while paused.** Changing either setting applies live mid-battle with no extra wiring; `SyncScrollDirection` runs before the play/pause early-return in both highways' Update.
- **Hit detection is purely beat/time based** (`NoteMatcher`). Scroll speed and direction are visual only and never affect judgment or fairness.
- **One shared `SettingsPanel`** serves both the main menu and the pause menu, with tabs Audio | Gameplay | Controls | Display. Typed numeric fields normalize comma to dot so they parse on any locale.
- Standing reminders unchanged from prior logs: SOLID, ScriptableObject configs, seeded determinism, code-generated UI (no prefabs), two-layer events, `GameLog` (Info/Warn/Error), Input System Only (`Keyboard.current`, no legacy `UnityEngine.Input`), generator/importer enum contract via strings, beat map owns its audio, importer updates in place to preserve GUIDs, DOP-leaning data layer with no ECS rewrite, `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings` must be fully qualified, warm 8-color palette with NO red.
- Working style: terse, iterative, tests in-engine and reports back, prefers direct edits and that Claude picks a direction and provides commit messages.
