using System;
using System.Collections.Generic;
using RhythmRogue.Util;
using UnityEngine;

namespace RhythmRogue.Data
{
    /// <summary>
    /// Loads and validates chart JSON files into sorted NoteData lists.
    /// 
    /// Responsibilities:
    ///   1. Deserialize JSON into ChartData
    ///   2. Validate each note entry (lane range, hold durations, duplicates)
    ///   3. Convert to NoteData structs
    ///   4. Sort by beat position (secondary sort by lane)
    ///   5. Return a clean list ready for the note highway
    /// 
    /// Invalid entries are logged and skipped — never crashes on bad data.
    /// This lets chart authors iterate without the game breaking.
    /// 
    /// SOLID breakdown:
    /// - S: Only loads and validates chart data. No rendering, no gameplay.
    /// - O: New note types are added to NoteType enum, not by modifying this class.
    /// - L: Returns standard List&lt;NoteData&gt; usable by any consumer.
    /// - I: Single public method, no forced dependencies.
    /// - D: Depends on ChartData (data) and NoteData (data), not on gameplay systems.
    /// </summary>
    public static class ChartLoader
    {
        private const int MinLane = 0;
        private const int MaxLane = 3;

        /// <summary>
        /// Load a chart from a TextAsset (drag JSON file into a TextAsset field).
        /// </summary>
        /// <param name="chartAsset">TextAsset containing the chart JSON.</param>
        /// <returns>Parsed chart data with a sorted note list, or null on failure.</returns>
        public static LoadedChart Load(TextAsset chartAsset)
        {
            if (chartAsset == null)
            {
                GameLog.Error("[ChartLoader] Chart TextAsset is null.");
                return null;
            }

            return Load(chartAsset.text);
        }

        /// <summary>
        /// Load a chart from a raw JSON string.
        /// </summary>
        /// <param name="json">JSON string matching the ChartData format.</param>
        /// <returns>Parsed chart data with a sorted note list, or null on failure.</returns>
        public static LoadedChart Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                GameLog.Error("[ChartLoader] JSON string is null or empty.");
                return null;
            }

            // Deserialize
            ChartData raw;

            try
            {
                raw = JsonUtility.FromJson<ChartData>(json);
            }
            catch (Exception ex)
            {
                GameLog.Error($"[ChartLoader] JSON parse failed: {ex.Message}");
                return null;
            }

            if (raw == null)
            {
                GameLog.Error("[ChartLoader] JSON deserialized to null.");
                return null;
            }

            // Validate metadata
            if (raw.bpm <= 0f)
            {
                GameLog.Error($"[ChartLoader] Invalid BPM: {raw.bpm}. Must be positive.");
                return null;
            }

            // Parse and validate notes
            List<NoteData> notes = ParseNotes(raw.notes);
            List<NoteData> enemyNotes = ParseNotes(raw.enemyNotes, warnIfEmpty: false);

            GameLog.Info($"[ChartLoader] Loaded '{raw.songName}' — {notes.Count} notes, " +
                      $"{enemyNotes.Count} enemy notes, {raw.bpm} BPM, offset {raw.offset}s");

            return new LoadedChart(raw, notes, enemyNotes);
        }

        /// <summary>
        /// Parse raw note entries into validated, sorted NoteData list.
        /// </summary>
        private static List<NoteData> ParseNotes(ChartData.RawNoteData[] rawNotes, bool warnIfEmpty = true)
        {
            if (rawNotes == null || rawNotes.Length == 0)
            {
                if (warnIfEmpty) GameLog.Warn("[ChartLoader] Chart has no notes.");
                return new List<NoteData>();
            }

            var notes = new List<NoteData>(rawNotes.Length);
            var occupied = new HashSet<(float beat, int lane)>();

            for (int i = 0; i < rawNotes.Length; i++)
            {
                ChartData.RawNoteData raw = rawNotes[i];

                // Validate lane range
                if (raw.lane < MinLane || raw.lane > MaxLane)
                {
                    GameLog.Warn(
                        $"[ChartLoader] Note [{i}]: invalid lane {raw.lane} " +
                        $"(expected {MinLane}-{MaxLane}). Skipping.");
                    continue;
                }

                // Validate beat position
                if (raw.beat < 0f)
                {
                    GameLog.Warn(
                        $"[ChartLoader] Note [{i}]: negative beat position {raw.beat}. Skipping.");
                    continue;
                }

                // Parse note type
                NoteType noteType = ParseNoteType(raw.type);

                // Validate hold duration
                float holdDuration = 0f;

                if (noteType == NoteType.Hold)
                {
                    if (raw.holdDuration <= 0f)
                    {
                        GameLog.Warn(
                            $"[ChartLoader] Note [{i}]: hold note at beat {raw.beat} " +
                            $"has invalid duration {raw.holdDuration}. Treating as tap.");
                        noteType = NoteType.Tap;
                    }
                    else
                    {
                        holdDuration = raw.holdDuration;
                    }
                }

                // Check for duplicates (same beat + same lane)
                var key = (raw.beat, raw.lane);

                if (!occupied.Add(key))
                {
                    GameLog.Warn(
                        $"[ChartLoader] Note [{i}]: duplicate at beat {raw.beat}, " +
                        $"lane {raw.lane}. Skipping.");
                    continue;
                }

                notes.Add(new NoteData(raw.beat, raw.lane, noteType, holdDuration));
            }

            // Sort by beat position, then by lane for notes on the same beat
            notes.Sort((a, b) =>
            {
                int beatCompare = a.BeatPosition.CompareTo(b.BeatPosition);
                return beatCompare != 0 ? beatCompare : a.Lane.CompareTo(b.Lane);
            });

            return notes;
        }

        /// <summary>
        /// Parse note type string to enum. Defaults to Tap for unknown types.
        /// </summary>
        private static NoteType ParseNoteType(string typeStr)
        {
            if (string.IsNullOrEmpty(typeStr))
                return NoteType.Tap;

            return typeStr.ToLowerInvariant() switch
            {
                "tap" => NoteType.Tap,
                "hold" => NoteType.Hold,
                _ => LogAndDefault(typeStr)
            };
        }

        private static NoteType LogAndDefault(string typeStr)
        {
            GameLog.Warn($"[ChartLoader] Unknown note type '{typeStr}'. Defaulting to Tap.");
            return NoteType.Tap;
        }
    }

    /// <summary>
    /// Result of loading a chart — contains both metadata and the
    /// sorted note list ready for the highway to consume.
    /// </summary>
    public class LoadedChart
    {
        /// <summary>Song display name.</summary>
        public string SongName { get; }

        /// <summary>Starting BPM.</summary>
        public float BPM { get; }

        /// <summary>Song offset in seconds (lead-in before beat 1).</summary>
        public float Offset { get; }

        /// <summary>Audio filename (in Audio/Songs/).</summary>
        public string AudioFile { get; }

        /// <summary>Chart author name.</summary>
        public string ChartAuthor { get; }

        /// <summary>
        /// All notes sorted by beat position ascending, then by lane.
        /// This is the primary data the note highway iterates through.
        /// </summary>
        public IReadOnlyList<NoteData> Notes { get; }

        /// <summary>Total number of notes in the chart.</summary>
        public int NoteCount => Notes.Count;

        /// <summary>
        /// Optional enemy-side notes (auto-played on the enemy highway). Empty for most charts.
        /// </summary>
        public IReadOnlyList<NoteData> EnemyNotes { get; }

        /// <summary>Number of enemy notes (0 if none).</summary>
        public int EnemyNoteCount => EnemyNotes.Count;

        public LoadedChart(ChartData raw, List<NoteData> sortedNotes, List<NoteData> enemyNotes = null)
        {
            SongName = raw.songName ?? "Untitled";
            BPM = raw.bpm;
            Offset = raw.offset;
            AudioFile = raw.audioFile ?? "";
            ChartAuthor = raw.chartAuthor ?? "Unknown";
            Notes = sortedNotes.AsReadOnly();
            EnemyNotes = (enemyNotes ?? new List<NoteData>()).AsReadOnly();
        }
    }
}
