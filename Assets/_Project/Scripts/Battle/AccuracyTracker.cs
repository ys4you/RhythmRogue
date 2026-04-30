using UnityEngine;

namespace RhythmRogue.Battle
{
    [DisallowMultipleComponent]
    public class AccuracyTracker : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private JudgmentSystem _judgmentSystem;

        private readonly int[] _counts = new int[4];
        private float _totalAbsOffset;
        private float _totalSignedOffset;
        private int _hitNotes;

        public int TotalNotes { get; private set; }
        public int PerfectCount => _counts[(int)Judgment.Perfect];
        public int GoodCount => _counts[(int)Judgment.Good];
        public int BadCount => _counts[(int)Judgment.Bad];
        public int MissCount => _counts[(int)Judgment.Miss];

        public float Accuracy => TotalNotes > 0 ? (float)(PerfectCount + GoodCount) / TotalNotes : 1f;
        public float AverageAbsOffsetMs => _hitNotes > 0 ? _totalAbsOffset / _hitNotes : 0f;
        public float AverageSignedOffsetMs => _hitNotes > 0 ? _totalSignedOffset / _hitNotes : 0f;

        public string BiasDirection
        {
            get
            {
                float bias = AverageSignedOffsetMs;
                if (Mathf.Abs(bias) < 5f) return "Centered";
                return bias < 0f ? "Early" : "Late";
            }
        }

        private void OnEnable() { if (_judgmentSystem != null) _judgmentSystem.OnJudgment += HandleJudgment; }
        private void OnDisable() { if (_judgmentSystem != null) _judgmentSystem.OnJudgment -= HandleJudgment; }

        private void HandleJudgment(JudgmentResult result)
        {
            TotalNotes++;
            _counts[(int)result.Judgment]++;

            // Only track timing offset for player-triggered hits, not auto-misses
            if (!result.IsAutoMiss)
            {
                _hitNotes++;
                _totalAbsOffset += Mathf.Abs(result.AdjustedOffsetMs);
                _totalSignedOffset += result.AdjustedOffsetMs;
            }
        }

        public void Reset()
        {
            TotalNotes = 0;
            _hitNotes = 0;
            _totalAbsOffset = 0f;
            _totalSignedOffset = 0f;
            for (int i = 0; i < _counts.Length; i++) _counts[i] = 0;
        }
    }
}
