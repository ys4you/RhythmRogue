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

        public ChartResult Resolve(EnemyData enemy, bool isElite, ISeededRandom rng, TextAsset selectedChart = null)
        {
            if (enemy == null) { GameLog.Error("[ChartProvider] No enemy data."); return default; }

            Conductor conductor = Conductor.Instance;
            AssignSong(enemy, conductor);

            float difficulty = GetEffectiveDifficulty(enemy, isElite);
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
            ShapeLibrary library = GetShapeLibrary(enemy);
            float effectiveBPM = beatMap.bpm * bpmModifier;

            var markers = FilterByInstrument(new List<BeatMarker>(beatMap.markers), enemy.chartInstrument);
            var sections = beatMap.sections != null ? new List<SongSection>(beatMap.sections) : null;
            float totalBeats = beatMap.TotalBeats > 0f ? beatMap.TotalBeats : (markers.Count > 0 ? markers[^1].beat + 4f : 0f);

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

        private float GetEffectiveDifficulty(EnemyData enemy, bool isElite)
        {
            float d = enemy.markerDifficulty;
            if (isElite && _eliteConfig != null) d = Mathf.Clamp01(d + _eliteConfig.difficultyBoost * 0.1f);
            return d;
        }

        private float GetEffectiveBPMModifier(EnemyData enemy, bool isElite)
        {
            float m = enemy.bpmModifier;
            if (isElite && _eliteConfig != null) m = _eliteConfig.ScaleBPMModifier(m);
            return m;
        }
    }
}
