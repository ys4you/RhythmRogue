using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Decides which lane(s) a beat marker maps to.
    /// 
    /// This is the "musical intelligence" of the charting system.
    /// It tracks hand position (which lanes were recently used) and
    /// uses marker type, intensity, and direction to choose lanes
    /// that feel natural and flow well.
    /// 
    /// Design principles:
    ///   - Adjacent lane transitions are preferred (feels smooth)
    ///   - Marker type biases lane choice (kicks low, snares high)
    ///   - High intensity can produce jumps (two simultaneous notes)
    ///   - Melodic markers follow the direction field (ascending = right)
    ///   - No lane is hammered repeatedly without variety
    ///   - Break markers suppress all output
    /// 
    /// Stateful: tracks last lane and lane usage history across calls.
    /// Create a new instance per chart generation pass.
    /// 
    /// Pure C#, no MonoBehaviour. Deterministic given the same seed.
    /// </summary>
    public class LanePlanner
    {
        private const int LaneCount = 4;

        // Lane bias weights per marker type: [lane0, lane1, lane2, lane3]
        // Higher = more likely to be chosen for that marker type
        private static readonly float[][] TypeLaneBias =
        {
            new[] { 3f, 2f, 1f, 0.5f }, // Kick — heavy left/down bias
            new[] { 0.5f, 1f, 2f, 3f }, // Snare — heavy right/up bias
            new[] { 1f, 2f, 2f, 1f },   // HiHat — center bias, alternates
            new[] { 1f, 1f, 1f, 1f },   // Melodic — neutral, uses direction
            new[] { 1.5f, 1f, 1f, 1.5f }, // Accent — outer lanes
            new[] { 1f, 1f, 1f, 1f },   // Drop — neutral, jumps handle intensity
            new[] { 0f, 0f, 0f, 0f },   // Break — no notes
            new[] { 1f, 1.5f, 1.5f, 1f }, // Fill — center bias, rapid
        };

        // =================================================================
        // STATE
        // =================================================================

        private readonly ISeededRandom _rng;
        private int _lastLane = -1;
        private int _secondLastLane = -1;
        private readonly int[] _laneUsage = new int[LaneCount];
        private int _totalNotes;

        // =================================================================
        // CONFIG
        // =================================================================

        private const float AdjacencyBonus = 2.5f;
        private const float RepeatPenalty = 0.3f;
        private const float OverusePenaltyPerHit = 0.15f;
        private const float JumpIntensityThreshold = 0.8f;
        private const float JumpChance = 0.35f;

        // =================================================================
        // CONSTRUCTOR
        // =================================================================

        public LanePlanner(ISeededRandom rng)
        {
            _rng = rng;
        }

        // =================================================================
        // PLANNING
        // =================================================================

        public struct PlannedNote
        {
            public int Lane;
            public int JumpLane;
            public bool ShouldPlace;
            public bool IsJump => JumpLane >= 0;
        }

        public PlannedNote Plan(BeatMarker marker)
        {
            if (marker.type == MarkerType.Break)
                return new PlannedNote { ShouldPlace = false, Lane = -1, JumpLane = -1 };

            int lane = ChooseLane(marker);

            int jumpLane = -1;
            if (marker.intensity >= JumpIntensityThreshold && _rng.NextFloat() < JumpChance)
            {
                jumpLane = ChooseJumpLane(lane, marker);
            }

            RecordLane(lane);

            return new PlannedNote
            {
                ShouldPlace = true,
                Lane = lane,
                JumpLane = jumpLane
            };
        }

        // =================================================================
        // LANE CHOICE
        // =================================================================

        private int ChooseLane(BeatMarker marker)
        {
            float[] weights = new float[LaneCount];
            float[] typeBias = TypeLaneBias[(int)marker.type];

            for (int i = 0; i < LaneCount; i++)
            {
                weights[i] = typeBias[i];

                if (_lastLane >= 0)
                {
                    int distance = Mathf.Abs(i - _lastLane);
                    if (distance == 0)
                        weights[i] *= RepeatPenalty;
                    else if (distance == 1)
                        weights[i] *= AdjacencyBonus;
                }

                if (_lastLane >= 0 && _secondLastLane >= 0 &&
                    i == _lastLane && _lastLane == _secondLastLane)
                {
                    weights[i] = 0f;
                }

                if (_totalNotes > 4)
                {
                    float avgUsage = _totalNotes / (float)LaneCount;
                    float overuse = Mathf.Max(0f, _laneUsage[i] - avgUsage);
                    weights[i] *= Mathf.Max(0.1f, 1f - overuse * OverusePenaltyPerHit);
                }
            }

            if (marker.type == MarkerType.Melodic && Mathf.Abs(marker.direction) > 0.1f)
            {
                ApplyDirectionBias(weights, marker.direction);
            }

            return WeightedPick(weights);
        }

        private void ApplyDirectionBias(float[] weights, float direction)
        {
            float strength = Mathf.Abs(direction) * 2f;

            if (direction > 0f)
            {
                for (int i = 0; i < LaneCount; i++)
                {
                    if (_lastLane >= 0 && i > _lastLane)
                        weights[i] *= (1f + strength);
                    else if (_lastLane >= 0 && i < _lastLane)
                        weights[i] *= Mathf.Max(0.2f, 1f - strength * 0.5f);
                }
            }
            else
            {
                for (int i = 0; i < LaneCount; i++)
                {
                    if (_lastLane >= 0 && i < _lastLane)
                        weights[i] *= (1f + strength);
                    else if (_lastLane >= 0 && i > _lastLane)
                        weights[i] *= Mathf.Max(0.2f, 1f - strength * 0.5f);
                }
            }
        }

        private int ChooseJumpLane(int primaryLane, BeatMarker marker)
        {
            float[] weights = new float[LaneCount];

            for (int i = 0; i < LaneCount; i++)
            {
                if (i == primaryLane)
                {
                    weights[i] = 0f;
                    continue;
                }

                int distance = Mathf.Abs(i - primaryLane);

                if (marker.intensity >= 0.95f)
                    weights[i] = distance >= 2 ? 3f : 1f;
                else
                    weights[i] = distance == 1 ? 3f : 1f;
            }

            return WeightedPick(weights);
        }

        // =================================================================
        // STATE TRACKING
        // =================================================================

        private void RecordLane(int lane)
        {
            _secondLastLane = _lastLane;
            _lastLane = lane;
            _laneUsage[lane]++;
            _totalNotes++;
        }

        public void Reset()
        {
            _lastLane = -1;
            _secondLastLane = -1;
            _totalNotes = 0;

            for (int i = 0; i < LaneCount; i++)
                _laneUsage[i] = 0;
        }

        // =================================================================
        // WEIGHTED PICK
        // =================================================================

        private int WeightedPick(float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
                total += weights[i];

            if (total <= 0f)
                return _rng.Range(0, LaneCount);

            float roll = _rng.NextFloat() * total;
            float cumulative = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return i;
            }

            return weights.Length - 1;
        }
    }
}