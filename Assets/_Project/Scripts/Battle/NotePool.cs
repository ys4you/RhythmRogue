using RhythmRogue.Util.Pooling;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Pool for note highway objects.
    /// 
    /// Attach to a "NotePool" GameObject in the battle scene.
    /// Assign the NoteView prefab in the inspector and set prewarm
    /// count to ~40 (enough for a dense chart's visible window).
    /// 
    /// Extends MonoPool&lt;T&gt; from Util — handles instantiation,
    /// IPoolable lifecycle, and inspector configuration automatically.
    /// </summary>
    public class NotePool : MonoPool<NoteView> { }
}
