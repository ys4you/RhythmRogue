using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Which pose a character is showing. Sing states map to the four lanes
    /// (0 Left, 1 Down, 2 Up, 3 Right). Idle is the beat-driven bop; Miss is an
    /// optional slump on a dropped note.
    /// </summary>
    public enum CharacterState
    {
        Idle,
        SingLeft,
        SingDown,
        SingUp,
        SingRight,
        Miss
    }

    /// <summary>
    /// One drawn frame of a character animation. In authored art the sprite carries the pose
    /// and the offset/scale/flip only fine-tune alignment. For the built-in placeholder every
    /// frame reuses a single silhouette and the pose is expressed purely through these
    /// transform values, so real art and the placeholder run the exact same playback code.
    /// </summary>
    [System.Serializable]
    public class CharacterFrame
    {
        public Sprite sprite;
        [Tooltip("Local position offset from rest, in world units.")]
        public Vector2 offset = Vector2.zero;
        [Tooltip("Scale multiplier on the base scale (1 = base). Values <= 0 are treated as 1.")]
        public float scale = 1f;
        public bool flipX = false;
    }

    /// <summary>
    /// A named animation clip for one <see cref="CharacterState"/>. Idle advances one frame per
    /// beat (a two-frame idle becomes a left/right bop); sing and miss clips play at
    /// <see cref="fps"/> and, unless <see cref="loop"/> is set, hold their last frame while the
    /// note is sustained.
    /// </summary>
    [System.Serializable]
    public class CharacterClip
    {
        public CharacterState state = CharacterState.Idle;
        public CharacterFrame[] frames;
        [Tooltip("Frames per second for sing/miss playback. Idle ignores this and steps on the beat.")]
        public float fps = 12f;
        [Tooltip("Loop the frames. Idle loops on the beat; sing poses usually hold their last frame.")]
        public bool loop = false;

        public bool HasFrames => frames != null && frames.Length > 0;
    }

    /// <summary>
    /// The single swap point for a rhythm-battle performer's look, shared by the battle scene
    /// and the map marker. Assign authored clips per state to reskin; any state left empty falls
    /// back to a generated hooded-silhouette placeholder, so a fresh asset is usable with zero
    /// art. Both the player and (next pass) the enemy use this same config type.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterVisual", menuName = "RhythmRogue/Data/Character Visual", order = 40)]
    public class CharacterVisualConfig : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Performer";

        [Header("Clips (leave empty to use the placeholder)")]
        [Tooltip("Authored animation clips per state. Missing states fall back to a generated " +
                 "placeholder pose, so the config works with no art assigned.")]
        public CharacterClip[] clips;

        [Header("Placement")]
        [Tooltip("Base local scale of the visual. Multiplied by each frame's scale.")]
        [Min(0.01f)]
        public float baseScale = 1f;

        [Tooltip("Sprite sorting order for the battle character. Receptors sit at 1, so keep " +
                 "characters at or below 0 to stay behind the highway.")]
        public int sortingOrder = 0;

        [Tooltip("Flip the whole character horizontally, e.g. to face the opponent.")]
        public bool flip = false;

        /// <summary>
        /// The clip for a state: the authored one if present and non-empty, else a generated
        /// placeholder clip built from the shared silhouette.
        /// </summary>
        public CharacterClip GetClip(CharacterState state)
        {
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i] != null && clips[i].state == state && clips[i].HasFrames)
                        return clips[i];
            }
            return PlaceholderCharacterArt.GetClip(state);
        }

        /// <summary>
        /// Sprite used by the map marker: the first frame of the idle clip if authored, else the
        /// placeholder silhouette. Lets the map show the same figure that fights in battle.
        /// </summary>
        public Sprite MapSprite
        {
            get
            {
                if (clips != null)
                {
                    for (int i = 0; i < clips.Length; i++)
                    {
                        var c = clips[i];
                        if (c != null && c.state == CharacterState.Idle && c.HasFrames &&
                            c.frames[0] != null && c.frames[0].sprite != null)
                            return c.frames[0].sprite;
                    }
                }
                return PlaceholderCharacterArt.PlaceholderSprite;
            }
        }
    }
}
