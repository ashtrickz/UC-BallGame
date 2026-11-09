using System;
using Slime.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slime.UI
{
    /// <summary>Confirms an action after an unscaled hold and rewinds cancelled progress.</summary>
    [AddComponentMenu("UI/Hold Button")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.UI", sourceAssembly: "Game.UI", sourceClassName: "HoldButton")]
    public class HoldButton : FeedbackButton, ITickRegistryClient
    {
        public const float MinHoldSeconds = 0.3f;
        private const float DefaultHoldSeconds = 1f;
        private const float DefaultRewindSeconds = 0.25f;
        private const float MinimumRewindSeconds = 0.05f;

        [SerializeField] private bool requireHold = true;
        [SerializeField, Min(MinHoldSeconds)] private float holdSeconds = DefaultHoldSeconds;
        [Tooltip("How long a released, unfinished hold takes to empty again.")]
        [SerializeField, Min(MinimumRewindSeconds)] private float rewindSeconds = DefaultRewindSeconds;
        [Tooltip("Optional: an Image of type Filled; its fillAmount follows the progress (round or simple shapes).")]
        [SerializeField] private UnityEngine.UI.Image progressFill;
        [Tooltip("Optional: a RectMask2D over a copy of the plate; its right padding uncovers it from left to right (9-sliced plates).")]
        [SerializeField] private UnityEngine.UI.RectMask2D progressMask;
        [SerializeField] private ButtonClickedEvent onHoldCompleted = new ButtonClickedEvent();

        private ITickRegistry _ticks;
        private bool _holding;
        private bool _done;
        private bool _wasInteractable;

        public event Action HoldStarted;
        public event Action HoldCancelled;
        public event Action HoldCompleted;

        public float Progress
        {
            get;
            private set;
        }

        public bool RequireHold
        {
            get => requireHold;
            set
            {
                requireHold = value;
                ResetProgress();
            }
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

        public void Tick(float deltaTime)
        {
            if (!isActiveAndEnabled)
                return;

            bool interactable = IsInteractable();
            if (interactable && !_wasInteractable)
                _done = false; // enabled again after a command: the full bar may empty
            _wasInteractable = interactable;

            if (_holding)
            {
                if (!interactable)
                {
                    Release();
                }
                else
                {
                    Progress = Mathf.Min(1f, Progress + deltaTime / holdSeconds);
                    if (Progress >= 1f)
                        Complete();
                }
            }
            else if (!_done && Progress > 0f)
            {
                Progress = Mathf.Max(0f, Progress - deltaTime / rewindSeconds);
            }
            ApplyProgress();
        }

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
            _ticks?.Register(this);
            _wasInteractable = IsInteractable();
            ResetProgress();
        }

        protected override void OnDisable()
        {
            _ticks?.Unregister(this);
            if (_holding)
            {
                _holding = false;
                HoldCancelled?.Invoke();
            }
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            _ticks?.Unregister(this);
            base.OnDestroy();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!requireHold || eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
                return;

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
            if (!requireHold)
                base.OnPointerClick(eventData); // a held button never acts on a plain tap
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!requireHold)
            {
                base.OnSubmit(eventData);
                return;
            }
            if (IsActive() && IsInteractable())
            {
                PlayPressFeedback();
                Complete();
            }
        }

        private void Release()
        {
            if (!_holding)
                return;

            _holding = false;
            HoldCancelled?.Invoke();
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
            if (progressFill != null)
                progressFill.fillAmount = Progress;
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
