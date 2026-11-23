# (C) Chart Generation

> The charting pipeline as it actually exists in code, read from `ChartProvider.cs` and `EnemyData.cs` on July 1, 2026. This supersedes the earlier uncertainty about class names.

## The naming, settled

The April `HybridAssembler` / `PatternData` / `PatternLibrary` names are gone. They were renamed, not deleted, and the current code has two parallel charting families that both live on:

- **Pattern family (preferred):** `PatternAssembler` places authored rhythm fragments (`NotePattern`) drawn from a `NotePatternLibrary`.
- **Shape family (fallback):** `ShapeAssembler` places lane shapes (`LaneShape`) from a `ShapeLibrary`.

Both coexist by design. This is not dead code, though it is a redundancy worth being aware of: two systems that solve the same problem, with the pattern one favored.

## The stable concept

Unchanged across every rewrite: **human-authored fragments placed algorithmically. The algorithm decides when and which fragment to use, never where individual notes go.** Lane assignments come entirely from the authored fragments. This is what makes the charts feel hand-made rather than robotic.

## The entry point: ChartProvider.Resolve

`ChartProvider` is the single resolver. Given an `EnemyData`, an elite flag, and a seeded RNG, it returns a `ChartResult`. The decision tree:

1. **Assign the song.** The clip comes from `EnemyData.EffectiveSong`, which reads it off the enemy's `SongBeatMap` (the beat map and its audio are one swappable unit). The clip is set on the Conductor.
2. **Compute effective difficulty and BPM,** applying elite boosts from `EliteConfig` if the fight is elite.
3. **Legacy mode** (`_useLegacyChart` toggle): load a JSON chart via `ChartLoader`. This is a testing/back-compat path.
4. **Beat-map mode** (the real path, when `EnemyData.songBeatMap` is set):
   - If a `NotePatternLibrary` is available (the enemy's own `patternLibrary`, or the ChartProvider default), run **`PatternAssembler.Assemble`** with the raw markers, sections, the library, the enemy's instrument selector, the RNG, difficulty, BPM, total beats, and the computed lead-in. Result mode is `"pattern"`.
   - Otherwise, filter markers to the enemy's instrument and run **`ShapeAssembler.Assemble`** with a `ShapeLibrary`. Result mode is `"beat-map"`.
5. **No source** (no beat map, legacy off): logs an error and returns failure.

## RuntimeBeatAnalyzer is off the battle path

`RuntimeBeatAnalyzer` (raw AudioClip onset detection via IIR filters and spectral flux) still exists in the Battle folder, but `ChartProvider.Resolve` does not call it. The live battle flow reads hand-authored markers from the `SongBeatMap` instead of analyzing audio at runtime. So the older memory that "RuntimeBeatAnalyzer runs at battle start" is outdated. It may still be used by the Chart Gym test harness or may be vestigial. Worth confirming before relying on it.

## SongBeatMap and instruments

A `SongBeatMap` holds the clip, BPM, an audio offset, a list of `BeatMarker`s (each tagged with an instrument), and optional `SongSection`s. `EnemyData.chartInstrument` selects which stem the chart follows: `All` uses every marker (busiest), while Drums/Bass/Melody keep only that stem. If a selection leaves no markers (an untagged or old beat map), the code falls back to all markers so the chart is never accidentally empty. For the pattern path, the assembler does its own per-section instrument selection, so markers are passed unfiltered.

## The lead-in (and the notes-popping-in bug)

The song starts immediately when the Playing phase begins. To stop the opening notes from popping in on the receptor, `ChartProvider.ComputeLeadInBeats` computes how long a note takes to cross the off-screen spawn distance at the current scroll speed, converts that to beats at the song's BPM, and the `PatternAssembler` leaves that many opening beats note-free. The song's intro covers that runway. This is scroll-speed dependent (a slower scroll needs a longer runway). This mechanism is the fix for the "notes pop in at battle start" bug, and it is present in this codebase. Confirm the feel in-game.

## Difficulty

Difficulty is a 0.0 to 1.0 float (`EnemyData.markerDifficulty`) that controls which fragments are eligible and how dense the chart gets. Elite fights nudge it up. The mapping from the float to concrete constraints (intensity threshold, notes per beat, gaps, jumps, complexity tier) lives inside the assembler and its difficulty profile.

## Authoring tools

`Editor/StarterPatternGenerator.cs` and `Editor/StarterShapeGenerator.cs` generate starter assets for the two families. `Editor/BeatMapImporter.cs` imports beat map data produced by the external Python generator. The planned in-editor charting editor (PROTO-026) would let you place notes and sliders against the song directly, rather than generating JSON externally.
