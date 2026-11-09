using System;
using System.Collections.Generic;
using Game.Localization;
using TMPro;
using UnityEngine;

namespace Game.Integration.Game
{
    public sealed class GameView : UI.ViewBase
    {
        [SerializeField] private UnityEngine.UI.Button settingsButton;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private RectTransform sizeFill;
        [SerializeField] private RectTransform chargeFill;
        [SerializeField] private TMP_Text sizeLabel;
        [SerializeField] private TMP_Text seedLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private Color healthyColor = new Color(0.35f, 0.94f, 0.27f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.4f, 0.33f);

        private UnityEngine.UI.Image _fillImage;
        private ILocalizationService _localization;
        private float _lastSize = 1f;

        public event Action SettingsRequested;
        public event Action RestartRequested;
        public RectTransform SizeMeter => sizeFill != null ? sizeFill.parent as RectTransform : null;
        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { sizeLabel, seedLabel, hintLabel };

        protected override void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (sizeFill != null) _fillImage = sizeFill.GetComponent<UnityEngine.UI.Image>();
            base.Awake();
        }

        public void BindLocalization(ILocalizationService localization)
        {
            if (_localization != null) _localization.Changed -= RefreshSizeLabel;
            _localization = localization;
            if (_localization != null) _localization.Changed += RefreshSizeLabel;
            RefreshSizeLabel();
        }

        /// <param name="size">Current share of the spendable budget, 0..1.</param>
        /// <param name="beforeCharge">Share before the current hold.</param>
        public void ShowSize(float size, float beforeCharge)
        {
            SetFill(sizeFill, size);
            if (_fillImage != null)
                _fillImage.color = Color.Lerp(dangerColor, healthyColor, Mathf.SmoothStep(0, 1, (size - 0.08f) / 0.3f));
            SetFill(chargeFill, Mathf.Max(size, beforeCharge));
            _lastSize = size;
            RefreshSizeLabel();
        }

        public void ShowSeed(string text) { if (seedLabel != null) seedLabel.text = text; }

        public void ShowHint(string text)
        {
            if (hintLabel == null) return;
            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text)) hintLabel.text = text;
        }

        private void RefreshSizeLabel()
        {
            if (sizeLabel != null && _localization != null)
                sizeLabel.text = _localization.Format("hud.size", Mathf.RoundToInt(Mathf.Clamp01(_lastSize) * 100));
        }

        private static void SetFill(RectTransform fill, float amount)
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp01(amount), fill.anchorMax.y);
        }

        public void ShowPlayer(string name)
        {
            if (playerName != null) playerName.text = name;
        }

        private void OnSettings() => SettingsRequested?.Invoke();
        private void OnRestart() => RestartRequested?.Invoke();

        protected override void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestart);
            BindLocalization(null);
            base.OnDestroy();
        }
    }
}
