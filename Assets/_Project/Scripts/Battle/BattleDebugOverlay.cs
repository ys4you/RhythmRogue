using UnityEngine;

namespace RhythmRogue.Battle
{
    /// <summary>
    /// One-line battle debug bar. Toggle F3.
    /// </summary>
    public class BattleDebugOverlay : MonoBehaviour
    {
        [SerializeField] private BattleManager _battleManager;
        [SerializeField] private ComboSystem _comboSystem;
        [SerializeField] private EnemyHealth _enemyHealth;

        private bool _visible = true;
        private PlayerHealth _playerHealth;
        private GUIStyle _style;

        private void Awake() => _playerHealth = PlayerHealth.Instance;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3))
                _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }

            string phase = _battleManager != null ? _battleManager.CurrentPhase.ToString() : "?";
            int pH = _playerHealth != null ? _playerHealth.CurrentHP : 0;
            int pM = _playerHealth != null ? _playerHealth.MaxHP : 0;
            int eH = _enemyHealth != null ? _enemyHealth.CurrentHP : 0;
            int eM = _enemyHealth != null ? _enemyHealth.MaxHP : 0;
            int combo = _comboSystem != null ? _comboSystem.CurrentCombo : 0;
            float mult = _comboSystem != null ? _comboSystem.Multiplier : 1f;

            string text = $"<color=grey>{phase}</color>  " +
                          $"P:<color=lime>{pH}/{pM}</color>  " +
                          $"E:<color=red>{eH}/{eM}</color>  " +
                          $"C:<color=yellow>{combo}</color> x{mult:F1}";

            GUI.Label(new Rect(4, 2, 380, 16), text, _style);
        }
    }
}