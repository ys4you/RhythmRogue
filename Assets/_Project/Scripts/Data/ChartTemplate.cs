using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Defines the structure of a battle's chart as an ordered list
    /// of section slots.
    /// 
    /// Each slot specifies: which highway(s) play, how long the section
    /// is, and what kind of patterns to pick. The ChartAssembler fills
    /// each slot with patterns from the PatternLibrary.
    /// 
    /// RepeatMode controls how the template scales to any song length:
    ///   None      — sections play once (fixed-length chart)
    ///   LoopAll   — all sections repeat until the song ends
    ///   LoopRange — intro sections play once, body sections loop,
    ///               outro sections play once
    /// 
    /// Different ChartTemplates create different battle feels:
    ///   - A Slow Enemy might alternate EnemyOnly → PlayerOnly (turn-based)
    ///   - A Chaos Enemy might have all Both sections (simultaneous)
    ///   - A Boss might start turn-based and escalate to simultaneous
    /// 
    /// Assign to EnemyData.chartTemplate, or let the assembler use
    /// a default template.
    /// 
    /// Create via: Assets → Create → RhythmRogue → Chart Template
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Chart Template",
        menuName = "RhythmRogue/Chart Template",
        order = 21)]
    public class ChartTemplate : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Human-readable name (e.g. 'turn_based_basic', 'boss_escalating').")]
        public string templateName;

        [Header("Sections")]
        [Tooltip("Ordered list of section slots. The assembler processes these in order.")]
        public List<SectionSlot> sections = new();

        [Header("Timing")]
        [Tooltip("Lead-in silence before the first section, in beats. Gives the player a moment to prepare.")]
        [Min(0f)]
        public float leadInBeats = 4f;

        [Tooltip("Tail silence after the last section, in beats. Song fades out.")]
        [Min(0f)]
        public float tailBeats = 2f;

        [Header("Repeat")]
        [Tooltip("How this template fills the song duration.\n\n" +
                 "None = sections play once.\n" +
                 "LoopAll = repeat all sections to fill.\n" +
                 "LoopRange = intro plays once, body loops, outro plays once.")]
        public RepeatMode repeatMode = RepeatMode.LoopAll;

        [Tooltip("First section index that loops (inclusive). Only used with LoopRange.")]
        [Min(0)]
        public int loopStartIndex;

        [Tooltip("Last section index that loops (inclusive). Only used with LoopRange.")]
        [Min(0)]
        public int loopEndIndex;

        // =================================================================
        // QUERIES
        // =================================================================

        /// <summary>Total duration of all sections (excluding lead-in and tail).</summary>
        public float TotalSectionBeats
        {
            get
            {
                float total = 0f;
                if (sections != null)
                {
                    for (int i = 0; i < sections.Count; i++)
                        total += sections[i].durationBeats;
                }
                return total;
            }
        }

        /// <summary>Total duration including lead-in and tail (single pass, no looping).</summary>
        public float TotalBeats => leadInBeats + TotalSectionBeats + tailBeats;

        /// <summary>
        /// Get the total beats of the loopable body sections.
        /// For LoopAll, this is TotalSectionBeats.
        /// For LoopRange, this is the sum of sections in [loopStart, loopEnd].
        /// For None, returns 0.
        /// </summary>
        public float LoopBodyBeats
        {
            get
            {
                if (sections == null || sections.Count == 0) return 0f;

                switch (repeatMode)
                {
                    case RepeatMode.LoopAll:
                        return TotalSectionBeats;

                    case RepeatMode.LoopRange:
                    {
                        int start = Mathf.Clamp(loopStartIndex, 0, sections.Count - 1);
                        int end = Mathf.Clamp(loopEndIndex, start, sections.Count - 1);

                        float total = 0f;
                        for (int i = start; i <= end; i++)
                            total += sections[i].durationBeats;
                        return total;
                    }

                    default:
                        return 0f;
                }
            }
        }

        // =================================================================
        // VALIDATION
        // =================================================================

        private void OnValidate()
        {
            if (sections != null && sections.Count > 0)
            {
                loopStartIndex = Mathf.Clamp(loopStartIndex, 0, sections.Count - 1);
                loopEndIndex = Mathf.Clamp(loopEndIndex, loopStartIndex, sections.Count - 1);
            }
        }
    }
}