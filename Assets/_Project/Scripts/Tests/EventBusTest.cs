using System;
using UnityEngine;
using RhythmRogue.Util.Events;

namespace RhythmRogue.Tests
{
    /// <summary>
    /// Interactive test for the Event Bus system.
    /// 
    /// HOW TO USE:
    /// 1. Create an empty scene
    /// 2. Add an empty GameObject, name it "EventBusProvider"
    ///    and attach the EventBusProvider component
    /// 3. Add another empty GameObject, name it "EventTest"
    ///    and attach this script
    /// 4. Hit Play and use the keyboard controls below
    /// 
    /// CONTROLS:
    ///   [B] — Start a battle (publishes BattleStartedEvent)
    ///   [1] — Hit a Perfect note
    ///   [2] — Hit a Good note
    ///   [3] — Hit a Bad note
    ///   [4] — Miss a note
    ///   [E] — End the battle (publishes BattleEndedEvent)
    ///   [H] — Take 10 damage
    ///   [R] — Heal 15 HP
    /// 
    /// Watch the Console — every event is logged by both the
    /// publisher AND the subscribers so you can see the full flow.
    /// </summary>
    public class EventBusTest : MonoBehaviour
    {
        // These simulate game state
        private bool _battleActive;
        private int _combo;
        private float _multiplier;
        private int _playerHp;
        private int _playerMaxHp = 100;
        private int _enemyHp;
        private int _enemyMaxHp = 100;
        private int _beats; // currency

        // Event bindings — cleaned up automatically
        private EventBindingGroup _bindings;
        private IEventBus _bus;

        private readonly string[] _judgmentNames = { "Perfect", "Good", "Bad", "Miss" };

        // ===================================================================
        // SETUP & TEARDOWN
        // ===================================================================

        private void OnEnable()
        {
            _bus = EventBusProvider.Instance.Bus;
            _bindings = new EventBindingGroup();

            // Subscribe to everything — each handler just logs what it received
            _bindings
                .Add<BattleStartedEvent>(_bus, OnBattleStarted)
                .Add<BattleEndedEvent>(_bus, OnBattleEnded)
                .Add<NoteJudgedEvent>(_bus, OnNoteJudged)
                .Add<ComboChangedEvent>(_bus, OnComboChanged)
                .Add<ComboResetEvent>(_bus, OnComboReset)
                .Add<PlayerHpChangedEvent>(_bus, OnPlayerHpChanged)
                .Add<EnemyHpChangedEvent>(_bus, OnEnemyHpChanged)
                .Add<CurrencyChangedEvent>(_bus, OnCurrencyChanged);

            Debug.Log("<color=white>=== Event Bus Test Ready ===</color>");
            Debug.Log("<color=white>[B] Start Battle  [1-4] Notes (Perfect/Good/Bad/Miss)</color>");
            Debug.Log("<color=white>[E] End Battle  [H] Take Damage  [R] Heal</color>");
        }

        private void OnDisable()
        {
            // One line cleans up ALL subscriptions
            _bindings?.Dispose();
            Debug.Log("<color=grey>All event bindings disposed.</color>");
        }

        // ===================================================================
        // INPUT — simulates game actions
        // ===================================================================

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.B)) SimulateStartBattle();
            if (Input.GetKeyDown(KeyCode.Alpha1)) SimulateNoteHit(0); // Perfect
            if (Input.GetKeyDown(KeyCode.Alpha2)) SimulateNoteHit(1); // Good
            if (Input.GetKeyDown(KeyCode.Alpha3)) SimulateNoteHit(2); // Bad
            if (Input.GetKeyDown(KeyCode.Alpha4)) SimulateNoteHit(3); // Miss
            if (Input.GetKeyDown(KeyCode.E)) SimulateEndBattle();
            if (Input.GetKeyDown(KeyCode.H)) SimulateTakeDamage();
            if (Input.GetKeyDown(KeyCode.R)) SimulateHeal();
        }

        // ===================================================================
        // PUBLISHERS — these simulate what real game systems would do
        // ===================================================================

        private void SimulateStartBattle()
        {
            _battleActive = true;
            _combo = 0;
            _multiplier = 1f;
            _playerHp = _playerMaxHp;
            _enemyHp = _enemyMaxHp;
            _beats = 0;

            Debug.Log("<color=yellow>--- PUBLISHING: BattleStartedEvent ---</color>");

            _bus.Publish(new BattleStartedEvent
            {
                EnemyId = 1,
                Bpm = 140f,
                EnemyHp = _enemyMaxHp
            });
        }

        private void SimulateNoteHit(int judgment)
        {
            if (!_battleActive)
            {
                Debug.Log("<color=red>No active battle! Press [B] first.</color>");
                return;
            }

            Debug.Log($"<color=yellow>--- PUBLISHING: NoteJudgedEvent ({_judgmentNames[judgment]}) ---</color>");

            // Publish the judgment — combo system and UI will react
            _bus.Publish(new NoteJudgedEvent
            {
                Judgment = judgment,
                Lane = UnityEngine.Random.Range(0, 4),
                OffsetMs = UnityEngine.Random.Range(-30f, 30f)
            });

            // Simulate damage based on judgment (like the real game would)
            int[] enemyDamage = { 5, 3, 1, 0 };
            int[] playerDamage = { 0, 0, 0, 5 };

            // Handle combo
            if (judgment == 3) // Miss
            {
                int lostCombo = _combo;
                _combo = 0;
                _multiplier = 1f;

                Debug.Log($"<color=yellow>--- PUBLISHING: ComboResetEvent (lost {lostCombo}) ---</color>");
                _bus.Publish(new ComboResetEvent { LostCombo = lostCombo });
            }
            else
            {
                _combo++;
                _multiplier = Mathf.Min(1f + _combo * 0.1f, 3f);

                Debug.Log($"<color=yellow>--- PUBLISHING: ComboChangedEvent ({_combo}x, {_multiplier:F1}x) ---</color>");
                _bus.Publish(new ComboChangedEvent
                {
                    CurrentCombo = _combo,
                    Multiplier = _multiplier
                });
            }

            // Apply damage to enemy
            int dmgToEnemy = Mathf.RoundToInt(enemyDamage[judgment] * _multiplier);
            if (dmgToEnemy > 0)
            {
                _enemyHp = Mathf.Max(0, _enemyHp - dmgToEnemy);

                Debug.Log($"<color=yellow>--- PUBLISHING: EnemyHpChangedEvent (-{dmgToEnemy}) ---</color>");
                _bus.Publish(new EnemyHpChangedEvent
                {
                    CurrentHp = _enemyHp,
                    MaxHp = _enemyMaxHp,
                    Delta = -dmgToEnemy
                });
            }

            // Apply damage to player on miss
            if (playerDamage[judgment] > 0)
            {
                _playerHp = Mathf.Max(0, _playerHp - playerDamage[judgment]);

                Debug.Log($"<color=yellow>--- PUBLISHING: PlayerHpChangedEvent (-{playerDamage[judgment]}) ---</color>");
                _bus.Publish(new PlayerHpChangedEvent
                {
                    CurrentHp = _playerHp,
                    MaxHp = _playerMaxHp,
                    Delta = -playerDamage[judgment]
                });
            }
        }

        private void SimulateEndBattle()
        {
            if (!_battleActive)
            {
                Debug.Log("<color=red>No active battle!</color>");
                return;
            }

            _battleActive = false;
            int earned = 25;
            _beats += earned;

            Debug.Log($"<color=yellow>--- PUBLISHING: CurrencyChangedEvent (+{earned}) ---</color>");
            _bus.Publish(new CurrencyChangedEvent
            {
                CurrentBeats = _beats,
                Delta = earned
            });

            Debug.Log("<color=yellow>--- PUBLISHING: BattleEndedEvent ---</color>");
            _bus.Publish(new BattleEndedEvent
            {
                Victory = _enemyHp <= 0,
                PlayerHpRemaining = _playerHp,
                Accuracy = 0.85f
            });
        }

        private void SimulateTakeDamage()
        {
            if (!_battleActive) return;

            _playerHp = Mathf.Max(0, _playerHp - 10);

            Debug.Log("<color=yellow>--- PUBLISHING: PlayerHpChangedEvent (-10) ---</color>");
            _bus.Publish(new PlayerHpChangedEvent
            {
                CurrentHp = _playerHp,
                MaxHp = _playerMaxHp,
                Delta = -10
            });
        }

        private void SimulateHeal()
        {
            if (!_battleActive) return;

            int healAmount = Mathf.Min(15, _playerMaxHp - _playerHp);
            _playerHp += healAmount;

            Debug.Log("<color=yellow>--- PUBLISHING: PlayerHpChangedEvent (+{healAmount}) ---</color>");
            _bus.Publish(new PlayerHpChangedEvent
            {
                CurrentHp = _playerHp,
                MaxHp = _playerMaxHp,
                Delta = healAmount
            });
        }

        // ===================================================================
        // SUBSCRIBERS — these simulate what UI / game systems would do
        // Each one logs in a different color so you can trace the flow
        // ===================================================================

        private void OnBattleStarted(BattleStartedEvent evt)
        {
            Debug.Log($"<color=cyan>  [BattleHUD] Battle started! Enemy #{evt.EnemyId} at {evt.Bpm} BPM, {evt.EnemyHp} HP</color>");
        }

        private void OnBattleEnded(BattleEndedEvent evt)
        {
            string result = evt.Victory ? "VICTORY" : "DEFEAT";
            Debug.Log($"<color=cyan>  [BattleHUD] Battle ended: {result} | HP left: {evt.PlayerHpRemaining} | Accuracy: {evt.Accuracy:P0}</color>");
        }

        private void OnNoteJudged(NoteJudgedEvent evt)
        {
            Debug.Log($"<color=green>  [HitEffects] Showing {_judgmentNames[evt.Judgment]} effect on lane {evt.Lane} (offset: {evt.OffsetMs:+0.0;-0.0}ms)</color>");
        }

        private void OnComboChanged(ComboChangedEvent evt)
        {
            Debug.Log($"<color=magenta>  [ComboUI] Combo: {evt.CurrentCombo} | Multiplier: {evt.Multiplier:F1}x</color>");
        }

        private void OnComboReset(ComboResetEvent evt)
        {
            Debug.Log($"<color=red>  [ComboUI] COMBO BROKEN! Lost {evt.LostCombo} combo</color>");
        }

        private void OnPlayerHpChanged(PlayerHpChangedEvent evt)
        {
            string dir = evt.Delta >= 0 ? "+" : "";
            Debug.Log($"<color=orange>  [PlayerHP] {dir}{evt.Delta} → {evt.CurrentHp}/{evt.MaxHp}</color>");
        }

        private void OnEnemyHpChanged(EnemyHpChangedEvent evt)
        {
            Debug.Log($"<color=orange>  [EnemyHP] {evt.Delta} → {evt.CurrentHp}/{evt.MaxHp}</color>");
        }

        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            Debug.Log($"<color=#FFD700>  [Wallet] +{evt.Delta} Beats (total: {evt.CurrentBeats})</color>");
        }
    }
}
