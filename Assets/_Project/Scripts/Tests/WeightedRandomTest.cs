using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;
using RhythmRogue.Util;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the Weighted Random Selector.
    /// 
    /// HOW TO USE:
    /// 1. Create an empty scene
    /// 2. Add an empty GameObject, attach this script
    /// 3. Hit Play and use the keyboard controls
    /// 
    /// CONTROLS:
    ///   [Space] — Open a loot chest (pick 1 item from weighted table)
    ///   [U]     — Open a unique chest (pick 3 unique items)
    ///   [P]     — Print probability table
    ///   [S]     — Reset with same seed (proves determinism)
    ///   [R]     — Reset with random seed (different results)
    ///   [D]     — Toggle difficulty (changes weights live)
    /// 
    /// Watch the Console — you'll see how weights affect distribution
    /// and how the same seed always produces the same sequence.
    /// </summary>
    public class WeightedRandomTest : MonoBehaviour
    {
        private ISeedEncoder _encoder;
        private ISeededRandom _rng;
        private IWeightedSelector<string> _lootTable;
        private string _currentSeedCode;
        private int _chestCount;
        private int _difficulty;
        private Dictionary<string, int> _pickHistory;

        private void Start()
        {
            _encoder = new SeedEncoder();
            _pickHistory = new Dictionary<string, int>();
            _difficulty = 1;

            ResetWithNewSeed();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))  OpenChest();
            if (Input.GetKeyDown(KeyCode.U))      OpenUniqueChest();
            if (Input.GetKeyDown(KeyCode.P))      PrintProbabilities();
            if (Input.GetKeyDown(KeyCode.S))      ResetWithSameSeed();
            if (Input.GetKeyDown(KeyCode.R))      ResetWithNewSeed();
            if (Input.GetKeyDown(KeyCode.D))      ToggleDifficulty();
        }

        // ===================================================================
        // ACTIONS
        // ===================================================================

        private void OpenChest()
        {
            _chestCount++;
            string item = _lootTable.Pick(_rng);

            // Track history
            if (!_pickHistory.ContainsKey(item))
                _pickHistory[item] = 0;
            _pickHistory[item]++;

            string color = GetRarityColor(item);
            GameLog.Info($"<color={color}>  Chest #{_chestCount}: {item}</color>");
        }

        private void OpenUniqueChest()
        {
            _chestCount++;
            List<string> items = _lootTable.PickUnique(_rng, 3);

            GameLog.Info($"<color=white>  Chest #{_chestCount} (UNIQUE x3):</color>");
            foreach (string item in items)
            {
                string color = GetRarityColor(item);
                GameLog.Info($"<color={color}>    - {item}</color>");

                if (!_pickHistory.ContainsKey(item))
                    _pickHistory[item] = 0;
                _pickHistory[item]++;
            }
        }

        private void PrintProbabilities()
        {
            GameLog.Info("<color=white>  ── Probability Table ──</color>");
            for (int i = 0; i < _lootTable.Count; i++)
            {
                var entry = _lootTable.Entries[i];
                float chance = _lootTable.GetProbability(i) * 100f;
                string bar = new string('█', Mathf.RoundToInt(chance / 2f));
                string color = GetRarityColor(entry.Item);

                int actual = _pickHistory.ContainsKey(entry.Item) ? _pickHistory[entry.Item] : 0;
                string stats = _chestCount > 0
                    ? $" (actual: {actual}/{_chestCount})"
                    : "";

                GameLog.Info($"<color={color}>  {entry.Item,-16} {chance,5:F1}% {bar}{stats}</color>");
            }
        }

        private void ResetWithSameSeed()
        {
            GameLog.Info("<color=yellow>========================================</color>");
            GameLog.Info($"<color=yellow>  RESET — Same seed: {_currentSeedCode}</color>");
            GameLog.Info("<color=yellow>  Sequence will be IDENTICAL</color>");
            GameLog.Info("<color=yellow>========================================</color>");

            int numericSeed = _encoder.Encode(_currentSeedCode);
            _rng = new SeededRandom(numericSeed);
            _chestCount = 0;
            _pickHistory.Clear();
            PrintControls();
        }

        private void ResetWithNewSeed()
        {
            _currentSeedCode = _encoder.GenerateCode();

            GameLog.Info("<color=cyan>========================================</color>");
            GameLog.Info($"<color=cyan>  NEW SEED: {_currentSeedCode}</color>");
            GameLog.Info($"<color=cyan>  Difficulty: {_difficulty}</color>");
            GameLog.Info("<color=cyan>========================================</color>");

            int numericSeed = _encoder.Encode(_currentSeedCode);
            _rng = new SeededRandom(numericSeed);
            _chestCount = 0;
            _pickHistory.Clear();

            RebuildTable();
            PrintControls();
        }

        private void ToggleDifficulty()
        {
            _difficulty = _difficulty >= 3 ? 1 : _difficulty + 1;

            GameLog.Info($"<color=yellow>  Difficulty changed to {_difficulty} — weights updated!</color>");
            RebuildTable();
            PrintProbabilities();
        }

        // ===================================================================
        // TABLE BUILDING — weights change with difficulty
        // ===================================================================

        private void RebuildTable()
        {
            // Difficulty shifts weights: higher difficulty = rarer items
            // appear more often, common items appear less
            _lootTable = WeightedTable<string>.Build()
                .Add("Copper Coin",      Mathf.Max(5f, 40f - _difficulty * 10f))
                .Add("Silver Coin",      25f)
                .Add("Health Potion",    20f)
                .Add("Bronze Relic",     10f + _difficulty * 2f)
                .AddIf(_difficulty >= 2, "Silver Relic",  5f + _difficulty * 3f)
                .AddIf(_difficulty >= 2, "Gold Relic",    2f + _difficulty * 2f)
                .AddIf(_difficulty >= 3, "Diamond Relic", _difficulty * 2f)
                .Done();
        }

        // ===================================================================
        // HELPERS
        // ===================================================================

        private void PrintControls()
        {
            GameLog.Info("<color=white>  [Space] Open Chest  [U] Unique Chest (x3)</color>");
            GameLog.Info("<color=white>  [P] Probabilities  [S] Same Seed  [R] New Seed  [D] Difficulty</color>");
        }

        private string GetRarityColor(string item)
        {
            if (item.Contains("Diamond")) return "#FF55FF";
            if (item.Contains("Gold"))    return "#FFD700";
            if (item.Contains("Silver"))  return "#C0C0C0";
            if (item.Contains("Bronze"))  return "#CD7F32";
            if (item.Contains("Health"))  return "green";
            return "white";
        }
    }
}
