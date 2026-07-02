using UnityEngine;
using RhythmRogue.Core;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// Spawns particle bursts at receptor positions on both highways.
    /// 
    /// Player highway: bursts on every judgment (via HitFeedback).
    /// Enemy highway: bursts on every auto-hit (via EnemyHighway.OnAutoHit).
    /// Milestone: larger centered burst on combo thresholds.
    /// 
    /// Creates ParticleSystems at runtime — no prefabs needed.
    /// Pixel-art friendly: small squares, short lifetime, outward burst
    /// with gravity, shrink + fade.
    /// </summary>
    public class HitParticles : MonoBehaviour
    {
        // =================================================================
        // INSPECTOR
        // =================================================================

        [Header("References")]
        [Tooltip("Player highway — particles spawn at its receptor positions.")]
        [SerializeField] private NoteHighway _playerHighway;
        [Tooltip("Enemy highway — particles spawn on auto-hit.")]
        [SerializeField] private EnemyHighway _enemyHighway;

        [Header("Particle Sprite")]
        [Tooltip("Small square sprite for particles. If null, uses Unity default.")]
        [SerializeField] private Sprite _particleSprite;

        [Header("Player Hit Burst")]
        [Tooltip("Particles emitted on Perfect.")]
        [SerializeField] private int _perfectCount = 12;
        [Tooltip("Particles emitted on Good.")]
        [SerializeField] private int _goodCount = 8;
        [Tooltip("Particles emitted on Bad.")]
        [SerializeField] private int _badCount = 4;
        [Tooltip("Particles emitted on Miss (0 = no particles).")]
        [SerializeField] private int _missCount = 0;

        [Header("Enemy Auto-Hit Burst")]
        [Tooltip("Particles emitted per enemy auto-hit.")]
        [SerializeField] private int _enemyHitCount = 6;

        [Header("Particle Physics")]
        [Tooltip("Initial outward speed of particles.")]
        [SerializeField] private float _startSpeed = 1.5f;
        [Tooltip("How quickly particles slow down and fall.")]
        [SerializeField] private float _gravityModifier = 0.8f;
        [Tooltip("Particle lifetime in seconds.")]
        [SerializeField] private float _lifetime = 0.35f;
        [Tooltip("Start size in world units.")]
        [SerializeField] private float _startSize = 0.04f;

        [Header("Combo Milestone")]
        [Tooltip("Particles emitted on combo milestone.")]
        [SerializeField] private int _milestoneCount = 24;
        [Tooltip("Milestone burst speed (faster = more explosive).")]
        [SerializeField] private float _milestoneSpeed = 2.5f;
        [Tooltip("Milestone particle lifetime.")]
        [SerializeField] private float _milestoneLifetime = 0.5f;

        // =================================================================
        // STATE
        // =================================================================

        private ParticleSystem[] _playerParticles;
        private ParticleSystem[] _enemyParticles;
        private ParticleSystem _milestoneParticles;
        private bool _initialized;

        // =================================================================
        // COLORS
        // =================================================================

        private static readonly Color PerfectColor = new(1f, 0.85f, 0f);
        private static readonly Color GoodColor = new(0.3f, 1f, 0.3f);
        private static readonly Color BadColor = new(1f, 0.6f, 0.2f);
        private static readonly Color MissColor = new(1f, 0.25f, 0.25f);
        private static readonly Color MilestoneColor = new(1f, 0.85f, 0f);

        private static readonly Color[] EnemyLaneColors =
        {
            new(1f, 0.3f, 0.3f),   // Left  — red
            new(0.3f, 0.8f, 1f),    // Down  — cyan
            new(0.3f, 1f, 0.3f),    // Up    — green
            new(1f, 1f, 0.3f)       // Right — yellow
        };

        // =================================================================
        // PUBLIC — called by HitFeedback
        // =================================================================

        /// <summary>
        /// Emit a burst of particles at a player lane receptor.
        /// Called by HitFeedback on each judgment.
        /// </summary>
        public void Burst(int lane, Judgment judgment)
        {
            if (!_initialized || _playerParticles == null) return;

            int count = judgment switch
            {
                Judgment.Perfect => _perfectCount,
                Judgment.Good    => _goodCount,
                Judgment.Bad     => _badCount,
                Judgment.Miss    => _missCount,
                _                => 0
            };

            if (count <= 0 || lane < 0 || lane >= 4) return;

            Color color = judgment switch
            {
                Judgment.Perfect => PerfectColor,
                Judgment.Good    => GoodColor,
                Judgment.Bad     => BadColor,
                _                => MissColor
            };

            var ps = _playerParticles[lane];
            var main = ps.main;
            main.startColor = color;
            ps.Emit(count);
        }

        /// <summary>
        /// Emit a large centered burst for combo milestones.
        /// Called by HitFeedback on milestone thresholds.
        /// </summary>
        public void MilestoneBurst(int combo)
        {
            if (!_initialized || _milestoneParticles == null) return;

            int count = combo >= 100 ? Mathf.RoundToInt(_milestoneCount * 1.5f) : _milestoneCount;

            var main = _milestoneParticles.main;
            main.startColor = MilestoneColor;
            _milestoneParticles.Emit(count);
        }

        // =================================================================
        // LIFECYCLE
        // =================================================================

        private void Start()
        {
            StartCoroutine(InitNextFrame());
        }

        private System.Collections.IEnumerator InitNextFrame()
        {
            // Wait one frame for highways to initialize receptors
            yield return null;

            if (_playerHighway != null && _playerHighway.LanePositions != null)
            {
                _playerParticles = CreateLaneParticles(
                    _playerHighway, "PlayerHitParticles");
            }

            if (_enemyHighway != null && _enemyHighway.LanePositions != null)
            {
                _enemyParticles = CreateLaneParticles(
                    _enemyHighway, "EnemyHitParticles");

                // Subscribe to enemy auto-hit events
                _enemyHighway.OnAutoHit += HandleEnemyAutoHit;
            }

            CreateMilestoneParticles();
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_enemyHighway != null)
                _enemyHighway.OnAutoHit -= HandleEnemyAutoHit;

            _initialized = false;
        }

        // =================================================================
        // ENEMY AUTO-HIT HANDLER
        // =================================================================

        private void HandleEnemyAutoHit(int lane, float holdSeconds)
        {
            if (!_initialized || _enemyParticles == null) return;
            if (lane < 0 || lane >= 4 || _enemyHitCount <= 0) return;

            Color color = (lane < EnemyLaneColors.Length) ? EnemyLaneColors[lane] : Color.white;

            var ps = _enemyParticles[lane];
            var main = ps.main;
            main.startColor = color;
            ps.Emit(_enemyHitCount);
        }

        // =================================================================
        // PARTICLE SYSTEM CREATION
        // =================================================================

        private ParticleSystem[] CreateLaneParticles(HighwayBase highway, string prefix)
        {
            var systems = new ParticleSystem[4];

            for (int i = 0; i < 4; i++)
            {
                float x = highway.LanePositions[i];
                float y = highway.ReceptorY;

                systems[i] = CreateParticleSystem(
                    $"{prefix}_L{i}",
                    new Vector3(x, y, 0f),
                    _startSpeed,
                    _lifetime,
                    _startSize,
                    _gravityModifier);
            }

            return systems;
        }

        private void CreateMilestoneParticles()
        {
            // Position between both highways, slightly above receptor line
            float centerX = 0f;
            float centerY = 0f;

            if (_playerHighway != null)
                centerY = _playerHighway.ReceptorY + 1f;

            _milestoneParticles = CreateParticleSystem(
                "MilestoneParticles",
                new Vector3(centerX, centerY, 0f),
                _milestoneSpeed,
                _milestoneLifetime,
                _startSize * 1.5f,
                _gravityModifier * 0.5f);

            var shape = _milestoneParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
        }

        private ParticleSystem CreateParticleSystem(
            string name, Vector3 position,
            float speed, float lifetime, float size, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Main
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission — manual bursts only
            var emission = ps.emission;
            emission.rateOverTime = 0;

            // Shape — small point burst
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;

            // Size over lifetime — shrink to 0
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.6f, 0.8f),
                    new Keyframe(1f, 0f)));

            // Color over lifetime — fade alpha
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            // Renderer
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 5;

            if (_particleSprite != null)
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = _particleSprite.texture;
                renderer.material = mat;
            }
            else
            {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            return ps;
        }
    }
}