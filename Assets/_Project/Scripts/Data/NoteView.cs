using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util.Pooling;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Visual representation of a single note on the highway.
    /// 
    /// Handles both tap and hold notes. For holds, the body and tail
    /// are child objects that stretch based on hold duration.
    /// 
    /// Extends PoolableMonoBehaviour for object pooling — dozens of
    /// notes spawn and despawn per song. OnSpawn/OnDespawn reset
    /// visual state cleanly.
    /// 
    /// This component owns visual state only. Timing data comes from
    /// NoteData (immutable). Hit/miss state is tracked here because
    /// it's per-instance runtime state that the pool needs to reset.
    /// </summary>
    public class NoteView : PoolableMonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private SpriteRenderer _headRenderer;
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private SpriteRenderer _tailRenderer;

        // -----------------------------------------------------------------
        // NOTE DATA — set by highway on spawn
        // -----------------------------------------------------------------

        /// <summary>Immutable note data from the chart.</summary>
        public NoteData Data { get; private set; }

        /// <summary>Index into the chart's note list for quick lookup.</summary>
        public int NoteIndex { get; private set; }

        // -----------------------------------------------------------------
        // RUNTIME STATE — mutable, reset on pool recycle
        // -----------------------------------------------------------------

        /// <summary>Whether this note has been successfully hit.</summary>
        public bool IsHit { get; set; }

        /// <summary>Whether this note has been auto-judged as a miss.</summary>
        public bool IsMissed { get; set; }

        /// <summary>Whether this note is done and ready for despawn.</summary>
        public bool IsProcessed => IsHit || IsMissed;

        /// <summary>For hold notes: whether the player is currently holding.</summary>
        public bool IsBeingHeld { get; set; }

        // -----------------------------------------------------------------
        // SETUP — called by NoteHighway after getting from pool
        // -----------------------------------------------------------------

        /// <summary>
        /// Initialize the note with chart data and lane color.
        /// Called by NoteHighway immediately after pool.Get().
        /// </summary>
        /// <param name="data">Immutable note data from the chart.</param>
        /// <param name="noteIndex">Index in the chart's note list.</param>
        /// <param name="laneColor">Color for this lane.</param>
        /// <param name="beatHeight">World units per beat (for hold body scaling).</param>
        public void Setup(NoteData data, int noteIndex, Color laneColor, float beatHeight)
        {
            Data = data;
            NoteIndex = noteIndex;

            // Head color
            if (_headRenderer != null)
                _headRenderer.color = laneColor;

            // Hold note body and tail
            bool isHold = data.Type == NoteType.Hold;

            if (_bodyRenderer != null)
            {
                _bodyRenderer.gameObject.SetActive(isHold);

                if (isHold)
                {
                    _bodyRenderer.color = laneColor;
                    UpdateHoldBody(data.HoldDuration, beatHeight);
                }
            }

            if (_tailRenderer != null)
            {
                _tailRenderer.gameObject.SetActive(isHold);

                if (isHold)
                    _tailRenderer.color = laneColor;
            }
        }

        // -----------------------------------------------------------------
        // HOLD NOTE VISUALS
        // -----------------------------------------------------------------

        /// <summary>
        /// Update the hold note body length.
        /// Called each frame while the note is visible, and during setup.
        /// 
        /// The body stretches from the head position upward (toward incoming
        /// notes) by holdDuration × beatHeight world units.
        /// </summary>
        /// <param name="remainingDuration">Remaining hold duration in beats.</param>
        /// <param name="beatHeight">World units per beat.</param>
        public void UpdateHoldBody(float remainingDuration, float beatHeight)
        {
            if (_bodyRenderer == null || _tailRenderer == null)
                return;

            float bodyLength = remainingDuration * beatHeight;

            // Scale body to fill the distance
            // Body is assumed to be a 1-unit tall sprite scaled on Y
            _bodyRenderer.transform.localScale = new Vector3(1f, bodyLength, 1f);

            // Position body center between head and tail
            _bodyRenderer.transform.localPosition = new Vector3(0f, bodyLength * 0.5f, 0f);

            // Position tail at the end of the body
            _tailRenderer.transform.localPosition = new Vector3(0f, bodyLength, 0f);
        }

        // -----------------------------------------------------------------
        // POOL LIFECYCLE
        // -----------------------------------------------------------------

        /// <summary>
        /// Reset all per-note state when retrieved from pool.
        /// </summary>
        public override void OnSpawn()
        {
            IsHit = false;
            IsMissed = false;
            IsBeingHeld = false;
        }

        /// <summary>
        /// Clean up when returned to pool.
        /// </summary>
        public override void OnDespawn()
        {
            IsHit = false;
            IsMissed = false;
            IsBeingHeld = false;

            if (_bodyRenderer != null)
                _bodyRenderer.gameObject.SetActive(false);

            if (_tailRenderer != null)
                _tailRenderer.gameObject.SetActive(false);
        }
    }
}
