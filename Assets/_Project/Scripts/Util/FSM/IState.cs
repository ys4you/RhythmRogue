namespace RhythmRogue.Util.FSM
{
    /// <summary>
    /// Contract for a single state in a state machine.
    /// 
    /// Each state encapsulates its own behavior: what happens when
    /// entering, updating each frame, handling fixed updates, and
    /// exiting. States don't know about each other — transitions
    /// are managed by the state machine.
    /// 
    /// The generic parameter TKey identifies states (typically an enum).
    /// </summary>
    /// <typeparam name="TKey">
    /// Type used to identify states. Use an enum for compile-time safety:
    /// e.g. GameState, BattleState, BossPhase.
    /// </typeparam>
    public interface IState<TKey>
    {
        /// <summary>
        /// Unique identifier for this state.
        /// </summary>
        TKey Key { get; }

        /// <summary>
        /// Called once when transitioning INTO this state.
        /// Use for initialization, enabling objects, starting audio, etc.
        /// </summary>
        /// <param name="previousState">
        /// The state we're coming from. Default(TKey) on first entry.
        /// Useful for conditional setup (e.g. different intro animation
        /// when coming from Map vs coming from Pause).
        /// </param>
        void Enter(TKey previousState);

        /// <summary>
        /// Called every frame while this state is active.
        /// Equivalent to MonoBehaviour.Update.
        /// </summary>
        void Update();

        /// <summary>
        /// Called every fixed timestep while this state is active.
        /// Equivalent to MonoBehaviour.FixedUpdate.
        /// Use for physics-related logic if needed.
        /// </summary>
        void FixedUpdate();

        /// <summary>
        /// Called once when transitioning OUT of this state.
        /// Use for cleanup, disabling objects, stopping audio, etc.
        /// </summary>
        /// <param name="nextState">
        /// The state we're going to. Useful for conditional cleanup
        /// (e.g. different exit animation when going to Victory vs Defeat).
        /// </param>
        void Exit(TKey nextState);
    }
}
