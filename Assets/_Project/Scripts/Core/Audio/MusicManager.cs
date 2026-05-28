using System.Collections;
using UnityEngine;
using RhythmRogue.Util;

namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Background music playback with crossfading. Singleton, survives scene transitions
    /// via Singleton's DontDestroyOnLoad, so a track started on the main menu can carry
    /// into the map without interruption (or smoothly fade into a different track).
    ///
    /// Plays a single track at a time. Internally uses two AudioSources so that a Play()
    /// call to a different track crossfades A out while B fades in.
    ///
    /// Idempotent: calling Play(currentTrack) while it's already playing is a no-op.
    /// This lets scene Start() methods unconditionally request their music without
    /// flicker on returning to the same scene.
    ///
    /// Tracks are loaded lazily from Resources/Audio/Music/ on first use. The first
    /// Play() call for a given track will block briefly while the clip loads; subsequent
    /// calls reuse the cached clip.
    ///
    /// Volume convention: master and music sliders are perceptual 0-1. The square-law
    /// taper is applied in CurrentLinearGain, so AudioSource.volume always receives the
    /// tapered linear value. Call ApplyVolumeFromSettings() after AudioSettings changes
    /// to update in-flight playback.
    /// </summary>
    public class MusicManager : Singleton<MusicManager>
    {
        [Header("Crossfade")]
        [Tooltip("Seconds for one source to fade in or out during a track change. " +
                 "Higher = smoother, but track change feels lazier.")]
        [SerializeField] private float _crossfadeDuration = 1.5f;

        [Tooltip("Seconds for the first track to fade in when starting from silence.")]
        [SerializeField] private float _fadeInDuration = 2f;

        [Tooltip("Seconds to fade out when Stop() is called.")]
        [SerializeField] private float _stopFadeDuration = 1f;

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private AudioSource _activeSource;     // The source currently audible (or fading in).
        private AudioSource _inactiveSource;   // The source currently silent (or fading out).

        private MusicTrack _currentTrack = MusicTrack.None;
        private Coroutine _activeFade;

        // Cached, loaded-on-demand from Resources.
        private readonly System.Collections.Generic.Dictionary<MusicTrack, AudioClip> _clipCache = new();

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            BuildSources();
            ApplyVolumeFromSettings();
        }

        private void BuildSources()
        {
            _sourceA = CreateSource("MusicSource_A");
            _sourceB = CreateSource("MusicSource_B");
            _activeSource = _sourceA;
            _inactiveSource = _sourceB;
        }

        private AudioSource CreateSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            return src;
        }

        // === Public API ===

        /// <summary>
        /// Start playing the given track. If the same track is already playing, this is
        /// a no-op. If a different track is playing, crossfades between them. If nothing
        /// is playing, fades in from silence.
        /// </summary>
        public void Play(MusicTrack track)
        {
            if (track == MusicTrack.None)
            {
                Stop();
                return;
            }
            if (track == _currentTrack && _activeSource != null && _activeSource.isPlaying) return;

            AudioClip clip = LoadClip(track);
            if (clip == null)
            {
                GameLog.Warn($"[MusicManager] Could not load clip for '{track}'.");
                return;
            }

            // Swap active/inactive: the new track will fade in on the previously-silent source.
            (_activeSource, _inactiveSource) = (_inactiveSource, _activeSource);
            _activeSource.clip = clip;
            _activeSource.volume = 0f;
            _activeSource.Play();

            if (_activeFade != null) StopCoroutine(_activeFade);
            float fadeDuration = _currentTrack == MusicTrack.None ? _fadeInDuration : _crossfadeDuration;
            _activeFade = StartCoroutine(Crossfade(_activeSource, _inactiveSource, fadeDuration));

            _currentTrack = track;
        }

        /// <summary>Fade out and stop the current track.</summary>
        public void Stop()
        {
            if (_currentTrack == MusicTrack.None) return;
            if (_activeFade != null) StopCoroutine(_activeFade);
            _activeFade = StartCoroutine(FadeOutAndStop(_activeSource, _stopFadeDuration));
            _currentTrack = MusicTrack.None;
        }

        /// <summary>
        /// The current tapered linear gain to send to AudioSource.volume.
        /// Combines master and music sliders (perceptual) into a single linear gain.
        /// Square-law taper applied so 50% slider feels like 50% loudness.
        /// </summary>
        private float CurrentLinearGain =>
            AudioSettings.ToLinearGain(AudioSettings.MasterVolume * AudioSettings.MusicVolume);

        /// <summary>
        /// Pulls master * music volumes from AudioSettings and re-applies to the active
        /// source. Call this from AudioSettings setters so slider changes update live.
        /// </summary>
        public void ApplyVolumeFromSettings()
        {
            if (_activeSource != null && _activeSource.isPlaying)
                _activeSource.volume = CurrentLinearGain;
            // Inactive source stays at 0 unless a fade is running, in which case the
            // running coroutine will overwrite each frame anyway.
        }

        public bool IsPlaying => _currentTrack != MusicTrack.None && _activeSource != null && _activeSource.isPlaying;
        public MusicTrack CurrentTrack => _currentTrack;

        // === Internals ===

        private AudioClip LoadClip(MusicTrack track)
        {
            if (_clipCache.TryGetValue(track, out var cached) && cached != null) return cached;

            string path = track.ToResourcePath();
            if (string.IsNullOrEmpty(path))
            {
                GameLog.Warn($"[MusicManager] No resource path mapped for '{track}'.");
                return null;
            }

            var clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                GameLog.Warn($"[MusicManager] AudioClip not found at Resources/{path}.");
                return null;
            }
            _clipCache[track] = clip;
            return clip;
        }

        /// <summary>
        /// Lerps the active source up to the target volume while lerping the inactive
        /// source down to zero. When the fade completes, the inactive source is stopped
        /// and its clip cleared to free memory pressure (Resources stays cached either way).
        /// </summary>
        private IEnumerator Crossfade(AudioSource fadeIn, AudioSource fadeOut, float duration)
        {
            float startInVolume = fadeIn.volume;
            float startOutVolume = fadeOut.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Read target each frame so live slider changes feel responsive.
                // CurrentLinearGain applies the perceptual taper.
                float targetVolume = CurrentLinearGain;
                fadeIn.volume = Mathf.Lerp(startInVolume, targetVolume, t);
                fadeOut.volume = Mathf.Lerp(startOutVolume, 0f, t);
                yield return null;
            }

            fadeIn.volume = CurrentLinearGain;
            fadeOut.volume = 0f;
            if (fadeOut.isPlaying) fadeOut.Stop();
            fadeOut.clip = null;
            _activeFade = null;
        }

        private IEnumerator FadeOutAndStop(AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
            source.volume = 0f;
            source.Stop();
            source.clip = null;
            _activeFade = null;
        }
    }
}
