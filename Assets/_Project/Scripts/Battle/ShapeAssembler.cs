using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Chart generation pipeline: timing markers + phrase grouping + shape mapping.
    /// 
    /// Pipeline:
    ///   1. Filter markers by difficulty (intensity threshold + min gap)
    ///   2. Group filtered markers into phrases (clusters of nearby notes)
    ///   3. Pick a LaneShape per phrase (seed-controlled, family-aware)
    ///   4. Map shape lanes onto marker timings one-to-one
    /// 
    /// All curves are linear (Mathf.Lerp). Tuned through playtesting.
    /// 
    /// Not thread-safe: uses static mutable buffers for zero-allocation
    /// queries. This is intentional for Unity's single-threaded model.
    /// Do not call from background threads or Jobs.
    /// </summary>
    public static class ShapeAssembler
    {
        // Static buffers reused across calls to avoid GC allocations.
        // Safe in Unity's main-thread-only execution model.
        private static readonly List<LaneShape> _queryBuffer = new(32);
        private static readonly List<float> _scoreBuffer = new(32);

        // =================================================================
        // PHRASE
        // =================================================================

        public struct Phrase
        {
            public int StartIndex;
            public int Count;
            public float StartBeat;
            public float EndBeat;
        }

        // =================================================================
        // PICK CONTEXT
        // =================================================================

        private struct PickContext
        {
            public int PreviousEndLane;
            private string _family0;
            private string _family1;

            public static PickContext Fresh => new()
            {
                PreviousEndLane = -1,
                _family0 = null,
                _family1 = null
            };

            public void Update(LaneShape shape)
            {
                if (shape == null) return;
                PreviousEndLane = shape.EndLane;
                _family1 = _family0;
                _family0 = shape.EffectiveFamily;
            }

            public bool IsFamilyRecent(LaneShape shape)
            {
                if (shape == null) return false;
                string family = shape.EffectiveFamily;
                return family == _family0 || family == _family1;
            }
        }

        // =================================================================
        // TUNING
        // =================================================================

        private const float TransitionWeight = 0.6f;
        private const float MaxLaneDistance = 3f;

        // =================================================================
        // DIFFICULTY CURVES
        //
        // All linear via Mathf.Lerp(easyEnd, hardEnd, difficulty).
        //
        //   Diff  MinGap  Thresh  MaxPhrase  PhraseGap
        //   0.00  1.00b   0.35    4          1.50b
        //   0.25  0.81b   0.27    6          1.19b
        //   0.50  0.63b   0.19    7          0.88b
        //   0.75  0.44b   0.10    8          0.56b
        //   1.00  0.25b   0.02    10         0.25b
        // =================================================================

        /// <summary>
        /// Minimum beat gap between notes.
        /// 0.0 = 1.0 beats (sparse), 1.0 = 0.25 beats (sixteenth notes).
        /// </summary>
        private static float GetMinGap(float difficulty)
        {
            return Mathf.Lerp(1.0f, 0.25f, difficulty);
        }

        /// <summary>
        /// Intensity threshold for marker filtering.
        /// 0.0 = 0.35 (strong hits only), 1.0 = 0.02 (nearly everything).
        /// </summary>
        private static float GetIntensityThreshold(float difficulty)
        {
            return Mathf.Lerp(0.35f, 0.02f, difficulty);
        }

        /// <summary>
        /// Max notes per phrase before forced split.
        /// 0.0 = 4, 1.0 = 10.
        /// </summary>
        private static int GetMaxPhraseLength(float difficulty)
        {
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 10f, difficulty)), 4, 10);
        }

        /// <summary>
        /// Beat gap that starts a new phrase.
        /// 0.0 = 1.5 beats (aggressive splitting), 1.0 = 0.25 beats (tight grouping).
        /// </summary>
        private static float GetPhraseGap(float difficulty)
        {
            return Mathf.Lerp(1.5f, 0.25f, difficulty);
        }

        // =================================================================
        // PUBLIC API
        // =================================================================

        public static BattleChart Assemble(
            List<BeatMarker> markers,
            List<SongSection> sections,
            ShapeLibrary library,
            ISeededRandom rng,
            float difficulty,
            float bpm,
            float totalBeats,
            float leadInBeats = 4f,
            float tailBeats = 2f)
        {
            if (markers == null || markers.Count == 0)
            {
                Debug.LogError("[ShapeAssembler] No timing markers.");
                return null;
            }

            if (library == null || library.shapes.Count == 0)
            {
                Debug.LogError("[ShapeAssembler] Shape library is null or empty.");
                return null;
            }

            difficulty = Mathf.Clamp01(difficulty);
            int maxShapeDifficulty = Mathf.Clamp(Mathf.CeilToInt(difficulty * 10f), 1, 10);

            float intensityThreshold = GetIntensityThreshold(difficulty);
            float minGap = GetMinGap(difficulty);

            List<BeatMarker> filtered = FilterMarkers(markers, intensityThreshold, minGap);

            if (filtered.Count == 0)
            {
                Debug.LogWarning("[ShapeAssembler] All markers filtered out. Using top 10%.");
                filtered = FallbackFilter(markers, 0.1f);
            }

            float phraseGap = GetPhraseGap(difficulty);
            int maxPhraseLen = GetMaxPhraseLength(difficulty);
            List<Phrase> phrases = GroupPhrases(filtered, phraseGap, maxPhraseLen);

            ISeededRandom playerRng = rng.Fork("shape_player");
            ISeededRandom enemyRng = rng.Fork("shape_enemy");

            var chartSections = new List<ChartSection>();

            if (sections != null && sections.Count > 0)
            {
                PickContext playerCtx = PickContext.Fresh;
                PickContext enemyCtx = PickContext.Fresh;

                for (int s = 0; s < sections.Count; s++)
                {
                    SongSection section = sections[s];

                    var sectionPlayerNotes = new List<StampedNote>();
                    var sectionEnemyNotes = new List<StampedNote>();

                    bool doPlayer = section.highway == SectionType.PlayerOnly ||
                                    section.highway == SectionType.Both;
                    bool doEnemy = section.highway == SectionType.EnemyOnly ||
                                   section.highway == SectionType.Both;

                    for (int p = 0; p < phrases.Count; p++)
                    {
                        Phrase phrase = phrases[p];
                        float phraseMid = (phrase.StartBeat + phrase.EndBeat) * 0.5f;

                        if (phraseMid < section.startBeat || phraseMid >= section.endBeat)
                            continue;

                        if (doPlayer)
                        {
                            MapPhraseToNotes(
                                filtered, phrase, library, maxShapeDifficulty,
                                playerRng, ref playerCtx, sectionPlayerNotes);
                        }

                        if (doEnemy)
                        {
                            MapPhraseToNotes(
                                filtered, phrase, library, maxShapeDifficulty,
                                enemyRng, ref enemyCtx, sectionEnemyNotes);
                        }
                    }

                    chartSections.Add(new ChartSection(
                        section.highway,
                        section.startBeat,
                        section.DurationBeats,
                        sectionEnemyNotes,
                        sectionPlayerNotes,
                        null, null));
                }
            }
            else
            {
                var playerNotes = new List<StampedNote>();
                PickContext playerCtx = PickContext.Fresh;

                for (int p = 0; p < phrases.Count; p++)
                {
                    MapPhraseToNotes(
                        filtered, phrases[p], library, maxShapeDifficulty,
                        playerRng, ref playerCtx, playerNotes);
                }

                chartSections.Add(new ChartSection(
                    SectionType.PlayerOnly,
                    0f,
                    totalBeats,
                    new List<StampedNote>(),
                    playerNotes,
                    null, null));
            }

            float chartTotalBeats = leadInBeats + totalBeats + tailBeats;
            var chart = new BattleChart(bpm, leadInBeats, chartTotalBeats, chartSections);

            Debug.Log($"[ShapeAssembler] diff={difficulty:F2} | " +
                      $"threshold={intensityThreshold:F2} gap={minGap:F1}b | " +
                      $"{markers.Count} raw -> {filtered.Count} filtered -> " +
                      $"{phrases.Count} phrases -> " +
                      $"{chart.PlayerNoteCount}P + {chart.EnemyNoteCount}E notes");

            return chart;
        }

        // =================================================================
        // STEP 1: FILTER MARKERS
        // =================================================================

        private static List<BeatMarker> FilterMarkers(
            List<BeatMarker> markers,
            float intensityThreshold,
            float minGapBeats)
        {
            var result = new List<BeatMarker>(markers.Count);
            float lastBeat = float.MinValue;

            for (int i = 0; i < markers.Count; i++)
            {
                BeatMarker m = markers[i];

                if (m.type == MarkerType.Break)
                    continue;

                if (m.intensity < intensityThreshold)
                    continue;

                if (m.beat - lastBeat < minGapBeats)
                    continue;

                result.Add(m);
                lastBeat = m.beat;
            }

            return result;
        }

        private static List<BeatMarker> FallbackFilter(List<BeatMarker> markers, float topPercent)
        {
            var sorted = new List<BeatMarker>(markers);
            sorted.Sort((a, b) => b.intensity.CompareTo(a.intensity));

            int count = Mathf.Max(4, Mathf.CeilToInt(sorted.Count * topPercent));
            count = Mathf.Min(count, sorted.Count);

            var result = sorted.GetRange(0, count);
            result.Sort((a, b) => a.beat.CompareTo(b.beat));
            return result;
        }

        // =================================================================
        // STEP 2: GROUP INTO PHRASES
        // =================================================================

        private static List<Phrase> GroupPhrases(
            List<BeatMarker> markers,
            float phraseGapBeats,
            int maxPhraseLength)
        {
            var phrases = new List<Phrase>();
            if (markers.Count == 0) return phrases;

            int phraseStart = 0;

            for (int i = 1; i <= markers.Count; i++)
            {
                bool endPhrase = false;

                if (i == markers.Count)
                {
                    endPhrase = true;
                }
                else
                {
                    float gap = markers[i].beat - markers[i - 1].beat;
                    int currentLength = i - phraseStart;

                    if (gap >= phraseGapBeats || currentLength >= maxPhraseLength)
                        endPhrase = true;
                }

                if (endPhrase)
                {
                    phrases.Add(new Phrase
                    {
                        StartIndex = phraseStart,
                        Count = i - phraseStart,
                        StartBeat = markers[phraseStart].beat,
                        EndBeat = markers[i - 1].beat,
                    });

                    phraseStart = i;
                }
            }

            return phrases;
        }

        // =================================================================
        // STEP 3: MAP PHRASE TO NOTES
        // =================================================================

        private static void MapPhraseToNotes(
            List<BeatMarker> markers,
            Phrase phrase,
            ShapeLibrary library,
            int maxDifficulty,
            ISeededRandom rng,
            ref PickContext ctx,
            List<StampedNote> output)
        {
            LaneShape shape = PickShape(library, maxDifficulty, rng, ref ctx);

            if (shape == null)
            {
                for (int i = 0; i < phrase.Count; i++)
                {
                    BeatMarker m = markers[phrase.StartIndex + i];
                    output.Add(new StampedNote(0, m.beat, m.holdBeats));
                }
                return;
            }

            for (int i = 0; i < phrase.Count; i++)
            {
                BeatMarker m = markers[phrase.StartIndex + i];
                int lane = shape.GetLane(i);
                output.Add(new StampedNote(lane, m.beat, m.holdBeats));
            }

            ctx.Update(shape);
        }

        // =================================================================
        // SHAPE SELECTION
        // =================================================================

        private static LaneShape PickShape(
            ShapeLibrary library,
            int maxDifficulty,
            ISeededRandom rng,
            ref PickContext ctx)
        {
            int count = library.QueryAll(maxDifficulty, _queryBuffer);

            if (count == 0)
            {
                Debug.LogWarning("[ShapeAssembler] No shapes at difficulty " + maxDifficulty);
                return null;
            }

            int distinctFamilies = CountDistinctFamilies(_queryBuffer);

            _scoreBuffer.Clear();
            float totalScore = 0f;
            int nonExcludedCount = 0;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                LaneShape candidate = _queryBuffer[i];

                if (ctx.IsFamilyRecent(candidate) && distinctFamilies > 2)
                {
                    _scoreBuffer.Add(0f);
                    continue;
                }

                nonExcludedCount++;
                float score = candidate.weight;

                if (ctx.PreviousEndLane >= 0 && candidate.StartLane >= 0)
                {
                    int distance = candidate.TransitionDistance(ctx.PreviousEndLane);
                    float proximity = 1f - (distance / MaxLaneDistance);
                    score *= (1f + TransitionWeight * proximity);
                }

                _scoreBuffer.Add(score);
                totalScore += score;
            }

            if (nonExcludedCount == 0)
            {
                totalScore = 0f;
                for (int i = 0; i < _queryBuffer.Count; i++)
                {
                    float score = _queryBuffer[i].weight;
                    _scoreBuffer[i] = score;
                    totalScore += score;
                }
            }

            if (totalScore <= 0f)
                return _queryBuffer[0];

            float roll = rng.NextFloat() * totalScore;
            float cumulative = 0f;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                cumulative += _scoreBuffer[i];
                if (roll < cumulative)
                    return _queryBuffer[i];
            }

            return _queryBuffer[_queryBuffer.Count - 1];
        }

        private static int CountDistinctFamilies(List<LaneShape> shapes)
        {
            int count = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                string family = shapes[i].EffectiveFamily;
                bool unique = true;
                for (int j = 0; j < i; j++)
                {
                    if (shapes[j].EffectiveFamily == family)
                    {
                        unique = false;
                        break;
                    }
                }
                if (unique) count++;
            }
            return count;
        }
    }
}
