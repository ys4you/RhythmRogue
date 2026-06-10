#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Battle;

namespace RhythmRogue.DevTools.Console
{
    /// <summary>
    /// Lazily resolves the shared game state the console commands act on, so each command does not
    /// repeat the lookups. Injected into commands at construction (dependency injection), which
    /// keeps the Util console framework free of any game knowledge.
    ///
    /// RunState and RelicPool are ScriptableObjects, found among loaded assets. PlayerHealth and
    /// EnemyHealth are scene objects, found non-destructively via FindFirstObjectByType (never via
    /// the auto-creating Singleton.Instance, which would spawn a stray PlayerHealth in scenes that
    /// have no battle). Anything absent returns null and the commands report that.
    /// </summary>
    public class DevConsoleContext
    {
        private RunState _runState;
        private RelicPool _relicPool;

        public RunState RunState
        {
            get
            {
                if (_runState == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<RunState>();
                    if (all != null && all.Length > 0) _runState = all[0];
                }
                return _runState;
            }
        }

        public RelicPool RelicPool
        {
            get
            {
                if (_relicPool == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<RelicPool>();
                    if (all != null && all.Length > 0) _relicPool = all[0];
                }
                return _relicPool;
            }
        }

        // Scene-scoped; resolved fresh each access since they change per battle / scene load.
        public PlayerHealth Player => Object.FindFirstObjectByType<PlayerHealth>();
        public EnemyHealth Enemy => Object.FindFirstObjectByType<EnemyHealth>();
    }
}
#endif
