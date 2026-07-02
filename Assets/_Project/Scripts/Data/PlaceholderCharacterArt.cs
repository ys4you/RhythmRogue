using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// The white 32x32 placeholder sprite for characters with no art assigned. Posing is delegated
    /// to <see cref="SpritePoser"/>, so the placeholder square and any single-sprite character (the
    /// enemy) bob and lean identically. The sprite is generated once and cached for the session.
    /// </summary>
    public static class PlaceholderCharacterArt
    {
        private const int TexSize = 32;
        private const float PixelsPerUnit = 32f;

        private static Sprite _placeholder;

        /// <summary>The cached white 32x32 placeholder sprite (built on first access).</summary>
        public static Sprite PlaceholderSprite
        {
            get
            {
                if (_placeholder == null) _placeholder = BuildPlaceholderSprite();
                return _placeholder;
            }
        }

        /// <summary>Placeholder clip for a state: the shared poser applied to the white square.</summary>
        public static CharacterClip GetClip(CharacterState state) => SpritePoser.GetClip(PlaceholderSprite, state);

        private static Sprite BuildPlaceholderSprite()
        {
            var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var px = new Color[TexSize * TexSize];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();

            // Pivot bottom-centre so the character stands on its anchor, matching real art.
            return Sprite.Create(tex, new Rect(0, 0, TexSize, TexSize), new Vector2(0.5f, 0f), PixelsPerUnit);
        }
    }
}
