using UnityEngine;
using UnityEngine.UI;
using RhythmRogue.Core;

namespace RhythmRogue.UI
{
    /// <summary>
    /// A small, self-contained currency readout: a coin glyph plus the current run
    /// currency amount, anchored to the top-right corner of whatever canvas it is
    /// parented to. Subscribes to RunState.OnCurrencyChanged so it updates live when the
    /// player earns or spends, and unsubscribes when destroyed (the RunState is a
    /// ScriptableObject that outlives the scene, so leaving a dangling handler would leak).
    ///
    /// One shared widget so every screen (shop, reward, event, ...) shows currency the
    /// same way without each rebuilding its own. The coin sprite loads from
    /// Resources/HUD/currency (drop currency.png there). If it is missing, the readout
    /// falls back to "Name: amount" text so nothing breaks.
    ///
    /// Matches the map HUD's currency placement (top-right, 44px coin, gold amount) so the
    /// readout looks identical across screens.
    ///
    /// SOLID:
    ///   S - Displays the run currency amount. Nothing else.
    ///   O - New visual treatments are new factory options, not edits to callers.
    ///   D - Depends on the RunState abstraction (Currency + OnCurrencyChanged), not on any
    ///       specific screen.
    /// </summary>
    public class CurrencyReadout : MonoBehaviour
    {
        private const float Margin = 50f;
        private const float IconSize = 44f;
        private const float Gap = 10f;

        private RunState _runState;
        private Text _amountText;
        private Image _iconImage;
        private bool _subscribed;

        /// <summary>
        /// Build a top-right currency readout under <paramref name="canvas"/> and wire it to
        /// <paramref name="runState"/>. Returns null if either argument is null.
        /// </summary>
        public static CurrencyReadout Create(RectTransform canvas, RunState runState)
        {
            if (canvas == null || runState == null) return null;

            var go = new GameObject("CurrencyReadout", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400, 60);

            var readout = go.AddComponent<CurrencyReadout>();
            readout._runState = runState;
            readout.Build();
            return readout;
        }

        private void Build()
        {
            var selfRT = (RectTransform)transform;

            // Coin glyph: Inspector-free, loaded from Resources so every screen shares one
            // source. Absent sprite -> text-only fallback (handled in Refresh).
            Sprite coin = Resources.Load<Sprite>("HUD/currency");

            float textRightInset = Margin;
            if (coin != null)
            {
                var iconGO = new GameObject("Coin", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(selfRT, false);
                var iconRT = iconGO.GetComponent<RectTransform>();
                iconRT.anchorMin = iconRT.anchorMax = new Vector2(1, 1);
                iconRT.pivot = new Vector2(1, 1);
                iconRT.anchoredPosition = new Vector2(-Margin, -Margin);
                iconRT.sizeDelta = new Vector2(IconSize, IconSize);
                _iconImage = iconGO.GetComponent<Image>();
                _iconImage.sprite = coin;
                _iconImage.color = UIHelpers.OffWhite;
                _iconImage.raycastTarget = false;
                _iconImage.preserveAspect = true;
                textRightInset = Margin + IconSize + Gap;
            }

            // Amount text: right-aligned so it ends just left of the coin, vertically centred
            // against the coin (coin top at -Margin, IconSize tall).
            var textGO = new GameObject("Amount", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(selfRT, false);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = textRT.anchorMax = new Vector2(1, 1);
            textRT.pivot = new Vector2(1, 1);
            textRT.anchoredPosition = new Vector2(-textRightInset, -(Margin + 2f));
            textRT.sizeDelta = new Vector2(300, 40);
            _amountText = textGO.GetComponent<Text>();
            _amountText.font = UIHelpers.GetDefaultFont(26);
            _amountText.fontSize = 26;
            _amountText.alignment = TextAnchor.MiddleRight;
            _amountText.color = UIHelpers.WarmGold;
            _amountText.fontStyle = FontStyle.Bold;
            _amountText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _amountText.verticalOverflow = VerticalWrapMode.Overflow;

            // Subscribe here (not in OnEnable): AddComponent fires OnEnable before _runState
            // is assigned, so OnEnable would see a null reference. These readouts live for the
            // whole screen and are never toggled, so Build-subscribe + OnDestroy-unsubscribe
            // is sufficient and avoids that ordering trap.
            if (!_subscribed)
            {
                _runState.OnCurrencyChanged += Refresh;
                _subscribed = true;
            }

            Refresh(_runState.Currency, 0);
        }

        private void OnDestroy()
        {
            if (_subscribed && _runState != null)
            {
                _runState.OnCurrencyChanged -= Refresh;
                _subscribed = false;
            }
        }

        // Signature matches RunState.OnCurrencyChanged (newTotal, delta). Delta is unused for
        // now but kept so this is a valid handler and a future earn/spend tween could use it.
        private void Refresh(int newTotal, int delta)
        {
            if (_amountText == null) return;
            if (_iconImage != null)
            {
                _amountText.text = newTotal.ToString();
            }
            else
            {
                string name = _runState != null && _runState.Economy != null ? _runState.Economy.CurrencyName : "Beats";
                _amountText.text = $"{name}: {newTotal}";
            }
        }
    }
}
