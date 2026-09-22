using UnityEngine;
using RhythmRogue.Util;

namespace RhythmRogue.Data.Modifiers
{
    /// <summary>
    /// The enemy refuses its first death: instead of dying it revives at a fraction of max HP and
    /// the fight continues. Built for bosses, where a second wind turns a clean kill into a real
    /// final phase, but usable on any enemy.
    ///
    /// Why it is fair: the revive is capped, the enemy comes back weakened, and nothing about the
    /// player's chart changes. The player keeps their combo, their guard and their relics, so the
    /// extra HP costs time rather than removing agency.
    ///
    /// Only <see cref="OnEnemyWouldDie"/> is overridden; every other hook stays a no-op, so this
    /// modifier costs nothing per frame.
    /// </summary>
    [CreateAssetMenu(fileName = "LastStand", menuName = "RhythmRogue/Modifiers/Last Stand", order = 30)]
    public sealed class LastStandModifier : EnemyModifier
    {
        [Header("Revive")]
        [Tooltip("Fraction of MAX HP the enemy comes back with. Read against max, not the HP it " +
                 "had, so the second phase is the same length whatever killed it. Keep this low: " +
                 "the boss already has the song's remaining time working against the player, and " +
                 "an over-generous revive turns a won fight into a timeout loss.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float _reviveHPPercent = 0.3f;

        [Tooltip("How many deaths this modifier may refuse in one fight. 1 is the boss standard. " +
                 "Two or more reads as the fight not respecting the player's damage.")]
        [Min(1)]
        [SerializeField] private int _maxRevives = 1;

        [Header("Gating")]
        [Tooltip("Only trigger on a boss node. On when this asset sits on a boss; turn it off to " +
                 "put a last stand on an elite or a normal enemy.")]
        [SerializeField] private bool _bossOnly = true;

        [Tooltip("Skip the revive when the player is below this fraction of THEIR max HP. A last " +
                 "stand against a nearly-dead player usually just converts their win into a loss " +
                 "they could not have prevented. 0 disables the mercy check.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _skipBelowPlayerHPPercent = 0.15f;

        [Header("Feedback")]
        [Tooltip("Headline flashed over the fight when the death is refused. Keep it short, it is " +
                 "drawn large and uppercased. Empty shows no banner.")]
        [SerializeField] private string _announceText = "It refuses to die";

        [Tooltip("One-shot sting played on the revive. Optional, but without it the revive reads " +
                 "as a bug rather than a moment. Anything low and ugly works; try a glitch or a " +
                 "bell from ThirdParty/Audio.")]
        [SerializeField] private AudioClip _reviveSound;

        [Range(0f, 1f)]
        [SerializeField] private float _reviveSoundVolume = 1f;

        [Tooltip("Playback pitch. Below 1 drops and lengthens the clip, which is how a short, bright " +
                 "UI blip becomes a heavy low sting without an audio editor. 0.5 is one octave down.")]
        [Range(0.25f, 2f)]
        [SerializeField] private float _reviveSoundPitch = 1f;

        [Header("Phase 2 difficulty")]
        [Tooltip("Added to the fight's 0-1 chart difficulty when the boss revives, so the second " +
                 "phase is genuinely denser and faster to play, not just more HP. The battle " +
                 "assembles that chart during the intro and splices it in past the spawn horizon, " +
                 "so no note on screen is disturbed.\n\n" +
                 "0.15 to 0.25 is a real step up without becoming a wall. 0 disables it and the " +
                 "fight keeps its opening chart, which also saves the second assemble entirely.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _phaseTwoDifficulty = 0.2f;

        public override float ChartEscalation => _phaseTwoDifficulty;

        // Per-fight state. Lives on the clone the EnemyModifierRunner makes, so the shared asset is
        // never mutated and two fights never share a count.
        private int _revivesUsed;

        public override void OnBattleStart(IBattleContext context) => _revivesUsed = 0;

        public override bool OnEnemyWouldDie(IBattleContext context)
        {
            if (context == null) return false;
            if (_bossOnly && !context.IsBoss) return false;
            if (_revivesUsed >= _maxRevives) return false;

            if (_skipBelowPlayerHPPercent > 0f && context.PlayerMaxHP > 0)
            {
                float playerPercent = context.PlayerCurrentHP / (float)context.PlayerMaxHP;
                if (playerPercent < _skipBelowPlayerHPPercent) return false;
            }

            int reviveHP = Mathf.Max(1, Mathf.RoundToInt(context.EnemyMaxHP * _reviveHPPercent));
            context.ReviveEnemy(reviveHP);

            // Defensive: if the revive did not take (no enemy health wired, or the pool refused),
            // let the death stand. Consuming a death we could not undo would leave the enemy at 0
            // HP and flagged dead while the battle waits for an end that never comes.
            if (context.EnemyCurrentHP <= 0) return false;

            _revivesUsed++;

            // Phase 2. The phase number is shared state any other modifier can read, so a future
            // counter-attack or lane trick can wake up here without either knowing the other.
            context.AdvancePhase();

            // The chart itself gets denser for the rest of the song. Silently a no-op when this
            // modifier declared no escalation or the fight cannot support one.
            context.EscalateChart();

            // Tell the player what just happened. Without this the HP bar simply refills, which
            // reads as a bug: the revive has to be legible as a deliberate move by the boss.
            context.Announce(_announceText);
            context.PlaySound(_reviveSound, _reviveSoundVolume, _reviveSoundPitch);

            GameLog.Info($"[LastStand] Death refused ({_revivesUsed}/{_maxRevives}), phase {context.Phase}. " +
                         $"Enemy back at {context.EnemyCurrentHP}/{context.EnemyMaxHP} HP.");
            return true;
        }
    }
}
