using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Pure lane movement sequence. No timing. The assembler maps lanes[i] onto
    /// marker[i] one-to-one. Same shape works at any tempo or rhythm.
    /// </summary>
    [CreateAssetMenu(fileName = "New Lane Shape", menuName = "RhythmRogue/Lane Shape", order = 30)]
    public class LaneShape : ScriptableObject
    {
        [Header("Identity")]
        public string shapeName;
        [Tooltip("Family group for repeat prevention.")]
        public string familyId;

        [Header("Lanes")]
        [Tooltip("Ordered lane indices (0=Left, 1=Down, 2=Up, 3=Right).")]
        public List<int> lanes = new();

        [Header("Metadata")]
        [Range(1, 10)] public int difficulty = 1;
        public ShapeTag tags = ShapeTag.None;
        [Min(0.1f)] public float weight = 1f;

        public int Length => lanes?.Count ?? 0;
        public int StartLane => lanes is { Count: > 0 } ? lanes[0] : -1;
        public int EndLane => lanes is { Count: > 0 } ? lanes[^1] : -1;
        public string EffectiveFamily => !string.IsNullOrEmpty(familyId) ? familyId : name;

        // Wraps if phrase is longer than shape
        public int GetLane(int index) => lanes is { Count: > 0 } ? lanes[index % lanes.Count] : 0;
        public bool IsSameFamily(LaneShape other) => other != null && EffectiveFamily == other.EffectiveFamily;

        public int TransitionDistance(int previousEndLane)
        {
            if (previousEndLane < 0 || StartLane < 0) return 0;
            return Mathf.Abs(StartLane - previousEndLane);
        }

        public bool HasTags(ShapeTag required) => required == ShapeTag.None || (tags & required) == required;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (lanes != null)
                for (int i = 0; i < lanes.Count; i++) lanes[i] = Mathf.Clamp(lanes[i], 0, 3);
        }
#endif
    }

    [Flags]
    public enum ShapeTag
    {
        None      = 0,
        Staircase = 1 << 0,
        Trill     = 1 << 1,
        Jack      = 1 << 2,
        Skip      = 1 << 3,
        Roll      = 1 << 4,
        Jump      = 1 << 5,
        Chaotic   = 1 << 6,
    }
}
