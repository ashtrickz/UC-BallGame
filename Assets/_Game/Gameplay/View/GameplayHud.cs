using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Gameplay.View
{
    /// <summary>Size meter, level label, retry, level idea hint and the end-of-attempt panel with two actions.</summary>
    public sealed class GameplayHud : MonoBehaviour
    {
        [SerializeField] private RectTransform sizeFill;
        [SerializeField] private RectTransform chargeFill;
        [SerializeField] private TMP_Text sizeLabel;
        [SerializeField] private TMP_Text seedLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text resultTitle;
        [SerializeField] private TMP_Text resultSubtitle;
        [FormerlySerializedAs("resultRestartButton")]
        [SerializeField] private Button resultPrimaryButton;
        [SerializeField] private TMP_Text resultPrimaryLabel;
        [SerializeField] private Button resultSecondaryButton;
        [SerializeField] private TMP_Text resultSecondaryLabel;
        [SerializeField] private Button resultMenuButton;
        [SerializeField] private RectTransform resultCard;
        [SerializeField] private Image resultDim;
        [SerializeField, Min(0.05f)] private float flyInDuration = 0.55f;

        [SerializeField] private Color healthyColor = new Color(0.35f, 0.94f, 0.27f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.4f, 0.33f);

        /// <summary>The always-visible retry button.</summary>
        public event Action RestartRequested;
        /// <summary>The size meter (for tutorial cut-outs).</summary>
        public RectTransform SizeMeter => sizeFill != null ? sizeFill.parent as RectTransform : null;
        public event Action PrimaryRequested;
        public event Action SecondaryRequested;
        public event Action MenuRequested;
        private Image _fillImage;
        private Coroutine _flyIn;
        private float _dimAlpha = -1;

        /// <summary>True while the result card is still flying in.</summary>
        public bool IsResultAnimating => _flyIn != null;

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (resultPrimaryButton != null) resultPrimaryButton.onClick.AddListener(OnPrimary);
            if (resultSecondaryButton != null) resultSecondaryButton.onClick.AddListener(OnSecondary);
            if (resultMenuButton != null) resultMenuButton.onClick.AddListener(OnMenu);
            if (sizeFill != null) _fillImage = sizeFill.GetComponent<Image>();
            HideResult();
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestart);
            if (resultPrimaryButton != null) resultPrimaryButton.onClick.RemoveListener(OnPrimary);
            if (resultSecondaryButton != null) resultSecondaryButton.onClick.RemoveListener(OnSecondary);
            if (resultMenuButton != null) resultMenuButton.onClick.RemoveListener(OnMenu);
        }

        private void OnRestart() => RestartRequested?.Invoke();
        private void OnPrimary() => PrimaryRequested?.Invoke();
        private void OnSecondary() => SecondaryRequested?.Invoke();
        private void OnMenu() => MenuRequested?.Invoke();

        /// <param name="size">Current share of the spendable budget, 0..1.</param>
        /// <param name="beforeCharge">Share before the current hold (equals size when not charging).</param>
        public void ShowSize(float size, float beforeCharge, float diameter)
        {
            SetFill(sizeFill, size);
            // The meter warms up as the slime nears the critical size: holding further means bursting.
            if (_fillImage != null) _fillImage.color = Color.Lerp(dangerColor, healthyColor, Mathf.SmoothStep(0, 1, (size - 0.08f) / 0.3f));
            SetFill(chargeFill, Mathf.Max(size, beforeCharge));
            if (sizeLabel != null) sizeLabel.text = $"SIZE  {Mathf.RoundToInt(Mathf.Clamp01(size) * 100)}%";
        }

        public void ShowSeed(string text) { if (seedLabel != null) seedLabel.text = text; }

        /// <summary>Shows the level idea / controls hint; null hides it.</summary>
        public void ShowHint(string text)
        {
            if (hintLabel == null) return;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) hintLabel.text = text;
        }

        /// <summary>End panel: a primary action and an optional secondary one (null hides it).</summary>
        public void ShowResult(string title, string subtitle, string primary, string secondary)
        {
            if (resultPanel == null) return;
            resultPanel.SetActive(true);
            if (resultTitle != null) resultTitle.text = title;
            if (resultSubtitle != null) resultSubtitle.text = subtitle;
            if (resultPrimaryLabel != null) resultPrimaryLabel.text = primary;
            if (resultSecondaryButton != null) resultSecondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            if (resultSecondaryLabel != null && !string.IsNullOrEmpty(secondary)) resultSecondaryLabel.text = secondary;
            if (_flyIn != null) StopCoroutine(_flyIn);
            _flyIn = isActiveAndEnabled ? StartCoroutine(FlyIn()) : null;
        }

        public void HideResult()
        {
            if (_flyIn != null) { StopCoroutine(_flyIn); _flyIn = null; }
            ResetCard();
            if (resultPanel != null) resultPanel.SetActive(false);
        }

        /// <summary>Dim fades in; the card flies up from below with an overshoot and a small swing; the title punches.</summary>
        private IEnumerator FlyIn()
        {
            if (resultDim != null && _dimAlpha < 0) _dimAlpha = resultDim.color.a;
            var title = resultTitle != null ? resultTitle.rectTransform : null;
            var t = 0f;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.unscaledDeltaTime / flyInDuration);
                if (resultDim != null)
                {
                    var c = resultDim.color;
                    c.a = _dimAlpha * Mathf.Clamp01(t * 2.2f);
                    resultDim.color = c;
                }
                if (resultCard != null)
                {
                    var e = BackOut(t);
                    resultCard.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-1700, 0, e));
                    resultCard.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(-9, 0, e));
                    resultCard.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1, e);
                }
                yield return null;
            }
            if (title != null)
            {
                for (var p = 0f; p < 1; p += Time.unscaledDeltaTime / 0.22f)
                {
                    title.localScale = Vector3.one * Mathf.Lerp(1.25f, 1, 1 - (1 - p) * (1 - p));
                    yield return null;
                }
                title.localScale = Vector3.one;
            }
            _flyIn = null;
        }

        private void ResetCard()
        {
            if (resultCard != null)
            {
                resultCard.anchoredPosition = Vector2.zero;
                resultCard.localRotation = Quaternion.identity;
                resultCard.localScale = Vector3.one;
            }
            if (resultTitle != null) resultTitle.rectTransform.localScale = Vector3.one;
            if (resultDim != null && _dimAlpha >= 0)
            {
                var c = resultDim.color;
                c.a = _dimAlpha;
                resultDim.color = c;
            }
        }

        private static float BackOut(float t)
        {
            const float s = 1.9f;
            t -= 1;
            return t * t * ((s + 1) * t + s) + 1;
        }

        private static void SetFill(RectTransform fill, float amount)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(amount), fill.anchorMax.y);
        }
    }
}
