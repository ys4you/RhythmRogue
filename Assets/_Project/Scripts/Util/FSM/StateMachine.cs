using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// Generic finite state machine.
    /// 
    /// Manages a dictionary of states keyed by TKey with only one
    /// active at a time. Handles transition lifecycle (Exit → Enter),
    /// guards against re-entry and mid-transition transitions, and
    /// exposes a state change event for external listeners.
    /// 
    /// SOLID breakdown:
    /// - S: Only manages state transitions and lifecycle. No game logic.
    /// - O: Add new states without modifying this class.
    /// - L: Any IState&lt;TKey&gt; works — BaseState, MonoBehaviour states, lambdas.
    /// - I: Consumers see IStateMachine&lt;TKey&gt;, states see IState&lt;TKey&gt;.
    /// - D: Depends on abstractions (IState), not concrete state implementations.
    /// </summary>
    /// <typeparam name="TKey">State identifier type (typically an enum).</typeparam>
    public class StateMachine<TKey> : IStateMachine<TKey>
    {
        private readonly Dictionary<TKey, IState<TKey>> _states = new();
        private IState<TKey> _currentState;
        private bool _isTransitioning;

        /// <inheritdoc/>
        public TKey CurrentStateKey => _currentState != null ? _currentState.Key : default;

        /// <inheritdoc/>
        public TKey PreviousStateKey { get; private set; }

        /// <inheritdoc/>
        public bool IsRunning => _currentState != null;

        /// <inheritdoc/>
        public event Action<TKey, TKey> OnStateChanged;

        /// <inheritdoc/>
        public void AddState(IState<TKey> state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (_states.ContainsKey(state.Key))
            {
                Debug.LogWarning($"[StateMachine] State '{state.Key}' is already registered. Overwriting.");
            }

            _states[state.Key] = state;
        }

        /// <inheritdoc/>
        public void Start(TKey initialState)
        {
            if (IsRunning)
            {
                Debug.LogWarning("[StateMachine] Already running. Use TransitionTo to change states.");
                return;
            }

            _currentState = GetState(initialState);
            _currentState.Enter(default);
        }

        /// <inheritdoc/>
        public void TransitionTo(TKey newState)
        {
            if (!IsRunning)
            {
                Debug.LogWarning("[StateMachine] Not running. Call Start first.");
                return;
            }

            // Guard: no re-entry (transitioning to the same state)
            if (EqualityComparer<TKey>.Default.Equals(_currentState.Key, newState))
            {
                return;
            }

            // Guard: no transitions during a transition
            if (_isTransitioning)
            {
                Debug.LogWarning(
                    $"[StateMachine] Transition to '{newState}' blocked — already transitioning " +
                    $"from '{PreviousStateKey}' to '{CurrentStateKey}'.");
                return;
            }

            IState<TKey> next = GetState(newState);

            _isTransitioning = true;

            TKey previousKey = _currentState.Key;
            _currentState.Exit(newState);

            PreviousStateKey = previousKey;
            _currentState = next;
            _currentState.Enter(previousKey);

            _isTransitioning = false;

            OnStateChanged?.Invoke(previousKey, newState);
        }

        /// <inheritdoc/>
        public void Update()
        {
            if (!IsRunning || _isTransitioning) return;
            _currentState.Update();
        }

        /// <inheritdoc/>
        public void FixedUpdate()
        {
            if (!IsRunning || _isTransitioning) return;
            _currentState.FixedUpdate();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            if (!IsRunning) return;

            _currentState.Exit(default);
            _currentState = null;
        }

        private IState<TKey> GetState(TKey key)
        {
            if (_states.TryGetValue(key, out IState<TKey> state))
            {
                return state;
            }

            throw new InvalidOperationException(
                $"[StateMachine] State '{key}' not registered. " +
                "Did you forget to call AddState?");
        }
    }
}
