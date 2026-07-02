using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Puts an existing enemy sprite renderer onto the shared character system in place: attaches a
    /// CharacterAnimator that poses the enemy's own sprite (idle bob plus directional sing) and an
    /// EnemyCharacter that drives it from the enemy note stream. No new GameObject and no new art,
    /// so it slots onto the scene's enemy renderer exactly where BeatBob used to sit.
    /// </summary>
    public static class EnemyCharacterFactory
    {
        public static EnemyCharacter Attach(SpriteRenderer enemyRenderer, Sprite sprite, Conductor conductor, EnemyHighway highway)
        {
            if (enemyRenderer == null)
            {
                GameLog.Warn("[EnemyCharacterFactory] No enemy renderer; cannot attach the enemy character.");
                return null;
            }

            Sprite baseSprite = sprite != null ? sprite : enemyRenderer.sprite;
            GameObject go = enemyRenderer.gameObject;

            // The animator poses the renderer's own transform in place, so its offsets are relative
            // to the enemy's authored position and scale (no reparenting, no double sprite).
            var animator = go.GetComponent<CharacterAnimator>();
            if (animator == null) animator = go.AddComponent<CharacterAnimator>();
            animator.Initialize(baseSprite, 1f, false, enemyRenderer, enemyRenderer.transform);

            var enemy = go.GetComponent<EnemyCharacter>();
            if (enemy == null) enemy = go.AddComponent<EnemyCharacter>();
            enemy.Initialize(animator, conductor, highway);

            return enemy;
        }
    }
}
