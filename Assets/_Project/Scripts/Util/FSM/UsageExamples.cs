// ============================================================================
// USAGE EXAMPLES — Finite State Machine
// 
// These are NOT part of the FSM utility. They show how a CONSUMER
// defines their own enums and passes them into the generic FSM.
// 
// The FSM framework knows nothing about games, battles, or bosses.
// It only knows TKey — whatever type YOU give it.
// ============================================================================

using UnityEngine;
using RhythmRogue.Util.FSM;
using RhythmRogue.Util.Events;

// ============================================================================
// STEP 1: The consumer defines their own enums.
// These live in YOUR game code, not in the utility folder.
// The FSM doesn't ship with any enums — you bring your own.
// ============================================================================

namespace MyGame.States
{
    /// <summary>
    /// Define this in your game project — e.g. Assets/_Project/Scripts/States/
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Map,
        Battle,
        Reward,
        Boss,
        RunSummary
    }

    public enum BattlePhase
    {
        Intro,
        Playing,
        Outro
    }

    public enum BossPhase
    {
        Phase1,
        Transition1,
        Phase2,
        Transition2,
        Phase3,
        Defeated
    }
}

// ============================================================================
// STEP 2: The consumer creates state classes using their enums.
// BaseState<TKey> doesn't care what TKey is — enum, string, int, whatever.
// ============================================================================

namespace MyGame.States
{
    // --- Class-based states (for complex behavior) --------------------------

    public class MainMenuState : BaseState<GameState>
    {
        public MainMenuState() : base(GameState.MainMenu) { }

        public override void Enter(GameState previousState)
        {
            Debug.Log("[GameFlow] Entered Main Menu");
        }

        public override void Exit(GameState nextState)
        {
            Debug.Log("[GameFlow] Leaving Main Menu");
        }
    }

    public class MapState : BaseState<GameState>
    {
        private readonly MonoStateMachine<GameState> _owner;
        private EventBindingGroup _bindings;

        public MapState(MonoStateMachine<GameState> owner) : base(GameState.Map)
        {
            _owner = owner;
        }

        public override void Enter(GameState previousState)
        {
            Debug.Log("[GameFlow] Entered Map");

            var bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings.Add<NodeSelectedEvent>(bus, evt =>
            {
                if (evt.NodeType == 5) // Boss node
                    _owner.TransitionTo(GameState.Boss);
                else
                    _owner.TransitionTo(GameState.Battle);
            });
        }

        public override void Exit(GameState nextState)
        {
            _bindings?.Dispose();
        }
    }

    public class BattleGameState : BaseState<GameState>
    {
        private readonly MonoStateMachine<GameState> _owner;
        private EventBindingGroup _bindings;

        public BattleGameState(MonoStateMachine<GameState> owner) : base(GameState.Battle)
        {
            _owner = owner;
        }

        public override void Enter(GameState previousState)
        {
            Debug.Log("[GameFlow] Entered Battle");

            var bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings.Add<BattleEndedEvent>(bus, evt =>
            {
                if (evt.Victory)
                    _owner.TransitionTo(GameState.Reward);
                else
                    _owner.TransitionTo(GameState.RunSummary);
            });
        }

        public override void Exit(GameState nextState)
        {
            _bindings?.Dispose();
        }
    }

    public class RewardState : BaseState<GameState>
    {
        private readonly MonoStateMachine<GameState> _owner;
        private EventBindingGroup _bindings;

        public RewardState(MonoStateMachine<GameState> owner) : base(GameState.Reward)
        {
            _owner = owner;
        }

        public override void Enter(GameState previousState)
        {
            Debug.Log("[GameFlow] Entered Reward Pick");

            var bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings.Add<RewardPickedEvent>(bus, evt =>
            {
                _owner.TransitionTo(GameState.Map);
            });
        }

        public override void Exit(GameState nextState)
        {
            _bindings?.Dispose();
        }
    }
}

// ============================================================================
// STEP 3: The consumer creates controllers, passing their enum as TKey.
// MonoStateMachine<TKey> becomes MonoStateMachine<GameState>,
// MonoStateMachine<BattlePhase>, MonoStateMachine<BossPhase>, etc.
// ============================================================================

namespace MyGame.Controllers
{
    using MyGame.States;

    // --- Game Flow: uses class-based states ---------------------------------

    /// <summary>
    /// StateMachine&lt;GameState&gt; — the FSM only knows "GameState"
    /// because you told it. Swap GameState for any other enum and
    /// it works identically.
    /// </summary>
    public class GameFlowController : MonoStateMachine<GameState>
    {
        protected override void ConfigureStates()
        {
            AddState(new MainMenuState());
            AddState(new MapState(this));
            AddState(new BattleGameState(this));
            AddState(new RewardState(this));
        }

        private void Start()
        {
            StartMachine(GameState.MainMenu);
        }
    }

    // --- Battle Flow: uses lambda states for simplicity ---------------------

    /// <summary>
    /// StateMachine&lt;BattlePhase&gt; — different enum, same FSM.
    /// Lambda states because battle phases are short and simple.
    /// </summary>
    public class BattleFlowController : MonoStateMachine<BattlePhase>
    {
        [SerializeField] private float _introCountdownSeconds = 3f;
        private float _introTimer;

        protected override void ConfigureStates()
        {
            AddState(BattlePhase.Intro,
                enter: _ =>
                {
                    Debug.Log("[Battle] Intro — counting down...");
                    _introTimer = _introCountdownSeconds;
                },
                update: () =>
                {
                    _introTimer -= Time.deltaTime;
                    if (_introTimer <= 0f)
                        TransitionTo(BattlePhase.Playing);
                }
            );

            AddState(BattlePhase.Playing,
                enter: _ =>
                {
                    Debug.Log("[Battle] Playing — music starts!");
                },
                exit: _ =>
                {
                    Debug.Log("[Battle] Stopping playback");
                }
            );

            AddState(BattlePhase.Outro,
                enter: _ =>
                {
                    Debug.Log("[Battle] Outro — showing results");
                }
            );
        }

        public void BeginBattle()
        {
            StartMachine(BattlePhase.Intro);
        }
    }

    // --- Boss Phases: lambda states with EventBus integration ---------------

    /// <summary>
    /// StateMachine&lt;BossPhase&gt; — yet another enum, same FSM.
    /// Demonstrates HP-threshold transitions and EventBus publishing.
    /// </summary>
    public class BossController : MonoStateMachine<BossPhase>
    {
        private int _bossHp = 300;
        private IEventBus _bus;
        private EventBindingGroup _bindings;

        protected override void ConfigureStates()
        {
            AddState(BossPhase.Phase1,
                enter: _ =>
                {
                    Debug.Log("[Boss] Phase 1 — BPM 120");
                    _bus?.Publish(new BossPhaseChangedEvent
                        { PhaseIndex = 1, NewBpm = 120f });
                },
                update: () =>
                {
                    if (_bossHp <= 200)
                        TransitionTo(BossPhase.Transition1);
                }
            );

            AddState(BossPhase.Transition1,
                enter: _ =>
                {
                    Debug.Log("[Boss] Transition — tempo shift");
                    TransitionTo(BossPhase.Phase2);
                }
            );

            AddState(BossPhase.Phase2,
                enter: _ =>
                {
                    Debug.Log("[Boss] Phase 2 — BPM 150, lane swaps!");
                    _bus?.Publish(new BossPhaseChangedEvent
                        { PhaseIndex = 2, NewBpm = 150f });
                },
                update: () =>
                {
                    if (_bossHp <= 100)
                        TransitionTo(BossPhase.Transition2);
                }
            );

            AddState(BossPhase.Transition2,
                enter: _ =>
                {
                    Debug.Log("[Boss] Counter-attack — survive!");
                    TransitionTo(BossPhase.Phase3);
                }
            );

            AddState(BossPhase.Phase3,
                enter: _ =>
                {
                    Debug.Log("[Boss] Phase 3 — BPM 180, everything!");
                    _bus?.Publish(new BossPhaseChangedEvent
                        { PhaseIndex = 3, NewBpm = 180f });
                },
                update: () =>
                {
                    if (_bossHp <= 0)
                        TransitionTo(BossPhase.Defeated);
                }
            );

            AddState(BossPhase.Defeated,
                enter: _ =>
                {
                    Debug.Log("[Boss] DEFEATED!");
                    _bus?.Publish(new BattleEndedEvent { Victory = true });
                }
            );
        }

        private void Start()
        {
            _bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings.Add<EnemyHpChangedEvent>(_bus, evt =>
            {
                _bossHp = evt.CurrentHp;
            });

            StartMachine(BossPhase.Phase1);
        }

        protected override void OnDestroy()
        {
            _bindings?.Dispose();
            base.OnDestroy();
        }
    }

    // -----------------------------------------------------------------------
    // BONUS: TKey can be anything — enums, strings, ints.
    // The FSM is type-agnostic.
    //
    //   var fsm = new StateMachine<string>();
    //   fsm.AddState(new LambdaState<string>("idle", enter: _ => { }));
    //   fsm.AddState(new LambdaState<string>("walking", enter: _ => { }));
    //   fsm.Start("idle");
    //   fsm.TransitionTo("walking");
    // -----------------------------------------------------------------------
}
