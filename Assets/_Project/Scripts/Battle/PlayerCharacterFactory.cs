using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Builds the player's battle presence in one call: a root placed in the world, a child
    /// SpriteRenderer, a <see cref="CharacterAnimator"/>, and a <see cref="PlayerCharacter"/>
    /// wired to input and the beat. The code equivalent of a prefab, matching the project's
    /// no-prefab, code-generated convention.
    /// </summary>
    public static class PlayerCharacterFactory
    {
        /// <summary>
        /// Create and wire the player character. If <paramref name="anchor"/> is set the character
        /// parents to it at local origin; otherwise it is placed at
        /// <paramref name="fallbackPosition"/> so it still appears without scene setup.
        /// </summary>
        public static PlayerCharacter Create(
            CharacterVisualConfig config,
            Transform anchor,
            Vector3 fallbackPosition,
            InputHandler input,
            Conductor conductor)
        {
            if (config == null)
            {
                GameLog.Error("[PlayerCharacterFactory] No CharacterVisualConfig; cannot build the player character.");
                return null;
            }

            var root = new GameObject("PlayerCharacter");
            if (anchor != null)
            {
                root.transform.SetParent(anchor, worldPositionStays: false);
                root.transform.localPosition = Vector3.zero;
            }
            else
            {
                root.transform.position = fallbackPosition;
                GameLog.Warn("[PlayerCharacterFactory] No player anchor assigned; using a fallback " +
                             "position mirrored from the enemy. Drop a PlayerAnchor transform in the " +
                             "scene to place it precisely.");
            }

            var visualGO = new GameObject("Visual");
            visualGO.transform.SetParent(root.transform, worldPositionStays: false);
            var sr = visualGO.AddComponent<SpriteRenderer>();
            sr.sortingOrder = config.sortingOrder;

            var animator = root.AddComponent<CharacterAnimator>();
            animator.Initialize(config, sr, visualGO.transform);

            var player = root.AddComponent<PlayerCharacter>();
            player.Initialize(animator, input, conductor);

            return player;
        }
    }
}
