using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// A pure movement shape: an ordered sequence of lane indices.
    /// 
    /// Contains NO timing data. The assembler maps these lanes onto
    /// timing markers one-to-one:
    ///   Marker 1 -> lanes[0]
    ///   Marker 2 -> lanes[1]
    ///   Marker 3 -> lanes[2]
    ///   ...
    /// 
    /// This separation means the same shape works at any tempo, any
    /// rhythm, any song. The timing comes from the beat map (hand-authored
    /// or auto-detected). The shape comes from the library (seed-controlled).
    /// 
    /// Examples:
    ///   "staircase_up"   = [0, 1, 2, 3]       (ascending sweep)
    ///   "bounce_outer"   = [0, 3, 0, 3]       (wide alternation)
    ///   "zigzag"         = [0, 2, 3, 1]       (skip lanes)
    ///   "roll_right"     = [0, 1, 2, 3, 0, 1] (repeating sweep)
    /// 
    /// Create via: Assets > Create > RhythmRogue > Lane Shape
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Lane Shape",
        menuName = "RhythmRogue/Lane Shape",
        order = 30)]
    public class LaneShape : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name (e.g. 'staircase_up', 'bounce_outer').")]
        public string shapeName;

        [Tooltip("Family group for repeat prevention. Shapes sharing a familyId " +
                 "(e.g. staircase_up and staircase_down) are treated as the same " +
                 "movement for no-repeat checks.")]
        public string familyId;

        [Header("Lanes")]
        [Tooltip("Ordered lane indices (0=Left, 1=Down, 2=Up, 3=Right). " +
                 "Each entry maps to one timing marker in order.")]
        public List<int> lanes = new();

        [Header("Metadata")]
        [Tooltip("Difficulty tier (1 = easiest, 10 = hardest). Controls which " +
                 "shapes are available at each difficulty level.")]
        [Range(1, 10)]
        public int difficulty = 1;

        [Tooltip("Movement character tags for filtering.")]
        public ShapeTag tags = ShapeTag.None;

        [Tooltip("Selection weight. Higher = more likely to be picked.")]
        [Min(0.1f)]
        public float weight = 1f;

        // =================================================================
        // QUERIES
        // =================================================================

        /// <summary>Number of lanes in this shape.</summary>
        public int Length => lanes?.Count ?? 0;

        /// <summary>First lane in the shape. -1 if empty.</summary>
        public int StartLane => lanes != null && lanes.Count > 0 ? lanes[0] : -1;

        /// <summary>Last lane in the shape. -1 if empty.</summary>
        public int EndLane => lanes != null && lanes.Count > 0 ? lanes[lanes.Count - 1] : -1;

        /// <summary>Effective family for no-repeat checks.</summary>
        public string EffectiveFamily =>
            !string.IsNullOrEmpty(familyId) ? familyId : name;

        /// <summary>
        /// Get the lane at a given index, wrapping if the phrase is
        /// longer than the shape. This allows short shapes to fill
        /// long phrases by cycling.
        /// </summary>
        public int GetLane(int index)
        {
            if (lanes == null || lanes.Count == 0) return 0;
            return lanes[index % lanes.Count];
        }

        /// <summary>Whether this shape is in the same family as another.</summary>
        public bool IsSameFamily(LaneShape other)
        {
            if (other == null) return false;
            return EffectiveFamily == other.EffectiveFamily;
        }

        /// <summary>
        /// Transition distance from a previous end lane to this shape's start.
        /// </summary>
        public int TransitionDistance(int previousEndLane)
        {
            if (previousEndLane < 0 || StartLane < 0) return 0;
            int diff = StartLane - previousEndLane;
            return diff < 0 ? -diff : diff;
        }

        /// <summary>Check if this shape has ALL required tags.</summary>
        public bool HasTags(ShapeTag required)
        {
            if (required == ShapeTag.None) return true;
            return (tags & required) == required;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Clamp lanes to 0-3
            if (lanes != null)
            {
                for (int i = 0; i < lanes.Count; i++)
                    lanes[i] = Mathf.Clamp(lanes[i], 0, 3);
            }
        }
#endif
    }

    /// <summary>
    /// Tags describing a shape's movement character.
    /// </summary>
    [Flags]
    public enum ShapeTag
    {
        None        = 0,
        /// <summary>Sequential ascending or descending lane movement.</summary>
        Staircase   = 1 << 0,
        /// <summary>Alternating between two lanes.</summary>
        Trill       = 1 << 1,
        /// <summary>Same lane repeated (jacks).</summary>
        Jack        = 1 << 2,
        /// <summary>Skipping lanes (non-adjacent movement).</summary>
        Skip        = 1 << 3,
        /// <summary>Smooth rolling motion across all lanes.</summary>
        Roll        = 1 << 4,
        /// <summary>Two simultaneous lanes (jumps).</summary>
        Jump        = 1 << 5,
        /// <summary>Irregular, unpredictable movement.</summary>
        Chaotic     = 1 << 6,
    }
}
