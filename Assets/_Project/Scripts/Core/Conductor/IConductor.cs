using System;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Abstraction for the central beat clock that drives all rhythm gameplay.
    /// 
    /// All timing-dependent systems (note highway, hit detection, animations,
    /// enemy attacks) read from this interface rather than calculating their
    /// own timing. Single source of truth for beat position.
    /// 
    /// Event strategy: C# events (not EventBus) because beat events fire
    /// every ~333ms at 180 BPM. The EventBus is for game-wide events
    /// (battle started, combo changed). High-frequency beat callbacks need
    /// zero-overhead direct subscriptions.
    /// 
    /// See Events/UsageExamples.cs for the two-layer event pattern:
    ///   Layer 1: C# events on IConductor (this interface) for per-beat timing
    ///   Layer 2: EventBus for game-wide events (NoteJudgedEvent, ComboChangedEvent)
    /// </summary>
    public interface IConductor
    {
        // =================================================================
        // TIMING — read-only state
        // =================================================================

        /// <summary>
        /// Current song position in beats (fractional).
        /// e.g. 4.75 means three-quarters through the 5th beat.
        /// </summary>
        float SongPositionInBeats { get; }

        /// <summary>
        /// Current song position in seconds, adjusted for offset.
        /// </summary>
        float SongPositionInSeconds { get; }

        /// <summary>
        /// Current whole beat number (floor of SongPositionInBeats).
        /// Increments once per beat. Starts at 0.
        /// </summary>
        int CurrentBeat { get; }

        /// <summary>
        /// Seconds per beat at the current BPM.
        /// Convenience: 60f / BPM.
        /// </summary>
        float SecPerBeat { get; }

        /// <summary>
        /// Current beats per minute.
        /// </summary>
        float BPM { get; }

        /// <summary>
        /// Whether the Conductor is currently playing a song.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Whether the Conductor is paused.
        /// </summary>
        bool IsPaused { get; }

        // =================================================================
        // EVENTS — high-frequency, C# events (not EventBus)
        // =================================================================

        /// <summary>
        /// Fired once per whole beat crossing. Parameter is the beat number.
        /// Subscribe from: note highway, hit detection, beat-synced animations.
        /// </summary>
        event Action<int> OnBeat;

        /// <summary>
        /// Fired on every half-beat (8th notes at current BPM).
        /// Parameter is the half-beat number (0, 1, 2, 3... where
        /// even = on-beat, odd = off-beat).
        /// </summary>
        event Action<int> OnHalfBeat;

        /// <summary>
        /// Fired when BPM changes at runtime.
        /// Parameters: old BPM, new BPM.
        /// Subscribe from: note highway (scroll speed), UI (BPM display).
        /// </summary>
        event Action<float, float> OnBpmChanged;

        /// <summary>
        /// Fired when the song finishes playing naturally (AudioSource ends).
        /// Subscribe from: BattleManager to detect end-of-song condition.
        /// </summary>
        event Action OnSongFinished;

        // =================================================================
        // PLAYBACK CONTROL
        // =================================================================

        /// <summary>
        /// Start playing a song. Records the DSP start time for all
        /// future beat calculations.
        /// </summary>
        /// <param name="bpm">Starting BPM for the song.</param>
        /// <param name="songOffset">
        /// Offset in seconds for lead-in silence before the first beat.
        /// Incorporates the player's audio calibration offset.
        /// </param>
        void Play(float bpm, float songOffset = 0f);

        /// <summary>
        /// Pause playback. Preserves current position.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume from a paused state.
        /// </summary>
        void Resume();

        /// <summary>
        /// Stop playback and reset all timing state.
        /// </summary>
        void Stop();

        /// <summary>
        /// Change BPM at runtime without breaking sync.
        /// Stores the beat position at the moment of change to prevent drift.
        /// Used by enemy modifiers and boss phase transitions.
        /// </summary>
        /// <param name="newBpm">New beats per minute.</param>
        void SetBPM(float newBpm);
    }
}