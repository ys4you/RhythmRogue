using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Assembles a BattleChart from a ChartTemplate and PatternLibrary.
    /// 
    /// For each section slot in the template:
    ///   1. Query the library for matching patterns (difficulty, tags)
    ///   2. Pick a pattern using the seeded random (deterministic)
    ///   3. Stamp the pattern's relative beat offsets into absolute positions
    ///   4. For Both sections, pick two patterns (one per highway)
    /// 
    /// Determinism: same seed + same template + same library = same chart.
    /// The seed is forked from RandomDomain.Charts per encounter.
    /// 
    /// Pure C#, no MonoBehaviour, no Unity dependencies beyond logging.
    /// 
    /// SOLID:
    ///   S — Only assembles charts. No playback, no rendering.
    ///   O — New section types or selection strategies don't modify this class.
    ///   D — Depends on ISeededRandom abstraction, not concrete SeededRandom.
    /// </summary>
    public static class ChartAssembler
    {
        // Reusable buffer to avoid per-section allocation
        private static readonly List<PatternData> _queryBuffer = new(32);

        /// <summary>
        /// Assemble a BattleChart from a template and pattern library.
        /// </summary>
        /// <param name="template">The section structure to fill.</param>
        /// <param name="library">Available patterns to pick from.</param>
        /// <param name="rng">Seeded random for deterministic selection.</param>
        /// <param name="bpm">Final BPM for this battle (after enemy modifier).</param>
        /// <returns>Fully assembled BattleChart ready for playback.</returns>
        public static BattleChart Assemble(
            ChartTemplate template,
            PatternLibrary library,
            ISeededRandom rng,
            float bpm)
        {
            if (template == null)
            {
                Debug.LogError("[ChartAssembler] Template is null.");
                return null;
            }

            if (library == null || library.patterns.Count == 0)
            {
                Debug.LogError("[ChartAssembler] Pattern library is null or empty.");
                return null;
            }

            if (rng == null)
            {
                Debug.LogError("[ChartAssembler] Seeded random is null.");
                return null;
            }

            // Fork the RNG so enemy pattern picks don't shift player picks
            ISeededRandom enemyRng = rng.Fork("enemy_patterns");
            ISeededRandom playerRng = rng.Fork("player_patterns");

            var sections = new List<ChartSection>(template.sections.Count);
            float currentBeat = template.leadInBeats;

            for (int i = 0; i < template.sections.Count; i++)
            {
                SectionSlot slot = template.sections[i];

                ChartSection section = AssembleSection(
                    slot, currentBeat, library, enemyRng, playerRng);

                sections.Add(section);
                currentBeat += slot.durationBeats;
            }

            float totalBeats = currentBeat + template.tailBeats;

            var chart = new BattleChart(bpm, template.leadInBeats, totalBeats, sections);

            Debug.Log($"[ChartAssembler] Assembled: {sections.Count} sections, " +
                      $"{chart.PlayerNoteCount} player notes, " +
                      $"{chart.EnemyNoteCount} enemy notes, " +
                      $"{totalBeats:F0} total beats at {bpm} BPM");

            return chart;
        }

        /// <summary>
        /// Assemble a single section from a slot definition.
        /// </summary>
        private static ChartSection AssembleSection(
            SectionSlot slot,
            float startBeat,
            PatternLibrary library,
            ISeededRandom enemyRng,
            ISeededRandom playerRng)
        {
            List<StampedNote> enemyNotes = null;
            List<StampedNote> playerNotes = null;
            string enemyPatternName = null;
            string playerPatternName = null;

            switch (slot.type)
            {
                case SectionType.EnemyOnly:
                {
                    PatternData pattern = PickPattern(slot, library, enemyRng);
                    enemyNotes = StampPattern(pattern, startBeat, slot.durationBeats);
                    enemyPatternName = pattern?.patternName;
                    playerNotes = new List<StampedNote>();
                    break;
                }

                case SectionType.PlayerOnly:
                {
                    PatternData pattern = PickPattern(slot, library, playerRng);
                    playerNotes = StampPattern(pattern, startBeat, slot.durationBeats);
                    playerPatternName = pattern?.patternName;
                    enemyNotes = new List<StampedNote>();
                    break;
                }

                case SectionType.Both:
                {
                    PatternData ePattern = PickPattern(slot, library, enemyRng);
                    PatternData pPattern = PickPattern(slot, library, playerRng);

                    enemyNotes = StampPattern(ePattern, startBeat, slot.durationBeats);
                    playerNotes = StampPattern(pPattern, startBeat, slot.durationBeats);

                    enemyPatternName = ePattern?.patternName;
                    playerPatternName = pPattern?.patternName;
                    break;
                }
            }

            return new ChartSection(
                slot.type,
                startBeat,
                slot.durationBeats,
                enemyNotes,
                playerNotes,
                enemyPatternName,
                playerPatternName);
        }

        /// <summary>
        /// Pick a pattern from the library matching the slot's constraints.
        /// Uses weighted selection based on pattern.weight.
        /// </summary>
        private static PatternData PickPattern(
            SectionSlot slot,
            PatternLibrary library,
            ISeededRandom rng)
        {
            // Forced pattern overrides random selection
            if (slot.IsForced)
                return slot.forcedPattern;

            // Query matching patterns
            int count = library.Query(slot.maxDifficulty, slot.requiredTags, _queryBuffer);

            if (count == 0)
            {
                // Fallback: try with relaxed tag requirement (any instead of all)
                count = library.QueryAny(slot.maxDifficulty, slot.requiredTags, _queryBuffer);
            }

            if (count == 0)
            {
                // Last resort: pick any pattern at or below difficulty
                count = library.Query(slot.maxDifficulty, PatternTag.None, _queryBuffer);
            }

            if (count == 0)
            {
                Debug.LogWarning("[ChartAssembler] No patterns found for slot. " +
                                 $"Difficulty: {slot.maxDifficulty}, Tags: {slot.requiredTags}");
                return null;
            }

            // Weighted random pick
            float totalWeight = 0f;
            for (int i = 0; i < _queryBuffer.Count; i++)
                totalWeight += _queryBuffer[i].weight;

            float roll = rng.NextFloat() * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                cumulative += _queryBuffer[i].weight;
                if (roll < cumulative)
                    return _queryBuffer[i];
            }

            // Fallback (shouldn't reach here but just in case)
            return _queryBuffer[_queryBuffer.Count - 1];
        }

        /// <summary>
        /// Stamp a pattern's relative notes into absolute beat positions.
        /// Notes beyond the section's duration are trimmed.
        /// </summary>
        private static List<StampedNote> StampPattern(
            PatternData pattern,
            float startBeat,
            float sectionDuration)
        {
            var result = new List<StampedNote>();

            if (pattern == null || pattern.notes == null)
                return result;

            for (int i = 0; i < pattern.notes.Count; i++)
            {
                PatternNote note = pattern.notes[i];

                // Trim notes that start beyond the section
                if (note.BeatOffset >= sectionDuration)
                    continue;

                float absoluteBeat = startBeat + note.BeatOffset;

                // Trim hold duration to not exceed section
                float holdBeats = note.HoldBeats;
                float noteEnd = note.BeatOffset + holdBeats;
                if (noteEnd > sectionDuration)
                    holdBeats = sectionDuration - note.BeatOffset;

                result.Add(new StampedNote(note.Lane, absoluteBeat, holdBeats));
            }

            return result;
        }
    }
}
