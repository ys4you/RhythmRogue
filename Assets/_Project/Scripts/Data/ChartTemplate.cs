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

        [Header("Defaults")]
        [Tooltip("Lead-in silence before the first section, in beats. Gives the player a moment to prepare.")]
        [Min(0f)]
        public float leadInBeats = 4f;

        [Tooltip("Tail silence after the last section, in beats. Song fades out.")]
        [Min(0f)]
        public float tailBeats = 2f;

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

        /// <summary>Total duration including lead-in and tail.</summary>
        public float TotalBeats => leadInBeats + TotalSectionBeats + tailBeats;
    }
}
