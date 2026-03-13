using System;

namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// Abstraction for a finite state machine.
    /// 
    /// Manages a set of states identified by TKey, with only one
    /// state active at a time. Handles transitions with proper
    /// Enter/Exit lifecycle and exposes events for external listeners.
    /// </summary>
    /// <typeparam name="TKey">
    /// Type used to identify states (typically an enum).
    /// </typeparam>
    public interface IStateMachine<TKey>
    {
        /// <summary>
        /// Key of the currently active state.
        /// </summary>
        TKey CurrentStateKey { get; }

        /// <summary>
        /// Key of the previous state. Default(TKey) if no transition has occurred.
        /// </summary>
        TKey PreviousStateKey { get; }

        /// <summary>
        /// Whether the state machine has been started (has an active state).
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Fired after a state transition completes (after Exit and Enter).
        /// Parameters: previousState, newState.
        /// </summary>
        event Action<TKey, TKey> OnStateChanged;

        /// <summary>
        /// Register a state. Must be done before starting the machine
        /// or transitioning to that state.
        /// </summary>
        void AddState(IState<TKey> state);

        /// <summary>
        /// Start the machine in the given state. Calls Enter on the initial state.
        /// Can only be called once — use TransitionTo for subsequent changes.
        /// </summary>
        void Start(TKey initialState);

        /// <summary>
        /// Transition to a new state. Calls Exit on the current state,
        /// then Enter on the new state.
        /// 
        /// Transitioning to the current state is a no-op by default.
        /// </summary>
        void TransitionTo(TKey newState);

        /// <summary>
        /// Call every frame to update the current state.
        /// Typically called from a MonoBehaviour's Update.
        /// </summary>
        void Update();

        /// <summary>
        /// Call every fixed timestep to update the current state.
        /// Typically called from a MonoBehaviour's FixedUpdate.
        /// </summary>
        void FixedUpdate();

        /// <summary>
        /// Stop the machine. Calls Exit on the current state.
        /// </summary>
        void Stop();
    }
}
