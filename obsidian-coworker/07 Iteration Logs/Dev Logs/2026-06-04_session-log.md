# RhythmRogue Dev Log — 2026-06-04

A handoff log for the next chat. Covers everything done this session. Read this first.

---

## Quick status

- **Game loop confirmed working in-engine by the user at end of session** (their words: "gameloop works"). This is the green baseline.
- This session was almost entirely chart/audio plumbing: a critical beat-map import bug, an instrument-following chart feature, a song-ownership rework, and a senior code-quality pass. Almost no gameplay/UI changes.
- Biggest single thing: the importer was silently dropping ALL player notes (read on as a "long intro"). Root cause found and fixed. That bug had been masking everything.
- Highest-value next step in code is **`ShapeAssembler` chart feel** (the recurring "feels robotic" lever), but it needs the user playing it. Recurring non-code recommendation from last session still stands: an external playtest (3 testers, GDD criterion).

### Machine paths (call `Filesystem:list_allowed_directories` first each session)
- Laptop (active this session): `C:\PersonalProjects\RhythmRogue`
- Desktop: `C:\Users\Computerpilot\Documents\Github\RhythmRogue`
- Claude CANNOT run git or the terminal on the user's machine. User commits/pushes and runs the Python generator themselves (or Claude runs the generator on Claude's own env against uploaded files).

---

## THE BIG BUG: beat map import was reading enums as ints (FIXED)

**Symptom:** battles felt like they had an endless intro. Actually they had ZERO player notes for the whole song.

**Root cause:** `BeatMapImporter` declared section `type`/`highway` and marker `type` as `int` in its JSON DTO, but the Python generator writes enum-NAME STRINGS (e.g. `"PlayerOnly"`, `"Snare"`). `JsonUtility` couldn't parse a string into an int field, so it silently left them at `0` — meaning every section imported as `EnemyOnly` and every marker as `Kick`. EnemyOnly sections emit no player notes, so the player never got a single note. It was masked because the beat-map path was active and `_useLegacyChart` is off.

**Fix:** added `ParseEnum<T>(string, fallback)` (case-insensitive, numeric fallback) to the importer; changed the DTO fields `type`/`highway` from int to string; added `instrument` and `songName` strings. Verified the enum names line up: `SongSectionType {Intro,Verse,Chorus,Bridge,Drop,Break,Outro}`, `SectionType {EnemyOnly,PlayerOnly,Both}`, `MarkerType {Kick,Snare,HiHat,Melodic,Accent,...}`.

**Lesson (carry forward):** the generator and importer share an enum contract via STRINGS. Any new enum field must be a string in the importer DTO and go through `ParseEnum`, never a raw int.

---

## Instrument-following charts (NEW FEATURE, DONE)

[DECISION] User wanted to pick an instrument in Unity and have the chart follow it, changeable in the Inspector (not a pre-build flag). Stem SEPARATION can't happen live in Unity, so it's precomputed offline in Python; every stem's onsets are baked into one JSON, tagged per marker; Unity picks which stream drives the chart. Lanes still come from hand-authored shapes (respects the HybridAssembler principle: the algorithm chooses when/which pattern, never where individual notes go).

[DECISION] Chose **HPSS** (librosa, no new deps) giving Drums/Bass/Melody over Demucs (deferred: named stems but heavy).

- NEW `Scripts/Data/ChartInstrument.cs` — enum `{ All, Drums, Bass, Melody }`. `All` = selector "use every marker"; markers are tagged Drums/Bass/Melody.
- `Scripts/Data/BeatMarker.cs` — added `ChartInstrument instrument` field + ctor param (defaults `All`, back-compatible).
- `Scripts/Data/EnemyData.cs` — added `ChartInstrument chartInstrument = All` (Inspector enum, full tooltip). Change freely, no regeneration needed.
- `Scripts/Battle/ChartProvider.cs` — `public static List<BeatMarker> FilterByInstrument(markers, instrument)`. `All` passes through; if a selection yields zero markers (untagged/old beat map, or an empty stem) it returns ALL markers and warns, so the chart never silently goes empty.
- `python/beatmap_generator.py` — FULL REWRITE. HPSS + a scipy butterworth low/high split of the harmonic band → Drums (percussive) / Bass (low harmonic) / Melody (high harmonic); per-stem onset detection tagged with its instrument. `--no-stems` keeps the old blended path (everything tagged `All`). Writes `instrument` per marker, a `stems` flag, `songName`, `totalBeats`.

**Note:** Zealot's `chartInstrument` is set to `Melody` (3). For that to actually do anything the beat map must be regenerated with the NEW stem generator. If the user only re-imported the OLD (no-stem) JSONs to get the loop working, Melody currently falls back to all-instruments WITH a console warning. Regenerating unlocks the feature. Not blocking.

---

## Song ownership moved onto the beat map (REWORK, DONE — partial revert of an earlier mid-session change)

[DECISION, user-driven] The beat map owns its audio, NOT the enemy. A beat map and its clip are one generated unit (markers were detected from that exact file), so they should travel together as a single swappable reference. This is what the original code intended (the `song` field was already labelled a fallback); the missing piece was that the importer never populated the clip.

- `Scripts/Data/SongBeatMap.cs` — `public AudioClip clip;` (lives here now). Kept `totalBeats` (stored, used by `TotalBeats` instead of `clip.length` so it's correct before the clip loads).
- `Scripts/Editor/BeatMapImporter.cs` — `FindAudioClip(songName)` auto-links the clip by name (exact-name match preferred) on import; logs `Clip: <name>` or `Clip: NONE FOUND`. Audio files are named to match song names so all four auto-link.
- `Scripts/Data/EnemyData.cs` — **removed the `song` field entirely** (user found the fallback confusing). `EffectiveSong => songBeatMap != null ? songBeatMap.clip : null`.
- `Scripts/Battle/ChartProvider.cs` — **removed `ResolveFromAudioAnalysis` and its branch.** With audio only on the beat map, a beat-map-less enemy has no clip, so that branch was unreachable. An enemy with no beat map now errors cleanly ("Assign the enemy a SongBeatMap").
- `Scripts/Core/Conductor/Conductor.cs` — added `SetClip(AudioClip)`; `ChartProvider` now uses it instead of three `GetComponent<AudioSource>()` calls (better encapsulation). (`Clip`/`HasClip` were added then removed once the analysis branch was deleted.)
- `RuntimeBeatAnalyzer` KEPT — the Chart Gym still calls it directly for ad-hoc clips. It's just no longer in the battle flow.
- The old `song:` line is now orphaned in the enemy `.asset` files. Harmless; Unity drops it on next save.

**Consequence (important):** re-import is now REQUIRED for audio, not just notes. Until the beat maps carry their clip, battles are silent (the enemy fallback is gone). User confirmed the loop works, so this is presumably done.

---

## Importer GUID preservation (FIX, DONE)

The importer was doing `DeleteAsset` + `CreateAsset` on overwrite, which mints a NEW GUID every time and would break every enemy's beat-map reference on re-import. Changed to update IN PLACE: `EditorUtility.CopySerialized(newData, existing)` + `SetDirty` when the asset exists, else `CreateAsset`. Dialog button is now "Update". Enemy references (Acolyte/Cultist/High Priest, plus the new Zealot link) survive re-import.

---

## Misc code-quality this session (DONE)

- **Boss HP:** `High Priest.asset` maxHP 100 -> 250 (GDD wants 250-400 for bosses; also makes `IsBoss` true so the fallback agrees with `NodeType.Boss`).
- **Boss identity:** `BattleManager` got `_isBoss` (from `NodeType.Boss`, `EnemyData.IsBoss` fallback) + `public bool IsBoss`/`WasBoss`.
- **Chart Gym repaired:** `ChartGymController` was THROWING under "Input System Only" (legacy `Input.GetKeyDown`). Ported to `Keyboard.current` under `#if ENABLE_INPUT_SYSTEM`. Added `_chartInstrument` + `CycleInstrument()` (key **I** cycles + regenerates), reusing `ChartProvider.FilterByInstrument`. OnGUI shows the current instrument.
- **GameLog consistency:** `RuntimeBeatAnalyzer` `Debug.*` -> `GameLog`.
- **Dead fields removed:** `SongBeatMap.leadInBeats` / `tailBeats` (never written by importer, never read).

---

## Enemy → song mapping (VERIFIED from assets this session)

| Enemy | bpmMod | diff | HP | Beat map | Status |
|---|---|---|---|---|---|
| Acolyte | 0.85 | 0.3 | 80 | Stellar Saga (`6cb23b73`) | assigned |
| Cultist | 1.0 | 0.4 | 100 | Neuro-Synth (`57f92667`) | assigned |
| Zealot | 1.25 | 0.6 | 90 | Crash Runner (`b571f5ec`) | assigned THIS session (was empty); chartInstrument = Melody |
| High Priest (boss) | 1.1 | 0.8 | 250 | Obsidian Overdrive (`96e7fe91`) | assigned |

Audio at `Resources/Audio/Songs/` as `Stellar Saga.mp3`, `Neuro-Synth.mp3`, `Crash-Runner.mp3`, `Obsidian Overdrive.mp3`. Beat map assets in `Assets/_Project/Data/BeatMaps/`. JSONs in `python/`.

---

## Generator commands (UPDATED — stems on by default, shorter intro)

```
cd C:\PersonalProjects\RhythmRogue\python
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Stellar Saga.mp3" --bpm 99 --sensitivity 0.4 --output stellar_saga_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Neuro-Synth.mp3" --bpm 117 --sensitivity 0.5 --output neuro_synth_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Crash-Runner.mp3" --bpm 144 --sensitivity 0.6 --output crash_runner_beatmap.json
python beatmap_generator.py "..\Assets\_Project\Resources\Audio\Songs\Obsidian Overdrive.mp3" --bpm 161 --sensitivity 0.6 --output obsidian_overdrive_beatmap.json
```
- Stems are ON by default now (writes Drums/Bass/Melody tags). Add `--no-stems` for the old blended behavior.
- `--intro-beats` default is now **2** (was 4). First body section is forced playable (never an EnemyOnly Break).
- Needs `librosa soundfile numpy scipy` (scipy is new this session, for the butterworth filter). Import with `RhythmRogue > Import Beat Map JSON` (the menu, NOT a Unity Reimport of the `.asset`).

---

## Pending / TODO (carry into next chat)

1. **Commit + push** this session (combined message provided in chat: import enum fix, instrument feature, beat-map-owns-audio, importer GUID fix, boss HP/identity, Chart Gym, this log).
2. **Regenerate the 4 beat maps with the STEM generator** if not already, then re-import — this is what activates Zealot's Melody following. Confirm each import logs `Clip: <name>` (auto-link working).
3. **`_introDelay` on BattleManager** ~0.5 in BattleScene if the intro still feels long (the real cause was the import bug, so it may already feel fine).
4. **`ShapeAssembler` chart feel** — the "feels robotic" rework. Needs the user playing it; that's why it waited for the green baseline. NEXT code target.
5. **Event/lifecycle audit** — subscribe/unsubscribe symmetry across systems (standard Unity leak source). Low risk hardening.
6. Carry-overs from horizon: save system, events system, relic viewing screen, real relic art, battle background.
7. **Recurring:** cut a Windows build, get to 3 testers (GDD success criterion; every precondition is met).

---

## Architecture reminders (unchanged unless noted)

- SOLID, ScriptableObject configs, seeded determinism (`SeededRandom` / `ISeededRandom`), code-generated UI (no prefabs), two-layer events (C# + EventBus), `GameLog` (Info/Warn/Error — NOT Warning), `[Conditional]` so zero release overhead.
- RunState (SO) = canonical cross-scene store at `Assets/_Project/SCO/RunState.asset`.
- **Active Input Handling = "Input System Only"** — legacy `UnityEngine.Input` is DEAD; read `Keyboard.current` etc. under `#if ENABLE_INPUT_SYSTEM`. (Bit the Chart Gym this session.)
- **Generator/importer enum contract is via STRINGS** (new this session) — importer DTO enum fields must be string + `ParseEnum`, never int.
- **Beat map owns its audio** (new this session) — `SongBeatMap.clip`, auto-linked by name on import; there is no enemy-side song fallback anymore.
- **Importer updates assets in place** (new this session) — preserves GUID; never delete+recreate a referenced asset.
- Data layer is already DOP-leaning and that's deliberate: `NoteData`/`StampedNote` are `readonly struct`, `BeatMarker`/`SongSection` are structs in contiguous lists, transformers (`ShapeAssembler`, `RuntimeBeatAnalyzer`) are static functions over arrays, immutable data is kept separate from per-frame state (on `NoteView`). NO ECS/DOTS rewrite — wrong scale, no perf need.
- `RectTransform` has no `SetActive`/`activeSelf` → use `.gameObject`. `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings` → fully-qualify.
- Palette: BgDeep #150C21, BgSurface #332232, BgLight #703C67, Shadow #5A2D2A, RustOrange #B15B35, AmberOrange #EAA44A, WarmGold #F0CE6E, OffWhite #FCF8F4. NO red. Dark/occult/gothic (Castlevania-to-Inscryption), NOT FNF-styled.
- Working style: terse, iterative, tests in-engine and reports back; prefers full file rewrites but minimal output when simple; decision fatigue → Claude picks a direction and provides commit messages.
