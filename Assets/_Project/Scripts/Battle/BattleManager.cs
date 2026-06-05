using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Core.Audio;
using RhythmRogue.Data;
using RhythmRogue.Map;
using RhythmRogue.Util.FSM;
using RhythmRogue.Util;
using RhythmRogue.Util.Random;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Orchestrates battle lifecycle: Intro -> Playing -> Won/Lost.
    /// Chart resolution delegated to ChartProvider. Pause to PauseMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class BattleManager : MonoBehaviour
    {
        [Header("Run State")]
        [SerializeField] private RunState _runState;

        [Header("Chart")]
        [SerializeField] private ChartProvider _chartProvider;
        [SerializeField] private EnemyData _defaultEnemy;

        [Header("Highways")]
        [SerializeField] private NoteHighway _highway;
        [SerializeField] private EnemyHighway _enemyHighway;

        [Header("Elite Scaling")]
        [SerializeField] private EliteConfig _eliteConfig;

        [Header("Relic Effects")]
        [SerializeField] private RelicEffectHandler _relicEffectHandler;

        [Header("Timing")]
        [Tooltip("Flat pause after the enemy appears before the song starts. Kept short so the " +
                 "player isn't waiting through dead air every fight; the song's own intro " +
                 "section provides the 'settle in' beat before player notes arrive.")]
        [SerializeField] private float _introDelay = 1f;
        [SerializeField] private float _endDelay = 2f;

        [Header("Systems")]
        [SerializeField] private InputHandler _inputHandler;
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
        [SerializeField] private PauseMenu _pauseMenu;

        private StateMachine<BattlePhase> _fsm;
        private Conductor _conductor;
        private PlayerHealth _playerHealth;
        private EnemyData _currentEnemy;
        private ChartProvider.ChartResult _chart;
        private float _phaseTimer;
        private bool _battleEnded;
        private bool _isElite;
        private bool _isBoss;

        public static BattleStats LastBattleStats { get; private set; }
        public BattlePhase CurrentPhase => _fsm != null && _fsm.IsRunning ? _fsm.CurrentStateKey : BattlePhase.Intro;
        public bool IsPaused => _conductor != null && _conductor.IsPaused;
        public EnemyData CurrentEnemy => _currentEnemy;
        public bool IsElite => _isElite;
        public bool IsBoss => _isBoss;
        public event System.Action<bool> OnBattleCompleted;

        private void Awake()
        {
            _conductor = Conductor.Instance;
            _playerHealth = PlayerHealth.Instance;

            // Fade out the map ambient before the conductor's song takes over. The fade
            // begins immediately so by the time the intro delay elapses, the room is quiet
            // and the chart audio enters cleanly without a music clash.
            MusicManager.Instance.Stop();

            _currentEnemy = (_runState?.SelectedNode?.EnemyData) ?? _defaultEnemy;
            _isElite = _runState?.SelectedNode?.Type == NodeType.Elite;
            // Boss-ness comes from the node type (authoritative), not the enemy's HP. The
            // EnemyData.IsBoss (maxHP >= 250) heuristic is only a defensive fallback, so a
            // boss whose HP is tuned below 250 is still labelled correctly.
            _isBoss = _runState?.SelectedNode?.Type == NodeType.Boss
                      || (_currentEnemy != null && _currentEnemy.IsBoss);

            if (_currentEnemy == null)
            {
                GameLog.Error("[BattleManager] No enemy data! Assign a default in Inspector.");
                return;
            }

            ISeededRandom rng = GetChartRng();
            _chart = _chartProvider.Resolve(_currentEnemy, _isElite, rng, _runState?.SelectedChart);

            if (!_chart.Success)
                GameLog.Error("[BattleManager] Chart resolution failed.");

            SetupFSM();
        }

        private void Start()
        {
            InitializeBattle();
            _fsm.Start(BattlePhase.Intro);
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed += OnReceptorPress;
                _inputHandler.OnLaneReleased += OnReceptorRelease;
            }
            if (_pauseMenu != null)
            {
                _pauseMenu.OnResumeRequested += ResumeBattle;
                _pauseMenu.OnQuitRequested += QuitToMenu;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnLanePressed -= OnReceptorPress;
                _inputHandler.OnLaneReleased -= OnReceptorRelease;
            }
            if (_pauseMenu != null)
            {
                _pauseMenu.OnResumeRequested -= ResumeBattle;
                _pauseMenu.OnQuitRequested -= QuitToMenu;
            }
        }

        private void Update()
        {
            _fsm.Update();
            HandlePauseInput();
        }

        private void OnReceptorPress(int lane) => _highway.SetReceptorPressed(lane, true);
        private void OnReceptorRelease(int lane) => _highway.SetReceptorPressed(lane, false);

        private void InitializeBattle()
        {
            // Flat authored HP per enemy (EnemyData.maxHP), like the player's fixed pool. Elite
            // encounters scale it up. The fight lasts a fixed number of notes-to-kill, so each hit
            // is a meaningful chunk of a small bar and reaching 0 reads as dead immediately.
            int enemyHP = Mathf.Max(1, _currentEnemy.maxHP);
            if (_isElite && _eliteConfig != null)
                enemyHP = _eliteConfig.ScaleHP(enemyHP);

            _enemyHealth.InitForBattle(enemyHP);
            _enemyHealth.Health.OnDeath += OnEnemyDied;
            _playerHealth.Health.OnDeath += OnPlayerDied;

            if (_enemyRenderer != null && _currentEnemy.sprite != null)
                _enemyRenderer.sprite = _currentEnemy.sprite;

            if (_chart.IsLegacy)
            {
                _highway.LoadChart(_chart.LegacyChart);
            }
            else if (_chart.BattleChart != null)
            {
                _highway.LoadNotes(_chart.BattleChart.AllPlayerNotes);
                if (_enemyHighway != null)
                    _enemyHighway.LoadNotes(_chart.BattleChart.AllEnemyNotes);
            }

            _damagePipeline.SetEnemyHealth(_enemyHealth);
            _comboSystem.ResetAll();
            _accuracyTracker.Reset();

            RelicModifiers mods = RelicEffectAggregator.Aggregate(_runState?.ActiveRelics);
            _judgmentSystem.ApplyRelicModifiers(mods.BonusPerfectWindowMs);
            _comboSystem.ApplyRelicModifiers(mods.ComboRateBoost, mods.ComboCapBoost);
            _damagePipeline.ApplyRelicModifiers(mods.BonusPerfectDamage, mods.MissDamageReduction);

            if (_relicEffectHandler != null)
                _relicEffectHandler.Initialize(mods);

            if (mods.HasAnyEffect)
                GameLog.Info($"[BattleManager] Relic modifiers active: {mods}");

            string eliteTag = _isElite ? " [ELITE]" : "";
            if (_chart.IsLegacy)
                GameLog.Info($"[BattleManager] Initialized (legacy){eliteTag}: {_currentEnemy.enemyName} ({enemyHP} HP) at {_chart.EffectiveBPM} BPM");
            else
                GameLog.Info($"[BattleManager] Initialized ({_chart.Mode}){eliteTag}: {_currentEnemy.enemyName} ({enemyHP} HP) at {_chart.EffectiveBPM} BPM, {_chart.BattleChart.PlayerNoteCount}P + {_chart.BattleChart.EnemyNoteCount}E notes");
        }

        private ISeededRandom GetChartRng()
        {
            if (_runState?.RunSeed != null)
                return _runState.RunSeed.GetRandom(RandomDomain.Charts, _runState.SelectedNode?.Id ?? 0);

            GameLog.Warn("[BattleManager] No RunSeed available, using fallback seed 42.");
            return new SeededRandom(42);
        }

        private void SetupFSM()
        {
            _fsm = new StateMachine<BattlePhase>();

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Intro,
                enter: _ =>
                {
                    _phaseTimer = _introDelay;
                    _battleEnded = false;
                    string eliteTag = _isElite ? " [ELITE]" : "";
                    GameLog.Info($"[BattleManager] INTRO{eliteTag} - {_currentEnemy.enemyName} appears!");
                },
                update: () =>
                {
                    _phaseTimer -= Time.deltaTime;
                    if (_phaseTimer <= 0f)
                        _fsm.TransitionTo(BattlePhase.Playing);
                }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Playing,
                enter: _ =>
                {
                    _conductor.OnSongFinished += OnSongEnded;
                    _conductor.Play(_chart.EffectiveBPM, _chart.AudioOffset);
                    GameLog.Info("[BattleManager] PLAYING - song started!");
                },
                exit: _ =>
                {
                    _conductor.OnSongFinished -= OnSongEnded;
                    if (_conductor.IsPlaying) _conductor.Stop();
                    _highway.ClearAllNotes();
                    _holdTracker.ClearAll();
                    if (!_chart.IsLegacy && _enemyHighway != null) _enemyHighway.Clear();
                }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Won,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(true);
                    _battleUI?.ShowResult(true);
                    GameLog.Info("<color=green>[BattleManager] VICTORY!</color>");
                },
                update: () => { _phaseTimer -= Time.deltaTime; if (_phaseTimer <= 0f) OnBattleComplete(); }
            ));

            _fsm.AddState(new LambdaState<BattlePhase>(
                BattlePhase.Lost,
                enter: _ =>
                {
                    _phaseTimer = _endDelay;
                    CollectStats(false);
                    _battleUI?.ShowResult(false);
                    GameLog.Info("<color=red>[BattleManager] DEFEATED!</color>");
                },
                update: () => { _phaseTimer -= Time.deltaTime; if (_phaseTimer <= 0f) OnBattleComplete(); }
            ));
        }

        private void HandlePauseInput()
        {
            // Read the pause key through the Input System. This project's Active Input
            // Handling is set to "Input System Only" (activeInputHandler: 2), so the legacy
            // UnityEngine.Input class is disabled and Input.GetKeyDown(Escape) never fires.
            // That was why Escape did nothing in battle: the legacy call was silently dead.
            // Gamepad Start also pauses, for controller play.
            if (!PausePressedThisFrame()) return;
            if (_fsm.CurrentStateKey != BattlePhase.Playing) return;

            if (_conductor.IsPaused) ResumeBattle();
            else PauseBattle();
        }

        private static bool PausePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && gp.startButton.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        private void PauseBattle()
        {
            _conductor.Pause();
            if (_pauseMenu != null) _pauseMenu.Show();
            GameLog.Info("[BattleManager] PAUSED");
        }

        private void ResumeBattle()
        {
            if (_pauseMenu != null) _pauseMenu.Hide();
            _conductor.Resume();
            GameLog.Info("[BattleManager] RESUMED");
        }

        private void QuitToMenu()
        {
            if (_pauseMenu != null) _pauseMenu.Hide();
            _conductor.Resume();
            _conductor.Stop();
            GameLog.Info("[BattleManager] Quit to menu.");

            var tm = SceneTransitionManager.Instance;
            if (tm != null) tm.GoTo("MainMenuScene");
            else UnityEngine.SceneManagement.SceneManager.LoadScene(SceneTransitionManager.MAIN_MENU_SCENE);
        }

        private void OnEnemyDied()
        {
            if (_battleEnded) return;
            LogKillTiming();
            EndBattle(true);
        }
        private void OnPlayerDied() { if (!_battleEnded) EndBattle(false); }

        // Dev aid for HP tuning: reports how far into the song the enemy died. Aim for the kill to
        // land around 80% for an average player, leaving a short decisive tail. If it dies very
        // early, raise that enemy's maxHP; if the song ends first (a loss), lower it.
        private void LogKillTiming()
        {
            if (_conductor == null || _chart.IsLegacy || _chart.BattleChart == null) return;
            float pos = _conductor.SongPositionInBeats;
            float total = _chart.BattleChart.TotalBeats;
            string pct = total > 0f ? $"{Mathf.RoundToInt(100f * pos / total)}%" : "n/a";
            GameLog.Info($"[BattleManager] Enemy down at beat {pos:F0}/{total:F0} ({pct} of song), maxHP {_enemyHealth.MaxHP}. Aim ~80%.");
        }

        private void OnSongEnded()
        {
            if (_battleEnded) return;
            if (_enemyHealth.IsAlive)
            {
                GameLog.Info("[BattleManager] Song ended - enemy survived!");
                EndBattle(false);
            }
        }

        private void EndBattle(bool victory)
        {
            _battleEnded = true;
            _fsm.TransitionTo(victory ? BattlePhase.Won : BattlePhase.Lost);
        }

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
                NotesHit = _accuracyTracker.PerfectCount + _accuracyTracker.GoodCount + _accuracyTracker.BadCount,
                EnemyName = _currentEnemy.enemyName,
                WasBoss = _isBoss
            };
        }

        private void OnBattleComplete()
        {
            if (_enemyHealth.Health != null) _enemyHealth.Health.OnDeath -= OnEnemyDied;
            _playerHealth.Health.OnDeath -= OnPlayerDied;
            GameLog.Info("[BattleManager] Battle complete - ready for scene transition.");
            OnBattleCompleted?.Invoke(LastBattleStats.Victory);
        }

        private void OnDestroy()
        {
            if (_enemyHealth != null && _enemyHealth.Health != null) _enemyHealth.Health.OnDeath -= OnEnemyDied;
            if (_playerHealth != null && _playerHealth.Health != null) _playerHealth.Health.OnDeath -= OnPlayerDied;
        }
    }
}
