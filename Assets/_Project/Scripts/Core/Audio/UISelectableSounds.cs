using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace RhythmRogue.Core.Audio
{
    /// <summary>
    /// Drop on any GameObject with a Selectable (Button, Slider, Toggle, InputField).
    /// Plays UiHover when focus enters and UiConfirm on Submit/Click.
    ///
    /// Use this instead of wiring sound effects manually into every onClick handler.
    /// Add via AddComponent in code (UISelectableStyle does this) or drop in editor.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class UISelectableSounds : MonoBehaviour, ISelectHandler, ISubmitHandler, IPointerEnterHandler, IPointerClickHandler
    {
        [Tooltip("If true, hover sound plays on pointer-enter as well as keyboard/gamepad selection.")]
        [SerializeField] private bool _playOnHover = true;

        public void OnSelect(BaseEventData _) => PlayHover();
        public void OnPointerEnter(PointerEventData _) { if (_playOnHover) PlayHover(); }
        public void OnSubmit(BaseEventData _) => PlayConfirm();
        public void OnPointerClick(PointerEventData _) => PlayConfirm();

        private void PlayHover()
        {
            var mgr = AudioManager.Instance;
            if (mgr != null) mgr.Play(SfxId.UiHover);
        }

        private void PlayConfirm()
        {
            var mgr = AudioManager.Instance;
            if (mgr != null && IsInteractable()) mgr.Play(SfxId.UiConfirm);
        }

        private bool IsInteractable()
        {
            var sel = GetComponent<Selectable>();
            return sel != null && sel.IsInteractable();
        }
    }
}
