using System;

namespace RhythmRogue.Data
{
    /// <summary>
    /// JSON-serializable chart data structure.
    /// 
    /// Matches the chart JSON format 1:1 for deserialization via
    /// JsonUtility.FromJson&lt;ChartData&gt;(). This is the raw data
    /// as authored — ChartLoader validates and converts it into
    /// a sorted NoteData list for runtime use.
    /// 
    /// JSON format example:
    /// <code>
    /// {
    ///     "songName": "Test Song",
    ///     "bpm": 120,
    ///     "offset": 0.0,
    ///     "audioFile": "test_song.ogg",
    ///     "chartAuthor": "Yesse",
    ///     "notes": [
    ///         { "beat": 1.0,  "lane": 0, "type": "tap" },
    ///         { "beat": 1.5,  "lane": 2, "type": "tap" },
    ///         { "beat": 2.0,  "lane": 1, "type": "hold", "holdDuration": 2.0 },
    ///         { "beat": 4.0,  "lane": 0, "type": "tap" },
    ///         { "beat": 4.0,  "lane": 2, "type": "tap" }
    ///     ]
    /// }
    /// </code>
    /// 
    /// Field reference:
    ///   songName      — display name for UI
    ///   bpm           — starting BPM (Conductor uses this)
    ///   offset        — seconds of silence before beat 1 (lead-in)
    ///   audioFile     — filename of the audio clip (in Audio/Songs/)
    ///   chartAuthor   — who authored the chart
    ///   notes[]       — array of note entries
    ///     beat          — beat position (float, fractional for 8th/16th notes)
    ///     lane          — 0=Left, 1=Down, 2=Up, 3=Right
    ///     type          — "tap" or "hold"
    ///     holdDuration  — beats to hold (only for hold notes, default 0)
    /// </summary>
    [Serializable]
    public class ChartData
    {
        public string songName;
        public float bpm;
        public float offset;
        public string audioFile;
        public string chartAuthor;
        public RawNoteData[] notes;

        /// <summary>
        /// Optional enemy-side notes. These play automatically on the enemy highway and damage
        /// the player only while their guard is down (after a Miss). Same shape as notes[]. Most
        /// charts leave this empty; the onboarding's shield lesson uses it to show the guard
        /// blocking and then taking hits. Omit the field entirely for a player-only chart.
        /// </summary>
        public RawNoteData[] enemyNotes;

        /// <summary>
        /// Raw note data matching the JSON structure.
        /// Validated and converted to NoteData by ChartLoader.
        /// </summary>
        [Serializable]
        public class RawNoteData
        {
            public float beat;
            public int lane;
            public string type;
            public float holdDuration;
        }
    }
}
