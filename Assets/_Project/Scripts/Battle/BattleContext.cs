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
        private readonly BattleAnnouncer _announcer;
        private readonly NoteHighway _playerHighway;
        private readonly BattleChart _escalatedChart;
        private readonly SongBeatMap _beatMap;
        private readonly List<BeatMarker> _markerScratch = new(128);

        private int _phase;
        private bool _escalated;

        public BattleContext(Conductor conductor, EnemyHealth enemyHealth, EnemyHighway enemyHighway,
            PlayerHealth playerHealth, DifficultyContext difficulty, ISeededRandom rng, bool isBoss,
            BattleAnnouncer announcer = null, NoteHighway playerHighway = null,
            BattleChart escalatedChart = null, SongBeatMap beatMap = null)
        {
            _conductor = conductor;
            _enemyHealth = enemyHealth;
            _enemyHighway = enemyHighway;
            _playerHealth = playerHealth;
            _difficulty = difficulty;
            _rng = rng;
            _isBoss = isBoss;
            _announcer = announcer;
            _playerHighway = playerHighway;
            _escalatedChart = escalatedChart;
            _beatMap = beatMap;
        }

        public float SongBeat => _conductor != null ? _conductor.SongPositionInBeats : 0f;
        public bool IsSongPlaying => _conductor != null && _conductor.IsPlaying && !_conductor.IsPaused;
        public bool IsBoss => _isBoss;
        public DifficultyContext Difficulty => _difficulty;
        public ISeededRandom Rng => _rng;

        public int EnemyCurrentHP => _enemyHealth != null ? _enemyHealth.CurrentHP : 0;
        public int EnemyMaxHP => _enemyHealth != null ? _enemyHealth.MaxHP : 0;
        public void HealEnemy(int amount) { if (_enemyHealth != null) _enemyHealth.Heal(amount); }
        public void ReviveEnemy(int hp) { if (_enemyHealth != null) _enemyHealth.Revive(hp); }

        public int PlayerCurrentHP => _playerHealth != null ? _playerHealth.CurrentHP : 0;
        public int PlayerMaxHP => _playerHealth != null ? _playerHealth.MaxHP : 0;

        public void Announce(string text)
        {
            if (_announcer == null) return;
            _announcer.Announce(text);
        }

        public void PlaySound(UnityEngine.AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null) return;
            var mgr = RhythmRogue.Core.Audio.AudioManager.Instance;
            if (mgr != null) mgr.PlayClip(clip, volumeScale, pitch);
        }

        public int Phase => _phase;
        public int AdvancePhase() => ++_phase;

        public bool EscalateChart()
        {
            if (_escalated || _escalatedChart == null || _playerHighway == null) return false;

            // Splice in past the spawn horizon plus a beat of margin, so every note already on
            // screen finishes its approach and the player never sees a note vanish. One extra beat
            // rather than exactly the horizon, because the horizon is recomputed per frame from the
            // live scroll speed and landing exactly on it would be a race.
            float seam = SongBeat + _playerHighway.SpawnLeadBeats + 1f;

            int spliced = _playerHighway.SpliceNotesFrom(_escalatedChart.AllPlayerNotes, seam);
            if (spliced <= 0) return false;

            _escalated = true;
            RhythmRogue.Util.GameLog.Info(
                $"[BattleContext] Chart escalated at beat {SongBeat:F1}, taking effect from {seam:F1}.");
            return true;
        }

        public void SetEnemyNotes(IReadOnlyList<ModifierNote> notes)
        {
            if (_enemyHighway == null || notes == null) return;
            var stamped = new List<StampedNote>(notes.Count);
            for (int i = 0; i < notes.Count; i++)
                stamped.Add(new StampedNote(notes[i].Lane, notes[i].Beat, notes[i].HoldBeats));
            _enemyHighway.LoadNotes(stamped);
        }

        public int AddEnemyNotes(IReadOnlyList<ModifierNote> notes)
        {
            if (_enemyHighway == null || notes == null || notes.Count == 0) return 0;

            var stamped = new List<StampedNote>(notes.Count);
            for (int i = 0; i < notes.Count; i++)
                stamped.Add(new StampedNote(notes[i].Lane, notes[i].Beat, notes[i].HoldBeats));

            int added = _enemyHighway.AddNotes(stamped);
            RhythmRogue.Util.GameLog.Info(
                $"[BattleContext] Enemy counter-attack scheduled: {added}/{notes.Count} notes accepted.");
            return added;
        }

        public IReadOnlyList<SongSection> Sections =>
            _beatMap != null && _beatMap.sections != null
                ? (IReadOnlyList<SongSection>)_beatMap.sections
                : System.Array.Empty<SongSection>();

        public int GetOnsets(float fromBeat, float toBeat, List<float> into, float minIntensity = 0f)
        {
            if (_beatMap == null || into == null || toBeat <= fromBeat) return 0;

            // GetMarkersInRange clears the list it is given, which is why this uses its own scratch
            // buffer and appends: callers accumulate across several windows.
            _beatMap.GetMarkersInRange(fromBeat, toBeat, _markerScratch);

            int added = 0;
            for (int i = 0; i < _markerScratch.Count; i++)
            {
                BeatMarker m = _markerScratch[i];
                if (m.type == MarkerType.Break) continue;
                if (m.intensity < minIntensity) continue;
                into.Add(m.beat);
                added++;
            }
            return added;
        }
    }
}
