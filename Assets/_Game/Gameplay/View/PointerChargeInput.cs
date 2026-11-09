using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Slime.Gameplay.View
{
    /// <summary>
    /// Press-and-release reader for the charge, independent of UI <c>HoldButton</c>. One active pointer: the primary
    /// touch on devices, the left mouse button in the Editor. Presses that start over UI are ignored until released.
    /// After a pause or focus loss the pointer must be lifted and pressed again: an old release never fires a shot.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.View", sourceAssembly: "Game.Gameplay.View", sourceClassName: "PointerChargeInput")]
    public sealed class PointerChargeInput : MonoBehaviour
    {
        private enum State { Idle, Held, IgnoredUntilRelease }

        private State _state = State.IgnoredUntilRelease;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _eventData;
        private bool? _virtualDown;
        private bool _virtualWasDown;

        public bool Enabled { get; set; }
        public bool PressedThisFrame { get; private set; }
        public bool IsHeld => _state == State.Held;

        public void SetVirtualPointer(bool? down)
        {
            _virtualDown = down;
            if (down == null)
                _virtualWasDown = false;
        }

        public void RequireFreshPress()
        {
            _state = State.IgnoredUntilRelease;
            PressedThisFrame = false;
        }

        public void Poll()
        {
            PressedThisFrame = false;
            bool down, pressedNow;
            Vector2 position;
            if (_virtualDown.HasValue)
            {
                down = _virtualDown.Value;
                pressedNow = down && !_virtualWasDown;
                _virtualWasDown = down;
                position = new Vector2(-1, -1); // off-screen: never over UI
            }
            else
                down = ReadPointer(out position, out pressedNow);
            switch (_state)
            {
                case State.IgnoredUntilRelease:
                    if (!down)
                        _state = State.Idle;
                    break;
                case State.Idle:
                    if (!pressedNow)
                        break;

                    if (!Enabled || IsOverUi(position))
                    {
                        _state = down ? State.IgnoredUntilRelease : State.Idle;
                        break;
                    }
                    // A tap may press and release within one frame: report the press with no hold (minimum shot).
                    _state = down ? State.Held : State.Idle;
                    PressedThisFrame = true;
                    break;
                case State.Held:
                    if (!down)
                        _state = State.Idle;
                    break;
            }
        }

        private static bool ReadPointer(out Vector2 position, out bool pressedThisFrame)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                var primary = touch.primaryTouch;
                if (primary.press.isPressed || primary.press.wasReleasedThisFrame)
                {
                    position = primary.position.ReadValue();
                    pressedThisFrame = primary.press.wasPressedThisFrame;
                    return primary.press.isPressed;
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
                return mouse.leftButton.isPressed;
            }
            position = default;
            pressedThisFrame = false;
            return false;
        }

        private bool IsOverUi(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null || position.x < 0)
                return false;

            _eventData ??= new PointerEventData(system);
            _eventData.Reset();
            _eventData.position = position;
            _hits.Clear();
            system.RaycastAll(_eventData, _hits);
            return _hits.Count > 0;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                RequireFreshPress();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused)
                RequireFreshPress();
        }
    }
}
