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
        [Tooltip("Silent pause after the enemy appears before the song starts, in seconds. The song " +
                 "then plays immediately, and the chart's own note-free intro gives the opening notes " +
                 "their scroll-in runway. Set to 0 for the song to start the instant the fight does; " +
                 "raise it for a longer enemy-reveal beat before the music.")]
        [Range(0f, 5f)]
        [SerializeField] private float _introDelay = 1f;
        [Tooltip("Pause on the result (Victory/Defeat) before the scene transitions out, in seconds.")]
        [Range(0f, 5f)]
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
        private DifficultyContext _difficulty;
        private string _lessonText;
        private bool _waitingForLesson;
        private bool _practice;
        private float _practiceEndBeat = float.MaxValue;
        private RhythmRogue.UI.RelicBar _relicBar;
        private LessonCallout _lessonCallout;
        private bool _completeFired;

        public static BattleStats LastBattleStats { get; private set; }
        public BattlePhase CurrentPhase => _fsm != null && _fsm.IsRunning ? _fsm.CurrentStateKey : BattlePhase.Intro;
        public bool IsPaused => _conductor != null && _conductor.IsPaused;
        public EnemyData CurrentEnemy => _currentEnemy;
        public bool IsElite => _isElite;
        public bool IsBoss => _isBoss;
        public bool IsPractice => _practice;
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

            // Onboarding paths attach a lesson to each node; if present it's shown during Intro
            // (before the song) and the fight waits on a keypress. Null on normal nodes.
            _lessonText = _runState?.SelectedNode?.TeachingText;

            if (_currentEnemy == null)
            {
                GameLog.Error("[BattleManager] No enemy data! Assign a default in Inspector.");
                return;
            }

            ISeededRandom rng = GetChartRng();

            // Build the difficulty context once: which area, how deep the node sits, the run's
            // forgiveness tier, and whether this is the boss. Chart difficulty and enemy HP both
            // derive from this via DifficultyCurve, so a fight's intensity follows its slot in the
            // run rather than being baked into the enemy. With no node/area (a direct battle-scene
            // launch) this defaults to the enemy's own fallback values.
            _difficulty = new DifficultyContext(
                _runState != null ? _runState.CurrentArea : null,
                _runState != null ? _runState.SelectedNodeDepthT() : 0f,
                _runState != null ? _runState.Tier : DifficultyTier.Normal,
                _isBoss);

            _chart = _chartProvider.Resolve(_currentEnemy, _isElite, rng, _runState?.SelectedChart, _difficulty);

            if (!_chart.Success)
                GameLog.Error("[BattleManager] Chart resolution failed.");

            // Practice (onboarding) mode: the fight can't be lost and ends in a win shortly after
            // the last chart note, so a first-timer always succeeds no matter how many notes they
            // hit. Driven by the area flag; the last-note beat comes from the resolved chart.
            _practice = _runState != null && _runState.CurrentArea != null && _runState.CurrentArea.IsPracticeRun;
            if (_practice)
            {
                float lastBeat = (_chart.IsLegacy && _chart.LegacyChart != null && _chart.LegacyChart.NoteCount > 0)
                    ? _chart.LegacyChart.Notes[_chart.LegacyChart.NoteCount - 1].BeatPosition
                    : 0f;
                _practiceEndBeat = lastBeat > 0f ? lastBeat + 2f : float.MaxValue;
            }

            SetupFSM();
        }

        private void Start()
        {
            InitializeBattle();

            // Relic bar on its own overlay canvas, so held relics stay visible during the fight
            // (and the onboarding's relic lesson has something to point at). Self-contained; shows
            // nothing when the player holds no relics.
            if (_runState != null) _relicBar = RhythmRogue.UI.RelicBar.Create(_runState);

            // Onboarding lane focus (dim + spotlight the next note's lane) is held back for now: on
            // its own, without the guided "freeze on the first note" step, it read as too much. Add
            // LaneFocus to _highway here (practice-only) when that step lands to bring it back.

            // Lane key labels are tutorial scaffolding: show them in practice (onboarding) only and
            // hide them in a normal run, where a returning player does not need per-lane reminders.
            if (_highway != null)
                _highway.GetComponent<LaneKeyLabels>()?.SetHidden(!_practice);

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
            // HP (fight length) comes from the area + node depth via DifficultyCurve, with the
            // enemy contributing only a relative hpFlavor and elites scaling up inside the curve.
            // Deeper nodes and later areas mean longer fights; the enemy's slot sets the length.
            int enemyHP = DifficultyCurve.EnemyHP(_difficulty, _currentEnemy, _isElite, _eliteConfig);

            _enemyHealth.InitForBattle(enemyHP);
            _enemyHealth.Health.OnDeath += OnEnemyDied;
            _playerHealth.Health.OnDeath += OnPlayerDied;

            if (_enemyRenderer != null && _currentEnemy.sprite != null)
                _enemyRenderer.sprite = _currentEnemy.sprite;

            if (_chart.IsLegacy)
            {
                _highway.LoadChart(_chart.LegacyChart);

                // Authored charts may carry enemy notes (an "enemyNotes" array in the JSON). Feed
                // them to the enemy highway so the guard has something real to block, just like a
                // procedural fight. Charts without them (most) load nothing here.
                if (_enemyHighway != null && _chart.LegacyChart != null && _chart.LegacyChart.EnemyNoteCount > 0)
                {
                    var enemyStamped = new System.Collections.Generic.List<StampedNote>(_chart.LegacyChart.EnemyNoteCount);
                    var en = _chart.LegacyChart.EnemyNotes;
                    for (int i = 0; i < en.Count; i++)
                        enemyStamped.Add(new StampedNote(en[i].Lane, en[i].BeatPosition, en[i].HoldDuration));
                    _enemyHighway.LoadNotes(enemyStamped);
                }
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
            _judgmentSystem.ApplyTier(_difficulty.Tier);
            _comboSystem.ApplyRelicModifiers(mods.ComboRateBoost, mods.ComboCapBoost);
            _damagePipeline.ApplyRelicModifiers(mods.BonusPerfectDamage, mods.MissDamageReduction, mods.MissShieldCharges);

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

                    // If this node carries a lesson (onboarding), show it now and wait for a key
                    // before counting down. The song hasn't started and no notes move during Intro,
                    // so the player reads it with the fight frozen behind.
                    // A lesson that teaches a specific HUD element (shield, relics) uses a coach-mark
                    // that dims the screen and spotlights that element; the rest use the plain card.
                    _waitingForLesson = !string.IsNullOrWhiteSpace(_lessonText);
                    if (_waitingForLesson)
                    {
                        RectTransform highlightTarget = ResolveHighlightTarget();
                        if (highlightTarget != null)
                        {
                            if (_lessonCallout == null) _lessonCallout = LessonCallout.Create();
                            _lessonCallout.Show(_lessonText, highlightTarget);
                        }
                        else if (_battleUI != null)
                        {
                            _battleUI.ShowLesson(_lessonText);
                        }
                    }

                    string eliteTag = _isElite ? " [ELITE]" : "";
                    GameLog.Info($"[BattleManager] INTRO{eliteTag} - {_currentEnemy.enemyName} appears!");
                },
                update: () =>
                {
                    if (_waitingForLesson)
                    {
                        if (!AnyAdvancePressedThisFrame()) return;
                        _waitingForLesson = false;
                        if (_battleUI != null) _battleUI.HideLesson();
                        if (_lessonCallout != null) _lessonCallout.Hide();
                        _phaseTimer = _introDelay; // let the reveal beat + note runway play after dismissing
                        return;
                    }

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
                    // Song starts immediately. The opening runway comes from the chart's own
                    // note-free lead-in (see PatternAssembler / ChartProvider), so the music plays
                    // from the first frame while the opening notes scroll in over the intro bars.
                    _conductor.Play(_chart.EffectiveBPM, _chart.AudioOffset);
                    GameLog.Info("[BattleManager] PLAYING - song started!");
                },
                update: () =>
                {
                    // Practice mode auto-wins shortly after the last note so the fight always ends
                    // in victory without waiting out the rest of the song.
                    if (_practice && _conductor.IsPlaying &&
                        _conductor.SongPositionInBeats >= _practiceEndBeat)
                    {
                        GameLog.Info("[BattleManager] Practice chart complete - auto win.");
                        EndBattle(true);
                    }
                },
                exit: _ =>
                {
                    _conductor.OnSongFinished -= OnSongEnded;
                    if (_conductor.IsPlaying) _conductor.Stop();
                    _highway.ClearAllNotes();
                    _holdTracker.ClearAll();
                    if (_enemyHighway != null) _enemyHighway.Clear();
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

        // Any-key advance, used to dismiss the onboarding lesson overlay during Intro.
        private static bool AnyAdvancePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;
            var gp = Gamepad.current;
            if (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame)) return true;
            return false;
#else
            return Input.anyKeyDown;
#endif
        }

        // Map the current node's onboarding highlight to the HUD element a coach-mark should point
        // at, or null for a plain lesson. The guard badge lives on the battle HUD; the relic bar is
        // its own overlay created in Start.
        private RectTransform ResolveHighlightTarget()
        {
            OnboardingHighlight h = _runState?.SelectedNode?.Highlight ?? OnboardingHighlight.None;
            return h switch
            {
                OnboardingHighlight.Shield => _battleUI != null ? _battleUI.GuardRect : null,
                OnboardingHighlight.Relics => _relicBar != null ? _relicBar.BarRect : null,
                _ => null
            };
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
        private void OnPlayerDied() { if (!_battleEnded && !_practice) EndBattle(false); }

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
            if (_practice)
            {
                GameLog.Info("[BattleManager] Song ended in practice mode - win.");
                EndBattle(true);
                return;
            }
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
            // Won/Lost run their end countdown in Update, so this is reached every frame once the
            // timer hits zero (until the scene actually changes). Fire completion exactly once.
            if (_completeFired) return;
            _completeFired = true;

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
