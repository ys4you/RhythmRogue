using System;

namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// State defined with lambda callbacks instead of a full class.
    /// 
    /// Useful for simple states where creating a dedicated class is
    /// overkill — rest nodes, pause screens, loading states, etc.
    /// For states with complex logic, prefer extending BaseState.
    /// 
    /// Usage:
    ///   fsm.AddState(new LambdaState&lt;GameState&gt;(
    ///       GameState.Loading,
    ///       enter: prev => ShowLoadingScreen(),
    ///       exit: next => HideLoadingScreen()
    ///   ));
    /// </summary>
    /// <typeparam name="TKey">State identifier type.</typeparam>
    public class LambdaState<TKey> : IState<TKey>
    {
        private readonly Action<TKey> _onEnter;
        private readonly Action _onUpdate;
        private readonly Action _onFixedUpdate;
        private readonly Action<TKey> _onExit;

        /// <inheritdoc/>
        public TKey Key { get; }

        /// <summary>
        /// Create a lambda-based state. All callbacks are optional.
        /// </summary>
        /// <param name="key">State identifier.</param>
        /// <param name="enter">Called on state enter. Receives previous state key.</param>
        /// <param name="update">Called every frame while active.</param>
        /// <param name="fixedUpdate">Called every fixed timestep while active.</param>
        /// <param name="exit">Called on state exit. Receives next state key.</param>
        public LambdaState(
            TKey key,
            Action<TKey> enter = null,
            Action update = null,
            Action fixedUpdate = null,
            Action<TKey> exit = null)
        {
            Key = key;
            _onEnter = enter;
            _onUpdate = update;
            _onFixedUpdate = fixedUpdate;
            _onExit = exit;
        }

        /// <inheritdoc/>
        public void Enter(TKey previousState) => _onEnter?.Invoke(previousState);

        /// <inheritdoc/>
        public void Update() => _onUpdate?.Invoke();

        /// <inheritdoc/>
        public void FixedUpdate() => _onFixedUpdate?.Invoke();

        /// <inheritdoc/>
        public void Exit(TKey nextState) => _onExit?.Invoke(nextState);
    }
}
