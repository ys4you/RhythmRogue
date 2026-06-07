using UnityEngine;
using RhythmRogue.Data;
using RhythmRogue.Util.Pooling;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Visual representation of a note. Handles tap and hold rendering.
    /// Pooled via PoolableMonoBehaviour.
    /// </summary>
    public class NoteView : PoolableMonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private SpriteRenderer _headRenderer;
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private SpriteRenderer _tailRenderer;

        public NoteData Data { get; private set; }
        public int NoteIndex { get; private set; }
        public bool IsHit { get; set; }
        public bool IsMissed { get; set; }
        public bool IsProcessed => IsHit || IsMissed;
        public bool IsBeingHeld { get; set; }

        // +1 when downscroll (hold body/tail extend up from the head), -1 when upscroll (extend down).
        private float _holdSign = 1f;

        private static readonly Quaternion[] LaneRotations =
        {
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 0f, 180f),
            Quaternion.identity,
            Quaternion.Euler(0f, 0f, -90f)
        };

        public void Setup(NoteData data, int noteIndex, Color laneColor, float beatHeight, bool downscroll = true)
        {
            Data = data;
            NoteIndex = noteIndex;
            _holdSign = downscroll ? 1f : -1f;

            int lane = Mathf.Clamp(data.Lane, 0, LaneRotations.Length - 1);
            if (_headRenderer != null)
            {
                _headRenderer.color = laneColor;
                _headRenderer.transform.localRotation = LaneRotations[lane];
            }

            bool isHold = data.Type == NoteType.Hold;

            if (_bodyRenderer != null)
            {
                _bodyRenderer.gameObject.SetActive(isHold);
                if (isHold) { _bodyRenderer.color = laneColor; UpdateHoldBody(data.HoldDuration, beatHeight); }
            }

            if (_tailRenderer != null)
            {
                _tailRenderer.gameObject.SetActive(isHold);
                if (isHold)
                {
                    _tailRenderer.color = laneColor;
                    // Downscroll: tail above head, flip 180. Upscroll: tail below, no flip.
                    _tailRenderer.transform.localRotation = downscroll ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
                }
            }
        }

        public void UpdateHoldBody(float remainingDuration, float beatHeight)
        {
            if (_bodyRenderer == null || _tailRenderer == null) return;
            float bodyLength = remainingDuration * beatHeight;
            _bodyRenderer.transform.localScale = new Vector3(1f, bodyLength, 1f);
            _bodyRenderer.transform.localPosition = new Vector3(0f, _holdSign * bodyLength * 0.5f, 0f);
            _tailRenderer.transform.localPosition = new Vector3(0f, _holdSign * bodyLength, 0f);
        }

        /// <summary>
        /// Re-apply hold orientation when the scroll direction flips mid-battle. Sets the
        /// body/tail extension sign and the tail arrow rotation. Follow with UpdateHoldBody
        /// to re-place the body for the new direction.
        /// </summary>
        public void ReorientHold(bool downscroll)
        {
            _holdSign = downscroll ? 1f : -1f;
            if (_tailRenderer != null && Data.Type == NoteType.Hold)
                _tailRenderer.transform.localRotation = downscroll ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
        }

        public override void OnSpawn()
        {
            IsHit = false; IsMissed = false; IsBeingHeld = false;
            if (_headRenderer != null) _headRenderer.transform.localRotation = Quaternion.identity;
            if (_tailRenderer != null) _tailRenderer.transform.localRotation = Quaternion.identity;
        }

        public override void OnDespawn()
        {
            IsHit = false; IsMissed = false; IsBeingHeld = false;
            if (_bodyRenderer != null) _bodyRenderer.gameObject.SetActive(false);
            if (_tailRenderer != null) _tailRenderer.gameObject.SetActive(false);
        }
    }
}
