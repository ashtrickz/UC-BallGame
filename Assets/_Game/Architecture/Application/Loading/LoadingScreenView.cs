using System;
using DG.Tweening;
using Game.UI;
using TMPro;
using UnityEngine;

namespace Game.App.Loading
{
    public sealed class LoadingScreenView : ViewBase, ILoadingScreenView
    {
        private const string ShaderProgressKey = "_Coverage";
        [SerializeField] private CanvasGroup contentContainer;
        [SerializeField] private TMP_Text message;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private Material shaderMaterial;
        [SerializeField] private UnityEngine.UI.Slider progressSlider;
        [SerializeField] private TMP_Text percentageLabel;
        private Material _runtimeMaterial;
        private Tween _progressTween;
        public event Action RetryRequested;

        protected override void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (shaderMaterial != null)
            {
                _runtimeMaterial = new Material(shaderMaterial);
                var graphic = GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null) graphic.material = _runtimeMaterial;
            }
            base.Awake();
        }

        private void OnRetry() => RetryRequested?.Invoke();

        protected override Tween CreateTransitionTween(bool visible)
        {
            var fade = base.CreateTransitionTween(visible);
            if (contentContainer == null && _runtimeMaterial == null) return fade;
            var sequence = DOTween.Sequence().SetUpdate(true);
            if (fade != null) sequence.Join(fade);
            if (contentContainer != null)
                sequence.Insert(visible ? _transitionDuration * 0.6f : 0f,
                    contentContainer.DOFade(visible ? 1f : 0f, _transitionDuration * 0.4f));
            if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(ShaderProgressKey))
            {
                _runtimeMaterial.SetFloat("_ReverseCascade", visible ? 0f : 1f);
                sequence.Insert(0f, _runtimeMaterial.DOFloat(visible ? 1f : 0f, ShaderProgressKey, _transitionDuration)
                    .SetEase(Ease.Linear));
            }
            return sequence;
        }

        public override void ShowInstant()
        {
            base.ShowInstant();
            if (contentContainer != null) contentContainer.alpha = 1f;
            if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(ShaderProgressKey))
                _runtimeMaterial.SetFloat(ShaderProgressKey, 1f);
        }

        public override void HideInstant()
        {
            base.HideInstant();
            if (contentContainer != null) contentContainer.alpha = 0f;
            if (_runtimeMaterial != null) _runtimeMaterial.SetFloat(ShaderProgressKey, 0f);
        }

        public void SetContent(float percentage, string content, bool isError, bool canRetry)
        {
            if (message != null)
            {
                message.text = content;
                message.color = isError ? new Color(1f, 0.6f, 0.6f) : Color.white;
            }
            if (retryButton != null) retryButton.gameObject.SetActive(isError && canRetry);
            _progressTween?.Kill();
            if (progressSlider != null)
            {
                var value = Mathf.Clamp01(percentage / 100f);
                if (!isActiveAndEnabled || value <= 0f || value >= 1f) SetProgress(value);
                else _progressTween = DOTween.To(() => progressSlider.value, SetProgress, value, 0.15f).SetUpdate(true);
            }
        }

        private void SetProgress(float value)
        {
            progressSlider.SetValueWithoutNotify(value);
            if (percentageLabel != null) percentageLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        protected override void OnDisable()
        {
            _progressTween?.Kill();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            _progressTween?.Kill();
            if (_runtimeMaterial != null) Destroy(_runtimeMaterial);
            base.OnDestroy();
        }
    }
}
