# (C) Codebase Map

> A navigable index of the RhythmRogue script codebase. Read from the desktop machine on July 1, 2026, at `Assets\_Project\Scripts`. About 150 source files across nine top-level areas. Roles for files marked (read) were confirmed by opening the file; the rest are described from names, signatures, and prior session context. `.meta` files omitted throughout.
>
> **Updated November 23, 2026:** the scripts folder now has 229 `.cs` files. Files added since July 1 (and a few the first pass missed) are in [[#Added since July 1]] at the bottom.

## Dependency direction

`Util` (generic framework, zero game knowledge) <- `Data` (ScriptableObjects, enums, pure services) <- `Core` and `Battle` and `Map` <- `UI`. `DevTools`, `Editor`, and `Tests` sit off to the side and depend inward. Assembly Definition files are not yet added, so this direction is currently a convention rather than compiler-enforced.

## Area summary

- **Util**: reusable engine-agnostic framework: Singleton, Events, Pooling, Random, FSM, Timing, Extensions, a developer console, and GameLog.
- **Core**: game-wide singletons and cross-scene state: Conductor, RunState, scene transitions, audio, display, keybinds, global pause.
- **Data**: all ScriptableObject configs, enums, and pure-data services: enemies, relics, economy, events, and the charting data types.
- **Battle**: the rhythm battle: the coordinator, the highways, input, judgment, combo, damage, health, and chart assembly.
- **Map**: node-based run map: generation, data, and the map and rest screens.
- **UI**: code-generated screens and the keyboard/gamepad navigation layer.
- **DevTools**: in-game dev console and cheat panel.
- **Editor**: import defaults and starter-asset generators.
- **Tests**: manual in-scene test harnesses (candidates for cleanup or an Editor test assembly).

---

## Util (framework)

Generic, reusable, ships no game-specific enums.

| Area | Files | Role |
|---|---|---|
| root | `Singleton.cs`, `GameLog.cs` | Generic MonoBehaviour singleton base (with a statics reset for Editor play sessions). GameLog wraps logging behind Conditional attributes for zero-overhead release stripping. |
| Events | `EventBus.cs`, `IEventBus.cs`, `IEvent.cs`, `EventBinding.cs`, `EventBindingGroup.cs`, `EventBusProvider.cs`, `GameEvents.cs` | Typed pub/sub EventBus with struct events, bindings, and bulk-cleanup groups. `GameEvents.cs` holds all game-wide event structs. |
| Pooling | `ObjectPool.cs`, `MonoPool.cs`, `IObjectPool.cs`, `IPoolable.cs`, `IPoolFactory.cs`, `PrefabFactory.cs`, `PoolableMonoBehaviour.cs`, `PoolRegistry.cs` | Object pooling for high-frequency spawns (notes, hit effects). |
| Random | `SeededRandom.cs`, `ISeededRandom.cs`, `RunSeed.cs`, `IRunSeed.cs`, `SeedEncoder.cs`, `ISeedEncoder.cs`, `RandomDomain.cs`, `WeightedSelector.cs`, `IWeightedSelector.cs`, `WeightedEntry.cs`, `WeightedTable.cs` | Deterministic seeded RNG with the XXXX-XXXX code encoder, per-domain forking (`RandomDomain`), and weighted selection. |
| FSM | `StateMachine.cs`, `MonoStateMachine.cs`, `IStateMachine.cs`, `IState.cs`, `BaseState.cs`, `LambdaState.cs` | Generic finite state machine with a `TKey`. BattleManager uses `StateMachine<BattlePhase>` with LambdaStates. |
| Timing | `Timer.cs`, `TimerManager.cs`, `TimerService.cs`, `ITimer.cs`, `ITimerManager.cs`, `Cooldown.cs` | Scaled and unscaled timers and cooldowns. |
| Extensions | `FloatExtensions.cs`, `IntExtensions.cs`, `VectorExtensions.cs`, `ColorExtensions.cs`, `TransformExtensions.cs`, `CollectionExtensions.cs`, `ComponentExtensions.cs`, `StringExtensions.cs` | C# and Unity convenience extensions. |
| Console | `DeveloperConsole.cs`, `IConsoleCommand.cs`, `ConsoleCommandBase.cs`, `LambdaConsoleCommand.cs`, `ConsoleArgs.cs`, `ConsoleOption.cs`, `ConsoleCompletion.cs` | The generic developer console engine (game commands live in DevTools). |

## Core (singletons and cross-scene state)

| File | Role |
|---|---|
| `Conductor/Conductor.cs` (read) | Central beat clock. Tracks song position in beats via `AudioSettings.dspTime`, `PlayScheduled` start, pause/resume with drift correction, BPM changes, and `OnBeat`/`OnHalfBeat`/`OnBpmChanged`/`OnSongFinished` events. `[DefaultExecutionOrder(-100)]`. |
| `Conductor/IConductor.cs`, `Conductor/ConductorDebugOverlay.cs` | Conductor interface and an F-key debug overlay. |
| `RunState.cs` (read) | The run's cross-scene state as a ScriptableObject: seed, RunSeed, MapData, SelectedNode, SelectedChart, ActiveRelics, currency (Beats) with AddCurrency/TrySpendCurrency, and run stats. Runtime fields are `[NonSerialized]` to avoid stale data between Editor sessions. |
| `SceneTransitionManager.cs` | Scene loading and the scene-name constants for the six scenes. |
| `KeybindManager.cs` | Keybinding state. |
| `GlobalPauseManager.cs` | Global pause coordination outside battle. |
| `Audio/AudioManager.cs`, `Audio/MusicManager.cs`, `Audio/MusicTrack.cs`, `Audio/SfxLibrary.cs`, `Audio/SfxId.cs`, `Audio/AudioSettings.cs`, `Audio/UISelectableSounds.cs` | SFX and music playback, cross-scene music continuity, the calibration offset (`AudioSettings.CalibrationOffsetSeconds`), and UI selection sounds. Note the `RhythmRogue.Core.Audio.AudioSettings` vs `UnityEngine.AudioSettings` name clash. |
| `Display/DisplaySettings.cs`, `CRTOverlay.cs` | Display settings and a CRT post effect. |

## Data (configs, enums, pure services)

| File | Role |
|---|---|
| `EnemyData.cs` (read) | Enemy ScriptableObject: patternLibrary, shapeLibrary, songBeatMap, chartInstrument, markerDifficulty, HP, isBoss, bpmModifier, sprite, modifiers. `EffectiveSong` reads the clip off the SongBeatMap. |
| `EnemyModifier.cs`, `EnemyPool.cs` | Modifier base with virtual hooks; weighted enemy pool. |
| `EliteConfig.cs` | Elite scaling (HP, BPM, difficulty boosts). |
| Charting data | `SongBeatMap.cs`, `BeatMarker.cs`, `MarkerType.cs`, `ChartInstrument.cs`, `SongSection.cs`, `SongSectionType.cs`, `SectionType.cs`, `NotePattern.cs`, `NotePatternLibrary.cs`, `LaneShape.cs`, `ShapeLibrary.cs`, `NoteData.cs`, `NoteType.cs`, `ChartData.cs`, `ChartLoader.cs` | The two charting data families. Pattern family (NotePattern, NotePatternLibrary) feeds PatternAssembler; shape family (LaneShape, ShapeLibrary) feeds ShapeAssembler. SongBeatMap holds markers, sections, BPM, clip, and audio offset. See [[04 System/Codebase/(C) Chart Generation|(C) Chart Generation]]. |
| Configs | `JudgmentConfig.cs`, `DamageConfig.cs`, `ComboConfig.cs`, `EconomyConfig.cs` | Tunable ScriptableObjects that replaced hardcoded serialized fields; load from `Resources/Configs/` as a fallback. |
| Economy | `EconomyService.cs` | Pure economy service alongside EconomyConfig. |
| Relics | `RelicData.cs`, `RelicEffect.cs`, `RelicRarity.cs`, `RelicPool.cs`, `RelicApplier.cs`, `RelicEffectAggregator.cs`, `RelicEffectHandler.cs`, `RelicModifiers.cs` | Relic system: pure-data relics, weighted pool by rarity, and the aggregator that folds active relics into a `RelicModifiers` struct consumed by the battle systems. |
| Events (map node) | `EventData.cs`, `EventChoice.cs`, `EventPool.cs`, `EventOutcomeApplier.cs` | Data for Event nodes: choices and their risk/reward outcomes. |
| Area | `Area.cs` | Area definition (music genre, enemy style, difficulty band). |
| Scroll settings | `ScrollSpeedSetting.cs`, `ScrollDirectionSetting.cs` | Player-facing scroll speed (units per second) and direction. |

## Battle (the rhythm fight)

| File | Role |
|---|---|
| `BattleManager.cs` (read) | The coordinator. Reads enemy/elite/boss from RunState.SelectedNode, delegates chart to ChartProvider, aggregates relics, runs the Intro/Playing/Won/Lost FSM, owns end conditions and stats. See [[04 System/Codebase/(C) Battle Flow|(C) Battle Flow]]. |
| `ChartProvider.cs` (read) | Resolves the chart: legacy JSON, else SongBeatMap via PatternAssembler (preferred) or ShapeAssembler (fallback). Computes the note-free lead-in. |
| `PatternAssembler.cs`, `ShapeAssembler.cs`, `RuntimeBeatAnalyzer.cs` | The two assemblers (pattern-fragment and lane-shape). RuntimeBeatAnalyzer (raw-audio onset detection) is not on the current battle path. See [[04 System/Codebase/(C) Chart Generation|(C) Chart Generation]]. |
| `BattleChart.cs`, `ChartSection.cs`, `StampedNote.cs` | The assembled chart output (player and enemy note lists) and its pieces. |
| Highways | `HighwayBase.cs`, `NoteHighway.cs`, `EnemyHighway.cs`, `NoteView.cs`, `NotePool.cs`, `ReceptorAnimator.cs` | The dual scrolling highways, pooled note views, and receptor animations. |
| Input and judgment | `InputHandler.cs`, `NoteMatcher.cs`, `NoteMatchResult.cs`, `JudgmentSystem.cs`, `Judgment.cs`, `JudgmentResult.cs` | 4-lane input, note matching, and hit judgment (Perfect/Good/Bad/Miss). |
| Holds | `HoldTracker.cs`, `HoldState.cs`, `HoldResult.cs` | Sustained-note tracking. |
| Combo and damage | `ComboSystem.cs`, `DamagePipeline.cs`, `DamageResult.cs`, `AccuracyTracker.cs` | Forgiving combo, damage math (judgment x multiplier), and accuracy stats. |
| Health | `HealthComponent.cs` (read), `PlayerHealth.cs`, `EnemyHealth.cs` | Pure-C# HP pool wrapped by the player (persists across battles) and enemy (full each battle). The death-latch clear lives here. |
| Feedback | `HitFeedback.cs`, `HitParticles.cs` | Judgment text, screen shake, and hit particles. |
| UI and pause | `BattleUI.cs`, `PauseMenu.cs` | The battle HUD and the pause overlay. |
| Flow types | `BattlePhase.cs`, `BattleResultHandler.cs`, `BattleStats.cs` | The FSM phase enum, post-battle routing (bosses to Summary, else Reward), and the stats struct. |
| Debug | `BattleDebugOverlay.cs`, `JudgmentDebugOverlay.cs` | F-key debug overlays. |

## Map

| File | Role |
|---|---|
| `MapGenerator.cs` | Seeded procedural map generation (branching nodes). |
| `MapData.cs`, `MapNode.cs`, `NodeType.cs` | Map graph, node model, and the node-type enum (Enemy, Elite, Event, Shop, Rest, Boss). |
| `MapScreen.cs`, `MapUI.cs` | The map screen controller and its code-generated UI (including the player marker, the reskin target for the player character system). |
| `RestScreen.cs` | The Rest node screen. |

## UI

| File | Role |
|---|---|
| `MainMenuScreen.cs`, `SummaryScreen.cs`, `ShopScreen.cs`, `EventScreen.cs`, `RewardPickScreen.cs` | Code-generated screens. |
| `RelicBar.cs`, `RelicDetailCard.cs` | The Slay-the-Spire-style relic HUD and detail card. One of four relic display surfaces. |
| `CurrencyReadout.cs` | The Beats currency HUD. |
| `SettingsPanel.cs`, `UIHelpers.cs` | The shared settings panel (Audio/Controls/Display) and the shared UI construction helpers. |
| Navigation | `UINavigationHelper.cs`, `MapNavigationBuilder.cs`, `UIEventSystemProvider.cs`, `UIFocusSetter.cs`, `UIFocusFrame.cs`, `UICancelHandler.cs`, `UINavigationGate.cs`, `UISelectableStyle.cs` | The keyboard/gamepad navigation layer so the mouse is never required (PROTO-028 territory). |

## DevTools, Editor, Tests

| File | Role |
|---|---|
| `DevTools/Console/DevConsoleUI.cs`, `DevTools/Console/DevConsoleContext.cs`, `DevTools/Console/GameConsoleCommands.cs` | The in-game dev console UI and its game-specific commands. |
| `DevTools/DevCheatPanel.cs`, `DevTools/DevAssets.cs` | A cheat panel and dev asset references. |
| `Editor/SpriteImportDefaults.cs` | Auto-sets Point filter and no compression on imported sprites. |
| `Editor/BeatMapImporter.cs` | Imports beat map data (from the Python generator). |
| `Editor/StarterPatternGenerator.cs`, `Editor/StarterShapeGenerator.cs` | Generate starter pattern and shape assets for the two charting families. |
| `Tests/*` | Manual in-scene harnesses: ChartGymController, ConductorTest, EventBusTest, FSMTest, HoldTest, InputTest, JudgmentTest, MapTest, TimerTest, WeightedRandomTest. Several are cleanup candidates (PROTO-025). |

---

## Added since July 1

Described from commit messages and each file's doc comment, not a full read.

| Area | File | Role |
|---|---|---|
| Battle | `BattleContext.cs` | Battle-layer implementation of `IBattleContext`, the surface modifiers act through. Since September also answers section and onset queries. |
| Battle | `EnemyModifierRunner.cs` | Runs the fight's cloned modifiers at the BattleManager seams. |
| Battle | `BattleAnnouncer.cs` | Transient headline over a fight ("IT REFUSES TO DIE"): screen flash, text pop, fade out. |
| Battle | `PerformerCharacter.cs`, `PlayerCharacter.cs`, `EnemyCharacter.cs`, `CharacterAnimator.cs`, `PlayerCharacterFactory.cs`, `EnemyCharacterFactory.cs` | Player and enemy character system. See [[03 Done/(C) Player Character System]]. |
| Battle | `BeatBob.cs` | Beat scale-pop. Superseded by `EnemyCharacter` on July 2 but still referenced by `BattleManager` and `EnemyCharacterFactory`. Cleanup candidate. |
| Battle | `HighwayBackdrop.cs`, `LaneBackdropFactory.cs` | Runtime-built dark lane panel, plus its visibility and tint. |
| Battle | `LaneFocus.cs` | Onboarding: darkens the screen and frames the lane of the upcoming note. |
| Battle | `LessonCallout.cs` | Onboarding coach-mark that spotlights one HUD element. |
| Battle | `LaneKeyLabels.cs` | Key labels under the lanes, shown in practice only. |
| Battle | `PlayerHealthAcquireContext.cs` | Battle-layer `IRelicAcquireContext`: lets relic effects act on player health when a relic is picked up. |
| Core | `OnboardingState.cs` | PlayerPrefs flag for onboarding completion, stamped per build. |
| Core | `PersistentSystems.cs` | Creates the global overlays (`GlobalPauseManager`, `CRTOverlay`) once at startup. |
| Data | `IBattleContext.cs` | The modifier-facing battle surface (Data layer). |
| Data | `Modifiers/LastStandModifier.cs`, `Modifiers/CounterAttackModifier.cs` | The two concrete enemy modifiers. See [[01 In Progress/(C) Update 1 - Variety and Encounters]]. |
| Data | `DifficultyCurve.cs` | Chart difficulty and enemy HP from area band, depth, run tier and elite config. Also the run-level forgiveness tier. |
| Data | `OnboardingSequence.cs` | The scripted onboarding lessons. |
| Data | `ICharacterVisual.cs`, `CharacterVisualConfig.cs`, `RuntimeCharacterVisual.cs`, `SpritePoser.cs`, `PlaceholderCharacterArt.cs` | Character visuals, data side. |
| Data | `BackdropSettings.cs` | Per-side lane backdrop visibility and colour in PlayerPrefs, with an `OnChanged` event. |
| Data | `RelicEffectDef.cs`, `RelicEffects.cs`, `RelicModifiersBuilder.cs` | Typed relic effect system (June 13): a relic holds a list of polymorphic effects that write into a builder, sealed into `RelicModifiers`. |
| UI | `RelicRarityPalette.cs` | Single source of truth for rarity colours on relic cards. |
| DevTools | `PlaytestReporter.cs` | Development builds only. F8 writes a bug report (run context, recent log, system info) to the clipboard and to `persistentDataPath`. |
| Editor | `RelicDataEditor.cs`, `RelicMigration.cs` | Custom relic inspector, and (by its name) a migration tool from the relic refactor. |
