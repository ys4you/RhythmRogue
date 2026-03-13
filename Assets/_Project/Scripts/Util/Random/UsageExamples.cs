// ============================================================================
// USAGE EXAMPLES — RhythmRogue Seeded Random System
// These are NOT part of the utility. They show how your game systems
// would use the seeded random for deterministic procedural generation.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Examples
{
    // -----------------------------------------------------------------------
    // 1. RUN MANAGER — creates and holds the RunSeed for the current run
    // -----------------------------------------------------------------------

    /// <summary>
    /// Manages run lifecycle. Creates the seed at run start, provides it
    /// to all procedural systems.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        private ISeedEncoder _encoder;
        private IRunSeed _currentSeed;

        /// <summary>
        /// The current run's seed. Null if no run is active.
        /// </summary>
        public IRunSeed CurrentSeed => _currentSeed;

        private void Awake()
        {
            _encoder = new SeedEncoder();
        }

        /// <summary>
        /// Start a new run with a random seed.
        /// </summary>
        public void StartNewRun()
        {
            _currentSeed = RunSeed.CreateRandom(_encoder);
            Debug.Log($"Starting run with seed: {_currentSeed.Code}");
        }

        /// <summary>
        /// Start a run from a player-entered seed code.
        /// </summary>
        public void StartSeededRun(string code)
        {
            if (!_encoder.IsValid(_encoder.Normalize(code)))
            {
                Debug.LogError($"Invalid seed code: {code}");
                return;
            }

            _currentSeed = new RunSeed(code, _encoder);
            Debug.Log($"Starting seeded run: {_currentSeed.Code}");
        }
    }

    // -----------------------------------------------------------------------
    // 2. MAP GENERATOR — uses the Map domain for deterministic map layout
    // -----------------------------------------------------------------------

    public enum NodeType { Enemy, Elite, Event, Shop, Rest, Boss }

    public class MapGenerator
    {
        /// <summary>
        /// Generate a simple branching map.
        /// Same seed → same map, guaranteed.
        /// </summary>
        public List<NodeType> GenerateArea(IRunSeed seed, int areaIndex)
        {
            // Fork per area so area 1's generation doesn't affect area 2
            ISeededRandom rng = seed.GetRandom(RandomDomain.Map, areaIndex);
            ISeededRandom nodeRng = seed.GetRandom(RandomDomain.NodeTypes, areaIndex);

            int nodeCount = rng.Range(4, 7); // 4-6 nodes per area
            var nodes = new List<NodeType>(nodeCount);

            for (int i = 0; i < nodeCount - 1; i++)
            {
                // Weighted node type selection
                float roll = nodeRng.NextFloat();

                if (roll < 0.45f) nodes.Add(NodeType.Enemy);
                else if (roll < 0.60f) nodes.Add(NodeType.Elite);
                else if (roll < 0.75f) nodes.Add(NodeType.Event);
                else if (roll < 0.85f) nodes.Add(NodeType.Shop);
                else nodes.Add(NodeType.Rest);
            }

            // Last node is always the boss
            nodes.Add(NodeType.Boss);

            return nodes;
        }
    }

    // -----------------------------------------------------------------------
    // 3. CHART GENERATOR — uses the Charts domain for pattern selection
    // -----------------------------------------------------------------------

    public class PatternData
    {
        public string Name;
        public int Difficulty;
    }

    public class ChartGenerator
    {
        private readonly List<PatternData> _patternLibrary;

        public ChartGenerator(List<PatternData> library)
        {
            _patternLibrary = library;
        }

        /// <summary>
        /// Select patterns for an encounter. Fork per encounter so
        /// skipping an optional battle doesn't shift subsequent charts.
        /// </summary>
        public List<PatternData> SelectPatterns(
            IRunSeed seed, int areaIndex, int encounterIndex, int count)
        {
            // Double fork: Charts domain → area → encounter
            ISeededRandom areaRng = seed.GetRandom(RandomDomain.Charts, areaIndex);
            ISeededRandom encounterRng = areaRng.Fork(encounterIndex);

            var selected = new List<PatternData>(count);

            for (int i = 0; i < count; i++)
            {
                PatternData pattern = encounterRng.Pick(_patternLibrary);
                selected.Add(pattern);
            }

            return selected;
        }
    }

    // -----------------------------------------------------------------------
    // 4. SEED UI — displaying and entering seeds
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows how the seed code is displayed and entered in UI.
    /// The seed code appears on the Map Screen and Run Summary Screen.
    /// </summary>
    public class SeedUI : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _seedDisplay;
        [SerializeField] private TMPro.TMP_InputField _seedInput;

        private ISeedEncoder _encoder;

        private void Start()
        {
            _encoder = new SeedEncoder();

            // Validate input as the player types
            _seedInput.onValueChanged.AddListener(OnSeedInputChanged);
        }

        public void DisplaySeed(IRunSeed seed)
        {
            _seedDisplay.text = seed.Code;
        }

        private void OnSeedInputChanged(string input)
        {
            string normalized = _encoder.Normalize(input);
            bool valid = _encoder.IsValid(normalized);

            // Visual feedback: green if valid, red if not
            _seedInput.textComponent.color = valid ? Color.green : Color.red;
        }
    }
}
