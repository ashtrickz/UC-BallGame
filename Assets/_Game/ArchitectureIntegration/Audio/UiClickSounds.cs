using System;
using System.Collections.Generic;
using Slime.Audio;
using Slime.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Slime.Infrastructure.Audio
{
    /// <summary>Plays toggle and slider feedback from the current EventSystem raycast.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Audio", sourceAssembly: "Game.Services", sourceClassName: "UiClickSounds")]
    public sealed class UiClickSounds : MonoBehaviour, ITickRegistryClient
    {
        private const float SliderTickStep = 0.04f;
        private ITickRegistry _ticks;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private EventSystem _system;
        private IAudioService _audio;
        private IHaptics _haptics;
        private Slider _slider;
        private float _sliderValue;

        public void BindTicks(ITickRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            if (ReferenceEquals(_ticks, registry))
                return;

            _ticks?.Unregister(this);
            _ticks = registry;
            if (isActiveAndEnabled)
                _ticks.Register(this);
        }

        public void Bind(IAudioService audio, IHaptics haptics)
        {
            _audio = audio;
            _haptics = haptics;
        }

        private void OnEnable() => _ticks?.Register(this);
        private void OnDisable() => _ticks?.Unregister(this);
        private void OnDestroy() => _ticks?.Unregister(this);

        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return;

            var pointer = Pointer.current;
            if (_audio == null || pointer == null)
                return;

            if (!pointer.press.isPressed)
                _slider = null;
            if (_slider != null && Mathf.Abs(_slider.normalizedValue - _sliderValue) >= SliderTickStep)
            {
                _sliderValue = _slider.normalizedValue;
                try
                {
                    _audio.PlaySound(GameSound.UiSliderTick);
                }
                catch (InvalidOperationException) { }
            }
            if (!pointer.press.wasPressedThisFrame)
                return;

            var system = EventSystem.current;
            if (system == null)
                return;

            if (_pointer == null || _system != system)
            {
                _pointer = new PointerEventData(system);
                _system = system;
            }
            _pointer.position = pointer.position.ReadValue();
            _hits.Clear();
            system.RaycastAll(_pointer, _hits);
            if (_hits.Count == 0)
                return;

            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_hits[0].gameObject)
                          ?? ExecuteEvents.GetEventHandler<IPointerDownHandler>(_hits[0].gameObject);
            var selectable = handler != null ? handler.GetComponent<Selectable>() : null;
            if (selectable is Slider slider && slider.IsInteractable())
            {
                _slider = slider;
                _sliderValue = slider.normalizedValue;
                return;
            }
            if (!(selectable is Toggle toggle) || !toggle.IsInteractable())
                return;

            try
            {
                _audio.PlaySound(toggle.isOn ? GameSound.UiToggleOff : GameSound.UiToggleOn);
                _haptics.Play(Slime.Audio.HapticKind.Tap);
            }
            catch (InvalidOperationException) { } // the audio is not set up yet (the loading screen): no click, nothing broken
        }
    }
}
