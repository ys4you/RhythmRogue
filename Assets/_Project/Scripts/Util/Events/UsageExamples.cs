// ============================================================================
// USAGE EXAMPLES — RhythmRogue Hybrid Event System
// Shows the two-layer approach:
//   1. C# events on Conductor for high-frequency beat events
//   2. EventBus for game-wide events (battle flow, UI, progression)
// ============================================================================

using System;
using UnityEngine;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Examples
{
    // -----------------------------------------------------------------------
    // 1. CONDUCTOR — C# events for per-beat timing (high frequency)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The Conductor uses plain C# events because:
    /// - Beats fire every ~333ms at 180 BPM — needs minimal overhead
    /// - Every rhythm system already knows the Conductor exists (it's a singleton)
    /// - Only 2-3 event types, all in one place
    /// - Zero allocation: no struct boxing, no dictionary lookup
    /// </summary>
    public class ConductorExample : MonoBehaviour
    {
        // C# events — subscribers hold a direct reference to Conductor
        public event Action<float> OnBeat;          // param: current beat number
        public event Action<float> OnHalfBeat;      // fires on 8th notes
        public event Action<float, float> OnBpmChanged; // oldBpm, newBpm

        private float _currentBeat;
        private float _bpm;

        private void Update()
        {
            // ... beat tracking logic using dspTime ...

            // When a beat hits:
            // OnBeat?.Invoke(_currentBeat);
        }

        public void SetBpm(float newBpm)
        {
            float old = _bpm;
            _bpm = newBpm;
            OnBpmChanged?.Invoke(old, newBpm);
        }
    }

    // -----------------------------------------------------------------------
    // 2. NOTE HIGHWAY — subscribes to Conductor C# events directly
    // -----------------------------------------------------------------------

    /// <summary>
    /// The note highway needs every single beat tick.
    /// Direct C# event subscription to the Conductor.
    /// </summary>
    public class NoteHighwayExample : MonoBehaviour
    {
        private ConductorExample _conductor;

        private void OnEnable()
        {
            _conductor = FindFirstObjectByType<ConductorExample>();
            _conductor.OnBeat += HandleBeat;
            _conductor.OnBpmChanged += HandleBpmChange;
        }

        private void OnDisable()
        {
            if (_conductor != null)
            {
                _conductor.OnBeat -= HandleBeat;
                _conductor.OnBpmChanged -= HandleBpmChange;
            }
        }

        private void HandleBeat(float beatNumber)
        {
            // Scroll notes, check for upcoming spawns
        }

        private void HandleBpmChange(float oldBpm, float newBpm)
        {
            // Adjust scroll speed
        }
    }

    // -----------------------------------------------------------------------
    // 3. HIT DETECTION — uses BOTH layers
    //    Reads from Conductor (C# event) → publishes to EventBus
    // -----------------------------------------------------------------------

    /// <summary>
    /// The bridge between the two event layers.
    /// Hit detection listens to per-beat conductor events (C# events),
    /// evaluates input, and publishes judgment results to the EventBus.
    /// This is where the high-frequency timing world meets the
    /// game-wide event world.
    /// </summary>
    public class HitDetectionExample : MonoBehaviour
    {
        private ConductorExample _conductor;
        private IEventBus _bus;

        private void OnEnable()
        {
            _conductor = FindFirstObjectByType<ConductorExample>();
            _bus = EventBusProvider.Instance.Bus;

            _conductor.OnBeat += EvaluateHits;
        }

        private void OnDisable()
        {
            if (_conductor != null)
                _conductor.OnBeat -= EvaluateHits;
        }

        private void EvaluateHits(float beatNumber)
        {
            // ... check input against expected notes ...

            // Publish judgment to the bus — combo counter, UI, effects all react
            _bus.Publish(new NoteJudgedEvent
            {
                Judgment = 0, // Perfect
                Lane = 2,
                OffsetMs = -12.5f
            });
        }
    }

    // -----------------------------------------------------------------------
    // 4. COMBO SYSTEM — EventBus subscriber and publisher
    // -----------------------------------------------------------------------

    /// <summary>
    /// Listens for note judgments on the bus, tracks combo state,
    /// publishes combo changes back to the bus.
    /// 
    /// Never touches the Conductor — fully decoupled from timing.
    /// Uses EventBindingGroup for clean lifecycle management.
    /// </summary>
    public class ComboSystemExample : MonoBehaviour
    {
        private IEventBus _bus;
        private EventBindingGroup _bindings;
        private int _currentCombo;

        private void OnEnable()
        {
            _bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings.Add<NoteJudgedEvent>(_bus, OnNoteJudged);
            _bindings.Add<BattleStartedEvent>(_bus, OnBattleStarted);
        }

        private void OnDisable()
        {
            _bindings?.Dispose();
        }

        private void OnNoteJudged(NoteJudgedEvent evt)
        {
            if (evt.Judgment == 3) // Miss
            {
                int lost = _currentCombo;
                _currentCombo = 0;

                _bus.Publish(new ComboResetEvent { LostCombo = lost });
            }
            else
            {
                _currentCombo++;
                float multiplier = Mathf.Min(1f + _currentCombo * 0.1f, 3f);

                _bus.Publish(new ComboChangedEvent
                {
                    CurrentCombo = _currentCombo,
                    Multiplier = multiplier
                });
            }
        }

        private void OnBattleStarted(BattleStartedEvent evt)
        {
            _currentCombo = 0;
        }
    }

    // -----------------------------------------------------------------------
    // 5. UI — listens to multiple EventBus events via binding group
    // -----------------------------------------------------------------------

    /// <summary>
    /// Battle HUD subscribing to several event types.
    /// Shows how EventBindingGroup makes multi-event cleanup trivial.
    /// </summary>
    public class BattleHudExample : MonoBehaviour
    {
        private EventBindingGroup _bindings;

        private void OnEnable()
        {
            var bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            _bindings
                .Add<PlayerHpChangedEvent>(bus, OnPlayerHpChanged)
                .Add<EnemyHpChangedEvent>(bus, OnEnemyHpChanged)
                .Add<ComboChangedEvent>(bus, OnComboChanged)
                .Add<ComboResetEvent>(bus, OnComboReset)
                .Add<NoteJudgedEvent>(bus, OnNoteJudged)
                .Add<CurrencyChangedEvent>(bus, OnCurrencyChanged);
        }

        private void OnDisable()
        {
            // One line cleans up all six subscriptions
            _bindings?.Dispose();
        }

        private void OnPlayerHpChanged(PlayerHpChangedEvent evt)
        {
            // Update player HP bar
        }

        private void OnEnemyHpChanged(EnemyHpChangedEvent evt)
        {
            // Update enemy HP bar
        }

        private void OnComboChanged(ComboChangedEvent evt)
        {
            // Update combo counter text and multiplier display
        }

        private void OnComboReset(ComboResetEvent evt)
        {
            // Flash combo counter red, show lost combo amount
        }

        private void OnNoteJudged(NoteJudgedEvent evt)
        {
            // Show judgment text (Perfect! Good! etc.)
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            // Update Beats counter
        }
    }

    // -----------------------------------------------------------------------
    // 6. BATTLE MANAGER — publishes flow events that many systems consume
    // -----------------------------------------------------------------------

    /// <summary>
    /// Shows how a core system publishes events without knowing
    /// who's listening. The BattleHUD, ComboSystem, RewardScreen,
    /// MapScreen, and MetaProgression all react independently.
    /// </summary>
    public class BattleManagerExample : MonoBehaviour
    {
        private IEventBus _bus;

        private void Awake()
        {
            _bus = EventBusProvider.Instance.Bus;
        }

        public void StartBattle(int enemyId, float bpm, int enemyHp)
        {
            _bus.Publish(new BattleStartedEvent
            {
                EnemyId = enemyId,
                Bpm = bpm,
                EnemyHp = enemyHp
            });
        }

        public void EndBattle(bool victory, int playerHp, float accuracy)
        {
            _bus.Publish(new BattleEndedEvent
            {
                Victory = victory,
                PlayerHpRemaining = playerHp,
                Accuracy = accuracy
            });
        }
    }
}
