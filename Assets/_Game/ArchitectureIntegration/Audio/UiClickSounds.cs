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
    /// The click of every button: when a finger (or the mouse) goes down on an interactable button or toggle, anywhere in any scene, the UI
    /// click sound plays. Found through the EventSystem's own raycast, so no prefab needs a component and a disabled button stays silent.
    /// Lives on ProjectScope (all scenes).
    /// </summary>
    public sealed class UiClickSounds : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private EventSystem _system;
        private IAudioService _audio;

        public void Bind(IAudioService audio) => _audio = audio;

        private void Update()
        {
            var pointer = Pointer.current;
            if (_audio == null || pointer == null || !pointer.press.wasPressedThisFrame) return;
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
            if (selectable == null || !selectable.IsInteractable() || !(selectable is Button || selectable is Toggle)) return;
            try { _audio.PlayUiSound(UiSound.Click); }
            catch (InvalidOperationException) { }   // the audio is not set up yet (the loading screen): no click, nothing broken
        }
    }
}
