using System.Collections.Generic;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// One assembled section of a battle chart.
    /// 
    /// Contains the stamped notes for each highway plus timing info.
    /// The BattleManager reads these sequentially to know which
    /// highways are active and what notes to feed them.
    /// </summary>
    public class ChartSection
    {
        /// <summary>Which highway(s) are active in this section.</summary>
        public SectionType Type { get; }

        /// <summary>Beat where this section starts (absolute).</summary>
        public float StartBeat { get; }

        /// <summary>Duration of this section in beats.</summary>
        public float DurationBeats { get; }

        /// <summary>Beat where this section ends (exclusive).</summary>
        public float EndBeat => StartBeat + DurationBeats;

        /// <summary>
        /// Notes for the enemy highway. Empty if Type == PlayerOnly.
        /// </summary>
        public IReadOnlyList<StampedNote> EnemyNotes => _enemyNotes;

        /// <summary>
        /// Notes for the player highway. Empty if Type == EnemyOnly.
        /// </summary>
        public IReadOnlyList<StampedNote> PlayerNotes => _playerNotes;

        /// <summary>The pattern used for the enemy side (null if PlayerOnly).</summary>
        public string EnemyPatternName { get; }

        /// <summary>The pattern used for the player side (null if EnemyOnly).</summary>
        public string PlayerPatternName { get; }

        private readonly List<StampedNote> _enemyNotes;
        private readonly List<StampedNote> _playerNotes;

        public ChartSection(
            SectionType type,
            float startBeat,
            float durationBeats,
            List<StampedNote> enemyNotes,
            List<StampedNote> playerNotes,
            string enemyPatternName = null,
            string playerPatternName = null)
        {
            Type = type;
            StartBeat = startBeat;
            DurationBeats = durationBeats;
            _enemyNotes = enemyNotes ?? new List<StampedNote>();
            _playerNotes = playerNotes ?? new List<StampedNote>();
            EnemyPatternName = enemyPatternName;
            PlayerPatternName = playerPatternName;
        }
    }
}
