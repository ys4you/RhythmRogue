using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// ScriptableObject mapping SfxId values to AudioClip assets.
    /// Edit in the Unity Inspector. Drop into Resources/Audio/SfxLibrary.asset
    /// for AudioManager to auto-load it.
    /// </summary>
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "RhythmRogue/Audio/SFX Library")]
    public class SfxLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public SfxId id;
            public AudioClip clip;
        }

        [SerializeField] private List<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
