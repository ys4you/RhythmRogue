using UnityEngine;
using RhythmRogue.Util.FSM;
using RhythmRogue.Util;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive FSM test — a simple coin flip betting game.
    /// 
    /// HOW TO USE:
    /// 1. Create an empty scene
    /// 2. Add an empty GameObject, attach this script
    /// 3. Hit Play and follow the console prompts
    /// 
    /// GAME RULES:
    ///   You start with 10 coins.
    ///   Each round you bet 1 coin on heads or tails.
    ///   Win = +2 coins. Lose = -1 coin.
    ///   Reach 20 coins = you win. Reach 0 coins = you lose.
    /// 
    /// STATES:
    ///   Menu     → [Space] to start
    ///   Betting  → [H] bet heads, [T] bet tails
    ///   Flipping → auto-resolves after a short delay
    ///   Result   → [Space] to continue
    ///   GameOver → [R] to restart
    /// 
    /// Watch the Console to see Enter/Exit lifecycle and transitions.
    /// </summary>
    public class FSMTest : MonoBehaviour
    {
        // Consumer defines their own enum — FSM doesn't ship with this
        public enum CoinFlipState
        {
            Menu,
            Betting,
            Flipping,
            Result,
            GameOver
        }

        // Game state
        private int _coins;
        private int _round;
        private bool _betHeads;
        private bool _lastFlipHeads;
        private bool _lastWon;
        private float _flipTimer;

        // The FSM — generic, knows nothing about coin flips
        private StateMachine<CoinFlipState> _fsm;

        // ===================================================================
        // SETUP
        // ===================================================================

        private void Start()
        {
            _fsm = new StateMachine<CoinFlipState>();

            // Listen to ALL state transitions from the outside
            _fsm.OnStateChanged += (from, to) =>
            {
                GameLog.Info($"<color=grey>  [FSM] Transition: {from} → {to}</color>");
            };

            // -----------------------------------------------------------
            // MENU — Lambda state (simple, no class needed)
            // -----------------------------------------------------------
            _fsm.AddState(new LambdaState<CoinFlipState>(
                CoinFlipState.Menu,
                enter: _ =>
                {
                    GameLog.Info("<color=white>========================================</color>");
                    GameLog.Info("<color=white>  COIN FLIP — FSM TEST</color>");
                    GameLog.Info("<color=white>  Reach 20 coins to win!</color>");
                    GameLog.Info("<color=white>========================================</color>");
                    GameLog.Info("<color=cyan>  [Space] Start Game</color>");
                }
            ));

            // -----------------------------------------------------------
            // BETTING — Class-based state (has Update logic)
            // -----------------------------------------------------------
            _fsm.AddState(new BettingState(this));

            // -----------------------------------------------------------
            // FLIPPING — Lambda state with timer in Update
            // -----------------------------------------------------------
            _fsm.AddState(new LambdaState<CoinFlipState>(
                CoinFlipState.Flipping,
                enter: _ =>
                {
                    _flipTimer = 1.5f;
                    GameLog.Info("<color=yellow>  Flipping coin...</color>");
                },
                update: () =>
                {
                    _flipTimer -= Time.deltaTime;
                    if (_flipTimer <= 0f)
                    {
                        // Resolve the flip
                        _lastFlipHeads = Random.value > 0.5f;
                        _lastWon = _betHeads == _lastFlipHeads;

                        if (_lastWon)
                            _coins += 2;
                        else
                            _coins -= 1;

                        _fsm.TransitionTo(CoinFlipState.Result);
                    }
                }
            ));

            // -----------------------------------------------------------
            // RESULT — Lambda state showing outcome
            // -----------------------------------------------------------
            _fsm.AddState(new LambdaState<CoinFlipState>(
                CoinFlipState.Result,
                enter: _ =>
                {
                    string coinSide = _lastFlipHeads ? "HEADS" : "TAILS";
                    string betSide = _betHeads ? "Heads" : "Tails";

                    if (_lastWon)
                    {
                        GameLog.Info($"<color=green>  Coin landed {coinSide}! You bet {betSide}. WIN +2!</color>");
                    }
                    else
                    {
                        GameLog.Info($"<color=red>  Coin landed {coinSide}! You bet {betSide}. LOSE -1!</color>");
                    }

                    GameLog.Info($"<color=white>  Coins: {_coins}</color>");

                    // Check win/lose conditions
                    if (_coins >= 20)
                    {
                        GameLog.Info("<color=green>  You reached 20 coins!</color>");
                        GameLog.Info("<color=cyan>  [Space] Continue</color>");
                    }
                    else if (_coins <= 0)
                    {
                        GameLog.Info("<color=red>  You're broke!</color>");
                        GameLog.Info("<color=cyan>  [Space] Continue</color>");
                    }
                    else
                    {
                        GameLog.Info("<color=cyan>  [Space] Next Round</color>");
                    }
                }
            ));

            // -----------------------------------------------------------
            // GAME OVER — Lambda state with restart option
            // -----------------------------------------------------------
            _fsm.AddState(new LambdaState<CoinFlipState>(
                CoinFlipState.GameOver,
                enter: prev =>
                {
                    bool won = _coins >= 20;
                    string result = won ? "VICTORY" : "DEFEAT";
                    string color = won ? "green" : "red";

                    GameLog.Info($"<color={color}>========================================</color>");
                    GameLog.Info($"<color={color}>  GAME OVER — {result}</color>");
                    GameLog.Info($"<color={color}>  Final: {_coins} coins in {_round} rounds</color>");
                    GameLog.Info($"<color={color}>========================================</color>");
                    GameLog.Info("<color=cyan>  [R] Restart</color>");
                }
            ));

            // Start the machine
            _fsm.Start(CoinFlipState.Menu);
        }

        // ===================================================================
        // UPDATE — pump the FSM + handle global input
        // ===================================================================

        private void Update()
        {
            _fsm.Update();

            CoinFlipState current = _fsm.CurrentStateKey;

            // Menu: Space starts the game
            if (current == CoinFlipState.Menu && Input.GetKeyDown(KeyCode.Space))
            {
                _coins = 10;
                _round = 0;
                _fsm.TransitionTo(CoinFlipState.Betting);
            }

            // Result: Space continues
            if (current == CoinFlipState.Result && Input.GetKeyDown(KeyCode.Space))
            {
                if (_coins >= 20 || _coins <= 0)
                    _fsm.TransitionTo(CoinFlipState.GameOver);
                else
                    _fsm.TransitionTo(CoinFlipState.Betting);
            }

            // Game Over: R restarts
            if (current == CoinFlipState.GameOver && Input.GetKeyDown(KeyCode.R))
            {
                _fsm.TransitionTo(CoinFlipState.Menu);
            }
        }

        // ===================================================================
        // CLASS-BASED STATE — for more complex logic
        // Shows how a state can reference the "owner" for shared data
        // ===================================================================

        private class BettingState : BaseState<CoinFlipState>
        {
            private readonly FSMTest _game;

            public BettingState(FSMTest game) : base(CoinFlipState.Betting)
            {
                _game = game;
            }

            public override void Enter(CoinFlipState previousState)
            {
                _game._round++;

                GameLog.Info($"<color=white>  ── Round {_game._round} ── Coins: {_game._coins} ──</color>");
                GameLog.Info("<color=cyan>  [H] Bet Heads   [T] Bet Tails</color>");
            }

            public override void Update()
            {
                if (Input.GetKeyDown(KeyCode.H))
                {
                    _game._betHeads = true;
                    GameLog.Info("<color=magenta>  You bet: HEADS</color>");
                    _game._fsm.TransitionTo(CoinFlipState.Flipping);
                }
                else if (Input.GetKeyDown(KeyCode.T))
                {
                    _game._betHeads = false;
                    GameLog.Info("<color=magenta>  You bet: TAILS</color>");
                    _game._fsm.TransitionTo(CoinFlipState.Flipping);
                }
            }

            public override void Exit(CoinFlipState nextState)
            {
                // Could save bet history, analytics, etc.
            }
        }
    }
}
