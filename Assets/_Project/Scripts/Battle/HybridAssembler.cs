using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Hybrid chart assembler: human-crafted patterns placed by algorithm.
    /// 
    /// Three selection features:
    /// 
    ///   TRANSITION-AWARE: Tracks the ending lane of the previous pattern
    ///   and scores candidates by proximity.
    /// 
    ///   FAMILY-AWARE NO-REPEAT (2-DEEP HISTORY): The two most recently
    ///   used pattern families are excluded. Mirrors and variants sharing
    ///   a familyId are treated as the same shape, preventing both exact
    ///   repeats and mirror-repeat (ABAB where B is A mirrored).
    /// 
    ///   DENSITY MATCHING: Audio analysis determines section density, and
    ///   the assembler picks patterns whose authored density matches.
    /// 
    /// Determinism: same seed + same library + same audio = same chart.
    /// </summary>
    public static class HybridAssembler
    {
        private static readonly List<PatternData> _queryBuffer = new(32);
        private static readonly List<PatternData> _filteredBuffer = new(32);
        private static readonly List<float> _scoreBuffer = new(32);

        // =================================================================
        // PICK CONTEXT - 2-deep family history
        // =================================================================

        private struct PickContext
        {
            public int PreviousEndLane;
            private string _family0; // most recent family
            private string _family1; // one before that

            public static PickContext Fresh => new()
            {
                PreviousEndLane = -1,
                _family0 = null,
                _family1 = null
            };

            public void Update(PatternData pattern)
            {
                if (pattern == null) return;
                PreviousEndLane = pattern.EndLane;
                _family1 = _family0;
                _family0 = pattern.EffectiveFamily;
            }

            /// <summary>
            /// Returns true if this pattern's family was used in the last 2 picks.
            /// </summary>
            public bool IsFamilyRecent(PatternData pattern)
            {
                if (pattern == null) return false;
                string family = pattern.EffectiveFamily;
                return family == _family0 || family == _family1;
            }
        }

        // =================================================================
        // TUNING
        // =================================================================

        private const float TransitionWeight = 0.6f;
        private const float MaxLaneDistance = 3f;
        private const float FitBonus = 1.15f;

        // =================================================================
        // PUBLIC API
        // =================================================================

        public static BattleChart Assemble(
            RuntimeBeatAnalyzer.AnalysisResult analysis,
            PatternLibrary library,
            ISeededRandom rng,
            float difficulty,
            float bpm,
            List<BeatMarker> allMarkers)
        {
            if (library == null || library.patterns.Count == 0)
            {
                Debug.LogError("[HybridAssembler] Pattern library is null or empty.");
                return null;
            }

            if (analysis.Sections == null || analysis.Sections.Count == 0)
            {
                Debug.LogError("[HybridAssembler] No sections from analysis.");
                return null;
            }

            difficulty = Mathf.Clamp01(difficulty);
            DifficultyProfile profile = DifficultyProfile.FromDifficulty(difficulty);

            int maxPatternDifficulty = Mathf.Clamp(Mathf.CeilToInt(difficulty * 10f), 1, 10);

            ISeededRandom enemyRng = rng.Fork("hybrid_enemy");
            ISeededRandom playerRng = rng.Fork("hybrid_player");

            PickContext enemyCtx = PickContext.Fresh;
            PickContext playerCtx = PickContext.Fresh;

            var chartSections = new List<ChartSection>(analysis.Sections.Count);

            for (int i = 0; i < analysis.Sections.Count; i++)
            {
                SongSection section = analysis.Sections[i];

                DensityCategory sectionDensity = CalculateDensity(
                    allMarkers, section, bpm, profile);

                ChartSection chartSection = AssembleSection(
                    section, sectionDensity, maxPatternDifficulty,
                    library, enemyRng, playerRng, profile,
                    ref enemyCtx, ref playerCtx);

                chartSections.Add(chartSection);
            }

            float totalBeats = 4f + analysis.TotalBeats + 2f;

            var chart = new BattleChart(bpm, 4f, totalBeats, chartSections);

            Debug.Log($"[HybridAssembler] Assembled: {chartSections.Count} sections, " +
                      $"{chart.PlayerNoteCount} player notes, " +
                      $"{chart.EnemyNoteCount} enemy notes, " +
                      $"difficulty {difficulty:F2} (max pattern tier {maxPatternDifficulty})");

            return chart;
        }

        // =================================================================
        // DENSITY CALCULATION
        // =================================================================

        private static DensityCategory CalculateDensity(
            List<BeatMarker> allMarkers,
            SongSection section,
            float bpm,
            DifficultyProfile profile)
        {
            int count = 0;
            for (int i = 0; i < allMarkers.Count; i++)
            {
                float beat = allMarkers[i].beat;
                if (beat < section.startBeat || beat >= section.endBeat) continue;
                if (allMarkers[i].type == MarkerType.Break) continue;

                float effective = allMarkers[i].intensity *
                    (section.intensityScale > 0f ? section.intensityScale : 1f);

                if (effective >= profile.IntensityThreshold)
                    count++;
            }

            float bars = section.DurationBeats / 4f;
            float notesPerBar = bars > 0f ? count / bars : 0f;

            if (notesPerBar <= 2f) return DensityCategory.Sparse;
            if (notesPerBar <= 4f) return DensityCategory.Light;
            if (notesPerBar <= 6f) return DensityCategory.Medium;
            if (notesPerBar <= 8f) return DensityCategory.Dense;
            return DensityCategory.VeryDense;
        }

        // =================================================================
        // SECTION ASSEMBLY
        // =================================================================

        private static ChartSection AssembleSection(
            SongSection section,
            DensityCategory sectionDensity,
            int maxDifficulty,
            PatternLibrary library,
            ISeededRandom enemyRng,
            ISeededRandom playerRng,
            DifficultyProfile profile,
            ref PickContext enemyCtx,
            ref PickContext playerCtx)
        {
            List<StampedNote> enemyNotes = new();
            List<StampedNote> playerNotes = new();

            bool generateEnemy = section.highway == SectionType.EnemyOnly ||
                                 section.highway == SectionType.Both;
            bool generatePlayer = section.highway == SectionType.PlayerOnly ||
                                  section.highway == SectionType.Both;

            if (generateEnemy)
            {
                enemyNotes = FillSectionWithPatterns(
                    section, sectionDensity, maxDifficulty,
                    library, enemyRng, profile, ref enemyCtx);
            }

            if (generatePlayer)
            {
                playerNotes = FillSectionWithPatterns(
                    section, sectionDensity, maxDifficulty,
                    library, playerRng, profile, ref playerCtx);
            }

            return new ChartSection(
                section.highway,
                section.startBeat,
                section.DurationBeats,
                enemyNotes,
                playerNotes,
                null,
                null);
        }

        private static List<StampedNote> FillSectionWithPatterns(
            SongSection section,
            DensityCategory sectionDensity,
            int maxDifficulty,
            PatternLibrary library,
            ISeededRandom rng,
            DifficultyProfile profile,
            ref PickContext ctx)
        {
            var notes = new List<StampedNote>();
            float currentBeat = section.startBeat;
            float endBeat = section.endBeat;
            int chainCount = 0;
            const int maxChain = 64;

            while (currentBeat < endBeat && chainCount < maxChain)
            {
                float remaining = endBeat - currentBeat;

                PatternData pattern = PickPattern(
                    library, sectionDensity, maxDifficulty,
                    remaining, rng, ref ctx);

                if (pattern == null || pattern.notes == null || pattern.notes.Count == 0)
                {
                    currentBeat += 4f;
                    chainCount++;
                    continue;
                }

                float patternDuration = GetPatternDuration(pattern);
                float stampDuration = Mathf.Min(patternDuration, remaining);

                StampPattern(pattern, currentBeat, stampDuration, notes, profile);
                ctx.Update(pattern);

                currentBeat += patternDuration;
                chainCount++;
            }

            notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return notes;
        }

        // =================================================================
        // TRANSITION-AWARE + FAMILY-AWARE PATTERN SELECTION
        // =================================================================

        private static PatternData PickPattern(
            PatternLibrary library,
            DensityCategory targetDensity,
            int maxDifficulty,
            float remainingBeats,
            ISeededRandom rng,
            ref PickContext ctx)
        {
            BuildCandidates(library, targetDensity, maxDifficulty);

            if (_filteredBuffer.Count == 0 && targetDensity > DensityCategory.Sparse)
                BuildCandidates(library, targetDensity - 1, maxDifficulty);

            if (_filteredBuffer.Count == 0 && targetDensity < DensityCategory.VeryDense)
                BuildCandidates(library, targetDensity + 1, maxDifficulty);

            if (_filteredBuffer.Count == 0)
            {
                int count = library.Query(maxDifficulty, PatternTag.None, _queryBuffer);
                if (count > 0)
                {
                    _filteredBuffer.Clear();
                    _filteredBuffer.AddRange(_queryBuffer);
                }
            }

            if (_filteredBuffer.Count == 0)
            {
                Debug.LogWarning($"[HybridAssembler] No patterns found. " +
                                 $"Density: {targetDensity}, MaxDifficulty: {maxDifficulty}");
                return null;
            }

            return ScoreAndPick(_filteredBuffer, remainingBeats, rng, ref ctx);
        }

        private static void BuildCandidates(
            PatternLibrary library,
            DensityCategory density,
            int maxDifficulty)
        {
            _filteredBuffer.Clear();
            int count = library.Query(maxDifficulty, PatternTag.None, _queryBuffer);
            if (count == 0) return;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                if (_queryBuffer[i].density == density)
                    _filteredBuffer.Add(_queryBuffer[i]);
            }
        }

        private static PatternData ScoreAndPick(
            List<PatternData> candidates,
            float remainingBeats,
            ISeededRandom rng,
            ref PickContext ctx)
        {
            // Count distinct families to know when exclusion is safe
            int distinctFamilies = CountDistinctFamilies(candidates);

            _scoreBuffer.Clear();
            float totalScore = 0f;
            int nonExcludedCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                PatternData candidate = candidates[i];

                // Family-aware exclusion: skip if this family was used
                // in the last 2 picks, but only when 3+ families exist
                if (ctx.IsFamilyRecent(candidate) && distinctFamilies > 2)
                {
                    _scoreBuffer.Add(0f);
                    continue;
                }

                nonExcludedCount++;
                float score = candidate.weight;

                // Transition bonus
                if (ctx.PreviousEndLane >= 0 && candidate.StartLane >= 0)
                {
                    int distance = candidate.TransitionDistance(ctx.PreviousEndLane);
                    float proximity = 1f - (distance / MaxLaneDistance);
                    score *= (1f + TransitionWeight * proximity);
                }

                // Duration fit bonus
                float patternDuration = GetPatternDuration(candidate);
                if (patternDuration <= remainingBeats)
                    score *= FitBonus;

                _scoreBuffer.Add(score);
                totalScore += score;
            }

            if (nonExcludedCount == 0)
            {
                totalScore = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    float score = candidates[i].weight;
                    _scoreBuffer[i] = score;
                    totalScore += score;
                }
            }

            if (totalScore <= 0f)
                return candidates[rng.Range(0, candidates.Count)];

            float roll = rng.NextFloat() * totalScore;
            float cumulative = 0f;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += _scoreBuffer[i];
                if (roll < cumulative)
                    return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private static int CountDistinctFamilies(List<PatternData> patterns)
        {
            int count = 0;
            for (int i = 0; i < patterns.Count; i++)
            {
                string family = patterns[i].EffectiveFamily;
                bool unique = true;
                for (int j = 0; j < i; j++)
                {
                    if (patterns[j].EffectiveFamily == family)
                    {
                        unique = false;
                        break;
                    }
                }
                if (unique) count++;
            }
            return count;
        }

        private static void StampPattern(
            PatternData pattern,
            float startBeat,
            float maxDuration,
            List<StampedNote> output,
            DifficultyProfile profile)
        {
            if (pattern.notes == null) return;

            float lastBeat = float.MinValue;

            for (int i = 0; i < pattern.notes.Count; i++)
            {
                PatternNote note = pattern.notes[i];

                if (note.BeatOffset >= maxDuration)
                    continue;

                float absoluteBeat = startBeat + note.BeatOffset;

                if (absoluteBeat - lastBeat < profile.MinNoteGapBeats)
                    continue;

                float holdBeats = note.HoldBeats;
                if (note.BeatOffset + holdBeats > maxDuration)
                    holdBeats = maxDuration - note.BeatOffset;

                output.Add(new StampedNote(note.Lane, absoluteBeat, holdBeats));
                lastBeat = absoluteBeat;
            }
        }

        private static float GetPatternDuration(PatternData pattern)
        {
            if (pattern.durationBeats > 0f)
                return pattern.durationBeats;

            if (pattern.notes == null || pattern.notes.Count == 0)
                return 4f;

            float lastBeat = 0f;
            for (int i = 0; i < pattern.notes.Count; i++)
            {
                float end = pattern.notes[i].BeatOffset + pattern.notes[i].HoldBeats;
                if (end > lastBeat)
                    lastBeat = end;
            }

            return Mathf.Ceil(lastBeat / 4f) * 4f;
        }
    }
}