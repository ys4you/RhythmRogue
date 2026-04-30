using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Core;
using RhythmRogue.Data;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Abstract base for note highways. Handles receptors, spawning, scrolling, despawning.
    /// Lane positions and note scale are read from receptor transforms at Awake.
    /// </summary>
    public abstract class HighwayBase : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] protected float _receptorY = -3f;

        [Header("Scrolling")]
        [SerializeField] protected float _beatHeight = 2f;
        [SerializeField] protected float _beatsShownInAdvance = 8f;
        [SerializeField] protected float _beatsDespawnBehind = 2f;
        [SerializeField] protected bool _downscroll = true;

        [Header("Lane Colors")]
        [SerializeField] protected Color[] _laneColors =
        {
            new Color(1f, 0.3f, 0.3f),
            new Color(0.3f, 0.8f, 1f),
            new Color(0.3f, 1f, 0.3f),
            new Color(1f, 1f, 0.3f)
        };

        [Header("References")]
        [SerializeField] protected NotePool _notePool;

        [Header("Receptors")]
        [SerializeField] protected SpriteRenderer[] _receptors;
        [SerializeField] protected Sprite _receptorIdleSprite;
        [SerializeField] protected Sprite _receptorPressedSprite;

        [Header("Auto-Generation Fallback")]
        [SerializeField] protected float[] _fallbackLanePositions = { -1.5f, -0.5f, 0.5f, 1.5f };
        [SerializeField] protected float _fallbackScale = 0.3f;

        protected float[] _laneX;
        protected float _noteScale;
        protected Conductor _conductor;

        protected float EffectiveBeatHeight => _beatHeight * ScrollSpeedSetting.Multiplier;

        public IReadOnlyList<float> LanePositions => _laneX;
        public float ReceptorY => _receptorY;
        public IReadOnlyList<SpriteRenderer> Receptors => _receptors;

        protected static readonly Quaternion[] LaneRotations =
        {
            Quaternion.Euler(0f, 0f, 90f),
            Quaternion.Euler(0f, 0f, 180f),
            Quaternion.identity,
            Quaternion.Euler(0f, 0f, -90f)
        };

        protected virtual void Awake()
        {
            _conductor = Conductor.Instance;
            EnsureReceptors();
            ReadLaneDataFromReceptors();
        }

        private void EnsureReceptors()
        {
            if (HasPrePlacedReceptors()) return;
            if (_receptorIdleSprite == null) return;

            _receptors = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject($"{GetReceptorPrefix()}_L{i}");
                go.transform.SetParent(transform);
                float x = i < _fallbackLanePositions.Length ? _fallbackLanePositions[i] : i;
                go.transform.position = new Vector3(x, _receptorY, 0f);
                go.transform.localRotation = LaneRotations[i];
                go.transform.localScale = Vector3.one * _fallbackScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _receptorIdleSprite;
                sr.color = i < _laneColors.Length ? _laneColors[i] : Color.white;
                sr.sortingOrder = 1;
                _receptors[i] = sr;
            }
        }

        private bool HasPrePlacedReceptors()
        {
            if (_receptors == null || _receptors.Length < 4) return false;
            for (int i = 0; i < 4; i++) if (_receptors[i] == null) return false;
            return true;
        }

        private void ReadLaneDataFromReceptors()
        {
            _laneX = new float[4];
            _noteScale = 1f;
            if (_receptors == null || _receptors.Length < 4) return;

            for (int i = 0; i < 4; i++)
                if (_receptors[i] != null) _laneX[i] = _receptors[i].transform.position.x;

            if (_receptors[0] != null)
            {
                _receptorY = _receptors[0].transform.position.y;
                _noteScale = _receptors[0].transform.lossyScale.x;
            }
        }

        protected virtual string GetReceptorPrefix() => "Receptor";

        protected void PositionNote(NoteView note, float currentBeat)
        {
            float distanceInBeats = note.Data.BeatPosition - currentBeat;
            int lane = Mathf.Clamp(note.Data.Lane, 0, _laneX.Length - 1);
            note.transform.position = new Vector3(_laneX[lane], _receptorY + distanceInBeats * EffectiveBeatHeight, 0f);
        }

        protected NoteView SpawnNoteView(NoteData noteData, int noteIndex, float currentBeat)
        {
            NoteView view = _notePool.Get();
            int lane = Mathf.Clamp(noteData.Lane, 0, _laneColors.Length - 1);
            view.Setup(noteData, noteIndex, _laneColors[lane], EffectiveBeatHeight, _downscroll);
            view.transform.localScale = Vector3.one * _noteScale;
            PositionNote(view, currentBeat);
            return view;
        }

        protected void ReturnNoteView(NoteView view)
        {
            if (view != null && _notePool != null) _notePool.Release(view);
        }
    }
}
