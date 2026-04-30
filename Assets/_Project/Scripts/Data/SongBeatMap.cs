using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Per-song musical event data: beat markers (timing) + sections (structure).
    /// The algorithm handles lane placement, difficulty, and variety.
    /// </summary>
    [CreateAssetMenu(fileName = "New Song Beat Map", menuName = "RhythmRogue/Song Beat Map", order = 25)]
    public class SongBeatMap : ScriptableObject
    {
        [Header("Song")]
        public string songName;
        [Min(1f)] public float bpm = 120f;
        public AudioClip clip;
        public float audioOffsetSeconds;

        [Header("Sections")]
        public List<SongSection> sections = new();

        [Header("Beat Markers")]
        public List<BeatMarker> markers = new();

        [Header("Defaults")]
        [Min(0f)] public float leadInBeats = 4f;
        [Min(0f)] public float tailBeats = 2f;

        public float TotalBeats => clip != null && bpm > 0f ? clip.length / (60f / bpm) : 0f;
        public int MarkerCount => markers?.Count ?? 0;

        public int GetMarkersInRange(float startBeat, float endBeat, List<BeatMarker> result)
        {
            result.Clear();
            for (int i = 0; i < markers.Count; i++)
            {
                float beat = markers[i].beat;
                if (beat >= startBeat && beat < endBeat) result.Add(markers[i]);
            }
            result.Sort();
            return result.Count;
        }

        public SongSection? GetSectionAtBeat(float beat)
        {
            for (int i = 0; i < sections.Count; i++)
                if (sections[i].ContainsBeat(beat)) return sections[i];
            return null;
        }

        public int EstimateNoteCount(float difficultyThreshold)
        {
            if (markers == null) return 0;
            int count = 0;
            for (int i = 0; i < markers.Count; i++)
                if (markers[i].type != MarkerType.Break && markers[i].intensity >= difficultyThreshold) count++;
            return count;
        }

        private void OnValidate()
        {
            if (markers is { Count: > 1 }) markers.Sort();
            if (sections is { Count: > 1 }) sections.Sort();
        }
    }
}
