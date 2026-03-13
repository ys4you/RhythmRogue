// ============================================================================
// USAGE EXAMPLES — Weighted Random Selector
// 
// These are NOT part of the utility. They show how a CONSUMER
// builds weighted tables and picks from them using ISeededRandom.
// 
// The selector knows nothing about games, patterns, or rewards.
// It only knows T and float weights — whatever YOU give it.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace MyGame.Examples
{
    // -----------------------------------------------------------------------
    // 1. CHART GENERATOR — picks patterns from a difficulty-weighted pool
    // -----------------------------------------------------------------------

    public class ChartGeneratorExample : MonoBehaviour
    {
        /// <summary>
        /// Build a pattern pool where harder patterns appear less often
        /// at lower difficulties, more often at higher difficulties.
        /// </summary>
        public List<string> SelectPatterns(ISeededRandom rng, int difficulty, int count)
        {
            // Weights shift based on difficulty — easy patterns become
            // less common as difficulty increases
            var table = WeightedTable<string>.Build()
                .Add("quarter_basic",    Mathf.Max(1f, 10f - difficulty * 2f))
                .Add("eighth_stream",    5f + difficulty)
                .Add("eighth_jumps",     3f + difficulty)
                .AddIf(difficulty >= 3,  "sixteenth_burst",  difficulty * 2f)
                .AddIf(difficulty >= 5,  "hold_crossover",   difficulty * 1.5f)
                .AddIf(difficulty >= 7,  "double_stream",    difficulty)
                .Done();

            return table.Pick(rng, count);
        }
    }

    // -----------------------------------------------------------------------
    // 2. REWARD SYSTEM — unique picks for post-battle reward selection
    // -----------------------------------------------------------------------

    public class RewardSystemExample
    {
        public enum RewardType { Relic, ChartModifier, StatUpgrade, CurrencyBonus }

        /// <summary>
        /// Generate 3 unique reward options for the player to choose from.
        /// Uses PickUnique so the player never sees duplicates.
        /// </summary>
        public List<RewardType> GenerateRewardOptions(ISeededRandom rng, int areaIndex)
        {
            var table = WeightedTable<RewardType>.Build()
                .Add(RewardType.Relic,          20f)
                .Add(RewardType.ChartModifier,  30f)
                .Add(RewardType.StatUpgrade,    35f)
                .Add(RewardType.CurrencyBonus,  15f)
                .Done();

            return table.PickUnique(rng, 3);
        }
    }

    // -----------------------------------------------------------------------
    // 3. SHOP INVENTORY — built from parallel arrays (data-driven)
    // -----------------------------------------------------------------------

    public class ShopExample
    {
        /// <summary>
        /// Generate shop inventory using data from ScriptableObjects.
        /// The From(items, weights) overload handles parallel arrays
        /// cleanly — no manual entry construction needed.
        /// </summary>
        public List<string> GenerateShopItems(
            ISeededRandom rng,
            string[] allItems,
            float[] allWeights,
            int shopSize)
        {
            var table = WeightedTable<string>.From(allItems, allWeights);
            return table.PickUnique(rng, shopSize);
        }
    }

    // -----------------------------------------------------------------------
    // 4. NODE TYPE GENERATION — conditional weights based on map position
    // -----------------------------------------------------------------------

    public class MapGeneratorExample
    {
        public enum NodeType { Enemy, Elite, Event, Shop, Rest }

        /// <summary>
        /// Node type weights change based on position in the area.
        /// Early nodes favor enemies, later nodes add more shops/rest.
        /// AddIf() conditionally includes entries without branching.
        /// </summary>
        public NodeType PickNodeType(ISeededRandom rng, int nodeIndex, int totalNodes)
        {
            bool isLateNode = nodeIndex > totalNodes / 2;
            bool isSecondHalf = nodeIndex > totalNodes * 0.6f;

            var table = WeightedTable<NodeType>.Build()
                .Add(NodeType.Enemy,   40f)
                .Add(NodeType.Elite,   15f)
                .Add(NodeType.Event,   20f)
                .AddIf(isLateNode,     NodeType.Shop, 15f)
                .AddIf(isSecondHalf,   NodeType.Rest, 10f)
                .Done();

            return table.Pick(rng);
        }
    }

    // -----------------------------------------------------------------------
    // 5. ENEMY SELECTION — showing probability readout for debug/UI
    // -----------------------------------------------------------------------

    public class EnemySpawnerExample
    {
        /// <summary>
        /// Shows how to inspect a table's probabilities for debugging
        /// or UI display (e.g. showing encounter rates on the map).
        /// </summary>
        public void DebugEnemyTable()
        {
            var table = WeightedTable<string>.Build()
                .Add("Slow Enemy",   30f)
                .Add("Speed Enemy",  25f)
                .Add("Trick Enemy",  25f)
                .Add("Chaos Enemy",  20f)
                .Done();

            // Print probabilities
            for (int i = 0; i < table.Count; i++)
            {
                float chance = table.GetProbability(i) * 100f;
                Debug.Log($"  {table.Entries[i].Item}: {chance:F1}%");
            }
        }
    }

    // -----------------------------------------------------------------------
    // 6. FULL INTEGRATION — SeededRandom + RunSeed + WeightedSelector
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows the full pipeline: RunSeed provides the domain random,
    /// WeightedSelector uses it for deterministic picks.
    /// Same seed = same shop every time.
    /// </summary>
    public class FullIntegrationExample : MonoBehaviour
    {
        private void Start()
        {
            // Create a seeded run
            var encoder = new SeedEncoder();
            var seed = new RunSeed("BEAT-7X3K", encoder);

            // Get the shop domain random
            ISeededRandom shopRng = seed.GetRandom(RandomDomain.Shop);

            // Build a shop table
            var shopTable = WeightedTable<string>.Build()
                .Add("HP Potion",        30f)
                .Add("Perfect Lens",     20f)  // Widens Perfect window
                .Add("Tempo Charm",      15f)  // BPM reducer
                .Add("Combo Crown",      15f)  // Higher combo cap
                .Add("Glass Shard",      10f)  // Damage up, HP down
                .Add("Beat Magnet",      10f)  // Currency bonus
                .Done();

            // Generate 4 unique shop items — deterministic with the seed
            List<string> shopItems = shopTable.PickUnique(shopRng, 4);

            Debug.Log($"Shop for seed {seed.Code}:");
            foreach (string item in shopItems)
            {
                Debug.Log($"  - {item}");
            }

            // Running this again with "BEAT-7X3K" will ALWAYS produce
            // the same 4 items in the same order.
        }
    }
}
