using UnityEngine;
using RhythmRogue.Core;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Makes a sprite pulse on the beat, the small constant motion that makes a rhythm scene feel
    /// alive instead of static (the characters bobbing in FNF do exactly this). It listens to the
    /// Conductor's beat and gives the transform a quick scale "pop" that eases back to rest, so the
    /// enemy breathes in time with the song. Purely visual, with no gameplay effect.
    ///
    /// It records the transform's authored local scale as the rest pose, so it layers on top of
    /// whatever scale the sprite was placed at and returns to it exactly (sign preserved, so a
    /// flipped sprite still works). The pop only happens while the song is playing, since the
    /// Conductor only fires beats then; the sprite sits still during the intro and after the fight.
    /// </summary>
    public class BeatBob : MonoBehaviour
    {
        [Tooltip("How much the sprite grows at the peak of a beat. 0.08 = 8% bigger.")]
        [SerializeField, Range(0f, 0.5f)] private float _punch = 0.08f;

        [Tooltip("Extra horizontal squash on the beat for a springier landing (0 = uniform pop).")]
        [SerializeField, Range(0f, 0.5f)] private float _squashX = 0.03f;

        [Tooltip("How quickly the pop eases back to rest. Higher is snappier.")]
        [SerializeField] private float _settleSpeed = 10f;

        [Tooltip("Pulse on every half-beat instead of every beat, for a busier feel.")]
        [SerializeField] private bool _onHalfBeat = false;

        private Conductor _conductor;
        private Vector3 _restScale = Vector3.one;
        private float _bob; // 1 right after a beat, eases back to 0 at rest
        private bool _subscribed;

        private void Awake() => _restScale = transform.localScale;

        private void OnEnable() => TrySubscribe();
        private void Start() => TrySubscribe();

        private void TrySubscribe()
        {
            if (_subscribed) return;
            _conductor = Conductor.Instance;
            if (_conductor == null) return;
            if (_onHalfBeat) _conductor.OnHalfBeat += HandleBeat;
            else _conductor.OnBeat += HandleBeat;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || _conductor == null) return;
            if (_onHalfBeat) _conductor.OnHalfBeat -= HandleBeat;
            else _conductor.OnBeat -= HandleBeat;
            _subscribed = false;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void HandleBeat(int _) => _bob = 1f;

        private void Update()
        {
            // The conductor is created early in the battle, but guard in case this enabled first.
            if (!_subscribed) TrySubscribe();

            if (_bob > 0f)
            {
                _bob -= _bob * Mathf.Min(1f, Time.deltaTime * _settleSpeed);
                if (_bob < 0.001f) _bob = 0f;
            }

            float grow = _punch * _bob;
            transform.localScale = new Vector3(
                _restScale.x * (1f + grow - _squashX * _bob),
                _restScale.y * (1f + grow),
                _restScale.z);
        }
    }
}
