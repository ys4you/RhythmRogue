using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util;

namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Centralised SFX playback. Resolves named clips from a registered library
    /// and plays them on a pooled set of AudioSources.
    ///
    /// Auto-loads on first access (Singleton pattern). Auto-loads the SfxLibrary
    /// ScriptableObject from Resources/Audio/SfxLibrary on Awake.
    ///
    /// Usage:
    ///   AudioManager.Instance.Play(SfxId.UiConfirm);
    ///   AudioManager.Instance.PlayPitched(SfxId.HitPerfect, pitch: 1.2f);
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        [SerializeField] private SfxLibrary _library;

        [Header("Source Pool")]
        [Tooltip("Number of pooled AudioSources for SFX. Higher = more concurrent sounds.")]
        [SerializeField] private int _sourcePoolSize = 8;

        [Header("Volume")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;

        private AudioSource[] _sourcePool;
        private int _nextSourceIndex;
        private Dictionary<SfxId, AudioClip> _clipMap;

        protected override void Awake()
        {
            base.Awake();

            // Only init if this instance won the singleton race
            if (Instance != this) return;

            LoadLibraryIfNeeded();
            BuildSourcePool();
            BuildClipMap();

            // Apply persisted volume settings now that we're set up
            _masterVolume = AudioSettings.MasterVolume;
            _sfxVolume = AudioSettings.SfxVolume;
        }

        private void LoadLibraryIfNeeded()
        {
            if (_library != null) return;

            _library = Resources.Load<SfxLibrary>("Audio/SfxLibrary");
            if (_library == null)
                GameLog.Warn("[AudioManager] No SfxLibrary assigned and none found at Resources/Audio/SfxLibrary. SFX playback will fail silently.");
        }

        private void BuildSourcePool()
        {
            _sourcePool = new AudioSource[_sourcePoolSize];
            for (int i = 0; i < _sourcePoolSize; i++)
            {
                var go = new GameObject($"SfxSource_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f; // 2D
                _sourcePool[i] = src;
            }
        }

        private void BuildClipMap()
        {
            _clipMap = new Dictionary<SfxId, AudioClip>();
            if (_library == null) return;

            foreach (var entry in _library.Entries)
            {
                if (entry.clip == null) continue;
                if (_clipMap.ContainsKey(entry.id))
                {
                    GameLog.Warn($"[AudioManager] Duplicate SfxId '{entry.id}' in library. First entry wins.");
                    continue;
                }
                _clipMap[entry.id] = entry.clip;
            }
        }

        // === Public API ===

        /// <summary>Play a registered SFX by id. No-op if not found.</summary>
        public void Play(SfxId id) => PlayInternal(id, pitch: 1f, volumeScale: 1f);

        /// <summary>Play with a pitch multiplier. Useful for tonal variation.</summary>
        public void PlayPitched(SfxId id, float pitch) => PlayInternal(id, pitch: pitch, volumeScale: 1f);

        /// <summary>Play with a volume scale (multiplied with master+sfx volume).</summary>
        public void PlayWithVolume(SfxId id, float volumeScale) => PlayInternal(id, pitch: 1f, volumeScale: volumeScale);

        /// <summary>Play with full control over pitch and volume scale.</summary>
        public void PlayAdvanced(SfxId id, float pitch, float volumeScale) => PlayInternal(id, pitch, volumeScale);

        public void SetMasterVolume(float v) => _masterVolume = Mathf.Clamp01(v);
        public void SetSfxVolume(float v) => _sfxVolume = Mathf.Clamp01(v);

        // === Internals ===

        private void PlayInternal(SfxId id, float pitch, float volumeScale)
        {
            if (_clipMap == null || !_clipMap.TryGetValue(id, out AudioClip clip) || clip == null)
            {
                GameLog.Warn($"[AudioManager] No clip registered for '{id}'.");
                return;
            }

            var src = GetNextSource();
            src.pitch = pitch;
            src.volume = _masterVolume * _sfxVolume * Mathf.Clamp01(volumeScale);
            src.PlayOneShot(clip);
        }

        private AudioSource GetNextSource()
        {
            var src = _sourcePool[_nextSourceIndex];
            _nextSourceIndex = (_nextSourceIndex + 1) % _sourcePoolSize;
            return src;
        }
    }
}
