using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A button that acts only after it has been held for <see cref="HoldSeconds"/>: holding is the confirmation, so a deliberate action
    /// needs no confirmation popup. While the finger is down a progress visual fills; letting go early, sliding off the button or losing
    /// interactability cancels and the progress rewinds. On completion <see cref="onHoldCompleted"/> fires once and the progress stays full
    /// until the button is pressed again or becomes interactable again (the caller usually disables it while the command is in flight).
    /// With <see cref="RequireHold"/> off it is an ordinary button (<c>onClick</c>), so one prefab serves both kinds of actions.
    /// Keyboard/gamepad Submit completes at once: it is a deliberate press already.
    /// The C# events (<see cref="HoldStarted"/>, <see cref="HoldCancelled"/>, <see cref="HoldCompleted"/>) are the hook for haptics later.
    /// </summary>
    [AddComponentMenu("UI/Hold Button")]
    public class HoldButton : FeedbackButton
    {
        /// <summary>Below this a hold is too easy to trigger by accident.</summary>
        public const float MinHoldSeconds = 0.3f;

        [SerializeField] private bool requireHold = true;
        [SerializeField, Min(MinHoldSeconds)] private float holdSeconds = 1f;
        [Tooltip("How long a released, unfinished hold takes to empty again.")]
        [SerializeField, Min(0.05f)] private float rewindSeconds = 0.25f;
        [Tooltip("Optional: an Image of type Filled; its fillAmount follows the progress (round or simple shapes).")]
        [SerializeField] private Image progressFill;
        [Tooltip("Optional: a RectMask2D over a copy of the plate; its right padding uncovers it from left to right (9-sliced plates).")]
        [SerializeField] private RectMask2D progressMask;
        [SerializeField] private ButtonClickedEvent onHoldCompleted = new ButtonClickedEvent();

        public event Action HoldStarted;
        public event Action HoldCancelled;
        public event Action HoldCompleted;

        private bool _holding, _done, _wasInteractable;

        /// <summary>0..1 while holding; 1 after completion.</summary>
        public float Progress { get; private set; }

        public bool RequireHold
        {
            get => requireHold;
            set { requireHold = value; ResetProgress(); }
        }

        public float HoldSeconds
        {
            get => holdSeconds;
            set => holdSeconds = Mathf.Max(MinHoldSeconds, value);
        }

        public ButtonClickedEvent OnHoldCompleted
        {
            get => onHoldCompleted;
            set => onHoldCompleted = value;
        }

        /// <summary>Back to empty and not holding (for a button reused for another action).</summary>
        public void ResetProgress()
        {
            _holding = false;
            _done = false;
            Progress = 0f;
            ApplyProgress();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _wasInteractable = IsInteractable();
            ResetProgress();
        }

        protected override void OnDisable()
        {
            if (_holding) { _holding = false; HoldCancelled?.Invoke(); }
            base.OnDisable();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!requireHold || eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            _holding = true;
            _done = false;
            HoldStarted?.Invoke();
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            Release();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            Release();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (!requireHold) base.OnPointerClick(eventData);   // a held button never acts on a plain tap
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!requireHold) { base.OnSubmit(eventData); return; }
            if (IsActive() && IsInteractable()) { PlayPressFeedback(); Complete(); }
        }

        private void Release()
        {
            if (!_holding) return;
            _holding = false;
            HoldCancelled?.Invoke();
        }

        private void Update()
        {
            bool interactable = IsInteractable();
            if (interactable && !_wasInteractable) _done = false;   // enabled again after a command: the full bar may empty
            _wasInteractable = interactable;

            if (_holding)
            {
                if (!interactable) { Release(); }
                else
                {
                    Progress = Mathf.Min(1f, Progress + Time.unscaledDeltaTime / holdSeconds);
                    if (Progress >= 1f) Complete();
                }
            }
            else if (!_done && Progress > 0f)
            {
                Progress = Mathf.Max(0f, Progress - Time.unscaledDeltaTime / rewindSeconds);
            }
            ApplyProgress();
        }

        private void Complete()
        {
            _holding = false;
            _done = true;
            Progress = 1f;
            ApplyProgress();
            PlayClickFeedback();
            HoldCompleted?.Invoke();
            onHoldCompleted.Invoke();
        }

        private void ApplyProgress()
        {
            if (progressFill != null) progressFill.fillAmount = Progress;
            if (progressMask != null)
            {
                // Hidden while empty, so nothing shows before the layout has given the mask its width.
                progressMask.gameObject.SetActive(Progress > 0f);
                var padding = progressMask.padding;
                padding.z = progressMask.rectTransform.rect.width * (1f - Progress);
                progressMask.padding = padding;
            }
        }
    }
}
