// ============================================================================
// USAGE EXAMPLES — RhythmRogue Object Pool System
// These are NOT part of the pool utility. They show how your game code
// would use the pool for notes, hit effects, and combo popups.
// ============================================================================

using UnityEngine;
using RhythmRogue.Util.Pooling;

// ---------------------------------------------------------------------------
// 1. POOLABLE NOTE — implements IPoolable via the convenience base class
// ---------------------------------------------------------------------------

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Visual representation of a note on the highway.
    /// Pooled because dozens spawn and despawn per song.
    /// </summary>
    public class NoteView : PoolableMonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;

        private int _lane;
        private float _beatPosition;

        public void Setup(int lane, float beatPosition, Color laneColor)
        {
            _lane = lane;
            _beatPosition = beatPosition;
            _spriteRenderer.color = laneColor;
        }

        public override void OnSpawn()
        {
            // Reset any per-note state
            _beatPosition = 0f;
            _lane = 0;
        }

        public override void OnDespawn()
        {
            // Cancel any running tweens, particles, etc.
        }
    }

    // -----------------------------------------------------------------------
    // 2. CONCRETE POOL — one-liner to define a pool for NoteView
    // -----------------------------------------------------------------------

    /// <summary>
    /// Pool for note highway objects. Attach to a "NotePool" GameObject,
    /// assign the NoteView prefab, set prewarm to ~40 (enough for a dense chart).
    /// </summary>
    public class NotePool : MonoPool<NoteView> { }

    // -----------------------------------------------------------------------
    // 3. SELF-REGISTERING POOL — auto-registers with PoolRegistry
    // -----------------------------------------------------------------------

    /// <summary>
    /// Example showing how a pool can register itself so other systems
    /// (like the chart generator) don't need a direct reference.
    /// </summary>
    public class HitEffectView : PoolableMonoBehaviour
    {
        [SerializeField] private ParticleSystem _particles;

        public void Play(Vector3 position, JudgmentType judgment)
        {
            transform.position = position;
            // Configure particle color/intensity based on judgment
            _particles.Play();
        }

        public override void OnDespawn()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public enum JudgmentType { Perfect, Good, Bad, Miss }

    /// <summary>
    /// Self-registering pool. Other systems fetch it via PoolRegistry.
    /// </summary>
    public class HitEffectPool : MonoPool<HitEffectView>
    {
        protected override void Awake()
        {
            base.Awake();
            PoolRegistry.Instance.Register<HitEffectView>(this);
        }

        protected override void OnDestroy()
        {
            PoolRegistry.Instance?.Unregister<HitEffectView>();
            base.OnDestroy();
        }
    }

    // -----------------------------------------------------------------------
    // 4. CONSUMER — chart generator spawns notes via the pool
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows how a system consumes the pool through the interface.
    /// Depends on IObjectPool&lt;NoteView&gt;, not the concrete NotePool class.
    /// </summary>
    public class ChartRenderer : MonoBehaviour
    {
        // Injected via inspector or a service locator
        [SerializeField] private NotePool _notePool;

        // Alternatively, fetch from PoolRegistry at runtime:
        // private IObjectPool<NoteView> _pool;
        // void Start() => _pool = PoolRegistry.Instance.Get<NoteView>();

        private readonly Color[] _laneColors = 
        {
            Color.red,    // Left
            Color.cyan,   // Down
            Color.green,  // Up
            Color.yellow  // Right
        };

        public NoteView SpawnNote(int lane, float beatPosition)
        {
            NoteView note = _notePool.Get();
            note.Setup(lane, beatPosition, _laneColors[lane]);
            return note;
        }

        public void DespawnNote(NoteView note)
        {
            _notePool.Release(note);
        }
    }
}
