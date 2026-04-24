using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Per-song musical event data.
    /// 
    /// Contains beat markers (WHEN musically important moments happen)
    /// and section annotations (structural role of each song region).
    /// 
    /// This is the bridge between music and gameplay. A human listens
    /// to the song and marks the moments — the algorithm handles
    /// lane placement, difficulty scaling, and pattern variety.
    /// 
    /// Authoring workflow:
    ///   1. Create a SongBeatMap asset for each song
    ///   2. Set BPM and assign the AudioClip
    ///   3. Define sections (Intro, Verse, Chorus, etc.)
    ///   4. Add markers at musically important beats
    ///      (future: tap-along editor tool for step 4)
    ///   5. Assign to EnemyData.songBeatMap
    /// 
    /// The same beat map produces different charts at different
    /// difficulties — the difficulty filter controls which markers
    /// become notes.
    /// 
    /// Create via: Assets → Create → RhythmRogue → Song Beat Map
    /// </summary>
    [CreateAssetMenu(
        fileName = "New Song Beat Map",
        menuName = "RhythmRogue/Song Beat Map",
        order = 25)]
    public class SongBeatMap : ScriptableObject
    {
        [Header("Song")]
        [Tooltip("Display name of the song.")]
        public string songName;

        [Tooltip("Beats per minute. Must match the audio.")]
        [Min(1f)]
        public float bpm = 120f;

        [Tooltip("Audio clip for this song.")]
        public AudioClip clip;

        [Tooltip("Audio offset in seconds (positive = audio starts late, " +
                 "negative = audio starts early). Adjusts all marker timing.")]
        public float audioOffsetSeconds;

        [Header("Sections")]
        [Tooltip("Structural sections of the song. Should cover the full duration without gaps.")]
        public List<SongSection> sections = new();

        [Header("Beat Markers")]
        [Tooltip("Musical events in the song. The algorithm places notes at these moments.")]
        public List<BeatMarker> markers = new();

        [Header("Defaults")]
        [Tooltip("Lead-in silence before first note, in beats.")]
        [Min(0f)]
        public float leadInBeats = 4f;

        [Tooltip("Tail silence after last note, in beats.")]
        [Min(0f)]
        public float tailBeats = 2f;

        // =================================================================
        // QUERIES
        // =================================================================

        /// <summary>Total duration of the audio clip in beats.</summary>
        public float TotalBeats
        {
            get
            {
                if (clip == null || bpm <= 0f) return 0f;
                return clip.length / (60f / bpm);
            }
        }

        /// <summary>
        /// Get all markers within a beat range, sorted by beat.
        /// Writes into the provided list to avoid allocation.
        /// </summary>
        public int GetMarkersInRange(float startBeat, float endBeat, List<BeatMarker> result)
        {
            result.Clear();

            for (int i = 0; i < markers.Count; i++)
            {
                float beat = markers[i].beat;
                if (beat >= startBeat && beat < endBeat)
                    result.Add(markers[i]);
            }

            result.Sort();
            return result.Count;
        }

        /// <summary>
        /// Get the section active at a given beat. Returns null info if not found.
        /// </summary>
        public SongSection? GetSectionAtBeat(float beat)
        {
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].ContainsBeat(beat))
                    return sections[i];
            }
            return null;
        }

        // =================================================================
        // VALIDATION
        // =================================================================

        private void OnValidate()
        {
            // Sort markers by beat
            if (markers != null && markers.Count > 1)
                markers.Sort();

            // Sort sections by start beat
            if (sections != null && sections.Count > 1)
                sections.Sort();
        }

        // =================================================================
        // STATS — for inspector info
        // =================================================================

        /// <summary>Total marker count.</summary>
        public int MarkerCount => markers?.Count ?? 0;

        /// <summary>Estimated note count at a given difficulty threshold.</summary>
        public int EstimateNoteCount(float difficultyThreshold)
        {
            if (markers == null) return 0;

            int count = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].type == MarkerType.Break) continue;
                if (markers[i].intensity >= difficultyThreshold)
                    count++;
            }
            return count;
        }
    }
}
