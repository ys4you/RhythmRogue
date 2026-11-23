# (C) Architecture and Conventions

> Hard-won lessons and patterns from past sessions. Read this before touching the code so you do not re-learn these the painful way.

## Layering and dependency rules

- Data <- Battle <- UI. The dependency arrow only points left.
- Enemy modifier example of this in practice: the modifier interface (`IBattleContext`, `ModifierNote`) lives in the Data layer, the implementation (`BattleContext`, `EnemyModifierRunner`, `EnemyModifier`) lives in the Battle layer. This keeps the dependency one-directional and avoids circular references once Assembly Definitions are added.

## Seeded randomness

- All procedural systems pull from `SeededRandom` / `ISeededRandom`, seeded from the run seed. Never `UnityEngine.Random`.
- Modifiers use a distinct seeded stream: `node id XOR 0x4D4F44`, so the modifier RNG cannot collide with the chart RNG.
- Relic option generation is deterministic: seeded RNG forked from run seed plus battle count. Same seed offers the same relics at the same encounters.

## Modifier system

- Modifiers are cloned per fight, so shared ScriptableObject assets are never mutated.
- `EnemyModifier` exposes virtual hooks, wired into `BattleManager` at five seams.

## Known pitfalls (each of these has bitten before)

- **Namespace collision:** `RhythmRogue.Core.Audio.AudioSettings` clashes with `UnityEngine.AudioSettings`. Requires full qualification.
- **UsageExamples files:** the pooling and events `UsageExamples.cs` files declared real class names (`NoteView`, `NotePool`, `JudgmentType`, etc.) in the real namespaces, causing conflicts. Delete these on sight.
- **RectTransform:** has no `SetActive` / `activeSelf`. Always use `.gameObject.SetActive()`.
- **Input System race:** `wasPressedThisFrame` shared across two readers in one frame causes an open-then-close race. Fix with `SuppressForOneFrame()`.
- **RunState hygiene:** a ScriptableObject mixing serialized and non-serialized runtime fields leaks stale data between Editor sessions. Keep serialized config and runtime state clearly separated.
- **Audio file renaming:** rename audio files inside Unity, not Windows Explorer, or you get orphaned `.meta` files that break Inspector references on enemy assets.
- **Relic display surfaces:** four surfaces build relic cards independently (RelicBar, RelicDetailCard, RewardPickScreen, ShopScreen). Any change to relic display must touch all four.

## Static mutable state warning

`BattleConfig` was a prototype shortcut: mutable static state carrying `EnemyData` and `ChartAsset` across scene loads. The intended fix was to fold it into `RunState.SelectedNode` (which already holds `EnemyData`) plus a `SelectedChart` field, and have `BattleManager`, `BattleUI`, `BattleResultHandler`, and `MapScreen` read from `RunState` directly. This migration is done, confirmed in the live code on July 1 (see [[04 System/Codebase/(C) Battle Flow]]).

## Boss routing

Boss victories skip RewardScene and route directly to SummaryScene.

## Key file locations

- Conductor: `Assets\_Project\Scripts\Core\Conductor\Conductor.cs` (note: under `Core\Conductor\`, not `Core\` directly)

## Working on the files (for Claude)

- **With a shell** (Claude Code, or the Claude app with this computer linked): run `git status` and `git log --oneline -10` first, so you know which machine was last active and what is uncommitted.
- **With the Filesystem MCP tools only:** call `list_allowed_directories` first to confirm the machine. If the connection is unresponsive, restart Claude Desktop, then retry. `search_files` needs a recursive glob, for example `**/Filename.cs`. `edit_file` needs an exact `oldText` match including whitespace and indentation; re-read the region after any edit before editing the same area again.
