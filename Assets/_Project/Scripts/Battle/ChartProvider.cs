using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Resolves chart data for a battle. Assigns song, assembles chart,
    /// applies difficulty/BPM scaling. Stateless after Resolve() returns.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChartProvider : MonoBehaviour
    {
        [Header("Chart Mode")]
        [SerializeField] private bool _useLegacyChart = false;

        [Header("Legacy Defaults")]
        [SerializeField] private TextAsset _defaultChart;

        [Header("Shape System")]
        [SerializeField] private ShapeLibrary _defaultShapeLibrary;

        [Header("Pattern System")]
        [Tooltip("Default fragment library for the human-feel PatternAssembler. An enemy's own " +
                 "patternLibrary overrides this. When a pattern library is available the " +
                 "PatternAssembler runs; with none, the older lane-shape ShapeAssembler does.")]
        [SerializeField] private NotePatternLibrary _defaultPatternLibrary;

        [Header("Elite Scaling")]
        [SerializeField] private EliteConfig _eliteConfig;

        public readonly struct ChartResult
        {
            public readonly BattleChart BattleChart;
            public readonly LoadedChart LegacyChart;
            public readonly bool IsLegacy;
            public readonly float EffectiveBPM;
            public readonly float AudioOffset;
            public readonly string Mode;
            public bool Success => IsLegacy ? LegacyChart != null : BattleChart != null;

            public ChartResult(BattleChart chart, string mode, float bpm, float offset)
            {
                BattleChart = chart; LegacyChart = null; IsLegacy = false;
                EffectiveBPM = bpm; AudioOffset = offset; Mode = mode;
            }

            public ChartResult(LoadedChart chart, float bpm, float offset)
            {
                BattleChart = null; LegacyChart = chart; IsLegacy = true;
                EffectiveBPM = bpm; AudioOffset = offset; Mode = "legacy";
            }
        }

        public ChartResult Resolve(EnemyData enemy, bool isElite, ISeededRandom rng, TextAsset selectedChart = null, DifficultyContext ctx = default)
        {
            if (enemy == null) { GameLog.Error("[ChartProvider] No enemy data."); return default; }

            Conductor conductor = Conductor.Instance;
            AssignSong(enemy, conductor);

            float difficulty = GetEffectiveDifficulty(enemy, isElite, ctx);
            float bpmModifier = GetEffectiveBPMModifier(enemy, isElite);

            if (_useLegacyChart) return ResolveLegacy(selectedChart, enemy, bpmModifier);
            if (enemy.songBeatMap != null) return ResolveFromBeatMap(enemy, rng, difficulty, bpmModifier);

            GameLog.Error("[ChartProvider] No chart source. Assign the enemy a SongBeatMap (or enable legacy chart mode).");
            return default;
        }

        private void AssignSong(EnemyData enemy, Conductor conductor)
        {
            AudioClip clip = enemy.EffectiveSong;
            if (clip == null) { GameLog.Warn("[ChartProvider] Enemy has no song."); return; }

            conductor.SetClip(clip);
            GameLog.Info($"[ChartProvider] Song assigned: {clip.name}");
        }

        private ChartResult ResolveLegacy(TextAsset selectedChart, EnemyData enemy, float bpmModifier)
        {
            TextAsset chartAsset = selectedChart ?? _defaultChart;
            if (chartAsset == null) { GameLog.Error("[ChartProvider] No legacy chart data."); return default; }

            LoadedChart chart = ChartLoader.Load(chartAsset);
            if (chart == null) { GameLog.Error("[ChartProvider] Failed to load legacy chart."); return default; }

            return new ChartResult(chart, chart.BPM * bpmModifier, chart.Offset);
        }

        private ChartResult ResolveFromBeatMap(EnemyData enemy, ISeededRandom rng, float difficulty, float bpmModifier)
        {
            SongBeatMap beatMap = enemy.songBeatMap;
            float effectiveBPM = beatMap.bpm * bpmModifier;

            var allMarkers = new List<BeatMarker>(beatMap.markers);
            var sections = beatMap.sections != null ? new List<SongSection>(beatMap.sections) : null;
            float totalBeats = beatMap.TotalBeats > 0f ? beatMap.TotalBeats : (allMarkers.Count > 0 ? allMarkers[^1].beat + 4f : 0f);

            // Preferred path: human-feel fragments. The assembler does its own per-section
            // instrument selection from the raw markers plus the enemy's selector, so they are
            // passed unfiltered here.
            NotePatternLibrary patternLib = enemy.patternLibrary != null ? enemy.patternLibrary : _defaultPatternLibrary;
            if (patternLib != null && patternLib.patterns.Count > 0)
            {
                float leadInBeats = ComputeLeadInBeats(effectiveBPM);
                BattleChart patternChart = PatternAssembler.Assemble(
                    allMarkers, sections, patternLib, enemy.chartInstrument, rng, difficulty, effectiveBPM, totalBeats, leadInBeats);
                if (patternChart == null) { GameLog.Error("[ChartProvider] PatternAssembler returned null (beat map)."); return default; }
                return new ChartResult(patternChart, "pattern", effectiveBPM, beatMap.audioOffsetSeconds);
            }

            // Fallback: older lane-shape system. Markers are pre-filtered to the enemy's instrument.
            var markers = FilterByInstrument(allMarkers, enemy.chartInstrument);
            ShapeLibrary library = GetShapeLibrary(enemy);
            BattleChart chart = ShapeAssembler.Assemble(markers, sections, library, rng, difficulty, effectiveBPM, totalBeats);
            if (chart == null) { GameLog.Error("[ChartProvider] ShapeAssembler returned null (beat map)."); return default; }

            return new ChartResult(chart, "beat-map", effectiveBPM, beatMap.audioOffsetSeconds);
        }

        /// <summary>
        /// Keep only markers from the selected instrument stem, so the chart follows that
        /// instrument. <see cref="ChartInstrument.All"/> keeps everything. If the selection
        /// leaves no markers (an untagged/old beat map, or a stem with no onsets), the full
        /// list is returned instead so the chart still has notes rather than silently
        /// producing none. Sections are untouched: instrument selection only changes which
        /// onsets become notes, not the song structure. Public so the Chart Gym can audition
        /// stems through the same path battles use.
        /// </summary>
        public static List<BeatMarker> FilterByInstrument(List<BeatMarker> markers, ChartInstrument instrument)
        {
            if (instrument == ChartInstrument.All) return markers;

            var filtered = new List<BeatMarker>(markers.Count);
            for (int i = 0; i < markers.Count; i++)
                if (markers[i].instrument == instrument) filtered.Add(markers[i]);

            if (filtered.Count == 0)
            {
                GameLog.Warn($"[ChartProvider] No '{instrument}' markers in this beat map " +
                             "(untagged or empty stem); following all instruments instead.");
                return markers;
            }

            GameLog.Info($"[ChartProvider] Following {instrument}: {filtered.Count}/{markers.Count} markers.");
            return filtered;
        }

        private ShapeLibrary GetShapeLibrary(EnemyData enemy)
        {
            ShapeLibrary library = enemy.shapeLibrary ?? _defaultShapeLibrary;
            if (library == null || library.shapes.Count == 0)
                GameLog.Error("[ChartProvider] No ShapeLibrary available!");
            return library;
        }

        // Chart difficulty (0-1) for the assembler. Delegates to the central DifficultyCurve so
        // the area band, node depth, enemy flavour, elite boost, and run tier are combined in one
        // place. With no run context (default ctx) the curve falls back to the enemy's own value.
        private float GetEffectiveDifficulty(EnemyData enemy, bool isElite, in DifficultyContext ctx)
            => DifficultyCurve.ChartDifficulty(ctx, enemy, isElite, _eliteConfig);

        private float GetEffectiveBPMModifier(EnemyData enemy, bool isElite)
        {
            float m = enemy.bpmModifier;
            if (isElite && _eliteConfig != null) m = _eliteConfig.ScaleBPMModifier(m);
            return m;
        }

        // Beats of note-free intro at the very start of the chart. The song plays immediately, so the
        // opening notes need a runway to scroll in from off-screen rather than popping in on the
        // receptor. That runway is how long a note takes to cross the off-screen spawn distance at the
        // current scroll speed (distance / speed), converted to beats at this song's BPM. The
        // PatternAssembler leaves the first this-many beats note-free and the song's intro covers them.
        // Scroll-speed dependent: a slower scroll moves notes slower, so it needs a longer runway.
        private static float ComputeLeadInBeats(float bpm)
        {
            if (bpm <= 0f) return 0f;
            float scroll = Mathf.Max(0.1f, ScrollSpeedSetting.UnitsPerSecond);
            float travelSeconds = HighwayBase.DefaultSpawnAheadUnits / scroll;
            return Mathf.Ceil(travelSeconds * bpm / 60f);
        }
    }
}
