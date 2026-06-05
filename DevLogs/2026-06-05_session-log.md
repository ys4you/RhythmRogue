# RhythmRogue Dev Log, 2026-06-05

A handoff log for the next chat. Covers everything done this session. Read this first.

---

## Quick status

- **Pattern assembler validated in-engine by the user at end of session** (their words: "feels better"). The human-feel chart rework is the new green baseline. The calibration fix, note-scaled enemy HP, and lane transforms all rode along in that same play test and are confirmed good by feel.
- This session replaced the heart of chart generation. The old path stamped one note per detected audio onset, so note TIMING came entirely from onset detection and always felt robotic. The new `PatternAssembler` places whole hand-authored rhythm fragments on a bar grid: the song analysis directs WHEN, WHICH, and HOW DENSE; the fragment supplies the actual notes. This is the long-standing "feels robotic" lever, now pulled.
- Three supporting fixes shipped with it: audio calibration was completely dead (the slider saved a value nothing ever read), enemies died in seconds on dense charts (flat HP versus combo-scaled damage), and Cultist followed the sparsest stem so it barely had notes.
- NOT yet committed. Commit message is in the chat for this session. After commit, the highest-value next target is hit juice (screen shake, particles, receptor punch, hitstop): the biggest remaining feel lever now that timing and note content are solved.

### Machine paths (call `Filesystem:list_allowed_directories` first each session)
- Laptop (active this session): `C:\PersonalProjects\RhythmRogue`
- Desktop: `C:\Users\Computerpilot\Documents\Github\RhythmRogue`
- Claude CANNOT run git or the terminal on the user's machine. The user commits, pushes, and runs the Python generator themselves (or Claude runs the generator on Claude's own env against uploaded files).

---

## THE CHART REWORK: PatternAssembler (NEW, validated)

**Diagnosis.** `ShapeAssembler` placed one lane per audio onset, and the authored `LaneShape` data described only LANES, never timing. So every note's position in time was the onset detector's, not a human's. That is the root of "fully algorithmic placement always feels robotic." The fix matches the GDD / HybridAssembler principle exactly: the algorithm decides when, which, and how dense; a human decides the actual notes. Analysis is the DIRECTOR, fragments are the CONTENT.

**New files:**

- `Scripts/Data/NotePattern.cs` , a `ScriptableObject` fragment. Nested `[Serializable] struct Note { offsetBeats, lane (0-3), holdBeats }`. Fields: `patternName`, `familyId`, `lengthBeats` (default 4), `List<Note> notes`, `difficulty` (1-10), `tags` (ShapeTag), `weight`. Derived: `NoteCount`, `Density` (notes/beat), `StartLane`, `EndLane`, `EffectiveFamily`, `TransitionDistance`, `HasTags`. CreateAssetMenu "RhythmRogue/Note Pattern".
- `Scripts/Data/NotePatternLibrary.cs` , a `ScriptableObject` holding `List<NotePattern> patterns` with `Query(maxDiff, ShapeTag, results)` and `QueryAll(maxDiff, results)`. CreateAssetMenu "RhythmRogue/Note Pattern Library".
- `Scripts/Editor/StarterPatternGenerator.cs` , menu **RhythmRogue > Generate Starter Note Patterns**. Writes 14 fragments plus `StarterPatternLibrary.asset` to `Assets/_Project/SCO/Patterns/`. Update-in-place on re-run (`CopySerialized`) so GUIDs survive. Fragments (lane 0=L, 1=D, 2=U, 3=R): Pulse Walk, Backbeat, Open Hold (easy); Stair Up, Stair Down, Trill DU, Gallop, Sync Skip (medium); Stream, Burst, Jacks U, Jumps (dense); Chaos Sync, Hold Stream (hard). Densities 0.5 to 2.5.
- `Scripts/Battle/PatternAssembler.cs` , the centerpiece. Static, main-thread only (reused buffers), matching ShapeAssembler's threading model.

**How the output reads as hand-made** (the principles in the file header):
1. **Follow one instrument at a time.** With a stemmed beat map each section locks to a layer (verse/intro/outro: drums, bridge: bass, chorus: melody, drop/break: everything). A blended average of all the audio is what sounds like noise. A non-`All` `chartInstrument` on the enemy forces one layer for the whole fight.
2. **Dynamics track structure.** Per-bar density comes from how many onsets the song actually has in that bar, scaled by the section `intensityScale`. Quiet verses, busy choruses, real rests where the music rests (a zero-density bar is skipped, not filled).
3. **Repeat where the song repeats.** The fragment pick is seeded by musical POSITION (`Fork($"{(int)secType}:{bar}:{sub}:{bucket}")`), not a running sequence, so two choruses that land on the same density at the same bar draw the same fragments. Recognition reads as intent.
4. **Chart a clean version of the real rhythm.** Each bar picks the fragment whose density best matches that bar's onsets, scored `weight / (1 + 3 * |Density - target|)`, with a family-recency penalty so it does not lean on one shape.
5. **Flow in the hands, with variety.** See lane transforms below.

**Per-side structure.** Player and enemy each get their own RNG fork (`pattern_player`, `pattern_enemy`) and their own `PickContext` (previous exit lane + family recency). `globalMaxDiff = clamp(ceil(difficulty * 10), 1, 10)` caps how hard fragments may be; per-bar `maxDiff` lerps from 2 up to that cap by density bucket.

**Wiring:**
- `Scripts/Battle/ChartProvider.cs` , added `[SerializeField] NotePatternLibrary _defaultPatternLibrary`. `ResolveFromBeatMap` now routes to `PatternAssembler` (passing UNFILTERED markers plus `enemy.chartInstrument`) when a library is present (`enemy.patternLibrary ?? _defaultPatternLibrary`, non-null and non-empty); result mode `"pattern"`. Otherwise it falls back to `FilterByInstrument` plus `ShapeAssembler` (mode `"beat-map"`). Console logs `[PatternAssembler]` versus `[ShapeAssembler]` so you can see which path ran.
- `Scripts/Data/EnemyData.cs` , added `public NotePatternLibrary patternLibrary` under `[Header("Chart")]`, before `shapeLibrary`.

**To activate** (done this session, kept here for the next chat): run **Generate Starter Note Patterns**, assign `StarterPatternLibrary` (in `SCO/Patterns`) to ChartProvider's **Default Pattern Library** field in BattleScene (flips every enemy at once), or assign per-enemy. With no library assigned, an enemy stays on the old ShapeAssembler.

---

## Lane transforms: mirror plus rotation (NEW, in PatternAssembler)

[DECISION, user-driven] The user asked whether fragments could rotate across lanes (a wrap-around shift), e.g. a `0,1,2,3` staircase becoming `1,2,3,0`. Yes, and it is a real charting move. The earlier mirror-only decision is now a general lane transform.

- `LaneTransform` (private readonly struct): an optional left/right `Mirror` then a `Shift` of 0-3, applied identically to every note. `Apply(lane) = ((Mirror ? 3 - lane : lane) + Shift) & 3`. Shape preserved, only its board position moves.
- `ChooseTransform(frag, previousEndLane, rng)` scores all 8 variants (mirror by shift) by `TransformWeight = 1 / (1 + 2 * flowCost)`, where `flowCost = |transformedStart - previousEndLane|`. So it favors transforms whose first lane sits near the previous fragment's exit (smooth hand flow), but jumps stay possible. First fragment of a side has no exit lane, so all 8 are equally likely (max variety).
- Drawn from the same slot-keyed `pickRng` as the fragment pick (second draw from that fork), so it is deterministic for a seed.
- **Whole-fragment only.** The lane-to-key contract never changes WITHIN a fragment, only between fragments. That is the line between "varied" and "unreadable," and we stay on the readable side.

**Known behavior worth watching.** Because the transform reads `previousEndLane` (continuous state), two repeated choruses now draw the same FRAGMENTS (the rhythmic motif still repeats) but may land on slightly different lanes. That is intended variety on top of the motif. If repeats end up feeling too divergent, seed the transform purely on the slot key instead.

**Known caveat, not yet modeled.** A rotation is not difficulty-neutral the way a mirror is: an inner `1,2` trill rotated by +2 becomes a `3,0` outer jump-trill, which is harder. The flow score only looks at the start lane, not the fragment's internal span. If a specific fragment feels harder when rotated, add a per-fragment "no rotate" flag (easy follow-up).

---

## Calibration offset was completely dead (FIX)

**Symptom:** "doesn't feel like I press on beat," and the Audio Offset slider did nothing.

**Root cause:** the slider saved to PlayerPrefs `"audioOffset"`, but nothing ever read it. The Conductor's `_songOffset` was applied only to `SongPositionInSeconds`, while ALL gameplay (NoteHighway spawn/scroll/despawn, NoteMatcher hits) runs on `SongPositionInBeats`. So there was no working calibration anywhere.

**Fixes:**
- `Scripts/Core/Audio/AudioSettings.cs` , added `CalibrationOffsetMs` (PlayerPrefs `"audioOffset"`, clamp +/-200, default 0) and `CalibrationOffsetSeconds`.
- `Scripts/Core/Conductor/Conductor.cs` , added `_calibrationSeconds`, snapshotted at Play from `RhythmRogue.Core.Audio.AudioSettings.CalibrationOffsetSeconds`. New `AdjustedDspTime() = dspTime - totalPaused - (_songOffset + _calibrationSeconds)`, used in `UpdateTiming` and the `SetBPM` origin. Positive offset = notes later (you were hitting early); negative = earlier.
- `Scripts/UI/.../SettingsPanel.cs` , slider reads/writes `AudioSettings.CalibrationOffsetMs` live, range widened to +/-200, legacy raw-PlayerPrefs save removed from `Close()`.

Visual and hit timing are consistent because both NoteHighway and NoteMatcher are on `SongPositionInBeats`. The offset applies at song start.

---

## Enemy HP from note count (FIX)

**Symptom:** "enemies die too fast, I only hear the first few seconds."

**Why:** damage is `baseJudgment x comboMult` (Perfect 5, combo up to 3.0x at combo 20), so a capped combo deals about 15 per note. 100 HP dies in roughly 12 notes, which is seconds on a dense chart.

**Fix** (`Scripts/Battle/BattleManager.cs`): added `[Header("Balance")] _enemyHpPerNote = 8f`, `_bossHpMultiplier = 1.5f`. `InitializeBattle` counts player notes (`BattleChart.PlayerNoteCount` or `LegacyChart.NoteCount`) and `ComputeEnemyHP(n) = round(n x _enemyHpPerNote x (isBoss ? bossMult : 1))`. Elite scaling still stacks on top. Fights now last roughly the length of the song; `maxHP` is only a fallback when note count is unknown. These fields are code defaults, not yet written into the BattleScene YAML, so tune the prefab/scene values.

**Tradeoff to watch:** longer fights make miss damage (5 to the player, 100 HP) bite harder. Player survivability is a separate lever.

---

## Cultist barely had notes (FIX)

`Cultist.asset` had `chartInstrument = 2` (Bass), the sparsest stem, so it followed only the bassline. Changed to `0` (All). In PatternAssembler, difficulty governs how HARD fragments may be, not how MANY notes there are; note count comes from onsets and instrument choice. If All feels too busy for Cultist, Drums (1) is dense but clean.

---

## Enemy to song mapping (carry forward, with pattern library)

| Enemy | bpmMod | diff | maxHP (fallback) | Beat map | chartInstrument | Pattern library |
|---|---|---|---|---|---|---|
| Acolyte | 0.85 | 0.3 | 80 | Stellar Saga (`6cb23b73`) | All | default (via ChartProvider) |
| Cultist | 1.0 | 0.4 | 100 | Neuro-Synth (`57f92667`) | All (was Bass, fixed this session) | default |
| Zealot | 1.25 | 0.6 | 90 | Crash Runner (`b571f5ec`) | Melody | default |
| High Priest (boss) | 1.1 | 0.8 | 250 | Obsidian Overdrive (`96e7fe91`) | All | default |

`maxHP` is now only used if note count is unknown; real enemy HP is derived per battle from player note count. Per-enemy `patternLibrary` is `{fileID:0}` on all four, so they resolve through ChartProvider's Default Pattern Library. Audio at `Resources/Audio/Songs/` as `Stellar Saga.mp3`, `Neuro-Synth.mp3`, `Crash-Runner.mp3`, `Obsidian Overdrive.mp3`. Beat map assets in `Assets/_Project/Data/BeatMaps/`. JSONs in `python/`.

---

## Generator commands (unchanged from 2026-06-04)

```
cd C:\PersonalProjects\RhythmRogue\python
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Stellar Saga.mp3" --bpm 99 --sensitivity 0.4 --output stellar_saga_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Neuro-Synth.mp3" --bpm 117 --sensitivity 0.5 --output neuro_synth_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Crash-Runner.mp3" --bpm 144 --sensitivity 0.6 --output crash_runner_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Obsidian Overdrive.mp3" --bpm 161 --sensitivity 0.6 --output obsidian_overdrive_beatmap.json
```
- Stems are ON by default (writes Drums/Bass/Melody tags). `--no-stems` for the old blended path. Needs `librosa soundfile numpy scipy`.
- **Stems matter for PatternAssembler too:** with stems, `SectionInstrument` gives each section its own layer (drums verse, melody chorus, and so on). Without stems it returns `All` everywhere, which still works, just with no per-section instrument variety. Regenerate with stems to get the full effect, then re-import via **RhythmRogue > Import Beat Map JSON** (the menu, not a Unity Reimport of the `.asset`). Each import should log `Clip: <name>`.

---

## Pending / TODO (carry into next chat)

1. **Commit + push** this session (message in chat: pattern assembler, NotePattern data model, starter generator, lane transforms, calibration fix, note-scaled HP, ChartProvider routing, this log).
2. **Tune the two open levers by feel:** `_enemyHpPerNote` on BattleManager in BattleScene (lower = longer fights), and the Audio Offset slider (Settings > Audio). Both validated as working; values are starting points.
3. **Deferred chart refinements:**
   - Accent alignment (jumps on crashes, holds on sustains). Needs a per-note accent tag on `NotePattern`. Header already notes this.
   - Sparse-tier pool is thin (few low-difficulty fragments). Author more if quiet sections feel repetitive.
   - Rotation internal-difficulty not modeled (see caveat above). Add a per-fragment "no rotate" flag if any fragment feels harder rotated.
   - Per-occurrence salt if repeated choruses ever feel too similar OR too divergent.
4. **Retire the lane-shape path** (`LaneShape`, `ShapeAssembler`) once PatternAssembler is fully trusted, and **move `ShapeTag` to its own file**: `NotePattern` depends on `ShapeTag`, which still lives in `LaneShape.cs`. Do this only after the shape path is gone from the battle flow.
5. **Hit juice.** `HitFeedback.cs` / `HitParticles.cs` exist but were never assessed (screen shake, particles, receptor punch, hitstop). Biggest remaining feel lever now that timing and content are solved. NEXT code target.
6. **Event/lifecycle audit** , subscribe/unsubscribe symmetry across systems. Low-risk hardening.
7. Carry-overs from horizon: save system, events system, relic viewing screen, real relic art, battle background.
8. **Recurring:** cut a Windows build, get to 3 testers (GDD success criterion; every precondition is met).

---

## Architecture reminders (unchanged unless noted)

- SOLID, ScriptableObject configs, seeded determinism (`SeededRandom` / `ISeededRandom`), code-generated UI (no prefabs), two-layer events (C# plus EventBus), `GameLog` (Info/Warn/Error, NOT Warning), `[Conditional]` so zero release overhead.
- **Battle chart path now goes through `PatternAssembler`** when a `NotePatternLibrary` is assigned (`enemy.patternLibrary`, else `ChartProvider._defaultPatternLibrary`); otherwise it falls back to `ShapeAssembler`. Console logs `[PatternAssembler]` vs `[ShapeAssembler]`. (New this session.)
- **Note COUNT comes from song onsets x section `intensityScale`, NOT from enemy difficulty** (difficulty only caps how hard the chosen fragments may be). To change density: instrument choice (Bass sparsest, Drums dense and clean, All busiest and smart-per-section), `intensityScale`, regen `--sensitivity`, or `_enemyHpPerNote` for fight length. (New this session.)
- **Lane transforms are whole-fragment only.** The lane-to-key contract changes between fragments, never inside one. (New this session.)
- **Calibration offset is applied at song start.** Conductor snapshots `RhythmRogue.Core.Audio.AudioSettings.CalibrationOffsetSeconds` into `AdjustedDspTime`; all gameplay reads `SongPositionInBeats`. Positive = notes later (you were hitting early). (New this session.)
- **Enemy HP is derived from player note count**, `round(notes x _enemyHpPerNote x bossMult)`; `maxHP` is a fallback. Watch player survivability as fights get longer. (New this session.)
- **Active Input Handling = "Input System Only"** , legacy `UnityEngine.Input` is DEAD; read `Keyboard.current` etc. under `#if ENABLE_INPUT_SYSTEM`.
- **Generator/importer enum contract is via STRINGS** , importer DTO enum fields must be string plus `ParseEnum`, never int.
- **Beat map owns its audio** , `SongBeatMap.clip`, auto-linked by name on import; no enemy-side song fallback.
- **Importer updates assets in place** , preserves GUID; never delete and recreate a referenced asset.
- Data layer is deliberately DOP-leaning: `StampedNote` / `NotePattern.Note` are `readonly struct`, `BeatMarker` / `SongSection` are structs in contiguous lists, transformers (`PatternAssembler`, `ShapeAssembler`, `RuntimeBeatAnalyzer`) are static functions over arrays. NO ECS/DOTS rewrite, wrong scale, no perf need.
- `RectTransform` has no `SetActive` / `activeSelf`, use `.gameObject`. `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings`, fully-qualify.
- Palette: BgDeep #150C21, BgSurface #332232, BgLight #703C67, Shadow #5A2D2A, RustOrange #B15B35, AmberOrange #EAA44A, WarmGold #F0CE6E, OffWhite #FCF8F4. NO red. Dark/occult/gothic (Castlevania to Inscryption), NOT FNF-styled.
- Working style: terse, iterative, tests in-engine and reports back; prefers full file rewrites but minimal output when simple; decision fatigue, so Claude picks a direction and provides commit messages.
