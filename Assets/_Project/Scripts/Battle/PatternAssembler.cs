using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Builds a chart by placing whole, hand-authored rhythm fragments (NotePattern) onto a
    /// bar-aligned timeline. Unlike ShapeAssembler, which stamped one lane per audio onset and
    /// let the onset detector decide every note time, this assembler treats the song analysis
    /// as a DIRECTOR and the fragments as the CONTENT. It decides when, which, and how dense;
    /// the fragment decides the actual notes, so the rhythm is always something a human wrote.
    ///
    /// What makes the output read as hand-made:
    ///   1. Follow one instrument at a time. With a stemmed beat map, each section locks to a
    ///      layer (verse: drums, chorus: melody, bridge: bass, drop: everything). A blended
    ///      average of all the audio is what sounds like noise. An enemy can also force one
    ///      instrument for the whole fight via its chartInstrument selector.
    ///   2. Dynamics track structure. Per-bar note density is driven by how many onsets the
    ///      song actually has in that bar (scaled by the section's intensity), so the chart
    ///      breathes: quiet verses, busy choruses, real rests where the music rests.
    ///   3. Repeat where the song repeats. The fragment pick is seeded by (section type, bar,
    ///      density), not drawn sequentially, so two choruses that share the same shape draw
    ///      the same fragments. Recognition reads as intent.
    ///   4. Chart a clean version of the real rhythm. Each bar picks the fragment whose density
    ///      best matches that bar's onsets, so the chart follows the song's groove through
    ///      clean, hittable, authored patterns instead of raw transients.
    ///   6. Flow in the hands, with variety. Each fragment is transformed as a unit, a left/right
    ///      mirror plus a 0-3 lane rotation, so the same authored shape appears starting on
    ///      different lanes instead of the same columns every time. The transform is weighted
    ///      toward keeping the first lane near the previous fragment's exit, so the hand keeps
    ///      flowing. The lane-to-key mapping never changes within a fragment, only between them.
    ///
    /// Accent alignment (jumps on crashes, holds on sustains) is left for a later pass; it needs
    /// a per-note accent tag on NotePattern. Not thread-safe (static reusable buffers, main
    /// thread only), matching ShapeAssembler.
    /// </summary>
    public static class PatternAssembler
    {
        private const float BarBeats = 4f;          // assumes 4/4; the only time-signature the prototype targets
        private const float Epsilon = 0.01f;

        // Reused per call to avoid GC. Main thread only.
        private static readonly List<NotePattern> _queryBuffer = new(32);
        private static readonly List<float> _scoreBuffer = new(32);

        private struct PickContext
        {
            public int PreviousEndLane;
            private string _family0, _family1;

            public static PickContext Fresh => new() { PreviousEndLane = -1 };

            public bool IsFamilyRecent(NotePattern p)
            {
                if (p == null) return false;
                string f = p.EffectiveFamily;
                return f == _family0 || f == _family1;
            }

            public void Update(NotePattern p, int endLane)
            {
                PreviousEndLane = endLane;
                if (p == null) return;
                _family1 = _family0;
                _family0 = p.EffectiveFamily;
            }
        }

        public static BattleChart Assemble(
            List<BeatMarker> markers, List<SongSection> sections,
            NotePatternLibrary library, ChartInstrument selector, ISeededRandom rng,
            float difficulty, float bpm, float totalBeats,
            float leadInBeats = 4f, float tailBeats = 2f)
        {
            if (markers == null || markers.Count == 0) { GameLog.Error("[PatternAssembler] No timing markers."); return null; }
            if (library == null || library.patterns.Count == 0) { GameLog.Error("[PatternAssembler] Pattern library empty."); return null; }

            difficulty = Mathf.Clamp01(difficulty);
            int globalMaxDiff = Mathf.Clamp(Mathf.CeilToInt(difficulty * 10f), 1, 10);
            bool hasStems = AnyTagged(markers);

            ISeededRandom playerRng = rng.Fork("pattern_player");
            ISeededRandom enemyRng = rng.Fork("pattern_enemy");
            PickContext playerCtx = PickContext.Fresh;
            PickContext enemyCtx = PickContext.Fresh;

            var chartSections = new List<ChartSection>();

            if (sections != null && sections.Count > 0)
            {
                for (int s = 0; s < sections.Count; s++)
                {
                    SongSection sec = sections[s];
                    bool doPlayer = sec.highway == SectionType.PlayerOnly || sec.highway == SectionType.Both;
                    bool doEnemy = sec.highway == SectionType.EnemyOnly || sec.highway == SectionType.Both;

                    ChartInstrument inst = SectionInstrument(selector, sec.type, hasStems);
                    List<BeatMarker> secMarkers = MarkersInRange(markers, sec.startBeat, sec.endBeat, inst);

                    var playerNotes = new List<StampedNote>();
                    var enemyNotes = new List<StampedNote>();

                    if (doPlayer)
                        FillSide(secMarkers, sec.type, sec.startBeat, sec.endBeat, library, globalMaxDiff,
                                 sec.intensityScale, leadInBeats, playerRng, ref playerCtx, playerNotes);
                    if (doEnemy)
                        FillSide(secMarkers, sec.type, sec.startBeat, sec.endBeat, library, globalMaxDiff,
                                 sec.intensityScale, leadInBeats, enemyRng, ref enemyCtx, enemyNotes);

                    chartSections.Add(new ChartSection(sec.highway, sec.startBeat, sec.DurationBeats, enemyNotes, playerNotes, null, null));
                }
            }
            else
            {
                // No section data: one player section across the whole song.
                List<BeatMarker> secMarkers = MarkersInRange(markers, 0f, totalBeats, ChartInstrument.All);
                var playerNotes = new List<StampedNote>();
                FillSide(secMarkers, SongSectionType.Verse, 0f, totalBeats, library, globalMaxDiff, 0f, leadInBeats, playerRng, ref playerCtx, playerNotes);
                chartSections.Add(new ChartSection(SectionType.PlayerOnly, 0f, totalBeats, new List<StampedNote>(), playerNotes, null, null));
            }

            var chart = new BattleChart(bpm, leadInBeats, leadInBeats + totalBeats + tailBeats, chartSections);
            GameLog.Info($"[PatternAssembler] diff={difficulty:F2} stems={hasStems} | {markers.Count} markers -> " +
                         $"{chart.PlayerNoteCount}P + {chart.EnemyNoteCount}E notes across {chartSections.Count} sections");
            return chart;
        }

        // ---- Instrument selection (point 1) ----

        private static bool AnyTagged(List<BeatMarker> markers)
        {
            for (int i = 0; i < markers.Count; i++)
                if (markers[i].instrument != ChartInstrument.All) return true;
            return false;
        }

        /// <summary>
        /// Which instrument a section follows. A non-All enemy selector forces that instrument
        /// for the whole fight (the player's explicit choice). With the default All selector and
        /// a stemmed beat map, the layer follows the section's musical role. An unstemmed beat
        /// map can only follow everything.
        /// </summary>
        private static ChartInstrument SectionInstrument(ChartInstrument selector, SongSectionType type, bool hasStems)
        {
            if (!hasStems) return ChartInstrument.All;
            if (selector != ChartInstrument.All) return selector;

            return type switch
            {
                SongSectionType.Intro => ChartInstrument.Drums,
                SongSectionType.Verse => ChartInstrument.Drums,
                SongSectionType.Outro => ChartInstrument.Drums,
                SongSectionType.Bridge => ChartInstrument.Bass,
                SongSectionType.Chorus => ChartInstrument.Melody,
                SongSectionType.Drop => ChartInstrument.All,
                SongSectionType.Break => ChartInstrument.All,
                _ => ChartInstrument.All
            };
        }

        private static List<BeatMarker> MarkersInRange(List<BeatMarker> all, float start, float end, ChartInstrument inst)
        {
            var result = new List<BeatMarker>();
            for (int i = 0; i < all.Count; i++)
            {
                BeatMarker m = all[i];
                if (m.beat < start || m.beat >= end) continue;
                if (m.type == MarkerType.Break) continue;
                if (inst != ChartInstrument.All && m.instrument != inst) continue;
                result.Add(m);
            }

            // If a section's chosen instrument is silent here, fall back to all in-range markers
            // so a section that should have notes is not accidentally empty.
            if (result.Count == 0 && inst != ChartInstrument.All)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    BeatMarker m = all[i];
                    if (m.beat < start || m.beat >= end) continue;
                    if (m.type == MarkerType.Break) continue;
                    result.Add(m);
                }
            }
            return result;
        }

        // ---- Per-section fill ----

        private static void FillSide(
            List<BeatMarker> secMarkers, SongSectionType secType, float secStart, float secEnd,
            NotePatternLibrary library, int globalMaxDiff, float intensityScale, float leadInBeats,
            ISeededRandom sideRng, ref PickContext ctx, List<StampedNote> output)
        {
            int bar = 0;
            float barStart = secStart;

            while (barStart < secEnd - Epsilon)
            {
                float barEnd = Mathf.Min(barStart + BarBeats, secEnd);
                float barLen = barEnd - barStart;

                int onsets = CountMarkersInRange(secMarkers, barStart, barEnd);
                float scaledOnsets = intensityScale > 0f ? onsets * intensityScale : onsets;
                int bucket = DensityBucket(scaledOnsets, barLen);

                if (bucket == 0)
                {
                    // The song rests here, so the chart rests. A real breather, not a dropped bar.
                    barStart += BarBeats;
                    bar++;
                    continue;
                }

                float targetDensity = Mathf.Clamp(scaledOnsets / Mathf.Max(0.5f, barLen), 0.1f, 3f);
                int maxDiff = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(2f, globalMaxDiff, bucket / 4f)), 1, globalMaxDiff);

                float pos = barStart;
                int sub = 0;
                int guard = 0;

                while (pos < barEnd - Epsilon && guard++ < 16)
                {
                    float remaining = barEnd - pos;

                    // Seed the pick by musical POSITION (section type, bar, sub-slot, density),
                    // not by a running sequence. Two choruses that land on the same density at
                    // bar 3 therefore draw the same fragment: motif repetition (point 3).
                    ISeededRandom pickRng = sideRng.Fork($"{(int)secType}:{bar}:{sub}:{bucket}");
                    NotePattern frag = PickPattern(library, maxDiff, targetDensity, remaining, ctx, pickRng);
                    if (frag == null) break;

                    LaneTransform transform = ChooseTransform(frag, ctx.PreviousEndLane, pickRng);
                    StampFragment(frag, pos, secEnd, leadInBeats, transform, output);

                    ctx.Update(frag, transform.Apply(frag.EndLane));

                    pos += Mathf.Max(0.25f, frag.lengthBeats);
                    sub++;
                }

                barStart += BarBeats;
                bar++;
            }
        }

        // ---- Fragment selection (points 3 + 4) ----

        private static NotePattern PickPattern(
            NotePatternLibrary library, int maxDiff, float targetDensity, float remaining,
            PickContext ctx, ISeededRandom pickRng)
        {
            int count = library.Query(maxDiff, ShapeTag.None, _queryBuffer);
            if (count == 0)
            {
                count = library.Query(10, ShapeTag.None, _queryBuffer);
                if (count == 0) return null;
            }

            int distinctFamilies = CountDistinctFamilies(_queryBuffer);
            _scoreBuffer.Clear();
            float total = 0f;
            bool anyFits = false;

            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                NotePattern c = _queryBuffer[i];

                if (c.lengthBeats > remaining + Epsilon) { _scoreBuffer.Add(0f); continue; } // must fit the slot
                anyFits = true;

                // Closeness of the fragment's density to the bar's real onset density (point 4).
                float score = c.weight / (1f + 3f * Mathf.Abs(c.Density - targetDensity));

                // Soften repeats of a recently used family, but only when there is variety to spare.
                if (distinctFamilies > 2 && ctx.IsFamilyRecent(c)) score *= 0.15f;

                _scoreBuffer.Add(score);
                total += score;
            }

            if (!anyFits) return null;            // nothing fits the remaining slot; leave a gap
            if (total <= 0f) return FirstFitting(remaining);

            float roll = pickRng.NextFloat() * total;
            float cum = 0f;
            for (int i = 0; i < _queryBuffer.Count; i++)
            {
                cum += _scoreBuffer[i];
                if (roll < cum) return _queryBuffer[i];
            }
            return FirstFitting(remaining);
        }

        private static NotePattern FirstFitting(float remaining)
        {
            for (int i = 0; i < _queryBuffer.Count; i++)
                if (_queryBuffer[i].lengthBeats <= remaining + Epsilon) return _queryBuffer[i];
            return null;
        }

        private static int CountDistinctFamilies(List<NotePattern> patterns)
        {
            int count = 0;
            for (int i = 0; i < patterns.Count; i++)
            {
                bool unique = true;
                for (int j = 0; j < i; j++)
                    if (patterns[j].EffectiveFamily == patterns[i].EffectiveFamily) { unique = false; break; }
                if (unique) count++;
            }
            return count;
        }

        // ---- Stamping + lane flow (point 6) ----

        private static void StampFragment(NotePattern frag, float startBeat, float sectionEnd, float leadInBeats, LaneTransform transform, List<StampedNote> output)
        {
            List<NotePattern.Note> notes = frag.notes;
            if (notes == null) return;

            for (int i = 0; i < notes.Count; i++)
            {
                NotePattern.Note n = notes[i];
                float beat = startBeat + n.offsetBeats;
                if (beat < leadInBeats) continue;           // note-free intro: the song's opening plays
                                                            // immediately while these first notes scroll in
                if (beat >= sectionEnd - 0.0001f) continue; // never spill past the section boundary
                output.Add(new StampedNote(transform.Apply(n.lane), beat, n.holdBeats));
            }
        }

        /// <summary>
        /// A whole-fragment lane remap: an optional left/right mirror, then a 0-3 lane rotation
        /// (wrap-around shift). Applied identically to every note in the fragment, so the shape is
        /// preserved and only its position on the board moves. Lanes are 0-3; rotation wraps.
        /// </summary>
        private readonly struct LaneTransform
        {
            public readonly bool Mirror;
            public readonly int Shift;
            public LaneTransform(bool mirror, int shift) { Mirror = mirror; Shift = shift; }

            public int Apply(int lane)
            {
                if (lane < 0) return lane;
                int l = Mathf.Clamp(lane, 0, 3);
                if (Mirror) l = 3 - l;
                return (l + Shift) & 3; // & 3 == mod 4 for non-negative
            }
        }

        /// <summary>
        /// Choose a lane transform for variety: the same authored fragment should not always land
        /// on the same columns. Weighted toward transforms whose first lane sits close to the
        /// previous fragment's exit, so the hand keeps flowing and transitions stay readable.
        /// With no previous lane (first fragment of a side) every transform is equally likely.
        /// Drawn from the slot-keyed RNG, so the variety is deterministic for a given seed.
        /// </summary>
        private static LaneTransform ChooseTransform(NotePattern frag, int previousEndLane, ISeededRandom rng)
        {
            int start = frag.StartLane;

            float total = 0f;
            for (int mi = 0; mi < 2; mi++)
                for (int shift = 0; shift < 4; shift++)
                    total += TransformWeight(start, mi == 1, shift, previousEndLane);

            if (total <= 0f) return new LaneTransform(false, 0);

            float roll = rng.NextFloat() * total;
            float cum = 0f;
            for (int mi = 0; mi < 2; mi++)
                for (int shift = 0; shift < 4; shift++)
                {
                    cum += TransformWeight(start, mi == 1, shift, previousEndLane);
                    if (roll < cum) return new LaneTransform(mi == 1, shift);
                }
            return new LaneTransform(false, 0);
        }

        private static float TransformWeight(int startLane, bool mirror, int shift, int previousEndLane)
        {
            // First fragment (no exit lane) or an empty fragment: every transform equally likely.
            if (previousEndLane < 0 || startLane < 0) return 1f;
            int sl = Mathf.Clamp(startLane, 0, 3);
            int transformedStart = (((mirror ? 3 - sl : sl) + shift) & 3);
            int flowCost = Mathf.Abs(transformedStart - previousEndLane); // 0 (adjacent) .. 3 (far)
            return 1f / (1f + 2f * flowCost); // smooth transitions favored, jumps still possible
        }

        // ---- Density helpers (points 2 + 4) ----

        private static int CountMarkersInRange(List<BeatMarker> markers, float start, float end)
        {
            int count = 0;
            for (int i = 0; i < markers.Count; i++)
                if (markers[i].beat >= start && markers[i].beat < end) count++;
            return count;
        }

        /// <summary>
        /// Map a bar's onset count to a density tier 0-4 (rest, sparse, medium, dense, very dense).
        /// Onsets are normalized to a full bar so short trailing bars are judged fairly.
        /// </summary>
        private static int DensityBucket(float onsetsInBar, float barLen)
        {
            float perBar = onsetsInBar * (BarBeats / Mathf.Max(0.5f, barLen));
            if (perBar <= 0.5f) return 0;
            if (perBar <= 2.5f) return 1;
            if (perBar <= 4.5f) return 2;
            if (perBar <= 6.5f) return 3;
            return 4;
        }
    }
}
