using System;
using System.Collections.Generic;
using DG.Tweening;
using Slime.UI;
using TMPro;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// Standalone result card. The presenter supplies all copy and actions through its model. The fly-in is one
    /// sequence built on first show and replayed afterwards; an interrupted fly-in snaps the card to its rest pose.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Game", sourceAssembly: "Game.Features.Game", sourceClassName: "ResultView")]
    public sealed class ResultView : ViewBase
    {
        private const float CardStartY = -1700f;
        private const float CardStartAngle = -9f;
        private const float CardStartScale = 0.8f;
        private const float DimFadeSpeed = 2.2f;
        private const float TitlePopScale = 1.25f;
        private const float TitlePopDuration = 0.22f;
        private const float BackOutOvershoot = 1.9f;
        private const float Start = 0f;
        private const float End = 1f;

        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private UnityEngine.UI.Button primaryButton;
        [SerializeField] private UnityEngine.UI.Button secondaryButton;
        [SerializeField] private UnityEngine.UI.Button menuButton;
        [SerializeField] private RectTransform card;
        [SerializeField] private UnityEngine.UI.Image dim;
        [SerializeField, Min(0.05f)] private float flyInDuration = 0.55f;
        [SerializeField] private StarRowView stars;
        [SerializeField] private WorldStarsView starFlight;

        public event Action Hidden;

        public StarRowView Stars => stars;

        public WorldStarsView StarFlight => starFlight;

        private ResultViewModel _model;
        private Sequence _flyIn;
        private float _dimAlpha = -1;

        public bool IsAnimating => _flyIn != null && _flyIn.IsActive() && _flyIn.IsPlaying();

        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { title, subtitle };

        public void Initialize(ResultViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (title != null)
                title.text = model.Title;

            if (subtitle != null)
                subtitle.text = model.Subtitle;

            if (secondaryButton != null)
                secondaryButton.gameObject.SetActive(model.SecondaryAction != null);

            if (menuButton != null)
                menuButton.gameObject.SetActive(model.MenuAction != null);

            if (card != null)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(card);
        }

        public void Show()
        {
            if (_model == null)
                throw new InvalidOperationException("Initialize the result view before showing it.");

            ShowInstant();
            StopAnimation();
            FlyIn().Restart();
            ApplyFrame(Start);
        }

        public void Hide()
        {
            Hidden?.Invoke();
            _model = null;
            StopAnimation();
            HideInstant();
        }

        protected override void Awake()
        {
            base.Awake();
            if (primaryButton != null)
                primaryButton.onClick.AddListener(OnPrimary);

            if (secondaryButton != null)
                secondaryButton.onClick.AddListener(OnSecondary);

            if (menuButton != null)
                menuButton.onClick.AddListener(OnMenu);
        }

        protected override void OnDisable()
        {
            Hidden?.Invoke();
            base.OnDisable();
            _model = null;
            StopAnimation();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _flyIn?.Kill();
            if (primaryButton != null)
                primaryButton.onClick.RemoveListener(OnPrimary);

            if (secondaryButton != null)
                secondaryButton.onClick.RemoveListener(OnSecondary);

            if (menuButton != null)
                menuButton.onClick.RemoveListener(OnMenu);
        }

        private void OnPrimary()
        {
            InvokeAction(_model?.PrimaryAction);
        }

        private void OnSecondary()
        {
            InvokeAction(_model?.SecondaryAction);
        }

        private void OnMenu()
        {
            InvokeAction(_model?.MenuAction);
        }

        private void InvokeAction(Action action)
        {
            if (action == null)
                return;

            Hide();
            action.Invoke();
        }

        private Sequence FlyIn()
        {
            if (_flyIn != null)
                return _flyIn;

            if (dim != null && _dimAlpha < 0)
                _dimAlpha = dim.color.a;

            // One driver in seconds: the card flies in, then the title pops. Rewinding it gives the exact start pose.
            var total = flyInDuration + TitlePopDuration;
            _flyIn = DOTween.Sequence()
                .Append(DOVirtual.Float(Start, total, total, ApplyFrame).SetEase(Ease.Linear))
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(gameObject)
                .Pause();

            return _flyIn;
        }

        private void ApplyFrame(float seconds)
        {
            if (seconds < flyInDuration)
            {
                ApplyFlyIn(seconds / flyInDuration);
                ApplyTitlePop(End);
                return;
            }

            ApplyFlyIn(End);
            ApplyTitlePop((seconds - flyInDuration) / TitlePopDuration);
        }

        private void ApplyFlyIn(float t)
        {
            if (dim != null)
            {
                var color = dim.color;
                color.a = _dimAlpha * Mathf.Clamp01(t * DimFadeSpeed);
                dim.color = color;
            }

            if (card != null)
            {
                var eased = BackOut(t);
                card.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(CardStartY, 0, eased));
                card.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(CardStartAngle, 0, eased));
                card.localScale = Vector3.one * Mathf.LerpUnclamped(CardStartScale, End, eased);
            }
        }

        private void ApplyTitlePop(float t)
        {
            if (title == null)
                return;

            var eased = 1 - (1 - t) * (1 - t);
            title.rectTransform.localScale = Vector3.one * Mathf.Lerp(TitlePopScale, End, eased);
        }

        private void StopAnimation()
        {
            if (_flyIn != null && _flyIn.IsActive())
                _flyIn.Pause();

            if (card != null)
            {
                card.anchoredPosition = Vector2.zero;
                card.localRotation = Quaternion.identity;
                card.localScale = Vector3.one;
            }

            if (title != null)
                title.rectTransform.localScale = Vector3.one;

            if (dim != null && _dimAlpha >= 0)
            {
                var color = dim.color;
                color.a = _dimAlpha;
                dim.color = color;
            }
        }

        private static float BackOut(float t)
        {
            t -= 1;

            return t * t * ((BackOutOvershoot + 1) * t + BackOutOvershoot) + 1;
        }
    }
}
