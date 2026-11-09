using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>Standalone result card. The controller supplies all copy and actions through its model.</summary>
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private UnityEngine.UI.Button primaryButton;
        [SerializeField] private TMP_Text primaryLabel;
        [SerializeField] private UnityEngine.UI.Button secondaryButton;
        [SerializeField] private TMP_Text secondaryLabel;
        [SerializeField] private UnityEngine.UI.Button menuButton;
        [SerializeField] private RectTransform card;
        [SerializeField] private UnityEngine.UI.Image dim;
        [SerializeField, Min(0.05f)] private float flyInDuration = 0.55f;

        private ResultViewModel _model;
        private Coroutine _flyIn;
        private float _dimAlpha = -1;

        public bool IsAnimating => _flyIn != null;
        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { title, subtitle, primaryLabel, secondaryLabel };

        private void Awake()
        {
            if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimary);
            if (secondaryButton != null) secondaryButton.onClick.AddListener(OnSecondary);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
        }

        /// <summary>Also refreshes an open card without replaying its animation.</summary>
        public void Initialize(ResultViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            if (title != null) title.text = model.Title;
            if (subtitle != null) subtitle.text = model.Subtitle;
            if (primaryLabel != null) primaryLabel.text = model.PrimaryLabel;
            if (secondaryButton != null)
                secondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(model.SecondaryLabel) && model.SecondaryAction != null);
            if (secondaryLabel != null) secondaryLabel.text = model.SecondaryLabel ?? string.Empty;
            if (menuButton != null) menuButton.gameObject.SetActive(model.MenuAction != null);
        }

        public void Show()
        {
            if (_model == null) throw new InvalidOperationException("Initialize the result view before showing it.");
            gameObject.SetActive(true);
            StopAnimation();
            _flyIn = isActiveAndEnabled ? StartCoroutine(FlyIn()) : null;
        }

        public void Hide()
        {
            _model = null;
            StopAnimation();
            gameObject.SetActive(false);
        }

        private void OnPrimary() => InvokeAction(_model?.PrimaryAction);
        private void OnSecondary() => InvokeAction(_model?.SecondaryAction);
        private void OnMenu() => InvokeAction(_model?.MenuAction);

        private void InvokeAction(Action action)
        {
            if (action == null) return;
            Hide();
            action.Invoke();
        }

        private void OnDisable()
        {
            _model = null;
            StopAnimation();
        }

        private void OnDestroy()
        {
            if (primaryButton != null) primaryButton.onClick.RemoveListener(OnPrimary);
            if (secondaryButton != null) secondaryButton.onClick.RemoveListener(OnSecondary);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMenu);
        }

        private IEnumerator FlyIn()
        {
            if (dim != null && _dimAlpha < 0) _dimAlpha = dim.color.a;
            var titleRect = title != null ? title.rectTransform : null;
            var t = 0f;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.unscaledDeltaTime / flyInDuration);
                if (dim != null)
                {
                    var color = dim.color;
                    color.a = _dimAlpha * Mathf.Clamp01(t * 2.2f);
                    dim.color = color;
                }
                if (card != null)
                {
                    var e = BackOut(t);
                    card.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-1700, 0, e));
                    card.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-9, 0, e));
                    card.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1, e);
                }
                yield return null;
            }
            if (titleRect != null)
            {
                for (var p = 0f; p < 1; p += Time.unscaledDeltaTime / 0.22f)
                {
                    titleRect.localScale = Vector3.one * Mathf.Lerp(1.25f, 1, 1 - (1 - p) * (1 - p));
                    yield return null;
                }
                titleRect.localScale = Vector3.one;
            }
            _flyIn = null;
        }

        private void StopAnimation()
        {
            if (_flyIn != null) { StopCoroutine(_flyIn); _flyIn = null; }
            if (card != null)
            {
                card.anchoredPosition = Vector2.zero;
                card.localRotation = Quaternion.identity;
                card.localScale = Vector3.one;
            }
            if (title != null) title.rectTransform.localScale = Vector3.one;
            if (dim != null && _dimAlpha >= 0)
            {
                var color = dim.color;
                color.a = _dimAlpha;
                dim.color = color;
            }
        }

        private static float BackOut(float t)
        {
            const float s = 1.9f;
            t -= 1;
            return t * t * ((s + 1) * t + s) + 1;
        }
    }
}
