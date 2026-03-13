// ============================================================================
// USAGE EXAMPLES — Timer / Cooldown System
// 
// These are NOT part of the utility. They show how a CONSUMER
// uses timers for gameplay scenarios.
// ============================================================================

using UnityEngine;
using RhythmRogue.Util.Timing;
using RhythmRogue.Util.Pooling;

namespace MyGame.Examples
{
    // -----------------------------------------------------------------------
    // 1. HIT EFFECTS — auto-return to pool after a duration
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawns a hit effect and returns it to the pool after a delay.
    /// One-liner with TimerService.Delay.
    /// </summary>
    public class HitEffectSpawnerExample : MonoBehaviour
    {
        [SerializeField] private float _effectLifetime = 0.5f;

        public void SpawnEffect(/* IObjectPool<HitEffect> pool, */ Vector3 position)
        {
            // var effect = pool.Get();
            // effect.transform.position = position;
            // effect.Play();

            // Auto-return to pool after lifetime expires
            // TimerService.Delay(_effectLifetime, () => pool.Release(effect));
        }
    }

    // -----------------------------------------------------------------------
    // 2. BATTLE INTRO COUNTDOWN — 3... 2... 1... GO!
    // -----------------------------------------------------------------------

    public class BattleIntroExample : MonoBehaviour
    {
        public void StartCountdown()
        {
            // Looping timer that fires every second
            int count = 3;
            ITimer countdown = TimerService.Every(1f, () =>
            {
                if (count > 0)
                {
                    Debug.Log($"{count}...");
                    count--;
                }
                else
                {
                    Debug.Log("GO!");
                    // countdown.Cancel() is called by the reference below
                }
            });

            // After 3 seconds, cancel the loop and start gameplay
            TimerService.Delay(3.5f, () =>
            {
                countdown.Cancel();
                // TransitionTo(BattlePhase.Playing);
            });
        }
    }

    // -----------------------------------------------------------------------
    // 3. REST NODE — heal over time with progress bar
    // -----------------------------------------------------------------------

    public class RestNodeExample : MonoBehaviour
    {
        [SerializeField] private float _healDuration = 2f;
        [SerializeField] private int _healAmount = 30;

        public void BeginHeal(/* PlayerHealth health */)
        {
            int healed = 0;

            TimerService.Lerp(_healDuration,
                // Per-frame progress: update the heal bar UI
                progress =>
                {
                    int target = Mathf.RoundToInt(_healAmount * progress);
                    int delta = target - healed;
                    if (delta > 0)
                    {
                        // health.Heal(delta);
                        healed = target;
                    }
                    // Update UI fill: healBar.fillAmount = progress;
                },
                // On complete: show "healed!" feedback
                () =>
                {
                    Debug.Log($"Healed {_healAmount} HP!");
                }
            );
        }
    }

    // -----------------------------------------------------------------------
    // 4. COOLDOWN — ability usage with ready/not-ready check
    // -----------------------------------------------------------------------

    public class AbilityExample : MonoBehaviour
    {
        private Cooldown _specialAbility;

        private void Start()
        {
            // 5 second cooldown, starts ready
            _specialAbility = new Cooldown(5f);

            // Optional: get notified when it becomes ready again
            _specialAbility.OnReady += () =>
            {
                Debug.Log("Special ability ready!");
            };
        }

        private void Update()
        {
            // Tick the cooldown
            _specialAbility.Update(Time.deltaTime);

            // Try to use it
            if (Input.GetKeyDown(KeyCode.Q))
            {
                if (_specialAbility.TryUse())
                {
                    Debug.Log("Special ability used!");
                }
                else
                {
                    Debug.Log($"On cooldown: {_specialAbility.Remaining:F1}s remaining");
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // 5. GLOBAL PAUSE — all gameplay timers freeze during pause menu
    // -----------------------------------------------------------------------

    public class PauseExample : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var timers = TimerService.Instance.Scaled;

                if (timers.IsGloballyPaused)
                {
                    timers.ResumeAll();
                    Time.timeScale = 1f;
                }
                else
                {
                    timers.PauseAll();
                    Time.timeScale = 0f;

                    // UI timers keep running during pause
                    TimerService.DelayUnscaled(0.3f, () =>
                    {
                        // Show pause menu with fade-in
                    });
                }
            }
        }
    }
}
