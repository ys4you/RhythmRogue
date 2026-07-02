using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Default placeholder art and single-sprite posing. Builds a plain white 32x32 sprite for
    /// characters with no art assigned, and poses any single sprite into the per-state clips (idle
    /// two-step plus directional sing leans) via transform offsets. The player uses the white
    /// square; the enemy poses its own existing sprite. Same code path in both cases.
    ///
    /// The white sprite is generated once and cached for the session.
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

        /// <summary>Placeholder clip for a state, posing the white square.</summary>
        public static CharacterClip GetClip(CharacterState state) => PoseClip(PlaceholderSprite, state);

        /// <summary>
        /// Pose any single sprite into a clip for a state: one frame offset/scaled to suggest the
        /// direction, except Idle which is a gentle two-frame bob that steps on the beat. Lets a
        /// character with only one sprite still bob and "sing" without directional art.
        /// </summary>
        public static CharacterClip PoseClip(Sprite baseSprite, CharacterState state)
        {
            Sprite s = baseSprite != null ? baseSprite : PlaceholderSprite;
            switch (state)
            {
                case CharacterState.Idle:
                    return Clip(state, loop: true,
                        Frame(s, new Vector2(0f, 0.02f), 1f),
                        Frame(s, new Vector2(0f, -0.03f), 1f));
                case CharacterState.SingLeft:
                    return Clip(state, false, Frame(s, new Vector2(-0.18f, 0.02f), 1f));
                case CharacterState.SingRight:
                    return Clip(state, false, Frame(s, new Vector2(0.18f, 0.02f), 1f));
                case CharacterState.SingUp:
                    return Clip(state, false, Frame(s, new Vector2(0f, 0.14f), 1.03f));
                case CharacterState.SingDown:
                    return Clip(state, false, Frame(s, new Vector2(0f, -0.10f), 0.97f));
                case CharacterState.Miss:
                    return Clip(state, false, Frame(s, new Vector2(0f, -0.05f), 0.96f));
                default:
                    return Clip(CharacterState.Idle, true, Frame(s, Vector2.zero, 1f));
            }
        }

        private static CharacterFrame Frame(Sprite sprite, Vector2 offset, float scale) =>
            new CharacterFrame { sprite = sprite, offset = offset, scale = scale, flipX = false };

        private static CharacterClip Clip(CharacterState state, bool loop, params CharacterFrame[] frames) =>
            new CharacterClip { state = state, frames = frames, fps = 8f, loop = loop };

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
