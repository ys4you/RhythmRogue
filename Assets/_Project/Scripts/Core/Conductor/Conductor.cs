using System;
using RhythmRogue.Util;
using UnityEngine;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Central beat clock. Tracks song position in beats via AudioSettings.dspTime
    /// for audio-thread precision. All timing-dependent systems read from this singleton.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public class Conductor : Util.Singleton<Conductor>, IConductor
    {
        private AudioSource _audioSource;

        // DSP timing
        private double _dspSongStartTime;
        private double _dspPauseTime;
        private double _totalPausedDuration;
        private float _songOffset;
        private float _calibrationSeconds;

        // BPM tracking
        private float _bpm;
        private float _secPerBeat;
        private float _bpmChangeBeatOrigin;
        private double _bpmChangeDspOrigin;

        // Beat events
        private int _lastReportedBeat = -1;
        private int _lastReportedHalfBeat = -1;

        private bool _isPlaying;
        private bool _isPaused;

        public float SongPositionInBeats { get; private set; }
        public float SongPositionInSeconds { get; private set; }
        public int CurrentBeat { get; private set; }
        public float SecPerBeat => _secPerBeat;
        public float BPM => _bpm;
        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;

        public event Action<int> OnBeat;
        public event Action<int> OnHalfBeat;
        public event Action<float, float> OnBpmChanged;
        public event Action OnSongFinished;

        /// <summary>Assign the clip the conductor will play. Call before <see cref="Play"/>.</summary>
        public void SetClip(AudioClip clip)
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            _audioSource.clip = clip;
        }

        protected override void Awake()
        {
            base.Awake();
            _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused) return;

            if (!_audioSource.isPlaying && SongPositionInSeconds > 0.5f)
            {
                _isPlaying = false;
                OnSongFinished?.Invoke();
                return;
            }

            UpdateTiming();
            CheckBeatEvents();
        }

        public void Play(float bpm, float songOffset = 0f)
        {
            if (_audioSource.clip == null) { GameLog.Error("[Conductor] No AudioClip assigned."); return; }

            _songOffset = songOffset;
            _calibrationSeconds = RhythmRogue.Core.Audio.AudioSettings.CalibrationOffsetSeconds;
            _bpm = bpm;
            _secPerBeat = 60f / _bpm;
            _bpmChangeBeatOrigin = 0f;
            _totalPausedDuration = 0.0;
            _lastReportedBeat = -1;
            _lastReportedHalfBeat = -1;

            // PlayScheduled aligns to the audio buffer boundary for precise start
            double startDsp = AudioSettings.dspTime + 0.1;
            _dspSongStartTime = startDsp;
            _bpmChangeDspOrigin = _dspSongStartTime;
            _audioSource.PlayScheduled(startDsp);

            _isPlaying = true;
            _isPaused = false;
            GameLog.Info($"[Conductor] Playing at {_bpm} BPM, offset {_songOffset}s");
        }

        public void Pause()
        {
            if (!_isPlaying || _isPaused) return;
            _isPaused = true;
            _dspPauseTime = AudioSettings.dspTime;
            _audioSource.Pause();
        }

        public void Resume()
        {
            if (!_isPlaying || !_isPaused) return;
            _totalPausedDuration += AudioSettings.dspTime - _dspPauseTime;
            _isPaused = false;
            _audioSource.UnPause();
        }

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

        public void SetBPM(float newBpm)
        {
            if (newBpm <= 0f) { GameLog.Error($"[Conductor] Invalid BPM: {newBpm}"); return; }
            if (newBpm == _bpm) return;

            float oldBpm = _bpm;

            // Snapshot current position before changing BPM to prevent drift
            _bpmChangeBeatOrigin = SongPositionInBeats;
            _bpmChangeDspOrigin = AdjustedDspTime();

            _bpm = newBpm;
            _secPerBeat = 60f / _bpm;
            GameLog.Info($"[Conductor] BPM changed: {oldBpm} -> {newBpm} at beat {_bpmChangeBeatOrigin:F2}");
            OnBpmChanged?.Invoke(oldBpm, newBpm);
        }

        // Combined timing offset (the song's authored offset plus the player's latency
        // calibration), subtracted from the DSP clock so it shifts note visuals AND hit
        // detection together, since both read SongPositionInBeats. Snapshotted at Play.
        private double AdjustedDspTime() => AudioSettings.dspTime - _totalPausedDuration - (_songOffset + _calibrationSeconds);

        // Beat position = origin at last BPM change + beats elapsed since
        private void UpdateTiming()
        {
            double dspNow = AdjustedDspTime();
            if (dspNow < _dspSongStartTime) return;

            SongPositionInSeconds = (float)(dspNow - _dspSongStartTime);
            float beatsSinceBpmChange = (float)((dspNow - _bpmChangeDspOrigin) / _secPerBeat);
            SongPositionInBeats = _bpmChangeBeatOrigin + beatsSinceBpmChange;
            CurrentBeat = Mathf.FloorToInt(SongPositionInBeats);
        }

        private void CheckBeatEvents()
        {
            if (CurrentBeat > _lastReportedBeat && SongPositionInBeats >= 0f)
            {
                _lastReportedBeat = CurrentBeat;
                OnBeat?.Invoke(CurrentBeat);
            }

            int halfBeat = Mathf.FloorToInt(SongPositionInBeats * 2f);
            if (halfBeat > _lastReportedHalfBeat && SongPositionInBeats >= 0f)
            {
                _lastReportedHalfBeat = halfBeat;
                OnHalfBeat?.Invoke(halfBeat);
            }
        }

        protected override void OnDestroy()
        {
            OnBeat = null; OnHalfBeat = null; OnBpmChanged = null; OnSongFinished = null;
            base.OnDestroy();
        }
    }
}
