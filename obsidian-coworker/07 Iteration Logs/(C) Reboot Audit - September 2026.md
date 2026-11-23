# (C) Reboot Audit - September 2026

> **Update November 23, 2026.** Two things here are out of date. (1) Steps 1 and 2 of the revised order are done: Last Stand (September 22, on the High Priest) and Counter-Attack (September 25, on the Cultist rather than the High Priest). The "zero implementations" finding no longer holds. (2) **Part 2 item 8 was wrong when written.** Two external playtests had already happened (June 6 and June 13), but their reports sat in `Playtesting/` outside the vault and this audit missed them. They now live in `02 Playtesting`. Current state: [[01 In Progress/(C) Update 1 - Variety and Encounters]].

> Written September 22, 2026 after a roughly three month gap, then **re-verified the same day against a fresh `git pull`**. The pull fast-forwarded past the July 3 commit and brought in one thing only: a lane backdrop system (`HighwayBackdrop`, `LaneBackdropFactory`, `BackdropSettings`). Every gap listed in Part 2 was re-checked after the pull and all of them still stand. This audit read the live desktop codebase and asset files directly, not memory. It supersedes the July 1 status in [[01 In Progress/(C) Update 1 - Variety and Encounters|(C) Update 1 - Variety and Encounters]] where the two disagree.

## One line summary

The game is a complete, playable roguelike loop with far more content than the GDD prototype scope asked for. What it lacks is not systems, it is **depth of encounter variety, a second area, and any persistence**. The single largest waste in the project right now is a finished enemy modifier framework that no enemy uses.

---

## Part 1: What exists (verified on disk)

### Core loop, fully wired

Eight scenes: MainMenu, Map, Battle, Rest, Shop, Event, Reward, Summary, plus a ChartGym test scene. `MapScreen.HandleNodeConfirmed` routes all six node types. `BattleResultHandler` routes win to Reward, boss win and loss to Summary. `SceneTransitionManager` fades between all of them.

### Rhythm layer

Conductor on `dspTime`, judgment with four windows, holds (`HoldTracker`, `HoldState`, `HoldResult`), forgiving combo, damage pipeline, accuracy tracking, hit particles, receptor animation, lane focus, dual highway (player plus enemy). Calibration **offset value** exists and the Conductor applies it. Debug overlays for conductor, judgment, and battle.

The manual black-square lane backdrop is now a proper system: `LaneBackdropFactory` builds it, `HighwayBackdrop` applies it, and `BackdropSettings` persists per-side visibility and RRGGBBAA colour in PlayerPrefs with an `OnChanged` event so the pause menu updates the live battle. This is the only change in the September pull, and it retires the hierarchy trap noted earlier (the backdrop is no longer hand-parented, so it cannot corrupt `HighwayBase`'s receptor world positions).

### Chart generation (more advanced than the GDD describes)

Two assembly layers, not one:
- `PatternAssembler` with 15 authored `NotePattern` assets and a `StarterPatternLibrary`.
- `ShapeAssembler` with a `ShapeLibrary` and **45 authored lane shapes** across four tiers (Tier1_Easy through Tier4_Expert).
- `RuntimeBeatAnalyzer` plus `SongBeatMap` assets for four songs (Crash Runner, Neuro Synth, Obsidian Overdrive, Stellar Saga), imported through a `BeatMapImporter` editor tool.
- `ChartProvider.ComputeLeadInBeats` gives a scroll-speed-dependent note-free runway at song start.

This is the HybridAssembler idea actually shipped.

### Map

`MapGenerator` builds 6 to 8 layers: 1 start, 1 opener fan of 3 to 4, 3 to 5 mid layers of 2 to 4, 1 boss. Mid layers roll roughly 20% elite, 18% shop, 15% rest, 15% event, 32% enemy. Separate RNG forks for structure, enemy selection, and jitter. Positions before connections, with a no-cross rule.

### Content authored

- **9 relics** with sprites, `RelicEffect` / `RelicEffectAggregator` / `RelicApplier`, a `MainRelicPool`, and four display surfaces.
- **8 events** (Coin-Eater, Gambler's Skull, Hollow Choir, Mirror Pool, Quiet Spring, Starving Altar, Tithe Bell, Wandering Debtor) in a `DefaultEventPool`, with `EventChoice` and `EventOutcomeApplier`. **The event flow is wired end to end.** This closes the July 1 open question.
- **4 enemies**: Acolyte, Cultist, Zealot (depth-gated at `minDepthT` 0.5), High Priest (boss). One area, `Area_Sanctum`.
- **A full scripted onboarding area** with 6 authored charts, `OnboardingSequence`, `LessonCallout`, practice mode (cannot lose, reward screen skipped), and per-node reward relics. This was not on any roadmap and is a real asset.
- Economy: `EconomyConfig` / `EconomyService`, currency with accuracy bonus, working shop.
- Audio: SFX library, `MusicManager` with crossfade, menu and map tracks, perceptual square-law volume taper.

### Tooling

`DeveloperConsole` with command registry and completion, `DevCheatPanel`, `PlaytestReporter`, `ChartGymScene`, `BeatMapImporter`, `StarterPatternGenerator`, `StarterShapeGenerator`, `RelicMigration`, `SpriteImportDefaults`.

### Foundations

Seeded RNG with per-domain forks, `SeedEncoder`, EventBus plus C# events, FSM, object pooling, timers, extension library, `GameLog`, `KeybindManager`, full keyboard and gamepad UI navigation (`UI/Navigation`), settings parity between pause and main menu.

> PROTO-027 (rebindable keybinds) and PROTO-028 (full keyboard and gamepad navigation) both appear **closed**. `KeybindManager` and the `UI/Navigation` subfolder exist and are used.

---

## Part 2: What is missing (verified gaps)

### 1. Enemy modifiers: framework complete, zero implementations

This is the headline finding. `EnemyModifier` (abstract, five hooks), `IBattleContext`, `ModifierNote`, `BattleContext`, and `EnemyModifierRunner` all exist and are wired into `BattleManager` at five seams. But:

- **No concrete `EnemyModifier` subclass exists anywhere in the codebase.**
- **All four enemy assets carry `modifiers: []`.**
- There is no Modifiers folder under `SCO`.

Every enemy today differs only by song, `bpmModifier`, `markerDifficulty`, `maxHP`, and a small `difficultyFlavor` tilt. Mechanically they are the same fight at different speeds. The machinery to fix that is already built and paid for.

### 2. One area, and the run ends at its boss

`BattleResultHandler` calls `EndRun(true)` on any boss win and goes to Summary. There is **no area chaining**. The GDD's "Boss Battle to Next Area or Run Summary" is not implemented. `Area_Sanctum` is the only real area.

### 3. No boss-unique mechanics

High Priest is a 1000 HP enemy with a denser chart. None of the GDD boss mechanics exist: no tempo change, no lane swap, no multi-phase, no counter-attack survival phase.

### 4. No save system

Every runtime field on `RunState` is `[System.NonSerialized]`. Nothing persists between sessions. Continue Run cannot work, and meta-progression has nothing to build on.

### 5. No meta-progression

No Score currency, no unlock system, no meta shop. Nothing in the codebase matches Save, Meta, Unlock, or Progress.

### 6. No calibration screen

The offset value and its plumbing exist, but the tap-along metronome UI from GDD 3.2 and 12 does not. This is the last unticked item in GDD 16.1 prototype scope.

### 7. No difficulty ladder

Correctly absent. The [[00 Ideas/(C) Roadmap and Backlog|backlog]] research verdict says do not build this while content is thin. Content is still thin. Leave it.

### 8. External playtesting: still zero

GDD 16.2 requires **3 external testers completing a full run without blocking bugs**. This has now been the flagged highest-value next step across multiple sessions and has not happened. See [[02 Playtesting/(C) Known Bugs and Playtest Plan|(C) Known Bugs and Playtest Plan]].

### Minor

- `Art/Fonts`, `_Project/UI`, and `Songs/MusicBG` are empty folders.
- PROTO-025 (dead code pass) is still open. `MapTest` and friends are still in `Scripts/Tests`.
- PROTO-026 (in-editor chart authoring tool) is still open.

---

## Part 3: The GDD is now stale on numbers

Worth a pass at some point, not urgent. Actual values have drifted far above the document:

| Thing | GDD says | Assets say |
|---|---|---|
| Standard enemy HP | 100 | 300 to 550 |
| Boss HP | 250 to 400 | 1000 on the asset, 760 on the area |
| Areas per run | multiple, chained | one |

Not a bug. The GDD just no longer describes the game.

---

## Part 4: Where to continue

### The roadmap needs one reorder

The July 1 plan sequenced Update 1 as Speed, Trick, Chaos, Events, Second area. Two things changed that:

1. **Events are done.** Increment 4 can be struck.
2. **The July 1 note itself found that Trick and Chaos need `IBattleContext` extensions.** That makes increment 2 a cold-start code task with uncertain scope, which is the exact shape of task that has historically stalled this project.

But `IBattleContext` already exposes `SetEnemyNotes`, and the enemy highway plus guard plus damage wiring already works. **A counter-attack modifier needs no new interface surface at all.** So the cheapest real modifier is not Trick, it is Counter-Attack, and it should go first.

### Revised order

0. **Open Unity, confirm it compiles, play one full run.** Three months cold. Do this before writing a line. Also run `git status`, since the modifier framework does not appear in the recent commit messages and may be uncommitted.
1. **Counter-Attack modifier.** One new `EnemyModifier` subclass, one asset, attached to High Priest. Uses only the existing surface. Makes the boss feel like a boss and proves the framework works. Smallest possible first win.
2. **Last Stand modifier.** Second subclass, uses `OnEnemyWouldDie`. Needs `ReviveEnemy` on `IBattleContext` to clear the `HealthComponent` death latch (see the playtest note). Small, scoped, and the interface extension is one method.
3. **Lane mirror as a chart-generation transform.** An `EnemyData` flag read at assembly time, not a runtime context change. This is the Trick enemy without the interface problem.
4. **Then stop and playtest.** Three modifiers plus eight events plus nine relics is a genuinely varied build. This is the natural point to finally get three testers on it.

Second area and meta-progression are Update 2. Save system is a prerequisite for meta-progression and should be built with it, not before.

### Why this order

Every step is a finishable unit that ends on a runnable build, the first one requires no new architecture, and the sequence converts an already-paid-for framework into visible gameplay before asking for any new system. It also reaches the playtest gate faster than the original plan did.
