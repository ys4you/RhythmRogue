using System.Collections.Generic;
using UnityEngine;
using RhythmRogue.Util;

namespace RhythmRogue.Data.Modifiers
{
    /// <summary>
    /// The enemy fights back in bursts. During chosen sections of the song (choruses by default)
    /// it plays notes on its own highway; between them its side is quiet.
    ///
    /// What this changes is the cost of a mistake, not the speed demanded of the player. Enemy
    /// notes glance off while the guard is up, so a clean run never feels them. Miss inside a
    /// burst and the guard is down while notes are still arriving, and they land in the gap before
    /// it recovers. The fight gains a shape: quiet stretches where a slip is cheap, and named
    /// windows where it is not.
    ///
    /// Bursts sit on song sections rather than a timer so the music telegraphs them. A chorus
    /// announces itself: the player hears the danger coming without a single UI element, and hears
    /// it in the same place every time. That is what makes the pressure learnable instead of
    /// arbitrary.
    ///
    /// Notes land on the song's real onsets, never on a fixed grid, so the enemy's attacks are
    /// part of the music rather than a metronome fighting it.
    /// </summary>
    [CreateAssetMenu(fileName = "CounterAttack", menuName = "RhythmRogue/Modifiers/Counter Attack", order = 31)]
    public sealed class CounterAttackModifier : EnemyModifier
    {
        [Header("When")]
        [Tooltip("Which kind of song section the enemy attacks during. Chorus is the default " +
                 "because it is the most audible: the player hears it coming.")]
        [SerializeField] private SongSectionType _targetSection = SongSectionType.Chorus;

        [Tooltip("How many of those sections get a burst, from the start of the song. Keeps a " +
                 "long track from turning into constant pressure.")]
        [Min(1)]
        [SerializeField] private int _maxBursts = 3;

        [Tooltip("Longest a single burst may run, in beats, even if the section is longer. Caps " +
                 "the exposure window on songs with very long choruses.")]
        [Min(4f)]
        [SerializeField] private float _maxBurstBeats = 32f;

        [Tooltip("Fight phase required before any of this happens. 0 = from the start. 1 or more " +
                 "makes this a later-phase layer, e.g. only after a Last Stand revive. The phase " +
                 "is shared, so this never has to know which modifier raised it.")]
        [Min(0)]
        [SerializeField] private int _requiredPhase = 0;

        [Header("How hard")]
        [Tooltip("Enemy notes per beat inside a burst. This is the main difficulty dial. 0.5 is " +
                 "one note every two beats, which is a real threat without being a wall. Start " +
                 "low: bursts land on choruses, where the player's own chart is already densest.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _notesPerBeat = 0.5f;

        [Tooltip("Ignore musical onsets weaker than this. Higher keeps only the strong hits, so " +
                 "attacks land on beats the player can feel in the music.")]
        [Range(0f, 1f)]
        [SerializeField] private float _minOnsetIntensity = 0.45f;

        [Tooltip("Minimum beats between two enemy notes. Stops a dense cluster of onsets from " +
                 "becoming an unsurvivable burst of damage.")]
        [Min(0.25f)]
        [SerializeField] private float _minGapBeats = 1f;

        [Header("Feedback")]
        [Tooltip("Headline flashed just BEFORE each burst begins, as a warning. Fires once per " +
                 "burst, not once per fight. Empty shows nothing.")]
        [SerializeField] private string _announceText = "It strikes back";

        [Tooltip("Beats of warning before the first note of a burst lands. Two beats is enough to " +
                 "read and react without being so early the connection is lost.")]
        [Range(0f, 8f)]
        [SerializeField] private float _warningLeadBeats = 2f;

        [SerializeField] private AudioClip _sound;

        [Range(0f, 1f)]
        [SerializeField] private float _soundVolume = 0.8f;

        [Range(0.25f, 2f)]
        [SerializeField] private float _soundPitch = 1f;

        // Per-fight state, on the clone the runner makes.
        private bool _scheduled;

        // Beat each burst's first note lands on, in order, plus how many have been announced.
        // Instance state, not static: two enemies in one session must not share a cursor.
        private readonly List<float> _burstStarts = new(8);
        private int _nextBurst;

        private static readonly List<float> Onsets = new(128);
        private static readonly List<ModifierNote> Notes = new(128);

        public override void OnBattleStart(IBattleContext context)
        {
            _scheduled = false;
            _burstStarts.Clear();
            _nextBurst = 0;
            // Phase 0 means "from the start", so the notes can be laid out up front. The warnings
            // still fire later, as each burst actually arrives.
            if (_requiredPhase <= 0) Schedule(context);
        }

        public override void OnUpdate(IBattleContext context)
        {
            if (context == null) return;

            if (!_scheduled)
            {
                if (context.Phase < _requiredPhase) return;
                Schedule(context);
                return;
            }

            // Warn just before each burst. This has to happen here rather than at schedule time:
            // scheduling runs during battle setup, before the song starts, so announcing there
            // means the warning fires minutes before the notes it is warning about.
            while (_nextBurst < _burstStarts.Count &&
                   context.SongBeat >= _burstStarts[_nextBurst] - _warningLeadBeats)
            {
                context.Announce(_announceText);
                context.PlaySound(_sound, _soundVolume, _soundPitch);
                GameLog.Info($"[CounterAttack] Burst {_nextBurst + 1}/{_burstStarts.Count} incoming " +
                             $"at beat {_burstStarts[_nextBurst]:F1}.");
                _nextBurst++;
            }
        }

        /// <summary>
        /// Lay out every burst at once. Only sections that start after the playhead are used, so
        /// this is safe whether it runs at battle start or mid-song after a phase change.
        /// </summary>
        private void Schedule(IBattleContext context)
        {
            _scheduled = true;

            IReadOnlyList<SongSection> sections = context.Sections;
            if (sections == null || sections.Count == 0)
            {
                GameLog.Warn("[CounterAttack] No song sections available (authored chart?). " +
                             "No bursts scheduled.");
                return;
            }

            Notes.Clear();
            int bursts = 0;
            int lastLane = -1;

            for (int i = 0; i < sections.Count && bursts < _maxBursts; i++)
            {
                SongSection section = sections[i];
                if (section.type != _targetSection) continue;

                float from = Mathf.Max(section.startBeat, context.SongBeat);
                float to = Mathf.Min(section.endBeat, from + _maxBurstBeats);
                if (to - from < 2f) continue;

                int before = Notes.Count;
                if (BuildBurst(context, from, to, ref lastLane) > 0)
                {
                    _burstStarts.Add(Notes[before].Beat);
                    bursts++;
                }
            }

            if (Notes.Count == 0)
            {
                GameLog.Warn($"[CounterAttack] Found no usable {_targetSection} windows ahead of " +
                             $"beat {context.SongBeat:F1}. The enemy will not counter-attack.");
                return;
            }

            int added = context.AddEnemyNotes(Notes);
            if (added <= 0) return;

            GameLog.Info($"[CounterAttack] {bursts} burst(s), {added} enemy notes scheduled.");
        }

        /// <summary>
        /// Fill one burst window. Walks the window's real onsets, thins them to the requested
        /// density with an even stride so the burst does not front-load, and enforces a minimum
        /// gap. Returns how many notes it placed.
        /// </summary>
        private int BuildBurst(IBattleContext context, float from, float to, ref int lastLane)
        {
            Onsets.Clear();
            context.GetOnsets(from, to, Onsets, _minOnsetIntensity);
            if (Onsets.Count == 0) return 0;

            int wanted = Mathf.Max(1, Mathf.RoundToInt((to - from) * _notesPerBeat));
            float stride = Mathf.Max(1f, Onsets.Count / (float)wanted);

            float lastBeat = float.NegativeInfinity;
            int placed = 0;

            for (float cursor = 0f; cursor < Onsets.Count && placed < wanted; cursor += stride)
            {
                float beat = Onsets[Mathf.Min(Onsets.Count - 1, Mathf.FloorToInt(cursor))];
                if (beat - lastBeat < _minGapBeats) continue;

                Notes.Add(new ModifierNote(PickLane(context, ref lastLane), beat));
                lastBeat = beat;
                placed++;
            }

            return placed;
        }

        /// <summary>
        /// Pick a lane, never the same one twice running. Repeated lanes read as a stuck note and
        /// are also trivially easy to ignore; moving across lanes makes the attack feel deliberate.
        /// </summary>
        private static int PickLane(IBattleContext context, ref int lastLane)
        {
            int lane = context.Rng.Range(0, 4);
            if (lane == lastLane) lane = (lane + 1 + context.Rng.Range(0, 3)) % 4;
            lastLane = lane;
            return lane;
        }
    }
}
