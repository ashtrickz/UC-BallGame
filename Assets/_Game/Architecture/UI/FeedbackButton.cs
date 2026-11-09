using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Slime.UI
{
    /// <summary>
    /// Shared press animation and explicitly bound interaction feedback for UI buttons. The punch is one tween built on
    /// the first press and replayed from the rest scale, so repeated presses never drift the scale.
    /// </summary>
    [AddComponentMenu("UI/Feedback Button")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.UI", sourceAssembly: "Game.UI", sourceClassName: "FeedbackButton")]
    public class FeedbackButton : UnityEngine.UI.Button
    {
        private const float DefaultPunchStrength = 0.08f;
        private const float DefaultPunchDuration = 0.2f;
        private const float MinimumPunchDuration = 0.01f;
        private const int DefaultPunchVibrato = 4;
        private const float DefaultPunchElasticity = 0.5f;

        [SerializeField] private bool punchEnabled = true;
        [Tooltip("Optional visual to animate. Defaults to this button's transform.")]
        [SerializeField] private Transform punchTarget;
        [SerializeField, Min(0f)] private float punchStrength = DefaultPunchStrength;
        [SerializeField, Min(MinimumPunchDuration)] private float punchDuration = DefaultPunchDuration;
        [SerializeField, Min(1)] private int punchVibrato = DefaultPunchVibrato;
        [SerializeField, Range(0f, 1f)] private float punchElasticity = DefaultPunchElasticity;
        [SerializeField] private bool soundEnabled = true;
        [SerializeField] private UiFeedbackSound clickSound = UiFeedbackSound.Click;

        private IUiFeedback _feedback;
        private Tween _punch;
        private Transform _animatedTarget;
        private Vector3 _restScale;

        public void Bind(IUiFeedback feedback)
        {
            _feedback = feedback;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
                PlayPressFeedback();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable())
                return;

            // Call before listeners: a click may close the popup or unload the scene.
            PlayClickFeedback();
            base.OnPointerClick(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
                return;

            PlayPressFeedback();
            PlayClickFeedback();
            base.OnSubmit(eventData);
        }

        protected virtual void PlayPressFeedback()
        {
            StopPunch();
            if (!punchEnabled || punchStrength <= 0f)
                return;

            Punch().Restart();
        }

        protected virtual void PlayClickFeedback()
        {
            if (!soundEnabled || _feedback == null)
                return;

            _feedback.Play(clickSound);
        }

        protected override void OnDisable()
        {
            StopPunch();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            _punch?.Kill();
            _punch = null;
            base.OnDestroy();
        }

        private Tween Punch()
        {
            if (_punch != null)
                return _punch;

            _animatedTarget = punchTarget != null ? punchTarget : transform;
            _restScale = _animatedTarget.localScale;
            var punch = Vector3.Scale(_restScale, new Vector3(punchStrength, punchStrength, 0f));
            _punch = _animatedTarget
                .DOPunchScale(punch, Mathf.Max(MinimumPunchDuration, punchDuration), Mathf.Max(1, punchVibrato), punchElasticity)
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(gameObject)
                .Pause();

            return _punch;
        }

        // Back to the rest scale before any replay or when the button goes away mid-punch.
        private void StopPunch()
        {
            if (_punch == null || !_punch.IsActive())
                return;

            _punch.Pause();
            if (_animatedTarget != null)
                _animatedTarget.localScale = _restScale;
        }
    }
}
