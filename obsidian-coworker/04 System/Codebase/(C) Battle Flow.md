# (C) Battle Flow

> How a single rhythm battle runs, traced from `BattleManager.cs`, `Conductor.cs`, `ChartProvider.cs`, and `HealthComponent.cs` on July 1, 2026. This is the confirmed runtime flow, not a guess.

## Entry: what the battle reads

When BattleScene loads, `BattleManager.Awake` pulls everything from `RunState`:
- `_currentEnemy` from `RunState.SelectedNode.EnemyData` (falls back to a serialized `_defaultEnemy` for standalone testing).
- `_isElite` from `SelectedNode.Type == NodeType.Elite`.
- `_isBoss` from `SelectedNode.Type == NodeType.Boss`, with `EnemyData.IsBoss` as a defensive fallback. Node type is authoritative.

There is no `BattleConfig` anymore. The old static cross-scene carrier was fully folded into RunState, confirmed: BattleManager reads `RunState.SelectedNode` and `RunState.SelectedChart` directly.

It also stops the map ambient (`MusicManager.Instance.Stop()`) so the battle song enters cleanly.

## Chart resolution

`Awake` builds a seeded RNG (`RunSeed.GetRandom(RandomDomain.Charts, node id)`, so charts are deterministic per seed and per node, and cannot collide with other RNG domains) and hands it to `ChartProvider.Resolve`. The provider assigns the song to the Conductor and returns a `ChartResult`. See [[(C) Chart Generation]] for the branching.

## The state machine

BattleManager runs a `StateMachine<BattlePhase>` with four LambdaStates:

1. **Intro**: waits `_introDelay` seconds (default 1) while the enemy is revealed. `_battleEnded` is reset here. Then transitions to Playing.
2. **Playing**: subscribes to `Conductor.OnSongFinished`, then calls `Conductor.Play`. The song starts immediately; the opening notes scroll in over the chart's own note-free lead-in rather than the audio being delayed. All the moment-to-moment systems (input, matching, judgment, combo, damage, highways) are already wired to each other and to the Conductor, so they run on their own during this phase. On exit it stops the conductor and clears both highways and the hold tracker.
3. **Won**: sets `_endDelay`, collects stats, shows the victory result, waits, then calls `OnBattleComplete`.
4. **Lost**: same as Won but for defeat.

## Initialization (once, before the FSM starts)

`InitializeBattle` (called from `Start`):
- Sets enemy HP from `EnemyData.maxHP`, scaled by `EliteConfig.ScaleHP` if elite. HP is a flat authored pool; the fight is tuned to last a fixed number of notes-to-kill (aim for the kill around 80 percent of the song, see `LogKillTiming`).
- Subscribes `_enemyHealth.OnDeath -> OnEnemyDied` and `_playerHealth.OnDeath -> OnPlayerDied`.
- Loads notes into the highways from the `BattleChart` (player notes to `NoteHighway`, enemy notes to `EnemyHighway`), or a legacy chart into the single highway.
- Aggregates relics: `RelicEffectAggregator.Aggregate(RunState.ActiveRelics)` returns a `RelicModifiers` struct, which is pushed into `JudgmentSystem` (bonus Perfect window), `ComboSystem` (rate and cap boosts), and `DamagePipeline` (bonus Perfect damage, Miss damage reduction), plus a `RelicEffectHandler` for the rest.

## End conditions

Three triggers, all guarded by `_battleEnded` so only the first fires:
- `OnEnemyDied` (enemy HP hits 0) -> Won.
- `OnPlayerDied` (player HP hits 0) -> Lost.
- `OnSongEnded` (song finishes with the enemy still alive) -> Lost.

`EndBattle(victory)` transitions the FSM. After the end delay, `OnBattleComplete` unsubscribes the death handlers and fires `OnBattleCompleted`, which `BattleResultHandler` uses to route: bosses to SummaryScene, everyone else to RewardScene.

## Health and the retry bug

`HealthComponent` is a pure-C# HP pool (not a MonoBehaviour) wrapped by `PlayerHealth` (persists across battles) and `EnemyHealth` (fresh each battle). It has a death latch (`_isDead`) so a dead pool no-ops further damage.

The "no damage after retrying a run" bug lived here: after death, the latch stayed set, so on the next run the player had a full HP bar but was invincible. The fix is in `SetMaxHP` and `ResetToFull`, both of which now clear the latch when HP is positive. The fix is present in this codebase, so the bug should be resolved. Confirm in-game, since I cannot run Unity.

## Pause

`HandlePauseInput` reads Escape (and gamepad Start) through the Input System, since this project is Input System Only and the legacy `Input.GetKeyDown` is dead. Pause only works during Playing. Pausing stops the Conductor and shows the PauseMenu; resume hides it and resumes; quit returns to the main menu.
