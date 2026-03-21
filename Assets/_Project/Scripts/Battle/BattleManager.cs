using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util.FSM;
using RhythmRogue.Util;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Orchestrates the full battle lifecycle.
    /// 
    /// Uses StateMachine&lt;BattlePhase&gt; from Util to manage phases:
    ///   Intro → Playing → Won or Lost
    /// 
    /// On Awake, reads BattleConfig for which enemy/chart to use.
    /// Falls back to serialized defaults for testing without scene transitions.
    /// 
    /// Responsibilities:
    ///   - Initialize all battle systems with correct data
    ///   - Start/stop the Conductor
    ///   - Monitor win/lose conditions each frame
    ///   - Collect stats on battle end
    ///   - Handle pause
    /// 
    /// Does NOT own the systems — they live on their own GameObjects.
    /// BattleManager just coordinates them.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR — fallback defaults for testing
        // =================================================================

        [Header("Defaults (used if BattleConfig is empty)")]
        [SerializeField] private EnemyData _defaultEnemy;
        [SerializeField] private TextAsset _defaultChart;

        [Header("Intro")]
        [Tooltip("Seconds of countdown before the song starts.")]
        [SerializeField] private float _introDelay = 2f;

        [Header("End")]
        [Tooltip("Seconds to pause on win/lose screen before transitioning.")]
        [SerializeField] private float _endDelay = 2f;

        [Header("System References")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private InputHandler _inputHandler;
        [SerializeField] private NoteMatcher _noteMatcher;
        [SerializeField] private HoldTracker _holdTracker;
        [SerializeField] private JudgmentSystem _judgmentSystem;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private AccuracyTracker _accuracyTracker;
        [SerializeField] private DamagePipeline _damagePipeline;
        [SerializeField] private EnemyHealth _enemyHealth;

        [Header("Enemy Display")]
        [SerializeField] private SpriteRenderer _enemyRenderer;

        [Header("UI")]
        [SerializeField] private BattleUI _battleUI;

        // =================================================================
        // STATE
        // =================================================================

        private StateMachine<BattlePhase> _fsm;
        private Conductor _conductor;
        private PlayerHealth _playerHealth;

        private EnemyData _currentEnemy;
        private LoadedChart _currentChart;
        private float _phaseTimer;
        private bool _battleEnded;

        /// <summary>Results from the last battle. Read by summary screen.</summary>
        public static BattleStats LastBattleStats { get; private set; }

        /// <summary>Current battle phase for debug/UI.</summary>
        public BattlePhase CurrentPhase => _fsm != null && _fsm.IsRunning ? _fsm.CurrentStateKey : BattlePhase.Intro;

        /// <summary>Whether the battle is paused.</summary>
        public bool IsPaused => _conductor != null && _conductor.IsPaused;

        /// <summary>Fired when the battle is fully complete (after result overlay).</summary>
        public event System.Action<bool> OnBattleCompleted;

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Awake()
        {
            _conductor = Conductor.Instance;
            _playerHealth = PlayerHealth.Instance;

            // Read config or fall back to defaults
            _currentEnemy = BattleConfig.Enemy ?? _defaultEnemy;
            TextAsset chartAsset = BattleConfig.ChartAsset ?? _defaultChart;

            if (_currentEnemy == null)
            {
                GameLog.Error("[BattleManager] No enemy data! Assign a default in Inspector.");
                return;
            }

            if (chartAsset == null)
            {
                GameLog.Error("[BattleManager] No chart data! Assign a default in Inspector.");
                return;
            }

            // Load chart
            _currentChart = ChartLoader.Load(chartAsset);

            if (_currentChart == null)
            {
                GameLog.Error("[BattleManager] Failed to load chart.");
                return;
            }

            SetupFSM();
        }

        private void Start()
        {
            InitializeBattle();
            _fsm.Start(BattlePhase.Intro);
        }

        private void OnEnable()
        {
            // Wire receptor visual feedback: input → highway sprite swap
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += OnReceptorPress;
                _inputHandler.OnLaneReleased += OnReceptorRelease;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed -= OnReceptorPress;
                _inputHandler.OnLaneReleased -= OnReceptorRelease;
            }
        }

        private void Update()
        {
            _fsm.Update();
            HandlePauseInput();
        }

        private void OnReceptorPress(int lane) => _highway.SetReceptorPressed(lane, true);
        private void OnReceptorRelease(int lane) => _highway.SetReceptorPressed(lane, false);

        // =================================================================
        // INITIALIZATION
        // =================================================================

        private void InitializeBattle()
        {
            // Enemy health
            _enemyHealth.InitForBattle(_currentEnemy.maxHP);

            // Subscribe to death events
            _enemyHealth.Health.OnDeath += OnEnemyDied;
            _playerHealth.Health.OnDeath += OnPlayerDied;

            // Enemy display
            if (_enemyRenderer != null && _currentEnemy.sprite != null)
                _enemyRenderer.sprite = _currentEnemy.sprite;

            // Load chart into highway
            _highway.LoadChart(_currentChart);

            // Calculate effective BPM
            float effectiveBPM = _currentChart.BPM * _currentEnemy.bpmModifier;

            // Point damage pipeline at this enemy
            _damagePipeline.SetEnemyHealth(_enemyHealth);

            // Reset trackers
            _comboSystem.ResetAll();
            _accuracyTracker.Reset();

            GameLog.Info($"[BattleManager] Battle initialized: {_currentEnemy.enemyName} " +
                      $"({_currentEnemy.maxHP} HP) at {effectiveBPM} BPM");
        }

        // =================================================================
        // FSM SETUP
        // =================================================================

        private void SetupFSM()
        {
            _fsm = new StateMachine<BattlePhase>();

            // --- INTRO: countdown before song starts ---
            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Intro,
                enter: _ =>
                {
                    _phaseTimer = _introDelay;
                    _battleEnded = false;

                    GameLog.Info($"[BattleManager] INTRO — {_currentEnemy.enemyName} appears!");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;

                    if (_phaseTimer <= 0f)
                    {
                        _fsm.TransitionTo(BattlePhase.Playing);
                    }
                }
            ));

            // --- PLAYING: song active, all systems running ---
            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Playing,
                enter: _ =>
                {
                    float effectiveBPM = _currentChart.BPM * _currentEnemy.bpmModifier;
                    _conductor.OnSongFinished += OnSongEnded;
                    _conductor.Play(effectiveBPM, _currentChart.Offset);

                    GameLog.Info("[BattleManager] PLAYING — song started!");
                },
                exit: _ =>
                {
                    _conductor.OnSongFinished -= OnSongEnded;

                    if (_conductor.IsPlaying)
                        _conductor.Stop();

                    _highway.ClearAllNotes();
                    _holdTracker.ClearAll();
                }
            ));

            // --- WON: enemy defeated ---
            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Won,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(true);
                    _battleUI?.ShowResult(true);

                    GameLog.Info("<color=green>[BattleManager] VICTORY!</color>");
                    GameLog.Info($"  HP remaining: {_playerHealth.CurrentHP}/{_playerHealth.MaxHP}");
                    GameLog.Info($"  Max combo: {_comboSystem.MaxCombo}");
                    GameLog.Info($"  Accuracy: {_accuracyTracker.Accuracy:P1}");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;

                    if (_phaseTimer <= 0f)
                    {
                        OnBattleComplete();
                    }
                }
            ));

            // --- LOST: player died or song ended ---
            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Lost,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(false);
                    _battleUI?.ShowResult(false);

                    GameLog.Info("<color=red>[BattleManager] DEFEATED!</color>");
                    GameLog.Info($"  Enemy HP remaining: {_enemyHealth.CurrentHP}/{_enemyHealth.MaxHP}");
                    GameLog.Info($"  Max combo: {_comboSystem.MaxCombo}");
                    GameLog.Info($"  Accuracy: {_accuracyTracker.Accuracy:P1}");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;

                    if (_phaseTimer <= 0f)
                    {
                        OnBattleComplete();
                    }
                }
            ));
        }

        // =================================================================
        // WIN/LOSE CONDITIONS
        // =================================================================

        private void OnEnemyDied()
        {
            if (_battleEnded) return;
            EndBattle(true);
        }

        private void OnPlayerDied()
        {
            if (_battleEnded) return;
            EndBattle(false);
        }

        private void OnSongEnded()
        {
            if (_battleEnded) return;

            if (_enemyHealth.IsAlive)
            {
                GameLog.Info("[BattleManager] Song ended — enemy survived!");
                EndBattle(false);
            }
        }

        private void EndBattle(bool victory)
        {
            _battleEnded = true;
            _fsm.TransitionTo(victory ? BattlePhase.Won : BattlePhase.Lost);
        }

        // =================================================================
        // STATS COLLECTION
        // =================================================================

        private void CollectStats(bool victory)
        {
            LastBattleStats = new BattleStats
            {
                Victory = victory,
                PlayerHPRemaining = _playerHealth.CurrentHP,
                EnemyHPRemaining = _enemyHealth.CurrentHP,
                MaxCombo = _comboSystem.MaxCombo,
                ComboResets = _comboSystem.TotalResets,
                Accuracy = _accuracyTracker.Accuracy,
                TotalNotes = _accuracyTracker.TotalNotes,
                NotesHit = _accuracyTracker.PerfectCount +
                           _accuracyTracker.GoodCount +
                           _accuracyTracker.BadCount,
                EnemyName = _currentEnemy.enemyName,
                WasBoss = _currentEnemy.IsBoss
            };
        }

        // =================================================================
        // BATTLE COMPLETE — transition out
        // =================================================================

        private void OnBattleComplete()
        {
            // Unsubscribe death events
            if (_enemyHealth.Health != null)
                _enemyHealth.Health.OnDeath -= OnEnemyDied;
            _playerHealth.Health.OnDeath -= OnPlayerDied;

            // For prototype: log stats.
            GameLog.Info("[BattleManager] Battle complete — ready for scene transition.");
            GameLog.Info($"  Result: {(LastBattleStats.Victory ? "WIN" : "LOSE")}");
            GameLog.Info($"  Notes hit: {LastBattleStats.NotesHit}/{LastBattleStats.TotalNotes}");
            GameLog.Info($"  Accuracy: {LastBattleStats.Accuracy:P1}");
            GameLog.Info($"  Max combo: {LastBattleStats.MaxCombo}");

            // Notify result handler for scene transition
            OnBattleCompleted?.Invoke(LastBattleStats.Victory);

            // Clear config after handler has read it
            BattleConfig.Clear();
        }

        // =================================================================
        // PAUSE
        // =================================================================

        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_fsm.CurrentStateKey != BattlePhase.Playing)
                    return;

                if (_conductor.IsPaused)
                    ResumeBattle();
                else
                    PauseBattle();
            }
        }

        private void PauseBattle()
        {
            _conductor.Pause();
            GameLog.Info("[BattleManager] PAUSED");
        }

        private void ResumeBattle()
        {
            _conductor.Resume();
            GameLog.Info("[BattleManager] RESUMED");
        }

        // =================================================================
        // CLEANUP
        // =================================================================

        private void OnDestroy()
        {
            if (_enemyHealth != null && _enemyHealth.Health != null)
                _enemyHealth.Health.OnDeath -= OnEnemyDied;

            if (_playerHealth != null && _playerHealth.Health != null)
                _playerHealth.Health.OnDeath -= OnPlayerDied;
        }
    }
}
