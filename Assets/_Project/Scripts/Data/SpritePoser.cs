using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Turns a single sprite into per-state pose clips using only transform offset and scale, no
    /// extra art. The idle is a beat-stepped scale pulse (size independent, so it reads on a small
    /// placeholder square and a full-size character alike); sing poses lean toward the lane and
    /// punch slightly. Shared by the white-square placeholder and by any character that poses its
    /// one sprite (the enemy). Real directional art replaces these with authored frames.
    /// </summary>
    public static class SpritePoser
    {
        public static CharacterClip GetClip(Sprite sprite, CharacterState state)
        {
            switch (state)
            {
                case CharacterState.Idle:
                    return Clip(state, loop: true,
                        Frame(sprite, Vector2.zero, 1f),
                        Frame(sprite, Vector2.zero, 1.06f));
                case CharacterState.SingLeft:
                    return Clip(state, false, Frame(sprite, new Vector2(-0.22f, 0f), 1.05f));
                case CharacterState.SingRight:
                    return Clip(state, false, Frame(sprite, new Vector2(0.22f, 0f), 1.05f));
                case CharacterState.SingUp:
                    return Clip(state, false, Frame(sprite, new Vector2(0f, 0.18f), 1.06f));
                case CharacterState.SingDown:
                    return Clip(state, false, Frame(sprite, new Vector2(0f, -0.12f), 0.96f));
                case CharacterState.Miss:
                    return Clip(state, false, Frame(sprite, new Vector2(0f, -0.06f), 0.95f));
                default:
                    return Clip(CharacterState.Idle, true, Frame(sprite, Vector2.zero, 1f));
            }
        }

        private static CharacterFrame Frame(Sprite sprite, Vector2 offset, float scale) =>
            new CharacterFrame { sprite = sprite, offset = offset, scale = scale, flipX = false };

        private static CharacterClip Clip(CharacterState state, bool loop, params CharacterFrame[] frames) =>
            new CharacterClip { state = state, frames = frames, fps = 8f, loop = loop };
    }
}
