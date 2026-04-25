using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Resolves chart data for a battle. Owns all chart assembly logic
    /// that was previously embedded in BattleManager.
    /// 
    /// Responsibilities:
    ///   - Assign the correct AudioClip to the Conductor
    ///   - Resolve the chart (legacy JSON, beat map, or auto-detect)
    ///   - Apply difficulty and BPM scaling (including elite modifiers)
    /// 
    /// BattleManager calls Resolve() once in Awake and consumes the result.
    /// This class has no Update loop and no persistent state.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChartProvider : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("Chart Mode")]
        [Tooltip("True = force legacy JSON chart. False = auto-detect from EnemyData.")]
        [SerializeField] private bool _useLegacyChart = false;

        [Header("Legacy Defaults")]
        [SerializeField] private TextAsset _defaultChart;

        [Header("Shape System")]
        [Tooltip("Fallback shape library if enemy has none assigned.")]
        [SerializeField] private ShapeLibrary _defaultShapeLibrary;

        [Header("Elite Scaling")]
        [SerializeField] private EliteConfig _eliteConfig;

        // =================================================================
        // RESULT
        // =================================================================

        /// <summary>
        /// Everything BattleManager needs after chart resolution.
        /// </summary>
        public readonly struct ChartResult
        {
            /// <summary>Assembled chart for the new pipeline. Null if legacy.</summary>
            public readonly BattleChart BattleChart;

            /// <summary>Loaded chart for the legacy JSON pipeline. Null if new pipeline.</summary>
            public readonly LoadedChart LegacyChart;

            /// <summary>Whether legacy mode was used.</summary>
            public readonly bool IsLegacy;

            /// <summary>Effective BPM after enemy and elite modifiers.</summary>
            public readonly float EffectiveBPM;

            /// <summary>Audio offset in seconds (from SongBeatMap or 0).</summary>
            public readonly float AudioOffset;

            /// <summary>Chart mode label for logging.</summary>
            public readonly string Mode;

            /// <summary>Whether resolution succeeded.</summary>
            public bool Success => IsLegacy ? LegacyChart != null : BattleChart != null;

            public ChartResult(BattleChart chart, string mode, float bpm, float offset)
            {
                BattleChart = chart;
                LegacyChart = null;
                IsLegacy = false;
                EffectiveBPM = bpm;
                AudioOffset = offset;
                Mode = mode;
            }

            public ChartResult(LoadedChart chart, float bpm, float offset)
            {
                BattleChart = null;
                LegacyChart = chart;
                IsLegacy = true;
                EffectiveBPM = bpm;
                AudioOffset = offset;
                Mode = "legacy";
            }
        }

        // =================================================================
        // PUBLIC API
        // =================================================================

        /// <summary>
        /// Resolve the chart for a battle. Assigns the song to the
        /// Conductor's AudioSource and assembles or loads the chart.
        /// 
        /// Call once during battle setup. Stateless after returning.
        /// </summary>
        /// <param name="enemy">The enemy to fight.</param>
        /// <param name="isElite">Whether this is an elite encounter.</param>
        /// <param name="rng">Seeded random for deterministic chart assembly.</param>
        /// <param name="selectedChart">Legacy chart override (from RunState).</param>
        public ChartResult Resolve(
            EnemyData enemy,
            bool isElite,
            ISeededRandom rng,
            TextAsset selectedChart = null)
        {
            if (enemy == null)
            {
                GameLog.Error("[ChartProvider] No enemy data.");
                return default;
            }

            Conductor conductor = Conductor.Instance;
            AssignSong(enemy, conductor);

            float difficulty = GetEffectiveDifficulty(enemy, isElite);
            float bpmModifier = GetEffectiveBPMModifier(enemy, isElite);

            if (_useLegacyChart)
                return ResolveLegacy(selectedChart, enemy, bpmModifier);

            if (enemy.songBeatMap != null)
                return ResolveFromBeatMap(enemy, rng, difficulty, bpmModifier);

            if (HasAudioClip(conductor))
                return ResolveFromAudioAnalysis(enemy, conductor, rng, difficulty, bpmModifier);

            GameLog.Error("[ChartProvider] No chart source available! " +
                          "Enemy needs a SongBeatMap or a song AudioClip.");
            return default;
        }

        // =================================================================
        // SONG ASSIGNMENT
        // =================================================================

        private void AssignSong(EnemyData enemy, Conductor conductor)
        {
            AudioClip clip = enemy.EffectiveSong;

            if (clip == null)
            {
                GameLog.Warn("[ChartProvider] Enemy has no song. " +
                             "Using whatever is on the Conductor's AudioSource.");
                return;
            }

            AudioSource source = conductor.GetComponent<AudioSource>();
            if (source == null)
            {
                GameLog.Error("[ChartProvider] Conductor has no AudioSource!");
                return;
            }

            source.clip = clip;
            GameLog.Info($"[ChartProvider] Song assigned: {clip.name}");
        }

        // =================================================================
        // LEGACY (JSON)
        // =================================================================

        private ChartResult ResolveLegacy(TextAsset selectedChart, EnemyData enemy, float bpmModifier)
        {
            TextAsset chartAsset = selectedChart ?? _defaultChart;

            if (chartAsset == null)
            {
                GameLog.Error("[ChartProvider] No legacy chart data.");
                return default;
            }

            LoadedChart chart = ChartLoader.Load(chartAsset);

            if (chart == null)
            {
                GameLog.Error("[ChartProvider] Failed to load legacy chart.");
                return default;
            }

            float effectiveBPM = chart.BPM * bpmModifier;
            return new ChartResult(chart, effectiveBPM, chart.Offset);
        }

        // =================================================================
        // BEAT MAP (curated songs)
        // =================================================================

        private ChartResult ResolveFromBeatMap(
            EnemyData enemy, ISeededRandom rng, float difficulty, float bpmModifier)
        {
            SongBeatMap beatMap = enemy.songBeatMap;
            ShapeLibrary library = GetShapeLibrary(enemy);
            float effectiveBPM = beatMap.bpm * bpmModifier;

            var markers = new List<BeatMarker>(beatMap.markers);
            var sections = beatMap.sections != null
                ? new List<SongSection>(beatMap.sections)
                : null;

            float totalBeats = beatMap.TotalBeats > 0f
                ? beatMap.TotalBeats
                : (markers.Count > 0 ? markers[markers.Count - 1].beat + 4f : 0f);

            BattleChart chart = ShapeAssembler.Assemble(
                markers, sections, library, rng,
                difficulty, effectiveBPM, totalBeats);

            if (chart == null)
            {
                GameLog.Error("[ChartProvider] ShapeAssembler returned null (beat map path).");
                return default;
            }

            return new ChartResult(chart, "beat-map", effectiveBPM, beatMap.audioOffsetSeconds);
        }

        // =================================================================
        // AUTO-DETECT (imported songs)
        // =================================================================

        private bool HasAudioClip(Conductor conductor)
        {
            AudioSource source = conductor.GetComponent<AudioSource>();
            return source != null && source.clip != null;
        }

        private ChartResult ResolveFromAudioAnalysis(
            EnemyData enemy, Conductor conductor, ISeededRandom rng,
            float difficulty, float bpmModifier)
        {
            AudioSource source = conductor.GetComponent<AudioSource>();
            AudioClip clip = source.clip;
            ShapeLibrary library = GetShapeLibrary(enemy);

            float sensitivity = Mathf.Lerp(0.3f, 0.8f, difficulty);

            float baseBPM;
            if (enemy.songBeatMap != null)
                baseBPM = enemy.songBeatMap.bpm;
            else if (_defaultChart != null)
                baseBPM = ChartLoader.Load(_defaultChart)?.BPM ?? 135f;
            else
                baseBPM = 135f;

            float effectiveBPM = baseBPM * bpmModifier;

            var analysis = RuntimeBeatAnalyzer.Analyze(clip, effectiveBPM, sensitivity);

            if (!analysis.Success || analysis.Markers.Count == 0)
            {
                GameLog.Error("[ChartProvider] Runtime analysis produced no markers.");
                return default;
            }

            BattleChart chart = ShapeAssembler.Assemble(
                analysis.Markers, analysis.Sections, library, rng,
                difficulty, effectiveBPM, analysis.TotalBeats);

            if (chart == null)
            {
                GameLog.Error("[ChartProvider] ShapeAssembler returned null (auto-detect path).");
                return default;
            }

            return new ChartResult(chart, "auto-detect", effectiveBPM, 0f);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private ShapeLibrary GetShapeLibrary(EnemyData enemy)
        {
            ShapeLibrary library = enemy.shapeLibrary ?? _defaultShapeLibrary;

            if (library == null || library.shapes.Count == 0)
            {
                GameLog.Error("[ChartProvider] No ShapeLibrary available! " +
                              "Assign one on EnemyData or set a default on ChartProvider.");
            }

            return library;
        }

        private float GetEffectiveDifficulty(EnemyData enemy, bool isElite)
        {
            float difficulty = enemy.markerDifficulty;
            if (isElite && _eliteConfig != null)
                difficulty = Mathf.Clamp01(difficulty + _eliteConfig.difficultyBoost * 0.1f);
            return difficulty;
        }

        private float GetEffectiveBPMModifier(EnemyData enemy, bool isElite)
        {
            float bpmModifier = enemy.bpmModifier;
            if (isElite && _eliteConfig != null)
                bpmModifier = _eliteConfig.ScaleBPMModifier(bpmModifier);
            return bpmModifier;
        }
    }
}
