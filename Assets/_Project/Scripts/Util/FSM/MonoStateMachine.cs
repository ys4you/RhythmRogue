using System;
using UnityEngine;

namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// MonoBehaviour wrapper around StateMachine&lt;TKey&gt;.
    /// 
    /// Handles the Update/FixedUpdate pumping automatically so you
    /// don't need to wire it up manually. Inherit from this, register
    /// your states, and call StartMachine.
    /// 
    /// Usage:
    ///   public class GameFlowController : MonoStateMachine&lt;GameState&gt;
    ///   {
    ///       protected override void ConfigureStates()
    ///       {
    ///           AddState(new MainMenuState());
    ///           AddState(new MapState());
    ///           AddState(new BattleState());
    ///       }
    ///   }
    /// </summary>
    /// <typeparam name="TKey">State identifier type (typically an enum).</typeparam>
    public abstract class MonoStateMachine<TKey> : MonoBehaviour
    {
        private StateMachine<TKey> _machine;

        /// <summary>
        /// The underlying state machine. Accessible for advanced usage.
        /// </summary>
        protected IStateMachine<TKey> Machine => _machine;

        /// <summary>
        /// Key of the currently active state.
        /// </summary>
        public TKey CurrentState => _machine.CurrentStateKey;

        /// <summary>
        /// Key of the previous state.
        /// </summary>
        public TKey PreviousState => _machine.PreviousStateKey;

        /// <summary>
        /// Whether the machine is running.
        /// </summary>
        public bool IsRunning => _machine != null && _machine.IsRunning;

        /// <summary>
        /// Fired after a state transition completes.
        /// Parameters: previousState, newState.
        /// </summary>
        public event Action<TKey, TKey> OnStateChanged
        {
            add => _machine.OnStateChanged += value;
            remove => _machine.OnStateChanged -= value;
        }

        protected virtual void Awake()
        {
            _machine = new StateMachine<TKey>();
            ConfigureStates();
        }

        /// <summary>
        /// Override to register all states via AddState().
        /// Called automatically in Awake.
        /// </summary>
        protected abstract void ConfigureStates();

        /// <summary>
        /// Register a state with the machine.
        /// </summary>
        protected void AddState(IState<TKey> state)
        {
            _machine.AddState(state);
        }

        /// <summary>
        /// Register a lambda state inline.
        /// </summary>
        protected void AddState(
            TKey key,
            Action<TKey> enter = null,
            Action update = null,
            Action fixedUpdate = null,
            Action<TKey> exit = null)
        {
            _machine.AddState(new LambdaState<TKey>(key, enter, update, fixedUpdate, exit));
        }

        /// <summary>
        /// Start the machine in the given state.
        /// </summary>
        protected void StartMachine(TKey initialState)
        {
            _machine.Start(initialState);
        }

        /// <summary>
        /// Transition to a new state.
        /// </summary>
        public void TransitionTo(TKey newState)
        {
            _machine.TransitionTo(newState);
        }

        protected virtual void Update()
        {
            _machine?.Update();
        }

        protected virtual void FixedUpdate()
        {
            _machine?.FixedUpdate();
        }

        protected virtual void OnDestroy()
        {
            _machine?.Stop();
        }
    }
}
