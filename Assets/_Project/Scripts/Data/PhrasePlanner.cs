using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Plans lane assignments for beat markers using voice-consistent mapping.
    /// 
    /// Core principle: the same musical voice always maps to the same lane
    /// within a section. This is how human FNF/VSRG charters work —
    /// kick is always left, snare is always right, hihats trill center.
    /// The player learns the mapping by ear and can predict notes.
    /// 
    /// Patterns emerge NATURALLY from the music:
    ///   - Kick on 1+3, snare on 2+4 → alternating L,R,L,R
    ///   - Add hihats on 8ths → L, center-trill, R, center-trill
    ///   - Ascending melody → staircase left to right
    ///   - Drum fill → stream 0,1,2,3,0,1,2,3
    /// 
    /// No abstract "pattern library" needed — the music IS the pattern.
    /// 
    /// Between sections, the VoiceMap rotates (e.g. kick moves from
    /// lane 0 to lane 3), creating variety across the song while
    /// keeping each section internally consistent.
    /// 
    /// Difficulty profile controls:
    ///   - Which markers become notes (intensity threshold)
    ///   - Minimum gap between notes (density cap)
    ///   - Whether jumps are allowed
    ///   - At beginner: only kicks and snares, very sparse
    ///   - At expert: everything, dense, with jumps on accents
    /// </summary>
    public class PhrasePlanner
    {
        private const int LaneCount = 4;

        // =================================================================
        // STATE
        // =================================================================

        private readonly ISeededRandom _rng;
        private readonly DifficultyProfile _profile;
        private int _lastLane = -1;
        private int _melodicLane;
        private int _noteIndex;

        // =================================================================
        // CONSTRUCTOR
        // =================================================================

        public PhrasePlanner(ISeededRandom rng, DifficultyProfile profile)
        {
            _rng = rng;
            _profile = profile;
        }

        // =================================================================
        // PUBLIC
        // =================================================================

        public struct PlannedNote
        {
            public float Beat;
            public int Lane;
            public int JumpLane;
            public float HoldBeats;
            public bool ShouldPlace;

            public bool IsJump => JumpLane >= 0;
        }

        /// <summary>
        /// Plan all notes for a section using a voice map.
        /// Each marker type gets a consistent lane from the map.
        /// </summary>
        public List<PlannedNote> PlanSection(
            List<BeatMarker> markers, VoiceMap voiceMap)
        {
            var result = new List<PlannedNote>(markers.Count);

            if (markers.Count == 0) return result;

            // Reset per-section state
            _melodicLane = voiceMap.MelodicStart;
            _noteIndex = 0;
            _lastLane = -1;

            // Thin by minimum gap
            var thinned = ThinByGap(markers);

            for (int i = 0; i < thinned.Count; i++)
            {
                BeatMarker marker = thinned[i];
                PlannedNote note = PlanNote(marker, voiceMap);

                if (note.ShouldPlace)
                {
                    result.Add(note);
                    _lastLane = note.Lane;
                    _noteIndex++;
                }
            }

            return result;
        }

        // =================================================================
        // NOTE PLANNING — voice-consistent
        // =================================================================

        private PlannedNote PlanNote(BeatMarker marker, VoiceMap voiceMap)
        {
            // Breaks never produce notes
            if (marker.type == MarkerType.Break)
                return new PlannedNote { ShouldPlace = false };

            int lane;
            int jumpLane = -1;

            if (marker.type == MarkerType.Melodic)
            {
                // Melodic notes follow pitch direction — staircase
                lane = PlanMelodicLane(marker);
            }
            else
            {
                // All other voices use their fixed lane from the map
                lane = voiceMap.GetLane(marker.type, _noteIndex);
            }

            // Jump decision — only for Accent/Drop and only if difficulty allows
            if (_profile.JumpsEnabled && marker.intensity >= 0.8f)
            {
                int possibleJump = voiceMap.GetJumpLane(marker.type);

                if (possibleJump >= 0 && possibleJump != lane)
                {
                    if (_rng.NextFloat() < _profile.JumpChance)
                        jumpLane = possibleJump;
                }
            }

            return new PlannedNote
            {
                Beat = marker.beat,
                Lane = Mathf.Clamp(lane, 0, LaneCount - 1),
                JumpLane = jumpLane,
                HoldBeats = marker.holdBeats,
                ShouldPlace = true
            };
        }

        // =================================================================
        // MELODIC LANE — follows pitch direction (staircase)
        // =================================================================

        /// <summary>
        /// Melodic notes create staircases by following the direction field.
        /// Positive direction = move right, negative = move left.
        /// Wraps around at lane boundaries for continuous movement.
        /// </summary>
        private int PlanMelodicLane(BeatMarker marker)
        {
            if (Mathf.Abs(marker.direction) > 0.1f)
            {
                // Move in the indicated direction
                if (marker.direction > 0f)
                    _melodicLane = Mathf.Min(_melodicLane + 1, LaneCount - 1);
                else
                    _melodicLane = Mathf.Max(_melodicLane - 1, 0);
            }
            else
            {
                // Neutral direction — stay or slightly vary
                if (_rng.NextFloat() < 0.3f)
                {
                    int shift = _rng.NextFloat() < 0.5f ? -1 : 1;
                    _melodicLane = Mathf.Clamp(_melodicLane + shift, 0, LaneCount - 1);
                }
            }

            return _melodicLane;
        }

        // =================================================================
        // DENSITY THINNING
        // =================================================================

        /// <summary>
        /// Remove notes that are too close together.
        /// Keeps the higher-intensity note in each gap window.
        /// </summary>
        private List<BeatMarker> ThinByGap(List<BeatMarker> markers)
        {
            if (_profile.MinNoteGapBeats <= 0f) return markers;

            var result = new List<BeatMarker>(markers.Count);
            float lastBeat = float.MinValue;

            for (int i = 0; i < markers.Count; i++)
            {
                float gap = markers[i].beat - lastBeat;

                if (gap >= _profile.MinNoteGapBeats)
                {
                    result.Add(markers[i]);
                    lastBeat = markers[i].beat;
                }
                else if (result.Count > 0 &&
                         markers[i].intensity > result[result.Count - 1].intensity)
                {
                    // Replace last note with this stronger one
                    result[result.Count - 1] = markers[i];
                    lastBeat = markers[i].beat;
                }
            }

            return result;
        }

        // =================================================================
        // RESET
        // =================================================================

        public void Reset()
        {
            _lastLane = -1;
            _melodicLane = 1;
            _noteIndex = 0;
        }
    }
}
