using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Puts an existing enemy sprite renderer onto the shared character system in place: attaches a
    /// CharacterAnimator (driven by an ICharacterVisual) and an EnemyCharacter that sings from the
    /// enemy note stream. The visual overlays the enemy's optional authored Character Visual on top
    /// of its own sprite: drawn poses use the config, undrawn ones pose the sprite, so a partial or
    /// empty config never blanks the enemy. No new GameObject: it slots onto the scene's enemy
    /// renderer where BeatBob used to sit.
    /// </summary>
    public static class EnemyCharacterFactory
    {
        public static EnemyCharacter Attach(
            SpriteRenderer enemyRenderer,
            Sprite sprite,
            CharacterVisualConfig visual,
            Conductor conductor,
            EnemyHighway highway)
        {
            if (enemyRenderer == null)
            {
                GameLog.Warn("[EnemyCharacterFactory] No enemy renderer; cannot attach the enemy character.");
                return null;
            }

            Sprite baseSprite = sprite != null ? sprite : enemyRenderer.sprite;

            // Overlay any authored config onto the enemy's own sprite. The animator poses the
            // renderer's own transform in place, so offsets are relative to the enemy's authored
            // position and scale (no reparent, no second sprite).
            ICharacterVisual source = new RuntimeCharacterVisual(baseSprite, visual);

            GameObject go = enemyRenderer.gameObject;

            var animator = go.GetComponent<CharacterAnimator>();
            if (animator == null) animator = go.AddComponent<CharacterAnimator>();
            animator.Initialize(source, enemyRenderer, enemyRenderer.transform);

            var enemy = go.GetComponent<EnemyCharacter>();
            if (enemy == null) enemy = go.AddComponent<EnemyCharacter>();
            enemy.Initialize(animator, conductor, highway);

            return enemy;
        }
    }
}
