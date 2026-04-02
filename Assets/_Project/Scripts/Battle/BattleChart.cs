using System.Collections.Generic;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// The fully assembled chart for a battle.
    /// 
    /// Produced by ChartAssembler. Contains an ordered list of sections,
    /// each with stamped notes for the enemy and/or player highway.
    /// 
    /// The BattleManager walks through sections sequentially, feeding
    /// notes to the appropriate highway(s) as each section starts.
    /// 
    /// Also provides flat note lists for the full song — used by
    /// systems that need all notes at once (accuracy tracking, progress bar).
    /// </summary>
    public class BattleChart
    {
        /// <summary>BPM for this battle (after enemy modifier applied).</summary>
        public float BPM { get; }

        /// <summary>Lead-in silence before first section, in beats.</summary>
        public float LeadInBeats { get; }

        /// <summary>Total duration of the chart in beats (including lead-in and tail).</summary>
        public float TotalBeats { get; }

        /// <summary>Ordered list of sections.</summary>
        public IReadOnlyList<ChartSection> Sections => _sections;

        /// <summary>All enemy notes across all sections, sorted by beat.</summary>
        public IReadOnlyList<StampedNote> AllEnemyNotes => _allEnemyNotes;

        /// <summary>All player notes across all sections, sorted by beat.</summary>
        public IReadOnlyList<StampedNote> AllPlayerNotes => _allPlayerNotes;

        /// <summary>Total player notes — used for accuracy % calculation.</summary>
        public int PlayerNoteCount => _allPlayerNotes.Count;

        /// <summary>Total enemy notes.</summary>
        public int EnemyNoteCount => _allEnemyNotes.Count;

        private readonly List<ChartSection> _sections;
        private readonly List<StampedNote> _allEnemyNotes;
        private readonly List<StampedNote> _allPlayerNotes;

        public BattleChart(
            float bpm,
            float leadInBeats,
            float totalBeats,
            List<ChartSection> sections)
        {
            BPM = bpm;
            LeadInBeats = leadInBeats;
            TotalBeats = totalBeats;
            _sections = sections ?? new List<ChartSection>();

            // Build flat note lists from sections
            _allEnemyNotes = new List<StampedNote>();
            _allPlayerNotes = new List<StampedNote>();

            for (int i = 0; i < _sections.Count; i++)
            {
                var section = _sections[i];
                _allEnemyNotes.AddRange(section.EnemyNotes);
                _allPlayerNotes.AddRange(section.PlayerNotes);
            }

            // Sort by beat (patterns may overlap at section boundaries)
            _allEnemyNotes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            _allPlayerNotes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
        }

        /// <summary>
        /// Find which section is active at the given beat position.
        /// Returns null if in lead-in or past all sections.
        /// </summary>
        public ChartSection GetSectionAtBeat(float beat)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                if (beat >= _sections[i].StartBeat && beat < _sections[i].EndBeat)
                    return _sections[i];
            }
            return null;
        }

        /// <summary>
        /// Get the section index at the given beat. Returns -1 if not in a section.
        /// </summary>
        public int GetSectionIndexAtBeat(float beat)
        {
            for (int i = 0; i < _sections.Count; i++)
            {
                if (beat >= _sections[i].StartBeat && beat < _sections[i].EndBeat)
                    return i;
            }
            return -1;
        }
    }
}
