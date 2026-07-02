using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A character visual built at runtime around a single sprite. It poses that sprite via
    /// <see cref="SpritePoser"/> (bob + lean, no extra art), and can optionally overlay an authored
    /// <see cref="CharacterVisualConfig"/>: any state the config has drawn uses those frames, any
    /// state it has not falls back to posing the sprite. So an enemy with a partial (or empty)
    /// config still shows its own art everywhere, never a blank, and directional poses drop in one
    /// at a time as they are drawn.
    /// </summary>
    public sealed class RuntimeCharacterVisual : ICharacterVisual
    {
        private readonly Sprite _sprite;
        private readonly CharacterVisualConfig _authored; // optional overlay; may be null

        public RuntimeCharacterVisual(Sprite sprite, CharacterVisualConfig authored = null,
                                      float baseScale = 1f, bool flip = false, int sortingOrder = 0)
        {
            _sprite = sprite;
            _authored = authored;
            BaseScale = authored != null ? authored.BaseScale : baseScale;
            Flip = authored != null ? authored.Flip : flip;
            SortingOrder = authored != null ? authored.SortingOrder : sortingOrder;
        }

        public float BaseScale { get; }
        public int SortingOrder { get; }
        public bool Flip { get; }
        public Sprite MapSprite => _sprite;

        public CharacterClip GetClip(CharacterState state)
        {
            CharacterClip authored = _authored != null ? _authored.GetAuthoredClip(state) : null;
            return authored ?? SpritePoser.GetClip(_sprite, state);
        }
    }
}
