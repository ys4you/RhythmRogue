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
    /// Looping:
    ///   When targetDurationBeats is provided and the template has a
    ///   RepeatMode other than None, the assembler repeats sections
    ///   to fill the target duration. Each loop pass gets a unique RNG
    ///   fork so repeated sections pick different patterns.
    /// 
    /// Determinism: same seed + same template + same library + same target
    /// = same chart. The seed is forked from RandomDomain.Charts per encounter.
    /// 
    /// Pure C#, no MonoBehaviour, no Unity dependencies beyond logging.
    /// 
    /// SOLID:
    ///   S — Only assembles charts. No playback, no rendering.
    ///   O — New section types or repeat modes don't modify core stamping.
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
        /// <param name="targetDurationBeats">
        /// Target chart duration in beats (typically derived from audio clip length).
        /// If &lt;= 0, the template is stamped once with no looping.
        /// </param>
        /// <returns>Fully assembled BattleChart ready for playback.</returns>
        public static BattleChart Assemble(
            ChartTemplate template,
            PatternLibrary library,
            ISeededRandom rng,
            float bpm,
            float targetDurationBeats = 0f)
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

            bool shouldLoop = targetDurationBeats > 0f
                              && template.repeatMode != RepeatMode.None;

            // Calculate the beat budget for sections (excluding lead-in and tail)
            float sectionBudget = shouldLoop
                ? targetDurationBeats - template.leadInBeats - template.tailBeats
                : template.TotalSectionBeats;

            sectionBudget = Mathf.Max(0f, sectionBudget);

            var sections = new List<ChartSection>();
            float currentBeat = template.leadInBeats;

            if (shouldLoop)
            {
                currentBeat = AssembleWithLooping(
                    template, library, rng, sections, currentBeat, sectionBudget);
            }
            else
            {
                currentBeat = AssembleSinglePass(
                    template, library, rng, sections, currentBeat);
            }

            float totalBeats = currentBeat + template.tailBeats;

            var chart = new BattleChart(bpm, template.leadInBeats, totalBeats, sections);

            Debug.Log($"[ChartAssembler] Assembled: {sections.Count} sections, " +
                      $"{chart.PlayerNoteCount} player notes, " +
                      $"{chart.EnemyNoteCount} enemy notes, " +
                      $"{totalBeats:F0} total beats at {bpm} BPM" +
                      (shouldLoop ? $" (looped to fill {targetDurationBeats:F0}b target)" : ""));

            return chart;
        }

        // =================================================================
        // SINGLE PASS — original behavior, no looping
        // =================================================================

        private static float AssembleSinglePass(
            ChartTemplate template,
            PatternLibrary library,
            ISeededRandom rng,
            List<ChartSection> sections,
            float currentBeat)
        {
            ISeededRandom enemyRng = rng.Fork("enemy_patterns");
            ISeededRandom playerRng = rng.Fork("player_patterns");

            for (int i = 0; i < template.sections.Count; i++)
            {
                SectionSlot slot = template.sections[i];
                ChartSection section = AssembleSection(
                    slot, currentBeat, library, enemyRng, playerRng);

                sections.Add(section);
                currentBeat += slot.durationBeats;
            }

            return currentBeat;
        }

        // =================================================================
        // LOOPING — repeats sections to fill target duration
        // =================================================================

        private static float AssembleWithLooping(
            ChartTemplate template,
            PatternLibrary library,
            ISeededRandom rng,
            List<ChartSection> sections,
            float currentBeat,
            float sectionBudget)
        {
            float budgetEnd = currentBeat + sectionBudget;

            // Determine loop range
            int loopStart, loopEnd;
            GetLoopRange(template, out loopStart, out loopEnd);

            // --- Phase 1: Stamp intro sections (before loop range) ---
            for (int i = 0; i < loopStart; i++)
            {
                if (currentBeat >= budgetEnd) break;

                SectionSlot slot = template.sections[i];
                ISeededRandom enemyRng = rng.Fork($"intro_enemy_{i}");
                ISeededRandom playerRng = rng.Fork($"intro_player_{i}");

                ChartSection section = AssembleSection(
                    slot, currentBeat, library, enemyRng, playerRng);

                sections.Add(section);
                currentBeat += slot.durationBeats;
            }

            // --- Phase 2: Loop body sections until budget filled ---
            float bodyBeats = template.LoopBodyBeats;

            if (bodyBeats > 0f)
            {
                // Calculate how many beats the outro needs so we stop looping in time
                float outroBeats = 0f;
                for (int i = loopEnd + 1; i < template.sections.Count; i++)
                    outroBeats += template.sections[i].durationBeats;

                float loopBudgetEnd = budgetEnd - outroBeats;
                int loopPass = 0;

                while (currentBeat < loopBudgetEnd)
                {
                    // Fork per loop pass for deterministic but varied patterns
                    ISeededRandom enemyRng = rng.Fork($"body_enemy_{loopPass}");
                    ISeededRandom playerRng = rng.Fork($"body_player_{loopPass}");

                    for (int i = loopStart; i <= loopEnd; i++)
                    {
                        if (currentBeat >= loopBudgetEnd) break;

                        SectionSlot slot = template.sections[i];

                        // Trim the last section if it would overshoot
                        float available = loopBudgetEnd - currentBeat;
                        SectionSlot trimmed = slot;
                        if (slot.durationBeats > available)
                            trimmed.durationBeats = available;

                        ChartSection section = AssembleSection(
                            trimmed, currentBeat, library, enemyRng, playerRng);

                        sections.Add(section);
                        currentBeat += trimmed.durationBeats;
                    }

                    loopPass++;
                }
            }

            // --- Phase 3: Stamp outro sections (after loop range) ---
            for (int i = loopEnd + 1; i < template.sections.Count; i++)
            {
                SectionSlot slot = template.sections[i];
                ISeededRandom enemyRng = rng.Fork($"outro_enemy_{i}");
                ISeededRandom playerRng = rng.Fork($"outro_player_{i}");

                ChartSection section = AssembleSection(
                    slot, currentBeat, library, enemyRng, playerRng);

                sections.Add(section);
                currentBeat += slot.durationBeats;
            }

            return currentBeat;
        }

        /// <summary>
        /// Determine which sections form the loopable body based on RepeatMode.
        /// </summary>
        private static void GetLoopRange(ChartTemplate template, out int start, out int end)
        {
            int count = template.sections.Count;

            switch (template.repeatMode)
            {
                case RepeatMode.LoopAll:
                    start = 0;
                    end = count - 1;
                    break;

                case RepeatMode.LoopRange:
                    start = Mathf.Clamp(template.loopStartIndex, 0, count - 1);
                    end = Mathf.Clamp(template.loopEndIndex, start, count - 1);
                    break;

                default:
                    start = 0;
                    end = count - 1;
                    break;
            }
        }

        // =================================================================
        // SECTION ASSEMBLY — shared by both paths
        // =================================================================

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