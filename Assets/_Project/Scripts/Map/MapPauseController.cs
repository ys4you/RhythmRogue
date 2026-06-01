using UnityEngine;

namespace RhythmRogue.Map
{
    /// <summary>
    /// DEPRECATED - superseded by RhythmRogue.Core.GlobalPauseManager.
    ///
    /// Pause is now handled globally by a single persistent GlobalPauseManager that covers
    /// every non-battle scene automatically, so no per-scene pause controller is needed.
    /// This component is intentionally inert and safe to remove.
    ///
    /// TO REMOVE: delete this file (and its .meta) from the Unity Project window, and remove
    /// the component from any GameObject it may still be attached to in MapScene.
    /// </summary>
    [System.Obsolete("Use GlobalPauseManager instead. This component does nothing and can be deleted.")]
    [DisallowMultipleComponent]
    public class MapPauseController : MonoBehaviour
    {
        // Intentionally empty. See GlobalPauseManager.
    }
}
