using System;
using System.Collections.Generic;
using DG.Tweening;
using Slime.UI;
using TMPro;
using UnityEngine;

namespace Slime.App.Loading
{
    /// <summary>
    /// Loading curtain: the shader cascade covers the screen, the content fades in after it, and the progress bar
    /// eases to each reported value with one reusable tween.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.App.Loading", sourceAssembly: "Game.Application", sourceClassName: "LoadingScreenView")]
    public sealed class LoadingScreenView : ViewBase, ILoadingScreenView
    {
        private const string CoverageProperty = "_Coverage";
        private const string ReverseCascadeProperty = "_ReverseCascade";
        private const string PercentFormat = "{0}%";
        private const float ContentShowStart = 0.6f;
        private const float ContentShare = 0.4f;
        private const float Hidden = 0f;
        private const float Shown = 1f;
        private const float Percent = 100f;
        private const float ProgressDuration = 0.15f;

        private static readonly int CoverageId = Shader.PropertyToID(CoverageProperty);
        private static readonly int ReverseCascadeId = Shader.PropertyToID(ReverseCascadeProperty);
        private static readonly Color ErrorColor = new Color(1f, 0.6f, 0.6f);

        [SerializeField] private CanvasGroup contentContainer;
        [SerializeField] private TMP_Text message;
        [SerializeField] private UnityEngine.UI.Button retryButton;
        [SerializeField] private Material shaderMaterial;
        [SerializeField] private UnityEngine.UI.Slider progressSlider;
        [SerializeField] private TMP_Text percentageLabel;

        private Material _runtimeMaterial;
        private Tween _progressTween;
        private float _progressFrom;
        private float _progressTo;
        private float _contentFrom;
        private float _coverageFrom;

        public event Action RetryRequested;

        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { message, percentageLabel };

        protected override bool HasTransition => base.HasTransition || contentContainer != null || HasCoverage;

        private bool HasCoverage => _runtimeMaterial != null && _runtimeMaterial.HasProperty(CoverageId);

        public override void ShowInstant()
        {
            base.ShowInstant();
            if (contentContainer != null)
                contentContainer.alpha = Shown;

            if (HasCoverage)
                _runtimeMaterial.SetFloat(CoverageId, Shown);
        }

        public override void HideInstant()
        {
            base.HideInstant();
            if (contentContainer != null)
                contentContainer.alpha = Hidden;

            if (_runtimeMaterial != null)
                _runtimeMaterial.SetFloat(CoverageId, Hidden);
        }

        public void SetContent(float percentage, string content, bool isError, bool canRetry)
        {
            if (message != null)
            {
                message.text = content;
                message.color = isError ? ErrorColor : Color.white;
            }

            if (retryButton != null)
                retryButton.gameObject.SetActive(isError && canRetry);

            StopProgress();
            if (progressSlider == null)
                return;

            var value = Mathf.Clamp01(percentage / Percent);
            if (!isActiveAndEnabled || value <= Hidden || value >= Shown)
            {
                SetProgress(value);
                return;
            }

            _progressFrom = progressSlider.value;
            _progressTo = value;
            ProgressTween().Restart();
        }

        protected override void Awake()
        {
            if (retryButton != null)
                retryButton.onClick.AddListener(OnRetry);

            if (shaderMaterial != null)
            {
                _runtimeMaterial = new Material(shaderMaterial);
                var graphic = GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null)
                    graphic.material = _runtimeMaterial;
            }

            base.Awake();
        }

        protected override void BeginTransition(bool visible)
        {
            base.BeginTransition(visible);
            if (contentContainer != null)
                _contentFrom = contentContainer.alpha;

            if (!HasCoverage)
                return;

            _coverageFrom = _runtimeMaterial.GetFloat(CoverageId);
            _runtimeMaterial.SetFloat(ReverseCascadeId, visible ? Hidden : Shown);
        }

        protected override void ApplyTransition(float progress)
        {
            base.ApplyTransition(progress);
            var target = TransitionVisible ? Shown : Hidden;
            if (contentContainer != null)
            {
                var contentStart = TransitionVisible ? ContentShowStart : Hidden;
                var local = Mathf.Clamp01((progress - contentStart) / ContentShare);
                contentContainer.alpha = Mathf.Lerp(_contentFrom, target, DOVirtual.EasedValue(Hidden, Shown, local, Ease.OutQuad));
            }

            if (HasCoverage)
                _runtimeMaterial.SetFloat(CoverageId, Mathf.Lerp(_coverageFrom, target, progress));
        }

        protected override void OnDisable()
        {
            StopProgress();
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            if (retryButton != null)
                retryButton.onClick.RemoveListener(OnRetry);

            _progressTween?.Kill();
            _progressTween = null;
            if (_runtimeMaterial != null)
                Destroy(_runtimeMaterial);

            base.OnDestroy();
        }

        private Tween ProgressTween()
        {
            if (_progressTween != null)
                return _progressTween;

            _progressTween = DOVirtual.Float(Hidden, Shown, ProgressDuration, ApplyProgress)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(gameObject)
                .Pause();

            return _progressTween;
        }

        private void ApplyProgress(float progress)
        {
            SetProgress(Mathf.LerpUnclamped(_progressFrom, _progressTo, progress));
        }

        private void StopProgress()
        {
            if (_progressTween != null && _progressTween.IsActive())
                _progressTween.Pause();
        }

        private void SetProgress(float value)
        {
            progressSlider.SetValueWithoutNotify(value);
            if (percentageLabel != null)
                percentageLabel.text = string.Format(PercentFormat, Mathf.RoundToInt(value * Percent));
        }

        private void OnRetry()
        {
            RetryRequested?.Invoke();
        }
    }
}
