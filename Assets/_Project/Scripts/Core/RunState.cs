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
        [System.NonSerialized] public IRunSeed RunSeed;
        [System.NonSerialized] public List<RelicData> ActiveRelics = new();
        [System.NonSerialized] public bool LastBattleWasElite;

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

            GameLog.Info($"[RunState] New run started. Seed: {Seed}");
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

        public void EndRun(bool victory)
        {
            WasVictory = victory;
            IsRunActive = false;
            GameLog.Info($"[RunState] Run ended: {(victory ? "VICTORY" : "DEFEAT")} Battles:{BattlesWon} Score:{TotalScore}");
        }
    }
}
