using System;
using DG.Tweening;
using Game.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Game.UI
{
    /// <summary>Shared press animation and injected click audio for UI buttons.</summary>
    [AddComponentMenu("UI/Feedback Button")]
    public class FeedbackButton : UnityEngine.UI.Button
    {
        [SerializeField] private bool punchEnabled = true;
        [Tooltip("Optional visual to animate. Defaults to this button's transform.")]
        [SerializeField] private Transform punchTarget;
        [SerializeField, Min(0f)] private float punchStrength = 0.08f;
        [SerializeField, Min(0.01f)] private float punchDuration = 0.2f;
        [SerializeField, Min(1)] private int punchVibrato = 4;
        [SerializeField, Range(0f, 1f)] private float punchElasticity = 0.5f;
        [SerializeField] private bool soundEnabled = true;
        [SerializeField] private GameSound clickSound = GameSound.UiClick;

        private IAudioService _audio;
        private Tween _punch;
        private Transform _animatedTarget;
        private Vector3 _restScale;

        [Inject]
        public void Construct(IAudioService audio) => _audio = audio;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable())
                PlayPressFeedback();
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            // Call before listeners: a click may close the popup or unload the scene.
            PlayClickFeedback();
            base.OnPointerClick(eventData);
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            PlayPressFeedback();
            PlayClickFeedback();
            base.OnSubmit(eventData);
        }

        protected virtual void PlayPressFeedback()
        {
            StopPunch();
            if (!punchEnabled || punchStrength <= 0f) return;
            _animatedTarget = punchTarget != null ? punchTarget : transform;
            _restScale = _animatedTarget.localScale;
            var punch = Vector3.Scale(_restScale, new Vector3(punchStrength, punchStrength, 0f));
            _punch = _animatedTarget.DOPunchScale(punch, Mathf.Max(0.01f, punchDuration),
                    Mathf.Max(1, punchVibrato), punchElasticity)
                .SetUpdate(true)
                .OnKill(() =>
                {
                    if (_animatedTarget != null) _animatedTarget.localScale = _restScale;
                    _animatedTarget = null;
                    _punch = null;
                });
        }

        protected virtual void PlayClickFeedback()
        {
            if (!soundEnabled || _audio == null) return;
            try { _audio.PlaySound(clickSound); }
            catch (InvalidOperationException) { } // Loading/retry remains usable before audio initialization.
        }

        protected override void OnDisable()
        {
            StopPunch();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            StopPunch();
            base.OnDestroy();
        }

        private void StopPunch()
        {
            _punch?.Kill();
            _punch = null;
        }
    }
}
