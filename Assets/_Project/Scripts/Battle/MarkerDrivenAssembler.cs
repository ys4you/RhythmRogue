using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Converts musical event data into a BattleChart using voice-consistent
    /// lane mapping — the same sound always goes to the same lane.
    /// 
    /// Each song section gets a VoiceMap that assigns:
    ///   Kick → fixed lane (e.g. 0)
    ///   Snare → fixed lane (e.g. 3)
    ///   HiHat → trill pair (e.g. 1,2)
    ///   Melodic → staircase following pitch direction
    ///   Accent/Drop → jump pair
    /// 
    /// Between sections, the voice map rotates (Standard → Mirrored → Center)
    /// so the chart has variety across the song while each section is
    /// internally consistent.
    /// 
    /// This produces charts where:
    ///   - Players can predict notes by listening
    ///   - Visual patterns match musical structure
    ///   - Repeated phrases look similar
    ///   - It feels human-charted
    /// </summary>
    public static class MarkerDrivenAssembler
    {
        private static readonly List<BeatMarker> _markerBuffer = new(128);

        // =================================================================
        // PUBLIC — from SongBeatMap
        // =================================================================

        public static BattleChart Assemble(
            SongBeatMap beatMap,
            ISeededRandom rng,
            float difficulty,
            float bpm)
        {
            if (beatMap == null)
            {
                Debug.LogError("[MarkerAssembler] SongBeatMap is null.");
                return null;
            }

            if (beatMap.markers == null || beatMap.markers.Count == 0)
            {
                Debug.LogError("[MarkerAssembler] SongBeatMap has no markers.");
                return null;
            }

            if (beatMap.sections == null || beatMap.sections.Count == 0)
            {
                Debug.LogError("[MarkerAssembler] SongBeatMap has no sections.");
                return null;
            }

            return AssembleInternal(
                beatMap.markers, beatMap.sections,
                beatMap.leadInBeats, beatMap.tailBeats, beatMap.TotalBeats,
                rng, difficulty, bpm, beatMap.songName);
        }

        // =================================================================
        // PUBLIC — from runtime analysis
        // =================================================================

        public static BattleChart Assemble(
            List<BeatMarker> markers,
            List<SongSection> sections,
            float leadInBeats,
            float tailBeats,
            float totalBeats,
            ISeededRandom rng,
            float difficulty,
            float bpm,
            string sourceName = "runtime")
        {
            if (markers == null || markers.Count == 0)
            {
                Debug.LogError("[MarkerAssembler] No markers provided.");
                return null;
            }

            if (sections == null || sections.Count == 0)
            {
                Debug.LogError("[MarkerAssembler] No sections provided.");
                return null;
            }

            return AssembleInternal(
                markers, sections,
                leadInBeats, tailBeats, totalBeats,
                rng, difficulty, bpm, sourceName);
        }

        // =================================================================
        // INTERNAL
        // =================================================================

        private static BattleChart AssembleInternal(
            List<BeatMarker> allMarkers,
            List<SongSection> sections,
            float leadInBeats,
            float tailBeats,
            float totalSongBeats,
            ISeededRandom rng,
            float difficulty,
            float bpm,
            string sourceName)
        {
            if (rng == null)
            {
                Debug.LogError("[MarkerAssembler] Seeded random is null.");
                return null;
            }

            difficulty = Mathf.Clamp01(difficulty);
            DifficultyProfile profile = DifficultyProfile.FromDifficulty(difficulty);

            Debug.Log($"[MarkerAssembler] Difficulty profile: {profile}");

            // Fork RNG per highway so enemy/player get independent voice maps
            ISeededRandom enemyRng = rng.Fork("voice_enemy");
            ISeededRandom playerRng = rng.Fork("voice_player");

            // Fork for voice map selection (shared so both highways
            // get complementary maps, not identical ones)
            ISeededRandom mapRng = rng.Fork("voice_maps");

            var chartSections = new List<ChartSection>(sections.Count);

            for (int i = 0; i < sections.Count; i++)
            {
                // Each section gets its own voice map — consistent within,
                // varied between sections
                VoiceMap voiceMap = VoiceMap.ForSection(mapRng, i);

                ChartSection chartSection = AssembleSection(
                    allMarkers, sections[i], profile, voiceMap,
                    enemyRng, playerRng);

                chartSections.Add(chartSection);
            }

            float totalBeats = leadInBeats + totalSongBeats + tailBeats;

            var chart = new BattleChart(bpm, leadInBeats, totalBeats, chartSections);

            Debug.Log($"[MarkerAssembler] Assembled from '{sourceName}': " +
                      $"{chartSections.Count} sections, " +
                      $"{chart.PlayerNoteCount} player notes, " +
                      $"{chart.EnemyNoteCount} enemy notes, " +
                      $"difficulty {difficulty:F2}");

            return chart;
        }

        // =================================================================
        // SECTION ASSEMBLY
        // =================================================================

        private static ChartSection AssembleSection(
            List<BeatMarker> allMarkers,
            SongSection section,
            DifficultyProfile profile,
            VoiceMap voiceMap,
            ISeededRandom enemyRng,
            ISeededRandom playerRng)
        {
            GetMarkersInRange(allMarkers, section.startBeat, section.endBeat, _markerBuffer);

            float sectionScale = section.intensityScale > 0f ? section.intensityScale : 1f;

            // Filter by difficulty
            var filtered = FilterMarkers(_markerBuffer, profile.IntensityThreshold, sectionScale);

            List<StampedNote> enemyNotes = new();
            List<StampedNote> playerNotes = new();

            bool generateEnemy = section.highway == SectionType.EnemyOnly ||
                                 section.highway == SectionType.Both;
            bool generatePlayer = section.highway == SectionType.PlayerOnly ||
                                  section.highway == SectionType.Both;

            if (generateEnemy && filtered.Count > 0)
            {
                var planner = new PhrasePlanner(enemyRng, profile);
                var planned = planner.PlanSection(filtered, voiceMap);
                enemyNotes = ConvertPlanned(planned);
            }

            if (generatePlayer && filtered.Count > 0)
            {
                var planner = new PhrasePlanner(playerRng, profile);
                var planned = planner.PlanSection(filtered, voiceMap);
                playerNotes = ConvertPlanned(planned);
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

        // =================================================================
        // MARKER FILTERING
        // =================================================================

        private static List<BeatMarker> FilterMarkers(
            List<BeatMarker> markers,
            float intensityThreshold,
            float sectionScale)
        {
            var result = new List<BeatMarker>(markers.Count);

            for (int i = 0; i < markers.Count; i++)
            {
                BeatMarker marker = markers[i];

                if (marker.type == MarkerType.Break)
                {
                    result.Add(marker);
                    continue;
                }

                float effective = marker.intensity * sectionScale;
                if (effective >= intensityThreshold)
                    result.Add(marker);
            }

            return result;
        }

        // =================================================================
        // CONVERSION
        // =================================================================

        private static List<StampedNote> ConvertPlanned(List<PhrasePlanner.PlannedNote> planned)
        {
            var notes = new List<StampedNote>(planned.Count);

            for (int i = 0; i < planned.Count; i++)
            {
                var p = planned[i];
                if (!p.ShouldPlace) continue;

                notes.Add(new StampedNote(p.Lane, p.Beat, p.HoldBeats));

                if (p.IsJump)
                    notes.Add(new StampedNote(p.JumpLane, p.Beat, 0f));
            }

            notes.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            return notes;
        }

        // =================================================================
        // RANGE QUERY
        // =================================================================

        private static void GetMarkersInRange(
            List<BeatMarker> allMarkers, float startBeat, float endBeat,
            List<BeatMarker> result)
        {
            result.Clear();

            for (int i = 0; i < allMarkers.Count; i++)
            {
                float beat = allMarkers[i].beat;
                if (beat >= startBeat && beat < endBeat)
                    result.Add(allMarkers[i]);
            }

            result.Sort();
        }
    }
}
