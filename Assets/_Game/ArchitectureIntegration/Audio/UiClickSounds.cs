using System;
using System.Collections.Generic;
using Game.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Integration.Audio
{
    /// <summary>
    /// Sounds for toggles and sliders, found through the EventSystem's own raycast.
    /// Buttons own their feedback through Game.UI.FeedbackButton and DI.
    /// Lives on ProjectScope (all scenes).
    /// </summary>
    public sealed class UiClickSounds : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private EventSystem _system;
        private IAudioService _audio;
        private Slider _slider;
        private float _sliderValue;

        public void Bind(IAudioService audio) => _audio = audio;

        private void Update()
        {
            var pointer = Pointer.current;
            if (_audio == null || pointer == null) return;
            if (!pointer.press.isPressed) _slider = null;
            if (_slider != null && Mathf.Abs(_slider.normalizedValue - _sliderValue) >= 0.04f)
            {
                _sliderValue = _slider.normalizedValue;
                try { _audio.PlaySound(GameSound.UiSliderTick); }
                catch (InvalidOperationException) { }
            }
            if (!pointer.press.wasPressedThisFrame) return;
            var system = EventSystem.current;
            if (system == null) return;
            if (_pointer == null || _system != system) { _pointer = new PointerEventData(system); _system = system; }
            _pointer.position = pointer.position.ReadValue();
            _hits.Clear();
            system.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0) return;
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_hits[0].gameObject)
                          ?? ExecuteEvents.GetEventHandler<IPointerDownHandler>(_hits[0].gameObject);
            var selectable = handler != null ? handler.GetComponent<Selectable>() : null;
            if (selectable is Slider slider && slider.IsInteractable())
            {
                _slider = slider;
                _sliderValue = slider.normalizedValue;
                return;
            }
            if (!(selectable is Toggle toggle) || !toggle.IsInteractable()) return;
            try
            {
                _audio.PlaySound(toggle.isOn ? GameSound.UiToggleOff : GameSound.UiToggleOn);
            }
            catch (InvalidOperationException) { }   // the audio is not set up yet (the loading screen): no click, nothing broken
        }
    }
}
