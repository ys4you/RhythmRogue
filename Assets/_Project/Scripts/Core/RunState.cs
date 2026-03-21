using UnityEngine;
using RhythmRogue.Map;
using RhythmRogue.Util;

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

        // =================================================================
        // METHODS
        // =================================================================

        /// <summary>
        /// Reset all run data for a new run.
        /// </summary>
        public void StartNewRun(string seed = null)
        {
            Seed = string.IsNullOrWhiteSpace(seed)
                ? System.DateTime.Now.Ticks.ToString().Substring(8)
                : seed;

            IsRunActive = true;
            CurrentNodeId = -1;
            BattlesWon = 0;
            TotalScore = 0;
            BestAccuracy = 0f;
            MaxCombo = 0;
            WasVictory = false;

            MapData = null;
            SelectedNode = null;

            GameLog.Info($"[RunState] New run started. Seed: {Seed}");
        }

        /// <summary>
        /// Record battle results. Called after a battle ends.
        /// </summary>
        public void RecordBattleResult(bool won, int score, float accuracy, int maxCombo)
        {
            if (won)
                BattlesWon++;

            TotalScore += score;

            if (accuracy > BestAccuracy)
                BestAccuracy = accuracy;

            if (maxCombo > MaxCombo)
                MaxCombo = maxCombo;

            GameLog.Info($"[RunState] Battle recorded: {(won ? "WIN" : "LOSS")} " +
                      $"Score+{score} Acc:{accuracy:P0} Combo:{maxCombo}");
        }

        /// <summary>
        /// Complete the currently selected node on the map.
        /// </summary>
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
        }

        /// <summary>
        /// Mark the run as ended (victory or defeat).
        /// </summary>
        public void EndRun(bool victory)
        {
            WasVictory = victory;
            IsRunActive = false;

            GameLog.Info($"[RunState] Run ended: {(victory ? "VICTORY" : "DEFEAT")} " +
                      $"Battles:{BattlesWon} Score:{TotalScore}");
        }
    }
}
