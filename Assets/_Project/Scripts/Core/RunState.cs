using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Map;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Core
{
    /// <summary>
    /// Persistent run data that survives scene transitions.
    /// 
    /// ScriptableObject approach: lives as an asset, referenced by
    /// any scene that needs run data. No DontDestroyOnLoad needed
    /// for the data itself — the asset persists in memory as long
    /// as anything references it.
    /// 
    /// Create via: Assets → Create → RhythmRogue → RunState
    /// </summary>
    [CreateAssetMenu(fileName = "RunState", menuName = "RhythmRogue/RunState")]
    public class RunState : ScriptableObject
    {
        // =================================================================
        // RUN DATA — set at run start, read/written during run
        // =================================================================

        [Header("Run Config")]
        public string Seed;
        public int StartingHP = 100;

        [Header("Runtime — do not edit in inspector")]
        [HideInInspector] public bool IsRunActive;
        [HideInInspector] public int CurrentNodeId = -1;

        // Stats
        [HideInInspector] public int BattlesWon;
        [HideInInspector] public int TotalScore;
        [HideInInspector] public float BestAccuracy;
        [HideInInspector] public int MaxCombo;
        [HideInInspector] public bool WasVictory;

        // =================================================================
        // NON-SERIALIZED — runtime only, lost on domain reload
        // =================================================================

        /// <summary>The generated map for this run.</summary>
        [System.NonSerialized] public MapData MapData;

        /// <summary>The node the player selected (set before battle scene load).</summary>
        [System.NonSerialized] public MapNode SelectedNode;

        /// <summary>
        /// The chart asset to play in the next battle (set before battle scene load).
        /// </summary>
        [System.NonSerialized] public TextAsset SelectedChart;

        /// <summary>
        /// The seeded random context for this run. Created in StartNewRun
        /// from the seed code. All procedural systems fork from this.
        /// </summary>
        [System.NonSerialized] public IRunSeed RunSeed;

        /// <summary>Relics collected during this run.</summary>
        [System.NonSerialized] public List<RelicData> ActiveRelics = new();

        /// <summary>
        /// Whether the last completed battle was an elite encounter.
        /// Read by RewardPickScreen to offer better rewards.
        /// </summary>
        [System.NonSerialized] public bool LastBattleWasElite;

        // =================================================================
        // METHODS
        // =================================================================

        public void StartNewRun(string seed = null)
        {
            var encoder = new SeedEncoder();

            if (string.IsNullOrWhiteSpace(seed))
            {
                Seed = encoder.GenerateCode();
            }
            else
            {
                Seed = encoder.Normalize(seed);
                if (!encoder.IsValid(Seed))
                {
                    GameLog.Warn($"[RunState] Invalid seed '{seed}', generating random.");
                    Seed = encoder.GenerateCode();
                }
            }

            RunSeed = new RhythmRogue.Util.Random.RunSeed(Seed, encoder);

            IsRunActive = true;
            CurrentNodeId = -1;
            BattlesWon = 0;
            TotalScore = 0;
            BestAccuracy = 0f;
            MaxCombo = 0;
            WasVictory = false;

            MapData = null;
            SelectedNode = null;
            SelectedChart = null;
            ActiveRelics = new List<RelicData>();
            LastBattleWasElite = false;

            GameLog.Info($"[RunState] New run started. Seed: {Seed}");
        }

        public void RecordBattleResult(bool won, int score, float accuracy, int maxCombo)
        {
            if (won) BattlesWon++;
            TotalScore += score;
            if (accuracy > BestAccuracy) BestAccuracy = accuracy;
            if (maxCombo > MaxCombo) MaxCombo = maxCombo;

            GameLog.Info($"[RunState] Battle recorded: {(won ? "WIN" : "LOSS")} " +
                      $"Score+{score} Acc:{accuracy:P0} Combo:{maxCombo}");
        }

        public void CompleteSelectedNode()
        {
            if (MapData == null || SelectedNode == null)
            {
                GameLog.Warn("[RunState] No map or selected node to complete.");
                return;
            }

            MapData.CompleteNode(SelectedNode);
            CurrentNodeId = SelectedNode.Id;
            SelectedNode = null;
            SelectedChart = null;
        }

        public void EndRun(bool victory)
        {
            WasVictory = victory;
            IsRunActive = false;
            GameLog.Info($"[RunState] Run ended: {(victory ? "VICTORY" : "DEFEAT")} " +
                      $"Battles:{BattlesWon} Score:{TotalScore}");
        }
    }
}