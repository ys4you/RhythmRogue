using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A character visual built at runtime from a single sprite, posed via <see cref="SpritePoser"/>.
    /// Used for the enemy, which has one authored sprite (from EnemyData) rather than a per-state
    /// clip set: it bobs and leans by transform, no extra art. Swap in an authored
    /// <see cref="CharacterVisualConfig"/> per enemy later for real directional poses.
    /// </summary>
    public sealed class RuntimeCharacterVisual : ICharacterVisual
    {
        private readonly Sprite _sprite;

        public RuntimeCharacterVisual(Sprite sprite, float baseScale = 1f, bool flip = false, int sortingOrder = 0)
        {
            _sprite = sprite;
            BaseScale = baseScale;
            Flip = flip;
            SortingOrder = sortingOrder;
        }

        public float BaseScale { get; }
        public int SortingOrder { get; }
        public bool Flip { get; }
        public Sprite MapSprite => _sprite;

        public CharacterClip GetClip(CharacterState state) => SpritePoser.GetClip(_sprite, state);
    }
}
