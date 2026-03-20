using System;
using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Central beat clock for all rhythm gameplay.
    /// 
    /// Tracks the current song position in beats using AudioSettings.dspTime
    /// (audio-thread precision, frame-rate independent). Every timing-dependent
    /// system reads from this singleton rather than calculating its own timing.
    /// 
    /// Extends Singleton&lt;T&gt; from Util — persists across scenes via
    /// DontDestroyOnLoad, auto-creates if accessed before scene load.
    /// 
    /// DSP timing overview:
    ///   AudioSettings.dspTime runs on the audio thread at the sample rate
    ///   (typically 48000 Hz). It does NOT pause with Time.timeScale, does NOT
    ///   drift with frame rate, and does NOT stutter on GC spikes. This is why
    ///   every rhythm game uses it instead of Time.time.
    /// 
    /// BPM change strategy:
    ///   When BPM changes mid-song (boss phases, enemy modifiers), we snapshot
    ///   the current beat position and DSP time. Future beat calculations use
    ///   the new BPM relative to that snapshot. This prevents accumulated drift
    ///   from rounding errors across BPM boundaries.
    /// 
    /// SOLID breakdown:
    /// - S: Only tracks beat position and fires beat events. No game logic.
    /// - O: Consumers extend behavior by subscribing to events.
    /// - L: Substitutable via IConductor anywhere timing is needed.
    /// - I: Consumers see only IConductor.
    /// - D: No dependency on game systems. Game systems depend on IConductor.
    /// </summary>
    [DefaultExecutionOrder(-100)] // Tick before everything else
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : Util.Singleton<Conductor>, IConductor
    {
        // =================================================================
        // AUDIO — AudioSource is auto-added via RequireComponent.
        // Just assign an AudioClip in the Inspector and hit Play.
        // =================================================================

        private AudioSource _audioSource;

        // =================================================================
        // DSP TIMING STATE
        // =================================================================

        /// <summary>DSP time when the song started playing.</summary>
        private double _dspSongStartTime;

        /// <summary>DSP time when we paused (for accurate resume).</summary>
        private double _dspPauseTime;

        /// <summary>Accumulated DSP time spent paused (subtracted from elapsed).</summary>
        private double _totalPausedDuration;

        /// <summary>Song offset in seconds (lead-in silence + calibration).</summary>
        private float _songOffset;

        // =================================================================
        // BPM CHANGE TRACKING
        // =================================================================

        /// <summary>Current BPM.</summary>
        private float _bpm;

        /// <summary>Cached seconds per beat (60f / _bpm).</summary>
        private float _secPerBeat;

        /// <summary>Beat position at the moment of the last BPM change.</summary>
        private float _bpmChangeBeatOrigin;

        /// <summary>Song time in seconds at the moment of the last BPM change.</summary>
        private double _bpmChangeDspOrigin;

        // =================================================================
        // BEAT EVENT TRACKING
        // =================================================================

        /// <summary>Last whole beat number we fired OnBeat for.</summary>
        private int _lastReportedBeat = -1;

        /// <summary>Last half-beat number we fired OnHalfBeat for.</summary>
        private int _lastReportedHalfBeat = -1;

        // =================================================================
        // PLAYBACK STATE
        // =================================================================

        private bool _isPlaying;
        private bool _isPaused;

        // =================================================================
        // IConductor — PROPERTIES
        // =================================================================

        /// <inheritdoc/>
        public float SongPositionInBeats { get; private set; }

        /// <inheritdoc/>
        public float SongPositionInSeconds { get; private set; }

        /// <inheritdoc/>
        public int CurrentBeat { get; private set; }

        /// <inheritdoc/>
        public float SecPerBeat => _secPerBeat;

        /// <inheritdoc/>
        public float BPM => _bpm;

        /// <inheritdoc/>
        public bool IsPlaying => _isPlaying;

        /// <inheritdoc/>
        public bool IsPaused => _isPaused;

        // =================================================================
        // IConductor — EVENTS (C# events, not EventBus)
        // =================================================================

        /// <inheritdoc/>
        public event Action<int> OnBeat;

        /// <inheritdoc/>
        public event Action<int> OnHalfBeat;

        /// <inheritdoc/>
        public event Action<float, float> OnBpmChanged;

        // =================================================================
        // UNITY LIFECYCLE
        // =================================================================

        protected override void Awake()
        {
            base.Awake();

            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        /// <summary>Fired when the song finishes playing naturally.</summary>
        public event Action OnSongFinished;

        private void Update()
        {
            if (!_isPlaying || _isPaused)
                return;

            // Detect natural end of song (AudioSource stopped on its own)
            if (!_audioSource.isPlaying && SongPositionInSeconds > 0.5f)
            {
                _isPlaying = false;
                OnSongFinished?.Invoke();
                return;
            }

            UpdateTiming();
            CheckBeatEvents();
        }

        // =================================================================
        // IConductor — PLAYBACK CONTROL
        // =================================================================

        /// <inheritdoc/>
        public void Play(float bpm, float songOffset = 0f)
        {
            if (_audioSource.clip == null)
            {
                Debug.LogError("[Conductor] No AudioClip assigned to AudioSource. Cannot play.");
                return;
            }

            _songOffset = songOffset;
            _bpm = bpm;
            _secPerBeat = 60f / _bpm;

            // Reset BPM change tracking — song starts at beat 0
            _bpmChangeBeatOrigin = 0f;
            _totalPausedDuration = 0.0;

            // Reset beat event tracking
            _lastReportedBeat = -1;
            _lastReportedHalfBeat = -1;

            // Schedule playback on the audio thread for precise start
            // PlayScheduled is more accurate than Play() because it
            // aligns to the audio buffer boundary.
            double startDsp = AudioSettings.dspTime + 0.1; // 100ms lead-in for scheduling
            _dspSongStartTime = startDsp;
            _bpmChangeDspOrigin = _dspSongStartTime;

            _audioSource.PlayScheduled(startDsp);

            _isPlaying = true;
            _isPaused = false;

            Debug.Log($"[Conductor] Playing at {_bpm} BPM, offset {_songOffset}s, " +
                      $"scheduled at DSP {startDsp:F4}");
        }

        /// <inheritdoc/>
        public void Pause()
        {
            if (!_isPlaying || _isPaused)
                return;

            _isPaused = true;
            _dspPauseTime = AudioSettings.dspTime;
            _audioSource.Pause();
        }

        /// <inheritdoc/>
        public void Resume()
        {
            if (!_isPlaying || !_isPaused)
                return;

            // Track how long we were paused so timing stays accurate
            double pauseDuration = AudioSettings.dspTime - _dspPauseTime;
            _totalPausedDuration += pauseDuration;

            _isPaused = false;
            _audioSource.UnPause();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;

            _audioSource.Stop();

            SongPositionInBeats = 0f;
            SongPositionInSeconds = 0f;
            CurrentBeat = 0;
            _lastReportedBeat = -1;
            _lastReportedHalfBeat = -1;
        }

        /// <inheritdoc/>
        public void SetBPM(float newBpm)
        {
            if (newBpm <= 0f)
            {
                Debug.LogError($"[Conductor] Invalid BPM: {newBpm}. Must be positive.");
                return;
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (newBpm == _bpm)
                return;

            float oldBpm = _bpm;

            // Snapshot current beat position before changing BPM.
            // All future beat calculations are relative to this snapshot.
            _bpmChangeBeatOrigin = SongPositionInBeats;
            _bpmChangeDspOrigin = AudioSettings.dspTime - _totalPausedDuration;

            _bpm = newBpm;
            _secPerBeat = 60f / _bpm;

            Debug.Log($"[Conductor] BPM changed: {oldBpm} → {newBpm} at beat {_bpmChangeBeatOrigin:F2}");

            OnBpmChanged?.Invoke(oldBpm, newBpm);
        }

        // =================================================================
        // TIMING CALCULATION
        // =================================================================

        /// <summary>
        /// Core timing update. Uses AudioSettings.dspTime for audio-thread
        /// precision. Handles BPM changes by calculating beats relative to
        /// the last BPM change snapshot.
        /// 
        /// Formula:
        ///   elapsedSinceBpmChange = (dspTime - totalPaused) - bpmChangeDspOrigin
        ///   beatsSinceBpmChange   = elapsedSinceBpmChange / secPerBeat
        ///   totalBeats            = bpmChangeBeatOrigin + beatsSinceBpmChange
        /// </summary>
        private void UpdateTiming()
        {
            double dspNow = AudioSettings.dspTime - _totalPausedDuration;

            // Song position in seconds (from song start, adjusted for offset)
            SongPositionInSeconds = (float)(dspNow - _dspSongStartTime) - _songOffset;

            // Beats since the last BPM change point
            double elapsedSinceBpmChange = dspNow - _bpmChangeDspOrigin;
            float beatsSinceBpmChange = (float)(elapsedSinceBpmChange / _secPerBeat);

            // Total beat position = origin at last BPM change + beats elapsed since
            SongPositionInBeats = _bpmChangeBeatOrigin + beatsSinceBpmChange;

            // Whole beat number
            CurrentBeat = Mathf.FloorToInt(SongPositionInBeats);
        }

        /// <summary>
        /// Check whether we've crossed a new whole beat or half-beat
        /// boundary since the last frame, and fire events if so.
        /// </summary>
        private void CheckBeatEvents()
        {
            // Whole beat events
            if (CurrentBeat > _lastReportedBeat && SongPositionInBeats >= 0f)
            {
                _lastReportedBeat = CurrentBeat;
                OnBeat?.Invoke(CurrentBeat);
            }

            // Half-beat events (8th notes)
            int currentHalfBeat = Mathf.FloorToInt(SongPositionInBeats * 2f);

            if (currentHalfBeat > _lastReportedHalfBeat && SongPositionInBeats >= 0f)
            {
                _lastReportedHalfBeat = currentHalfBeat;
                OnHalfBeat?.Invoke(currentHalfBeat);
            }
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        protected override void OnDestroy()
        {
            OnBeat = null;
            OnHalfBeat = null;
            OnBpmChanged = null;
            OnSongFinished = null;
            base.OnDestroy();
        }
    }
}