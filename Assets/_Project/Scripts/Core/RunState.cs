using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Map;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Core
{
    [CreateAssetMenu(fileName = "RunState", menuName = "RhythmRogue/RunState")]
    public class RunState : ScriptableObject
    {
        [Header("Run Config")]
        // Seed is per-run state, not config. Marked NonSerialized so the editor
        // doesn't carry the seed from one play session to the next, which would
        // make the first map of every session look identical.
        [System.NonSerialized] public string Seed;
        public int StartingHP = 100;

        [Tooltip("Economy tuning (currency name, awards, prices). If unassigned, loads Resources/Configs/DefaultEconomy.")]
        public EconomyConfig EconomyConfig;

        [Header("Runtime")]
        [System.NonSerialized] public bool IsRunActive;
        [System.NonSerialized] public int CurrentNodeId = -1;
        [System.NonSerialized] public int BattlesWon;
        [System.NonSerialized] public int TotalScore;
        [System.NonSerialized] public float BestAccuracy;
        [System.NonSerialized] public int MaxCombo;
        [System.NonSerialized] public bool WasVictory;

        [System.NonSerialized] public MapData MapData;
        [System.NonSerialized] public MapNode SelectedNode;
        [System.NonSerialized] public TextAsset SelectedChart;

        /// <summary>The Area the current map was generated from. Set by MapScreen. Drives the
        /// difficulty band and HP for battles in this area (see DifficultyCurve).</summary>
        [System.NonSerialized] public Area CurrentArea;

        /// <summary>Optional area chosen at run start (e.g. the menu launching the onboarding
        /// area). When set, MapScreen builds the map from this instead of its own serialized
        /// default. Null for a normal run. Set by the launcher before StartNewRun; cleared by
        /// EndRun so the next run (e.g. New Run from the summary) returns to the default area.</summary>
        [System.NonSerialized] public Area SelectedArea;

        /// <summary>Run-level forgiveness tier (chosen at run start). Defaults to Normal.</summary>
        [System.NonSerialized] public DifficultyTier Tier = DifficultyTier.Normal;
        [System.NonSerialized] public IRunSeed RunSeed;
        [System.NonSerialized] public List<RelicData> ActiveRelics = new();
        [System.NonSerialized] public bool LastBattleWasElite;

        // Run currency (working name 'Beats', configurable via EconomyConfig.CurrencyName).
        // NonSerialized so it doesn't persist across editor play sessions; reset each run.
        // Other systems should use AddCurrency / TrySpendCurrency rather than mutating directly.
        [System.NonSerialized] public int Currency;

        /// <summary>Fired whenever Currency changes (delta can be negative on spend). For HUD updates.</summary>
        public event System.Action<int, int> OnCurrencyChanged; // (newTotal, delta)

        /// <summary>
        /// Resolved economy config (Inspector reference if set, else Resources fallback).
        /// Exposed so battle/reward/shop systems share one source of economy truth.
        /// </summary>
        public EconomyConfig Economy => Data.EconomyConfig.Resolve(EconomyConfig);

        public void StartNewRun(string seed = null)
        {
            var encoder = new SeedEncoder();

            if (string.IsNullOrWhiteSpace(seed))
                Seed = encoder.GenerateCode();
            else
            {
                Seed = encoder.Normalize(seed);
                if (!encoder.IsValid(Seed)) { GameLog.Warn($"[RunState] Invalid seed '{seed}', generating random."); Seed = encoder.GenerateCode(); }
            }

            RunSeed = new RhythmRogue.Util.Random.RunSeed(Seed, encoder);
            IsRunActive = true;
            CurrentNodeId = -1;
            BattlesWon = 0; TotalScore = 0; BestAccuracy = 0f; MaxCombo = 0; WasVictory = false;
            MapData = null; SelectedNode = null; SelectedChart = null;
            ActiveRelics = new List<RelicData>();
            LastBattleWasElite = false;
            Currency = Economy != null ? Economy.StartingCurrency : 0;

            GameLog.Info($"[RunState] New run started. Seed: {Seed} Currency: {Currency}");
        }

        public void RecordBattleResult(bool won, int score, float accuracy, int maxCombo)
        {
            if (won) BattlesWon++;
            TotalScore += score;
            if (accuracy > BestAccuracy) BestAccuracy = accuracy;
            if (maxCombo > MaxCombo) MaxCombo = maxCombo;
        }

        public void CompleteSelectedNode()
        {
            if (MapData == null || SelectedNode == null) return;
            MapData.CompleteNode(SelectedNode);
            CurrentNodeId = SelectedNode.Id;
            SelectedNode = null;
            SelectedChart = null;
        }

        /// <summary>
        /// Normalized depth of the selected node within the current map: 0 at the opening layer,
        /// 1 at the boss layer. The difficulty curve uses this to ramp a fight by how deep it
        /// sits. Returns 0 if there is no map/selection (e.g. a direct battle-scene launch).
        /// </summary>
        public float SelectedNodeDepthT()
        {
            if (SelectedNode == null || MapData == null) return 0f;
            int layers = MapData.LayerCount;
            if (layers <= 1) return 0f;
            return Mathf.Clamp01(SelectedNode.Layer / (float)(layers - 1));
        }

        public void EndRun(bool victory)
        {
            WasVictory = victory;
            IsRunActive = false;
            // Drop any area override now the run is over, so the next StartNewRun (New Run / Retry
            // from the summary) uses the default area instead of silently relaunching the onboarding.
            // Cleared here, at run end, rather than in StartNewRun: MapScreen re-runs StartNewRun on
            // first entry, and clearing there would wipe the override mid-run (every later map
            // re-entry would fall back to the default area and lose practice mode).
            SelectedArea = null;
            GameLog.Info($"[RunState] Run ended: {(victory ? "VICTORY" : "DEFEAT")} Battles:{BattlesWon} Score:{TotalScore} Currency:{Currency}");
        }

        /// <summary>
        /// Acquire a relic (reward pick or shop purchase): add it to the active relics and apply
        /// any one-time on-pickup effects (e.g. Max HP) through the given context. Centralizes
        /// pickup so reward and shop share one path. Null relics are ignored.
        /// </summary>
        public void AcquireRelic(RelicData relic, IRelicAcquireContext acquireContext)
        {
            if (relic == null) return;
            ActiveRelics.Add(relic);

            var effects = relic.Effects;
            if (effects != null && acquireContext != null)
            {
                for (int i = 0; i < effects.Count; i++)
                    effects[i]?.OnAcquired(acquireContext);
            }

            GameLog.Info($"[RunState] Acquired relic: {relic.relicName}");
        }

        /// <summary>Add currency (e.g. battle reward). Negative amounts are ignored; use TrySpendCurrency to spend.</summary>
        public void AddCurrency(int amount)
        {
            if (amount <= 0) return;
            Currency += amount;
            OnCurrencyChanged?.Invoke(Currency, amount);
            GameLog.Info($"[RunState] +{amount} currency (total {Currency}).");
        }

        /// <summary>
        /// Attempt to spend currency. Returns true and deducts if the player can afford it;
        /// returns false and changes nothing otherwise. Used by the shop (when built).
        /// </summary>
        public bool TrySpendCurrency(int amount)
        {
            if (amount <= 0) return true;
            if (Currency < amount) return false;
            Currency -= amount;
            OnCurrencyChanged?.Invoke(Currency, -amount);
            GameLog.Info($"[RunState] -{amount} currency (total {Currency}).");
            return true;
        }
    }
}
