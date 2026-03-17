using System;
using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Tracks accuracy statistics for the current battle.
    /// 
    /// Subscribes to JudgmentSystem.OnJudgment and accumulates:
    ///   - Count per judgment type (Perfect, Good, Bad, Miss)
    ///   - Total notes judged
    ///   - Average absolute timing offset
    ///   - Early vs late bias (are they consistently early or late?)
    /// 
    /// These stats feed into:
    ///   - Battle UI (PROTO-015): live accuracy percentage
    ///   - Run Summary (PROTO-022): final accuracy stats
    ///   - Calibration hints: if average offset is consistently ±20ms+,
    ///     suggest the player adjust calibration
    /// 
    /// Separate from JudgmentSystem to respect SRP — judgment evaluates,
    /// this class accumulates.
    /// </summary>
    public class AccuracyTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;

        // =================================================================
        // STATS
        // =================================================================

        /// <summary>Counts per judgment type.</summary>
        private readonly int[] _counts = new int[4]; // Indexed by (int)Judgment

        /// <summary>Total notes judged (all types).</summary>
        public int TotalNotes { get; private set; }

        /// <summary>Sum of absolute offsets for averaging.</summary>
        private float _totalAbsOffset;

        /// <summary>Sum of signed offsets for early/late bias.</summary>
        private float _totalSignedOffset;

        /// <summary>Count of player-hit notes (excludes auto-misses for offset averaging).</summary>
        private int _hitNotes;

        // =================================================================
        // PUBLIC QUERIES
        // =================================================================

        public int PerfectCount => _counts[(int)Judgment.Perfect];
        public int GoodCount => _counts[(int)Judgment.Good];
        public int BadCount => _counts[(int)Judgment.Bad];
        public int MissCount => _counts[(int)Judgment.Miss];

        /// <summary>
        /// Percentage of notes that were Perfect or Good (0.0 to 1.0).
        /// This is the "accuracy" shown on the run summary.
        /// </summary>
        public float Accuracy => TotalNotes > 0
            ? (float)(PerfectCount + GoodCount) / TotalNotes
            : 1f;

        /// <summary>
        /// Average absolute timing offset in ms (excludes auto-misses).
        /// Lower = more precise player.
        /// </summary>
        public float AverageAbsOffsetMs => _hitNotes > 0
            ? _totalAbsOffset / _hitNotes
            : 0f;

        /// <summary>
        /// Average signed timing offset in ms (excludes auto-misses).
        /// Negative = consistently early, positive = consistently late.
        /// Use this to suggest calibration adjustments.
        /// </summary>
        public float AverageSignedOffsetMs => _hitNotes > 0
            ? _totalSignedOffset / _hitNotes
            : 0f;

        /// <summary>
        /// Human-readable bias direction.
        /// </summary>
        public string BiasDirection
        {
            get
            {
                float bias = AverageSignedOffsetMs;
                if (Mathf.Abs(bias) < 5f) return "Centered";
                return bias < 0f ? "Early" : "Late";
            }
        }

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void OnEnable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment += HandleJudgment;
        }

        private void OnDisable()
        {
            if (_judgmentSystem != null)
                _judgmentSystem.OnJudgment -= HandleJudgment;
        }

        // =================================================================
        // TRACKING
        // =================================================================

        private void HandleJudgment(JudgmentResult result)
        {
            TotalNotes++;
            _counts[(int)result.Judgment]++;

            // Only track timing offset for player-triggered hits (not auto-misses)
            if (!result.IsAutoMiss)
            {
                _hitNotes++;
                _totalAbsOffset += Mathf.Abs(result.AdjustedOffsetMs);
                _totalSignedOffset += result.AdjustedOffsetMs;
            }
        }

        /// <summary>
        /// Reset all stats. Call at the start of a new battle.
        /// </summary>
        public void Reset()
        {
            TotalNotes = 0;
            _hitNotes = 0;
            _totalAbsOffset = 0f;
            _totalSignedOffset = 0f;

            for (int i = 0; i < _counts.Length; i++)
                _counts[i] = 0;
        }
    }
}
