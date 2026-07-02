using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Supplies a character's animation clips and display settings to a CharacterAnimator.
    /// Implemented by the authored <see cref="CharacterVisualConfig"/> (the player) and by
    /// <see cref="RuntimeCharacterVisual"/> (the enemy, which poses its single sprite). Lets one
    /// animator drive any performer without knowing where the clips come from.
    /// </summary>
    public interface ICharacterVisual
    {
        /// <summary>The clip to play for a state (authored or a generated placeholder pose).</summary>
        CharacterClip GetClip(CharacterState state);

        /// <summary>Design scale multiplier applied on top of the visual transform's own scale.</summary>
        float BaseScale { get; }

        /// <summary>Preferred sprite sorting order. A factory may apply it or keep an authored one.</summary>
        int SortingOrder { get; }

        /// <summary>Flip the character horizontally (e.g. to face the opponent).</summary>
        bool Flip { get; }

        /// <summary>Representative sprite for out-of-battle display such as the map marker.</summary>
        Sprite MapSprite { get; }
    }
}
