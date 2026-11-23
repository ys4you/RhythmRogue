# (C) Shipped Systems

> What is built and working, organized by area. Written June 29, 2026. Updated November 23 with everything since, in the last section.

## Core rhythm engine

- **Conductor** (singleton): tracks song position in beats using `AudioSettings.dspTime`, not `Time.time`, to avoid frame-rate drift. Broadcasts beat events. Handles BPM changes mid-song. Supports `Pause()` / `Resume()` / `IsPaused`. Fires `OnSongFinished` when the AudioSource stops naturally.
- **Note data model + charts:** notes defined in beat positions, not milliseconds, so BPM changes rescale timing automatically.
- **Note Highway + input:** 4-lane arrow-key input, object-pooled note views scroll using the Conductor beat position. Hit detection compares input time against expected beat time, adjusted by the calibration offset.
- **Scoring / Combo / HP:** thin systems subscribing to judgment events. Combo uses the forgiving reset model (resets only on Miss, Bad still increments). Shared health model, player HP persists across battles.
- **Dual highway architecture:** `NoteHighway` and `EnemyHighway`, both extending `HighwayBase`.

## Battle flow

- **BattleManager** coordinates all systems via serialized references. Phases: Intro (countdown), Playing, Won, Lost.
- Three end conditions: enemy HP hits 0 (Win), player HP hits 0 (Lose), song ends with enemy alive (Lose). A `_battleEnded` flag prevents double-triggers.
- **PauseMenu:** dim overlay with Resume and Quit to Menu, Escape to resume, full keyboard navigation. Fully built and wired into BattleManager. If it ever seems missing, the cause is the component not being present in BattleScene or the Inspector slot being empty, not missing code.

## Game flow and scenes

- FSM game flow across nine scenes (MainMenu, Map, Battle, Rest, Shop, Event, Reward, Summary, plus the ChartGym test scene). Full loop is wired: MainMenu -> Map -> Battle -> Reward -> Map -> Boss -> Summary.

## Enemy systems

- **Enemy modifier framework:** `IBattleContext`, `ModifierNote` (Data), `BattleContext`, `EnemyModifierRunner`, `EnemyModifier` with virtual hooks (Battle), wired into BattleManager at five seams.
- **Enemy HP bar polish:** 780 x 48, WarmGold frame panel, size-30 bold HP number with a dark Outline.
- **BeatBob:** beat-driven scale-pop, auto-attached to the enemy sprite in `InitializeBattle`, auto-subscribes to Conductor beat events, only animates while the song plays. Superseded on July 2 by `EnemyCharacter` (see [[03 Done/(C) Player Character System]]), but the file is still referenced.
- **EliteConfig** ScriptableObject for elite variants.

## Relics and rewards

- **Relic system:** `RelicData` (SO per relic), `RelicEffect` (enum of modifier types), `RelicPool` (weighted random by rarity), `RelicApplier` (aggregates active relics into modifier values). BattleManager calls `RelicApplier.ComputeModifiers()` at start and feeds values to JudgmentSystem, DamagePipeline, ComboSystem.
- **RewardPickScreen:** code-generated reward pick UI with relic cards.
- Starter relics referenced: Perfect Lens (wider window), Power Chord, Combo Crown, Rhythm Heart.
- Shop node with Beats currency, working (confirmed September 22). 9 relics as of September.

## Chart generation

The stable design insight, which held across two rewrites: **human-authored pattern fragments placed algorithmically. The algorithm decides when and which fragment to use, never where individual notes go.** Lane assignments come entirely from the hand-authored fragments.

Pipeline (confirmed against live code, see [[04 System/Codebase/(C) Chart Generation|(C) Chart Generation]]):
- `ChartProvider` resolves the chart. When the enemy has a `SongBeatMap` and a `NotePatternLibrary` is available, `PatternAssembler` places authored fragments. With no pattern library, `ShapeAssembler` (older lane-shape system) is the fallback. Both families coexist.
- The run seed (RandomDomain.Charts, forked per node) chooses which matching fragment is placed, so the same seed reproduces the same chart.
- The battle path reads hand-authored markers from the `SongBeatMap`. `RuntimeBeatAnalyzer` (runtime audio onset detection) still exists but is NOT called on the battle path anymore.
- Difficulty is a 0.0 to 1.0 float (`EnemyData.markerDifficulty`) that gates fragment eligibility and density, nudged up for elites.

**Naming settled:** the April `HybridAssembler` / `PatternData` / `PatternLibrary` names were renamed to `PatternAssembler` / `NotePattern` / `NotePatternLibrary`. The June proposal to delete the older shape classes was not executed; `ShapeAssembler` / `ShapeLibrary` / `LaneShape` are still present as the fallback path.

## UI and navigation

- **KeybindManager** plus a UI / Navigation subfolder.
- Keyboard navigation work in progress across screens.
- `RestScreen` (~14 KB), `MainMenuScreen` (~39 KB).

## Editor tooling

- `StarterChartGenerator` editor tool (note: an older variant tied to `PatternData` may be flagged for deletion, see chart generation uncertainty above).
- `SpriteImportDefaults` editor script auto-sets Point filter and no compression on imported sprites.

## Added since June 29 (plus June work this note missed)

- **Guard mechanic** (June 8, recovery debt June 16): enemy notes only damage you while your guard is down. A Miss drops it, and it comes back after `guardRecoveryHits` clean hits. GUARDED/EXPOSED badge on the HUD.
- **Difficulty model** (June 14 to 17): `DifficultyCurve` computes chart difficulty and enemy HP from the area's band, node depth, run tier and elite config. Charts target notes per second, so difficulty no longer depends on BPM.
- **Onboarding** (June 18 to 21): scripted practice area (`Area_Onboarding`, `OnboardingSequence`) with lessons, coach-marks on the guard badge and relic bar, and lane key labels in practice. Starts on a first-time New Run and re-shows per build during playtesting.
- **Dev tools** (June 10 to 21): developer console, cheat panel, and `PlaytestReporter` (F8 bug report in development builds).
- **Player and enemy character system** (July 2): see [[03 Done/(C) Player Character System]].
- **Lane backdrop** (September 22): `LaneBackdropFactory`, `HighwayBackdrop`, `BackdropSettings`, with per-side toggle and colour in settings.
- **Last Stand modifier** with `BattleAnnouncer` and a mid-fight chart splice (September 22), on the High Priest.
- **Counter-Attack modifier** (September 25) with layered enemy-note insertion on `EnemyHighway`, on the Cultist.
- **Dev shortcuts** (September): boss-only map and a force-enemy toggle for testing.
- **Nix flake** (November 20): dev shell and `nix run` apps for the Python beatmap tools.
