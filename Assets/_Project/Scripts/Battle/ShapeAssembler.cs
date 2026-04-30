using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Chart generation: filter markers -> group phrases -> pick shapes -> map lanes.
    /// All curves linear (Mathf.Lerp). Not thread-safe (static mutable buffers).
    /// </summary>
    public static class ShapeAssembler
    {
        // Static buffers reused per call to avoid GC. Main-thread only.
        private static readonly List<LaneShape> _queryBuffer = new(32);
        private static readonly List<float> _scoreBuffer = new(32);

        public struct Phrase
        {
            public int StartIndex, Count;
            public float StartBeat, EndBeat;
        }

        private struct PickContext
        {
            public int PreviousEndLane;
            private string _family0, _family1;

            public static PickContext Fresh => new() { PreviousEndLane = -1 };

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

        private const float TransitionWeight = 0.6f;
        private const float MaxLaneDistance = 3f;

        // Difficulty curves (all linear):
        //   Diff  MinGap  Thresh  MaxPhrase  PhraseGap
        //   0.00  1.00b   0.35    4          1.50b
        //   0.50  0.63b   0.19    7          0.88b
        //   1.00  0.25b   0.02    10         0.25b
        private static float GetMinGap(float d) => Mathf.Lerp(1.0f, 0.25f, d);
        private static float GetIntensityThreshold(float d) => Mathf.Lerp(0.35f, 0.02f, d);
        private static int GetMaxPhraseLength(float d) => Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 10f, d)), 4, 10);
        private static float GetPhraseGap(float d) => Mathf.Lerp(1.5f, 0.25f, d);

        public static BattleChart Assemble(
            List<BeatMarker> markers, List<SongSection> sections,
            ShapeLibrary library, ISeededRandom rng,
            float difficulty, float bpm, float totalBeats,
            float leadInBeats = 4f, float tailBeats = 2f)
        {
            if (markers == null || markers.Count == 0) { Debug.LogError("[ShapeAssembler] No timing markers."); return null; }
            if (library == null || library.shapes.Count == 0) { Debug.LogError("[ShapeAssembler] Shape library empty."); return null; }

            difficulty = Mathf.Clamp01(difficulty);
            int maxShapeDiff = Mathf.Clamp(Mathf.CeilToInt(difficulty * 10f), 1, 10);
            float threshold = GetIntensityThreshold(difficulty);
            float minGap = GetMinGap(difficulty);

            var filtered = FilterMarkers(markers, threshold, minGap);
            if (filtered.Count == 0)
            {
                Debug.LogWarning("[ShapeAssembler] All markers filtered out. Using top 10%.");
                filtered = FallbackFilter(markers, 0.1f);
            }

            var phrases = GroupPhrases(filtered, GetPhraseGap(difficulty), GetMaxPhraseLength(difficulty));
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
                    var sPlayerNotes = new List<StampedNote>();
                    var sEnemyNotes = new List<StampedNote>();
                    bool doPlayer = section.highway == SectionType.PlayerOnly || section.highway == SectionType.Both;
                    bool doEnemy = section.highway == SectionType.EnemyOnly || section.highway == SectionType.Both;

                    for (int p = 0; p < phrases.Count; p++)
                    {
                        float mid = (phrases[p].StartBeat + phrases[p].EndBeat) * 0.5f;
                        if (mid < section.startBeat || mid >= section.endBeat) continue;
                        if (doPlayer) MapPhraseToNotes(filtered, phrases[p], library, maxShapeDiff, playerRng, ref playerCtx, sPlayerNotes);
                        if (doEnemy) MapPhraseToNotes(filtered, phrases[p], library, maxShapeDiff, enemyRng, ref enemyCtx, sEnemyNotes);
                    }

                    chartSections.Add(new ChartSection(section.highway, section.startBeat, section.DurationBeats, sEnemyNotes, sPlayerNotes, null, null));
                }
            }
            else
            {
                var playerNotes = new List<StampedNote>();
                PickContext playerCtx = PickContext.Fresh;
                for (int p = 0; p < phrases.Count; p++)
                    MapPhraseToNotes(filtered, phrases[p], library, maxShapeDiff, playerRng, ref playerCtx, playerNotes);
                chartSections.Add(new ChartSection(SectionType.PlayerOnly, 0f, totalBeats, new List<StampedNote>(), playerNotes, null, null));
            }

            var chart = new BattleChart(bpm, leadInBeats, leadInBeats + totalBeats + tailBeats, chartSections);
            Debug.Log($"[ShapeAssembler] diff={difficulty:F2} | {markers.Count} raw -> {filtered.Count} filtered -> {phrases.Count} phrases -> {chart.PlayerNoteCount}P + {chart.EnemyNoteCount}E notes");
            return chart;
        }

        private static List<BeatMarker> FilterMarkers(List<BeatMarker> markers, float threshold, float minGap)
        {
            var result = new List<BeatMarker>(markers.Count);
            float lastBeat = float.MinValue;
            for (int i = 0; i < markers.Count; i++)
            {
                BeatMarker m = markers[i];
                if (m.type == MarkerType.Break) continue;
                if (m.intensity < threshold) continue;
                if (m.beat - lastBeat < minGap) continue;
                result.Add(m);
                lastBeat = m.beat;
            }
            return result;
        }

        private static List<BeatMarker> FallbackFilter(List<BeatMarker> markers, float topPercent)
        {
            var sorted = new List<BeatMarker>(markers);
            sorted.Sort((a, b) => b.intensity.CompareTo(a.intensity));
            int count = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * topPercent), 4, sorted.Count);
            var result = sorted.GetRange(0, count);
            result.Sort((a, b) => a.beat.CompareTo(b.beat));
            return result;
        }

        private static List<Phrase> GroupPhrases(List<BeatMarker> markers, float phraseGap, int maxLen)
        {
            var phrases = new List<Phrase>();
            if (markers.Count == 0) return phrases;
            int start = 0;

            for (int i = 1; i <= markers.Count; i++)
            {
                bool end = i == markers.Count
                    || markers[i].beat - markers[i - 1].beat >= phraseGap
                    || i - start >= maxLen;

                if (end)
                {
                    phrases.Add(new Phrase { StartIndex = start, Count = i - start, StartBeat = markers[start].beat, EndBeat = markers[i - 1].beat });
                    start = i;
                }
            }
            return phrases;
        }

        private static void MapPhraseToNotes(List<BeatMarker> markers, Phrase phrase, ShapeLibrary library, int maxDiff, ISeededRandom rng, ref PickContext ctx, List<StampedNote> output)
        {
            LaneShape shape = PickShape(library, maxDiff, rng, ref ctx);

            for (int i = 0; i < phrase.Count; i++)
            {
                BeatMarker m = markers[phrase.StartIndex + i];
                int lane = shape != null ? shape.GetLane(i) : 0;
                output.Add(new StampedNote(lane, m.beat, m.holdBeats));
            }

            if (shape != null) ctx.Update(shape);
        }

        private static LaneShape PickShape(ShapeLibrary library, int maxDiff, ISeededRandom rng, ref PickContext ctx)
        {
            int count = library.QueryAll(maxDiff, _queryBuffer);
            if (count == 0) return null;

            int distinctFamilies = CountDistinctFamilies(_queryBuffer);
            _scoreBuffer.Clear();
            float totalScore = 0f;
            int nonExcluded = 0;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                LaneShape c = _queryBuffer[i];

                // Exclude recently used families to avoid repetition
                if (ctx.IsFamilyRecent(c) && distinctFamilies > 2) { _scoreBuffer.Add(0f); continue; }

                nonExcluded++;
                float score = c.weight;

                // Prefer shapes that start near the previous shape's end lane
                if (ctx.PreviousEndLane >= 0 && c.StartLane >= 0)
                {
                    float proximity = 1f - (c.TransitionDistance(ctx.PreviousEndLane) / MaxLaneDistance);
                    score *= (1f + TransitionWeight * proximity);
                }

                _scoreBuffer.Add(score);
                totalScore += score;
            }

            // If all excluded, allow everything
            if (nonExcluded == 0)
            {
                totalScore = 0f;
                for (int i = 0; i < _queryBuffer.Count; i++) { _scoreBuffer[i] = _queryBuffer[i].weight; totalScore += _scoreBuffer[i]; }
            }

            if (totalScore <= 0f) return _queryBuffer[0];

            float roll = rng.NextFloat() * totalScore;
            float cum = 0f;
            for (int i = 0; i < _queryBuffer.Count; i++) { cum += _scoreBuffer[i]; if (roll < cum) return _queryBuffer[i]; }
            return _queryBuffer[^1];
        }

        private static int CountDistinctFamilies(List<LaneShape> shapes)
        {
            int count = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                bool unique = true;
                for (int j = 0; j < i; j++) { if (shapes[j].EffectiveFamily == shapes[i].EffectiveFamily) { unique = false; break; } }
                if (unique) count++;
            }
            return count;
        }
    }
}
