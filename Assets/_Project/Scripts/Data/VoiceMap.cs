using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Maps each musical voice (Kick, Snare, HiHat, Melodic, etc.)
    /// to a fixed lane or lane-pair for the duration of a song section.
    /// 
    /// This is what makes charts feel human-charted. In FNF and every
    /// good VSRG chart, the same sound always maps to the same lane.
    /// The player learns the mapping subconsciously and can predict
    /// where the next note will be based on what they HEAR.
    /// 
    /// Core principle from VSRG charting theory:
    ///   "Lower sounds go left, higher sounds go right."
    ///   "A repeating kick drum should always be the same lane."
    ///   "Consistency within a section, variation between sections."
    /// 
    /// The map is generated once per section from a seeded random,
    /// then every note in that section uses it. Between sections,
    /// the map can rotate or mirror for variety.
    /// </summary>
    public struct VoiceMap
    {
        // Primary lane for each voice (where single notes go)
        public int KickLane;
        public int SnareLane;
        public int HiHatLaneA;   // HiHats trill between A and B
        public int HiHatLaneB;
        public int MelodicStart; // Starting lane for melodic movement
        public int AccentLaneA;  // Accents/drops use a jump pair
        public int AccentLaneB;
        public int FillDirection; // +1 = ascending, -1 = descending

        /// <summary>
        /// Get the primary lane for a marker type.
        /// For types with two lanes (HiHat trill, Accent jump),
        /// alternates based on the note index within the section.
        /// </summary>
        public int GetLane(MarkerType type, int noteIndex)
        {
            return type switch
            {
                MarkerType.Kick => KickLane,
                MarkerType.Snare => SnareLane,
                MarkerType.HiHat => (noteIndex % 2 == 0) ? HiHatLaneA : HiHatLaneB,
                MarkerType.Melodic => -1, // Handled separately with direction
                MarkerType.Accent => AccentLaneA,
                MarkerType.Drop => AccentLaneA,
                MarkerType.Break => -1, // No note
                MarkerType.Fill => GetFillLane(noteIndex),
                _ => KickLane
            };
        }

        /// <summary>
        /// Get the jump lane for high-intensity moments.
        /// Returns -1 if no jump should occur.
        /// </summary>
        public int GetJumpLane(MarkerType type)
        {
            return type switch
            {
                MarkerType.Accent => AccentLaneB,
                MarkerType.Drop => AccentLaneB,
                _ => -1
            };
        }

        /// <summary>
        /// Get the lane for a fill note. Fills cycle through all 4 lanes
        /// in order (ascending or descending based on FillDirection).
        /// </summary>
        private int GetFillLane(int noteIndex)
        {
            if (FillDirection > 0)
                return noteIndex % 4;           // 0,1,2,3,0,1,2,3...
            else
                return 3 - (noteIndex % 4);     // 3,2,1,0,3,2,1,0...
        }

        // =================================================================
        // PRESETS — common voice layouts
        // =================================================================

        /// <summary>
        /// Standard layout: kick left, snare right, hihats center.
        /// The most natural mapping matching "low=left, high=right."
        /// </summary>
        public static VoiceMap Standard => new VoiceMap
        {
            KickLane = 0,
            SnareLane = 3,
            HiHatLaneA = 1,
            HiHatLaneB = 2,
            MelodicStart = 1,
            AccentLaneA = 0,
            AccentLaneB = 3,
            FillDirection = 1
        };

        /// <summary>
        /// Mirrored: kick right, snare left. Used for section variety.
        /// </summary>
        public static VoiceMap Mirrored => new VoiceMap
        {
            KickLane = 3,
            SnareLane = 0,
            HiHatLaneA = 2,
            HiHatLaneB = 1,
            MelodicStart = 2,
            AccentLaneA = 3,
            AccentLaneB = 0,
            FillDirection = -1
        };

        /// <summary>
        /// Center-focused: kick and snare on inner lanes.
        /// Easier for beginners — less hand movement.
        /// </summary>
        public static VoiceMap Center => new VoiceMap
        {
            KickLane = 1,
            SnareLane = 2,
            HiHatLaneA = 0,
            HiHatLaneB = 3,
            MelodicStart = 0,
            AccentLaneA = 1,
            AccentLaneB = 2,
            FillDirection = 1
        };

        /// <summary>
        /// Wide: kick and snare on outer lanes, hihats center.
        /// More dramatic hand movement for higher difficulty.
        /// </summary>
        public static VoiceMap Wide => new VoiceMap
        {
            KickLane = 0,
            SnareLane = 3,
            HiHatLaneA = 2,
            HiHatLaneB = 1,
            MelodicStart = 0,
            AccentLaneA = 0,
            AccentLaneB = 3,
            FillDirection = -1
        };

        // =================================================================
        // GENERATION
        // =================================================================

        private static readonly VoiceMap[] AllLayouts =
        {
            Standard, Mirrored, Center, Wide
        };

        /// <summary>
        /// Pick a voice map for a section. Uses the section index as
        /// variation — different sections get different layouts, but
        /// the same section always gets the same layout (deterministic).
        /// </summary>
        public static VoiceMap ForSection(ISeededRandom rng, int sectionIndex)
        {
            // First section always gets Standard (most intuitive)
            if (sectionIndex == 0)
                return Standard;

            // Subsequent sections rotate through layouts
            // Using RNG so the same seed produces the same sequence
            int pick = rng.Range(0, AllLayouts.Length);
            return AllLayouts[pick];
        }
    }
}
