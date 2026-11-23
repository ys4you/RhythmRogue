# (C) Player and Enemy Character System

> Status: DONE. Built July 2, 2026 (`70d4338`, `9702910`, `216711e`). The player visual was wired into MapScene and the Zealot got a Character Visual on July 15 (`a543c62`). Player and enemy run on one shared FNF-style animation system behind an `ICharacterVisual` interface. Player uses a white 32x32 placeholder; enemies pose their own sprite by default and can now carry optional authored directional art.

## Architecture

One animator, driven by an interface, so it does not care where clips come from.

- `ICharacterVisual` (interface): `GetClip(state)`, `BaseScale`, `SortingOrder`, `Flip`, `MapSprite`. The contract the animator reads.
- `CharacterVisualConfig` (SO, implements the interface): authored per-state clips. Used by the player, and optionally per enemy for real directional art. `HasAnyClips` tells callers whether it is worth using or empty.
- `RuntimeCharacterVisual` (implements the interface): built at runtime from one sprite, poses it via SpritePoser. The enemy default (no extra art).
- `SpritePoser` (static): the single poser. Idle is a beat-stepped scale pulse (size independent), sing poses lean toward the lane and punch. Shared by the placeholder square and by any single-sprite character.
- `PlaceholderCharacterArt` (static): the white 32x32 sprite, posing delegated to SpritePoser.
- `CharacterAnimator`: plays whatever `ICharacterVisual` it is handed on a SpriteRenderer. Pose offsets are relative to the visual transform's own position and scale, so it attaches straight onto an already-placed enemy renderer.
- `PerformerCharacter` base + `PlayerCharacter` (sings on input) and `EnemyCharacter` (sings on `EnemyHighway.OnAutoHit`, eases back to idle).

## Enemy directional art (new)

- `EnemyData.visual` (optional `CharacterVisualConfig`). If it has clips, the enemy uses those authored poses. If null or empty, it poses its single `sprite` (bob + lean), exactly as before.
- `EnemyCharacterFactory.Attach` picks: `visual.HasAnyClips ? visual : new RuntimeCharacterVisual(sprite)`.
- **Fallback (handled):** any state you have not drawn yet poses the enemy's own sprite (bob + lean), never the white placeholder, so assigning a partial or empty config cannot blank the enemy. Draw poses one at a time and they replace the posed sprite as they appear. RuntimeCharacterVisual overlays the config onto the sprite; the config supplies only the states actually drawn, via `GetAuthoredClip`.

## How to author an enemy's directional art

1. Draw the poses at the same size and pivot as the enemy's idle sprite. Mirror left and right, so it is about three drawn poses: up, down, one side.
2. Create a Character Visual asset (Create > RhythmRogue > Data > Character Visual).
3. Add clips: Idle, SingUp, SingDown, and both SingLeft and SingRight. For the mirrored pair, use the same side sprite in both clips with the frame's `flipX` toggled on one.
4. Assign the asset to that enemy's EnemyData `Visual` field.
5. Tune `baseScale` on the asset if the poses read a touch large or small next to the idle.

## Current enemy art

acolyte, cultist, enemy_zealot, HighPriest are drawn as single sprites. Assign each to its EnemyData `sprite` and it already bobs and leans per note, no config needed. Add a Character Visual per enemy later for true directional frames.

## Editor / left to do

- Let Unity import the new scripts (they recompile on focus).
- Assign each enemy's sprite (and, when drawn, its Character Visual).
- The player's `PlayerVisual` is already assigned. Player placeholder idle is now a scale pulse (via SpritePoser), matching the enemy.

Committed July 2 to 15.

## Next (optional)

- Per-enemy directional configs as the art gets drawn (player and signature bosses first, per the art-scope note).
- The Miss pose exists but nothing triggers it yet.
