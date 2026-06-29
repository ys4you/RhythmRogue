using System.Collections.Generic;
using RhythmRogue.Core;
using RhythmRogue.Data;
using RhythmRogue.Util.Random;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// The battle's implementation of <see cref="IBattleContext"/>: the real systems an
    /// <see cref="EnemyModifier"/> acts through, behind the narrow interface so modifiers stay
    /// decoupled from BattleManager. Built once per fight and reused for every modifier hook.
    /// Holds only references it is handed; it never creates or owns those systems.
    /// </summary>
    public sealed class BattleContext : IBattleContext
    {
        private readonly Conductor _conductor;
        private readonly EnemyHealth _enemyHealth;
        private readonly EnemyHighway _enemyHighway;
        private readonly PlayerHealth _playerHealth;
        private readonly DifficultyContext _difficulty;
        private readonly ISeededRandom _rng;
        private readonly bool _isBoss;

        public BattleContext(Conductor conductor, EnemyHealth enemyHealth, EnemyHighway enemyHighway,
            PlayerHealth playerHealth, DifficultyContext difficulty, ISeededRandom rng, bool isBoss)
        {
            _conductor = conductor;
            _enemyHealth = enemyHealth;
            _enemyHighway = enemyHighway;
            _playerHealth = playerHealth;
            _difficulty = difficulty;
            _rng = rng;
            _isBoss = isBoss;
        }

        public float SongBeat => _conductor != null ? _conductor.SongPositionInBeats : 0f;
        public bool IsSongPlaying => _conductor != null && _conductor.IsPlaying && !_conductor.IsPaused;
        public bool IsBoss => _isBoss;
        public DifficultyContext Difficulty => _difficulty;
        public ISeededRandom Rng => _rng;

        public int EnemyCurrentHP => _enemyHealth != null ? _enemyHealth.CurrentHP : 0;
        public int EnemyMaxHP => _enemyHealth != null ? _enemyHealth.MaxHP : 0;
        public void HealEnemy(int amount) { if (_enemyHealth != null) _enemyHealth.Heal(amount); }

        public int PlayerCurrentHP => _playerHealth != null ? _playerHealth.CurrentHP : 0;
        public int PlayerMaxHP => _playerHealth != null ? _playerHealth.MaxHP : 0;

        public void SetEnemyNotes(IReadOnlyList<ModifierNote> notes)
        {
            if (_enemyHighway == null || notes == null) return;
            var stamped = new List<StampedNote>(notes.Count);
            for (int i = 0; i < notes.Count; i++)
                stamped.Add(new StampedNote(notes[i].Lane, notes[i].Beat, notes[i].HoldBeats));
            _enemyHighway.LoadNotes(stamped);
        }
    }
}
