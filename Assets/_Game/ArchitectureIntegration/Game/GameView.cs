using System;
using System.Collections.Generic;
using DG.Tweening;
using Game.Localization;
using TMPro;
using UnityEngine;

namespace Game.Integration.Game
{
    public sealed class GameView : UI.ViewBase
    {
        [SerializeField] private UnityEngine.UI.Button burgerButton;
        [SerializeField] private GameObject actionsMenu;
        [SerializeField] private UnityEngine.UI.Button settingsButton;
        [SerializeField] private TMP_Text playerName;
        [SerializeField] private RectTransform sizeFill;
        [SerializeField] private RectTransform chargeFill;
        [SerializeField] private TMP_Text sizeLabel;
        [SerializeField] private TMP_Text seedLabel;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private UnityEngine.UI.Button menuButton;
        [SerializeField, Min(0.01f)] private float actionsOpenDuration = 0.24f;
        [SerializeField, Min(0.01f)] private float actionsCloseDuration = 0.14f;
        [SerializeField, Min(0f)] private float actionsStagger = 0.055f;
        [SerializeField, Min(0f)] private float actionsSlideDistance = 54f;
        [SerializeField] private Color healthyColor = new Color(0.35f, 0.94f, 0.27f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.4f, 0.33f);

        private UnityEngine.UI.Image _fillImage;
        private ILocalizationService _localization;
        private float _lastSize = 1f;
        private UnityEngine.UI.Button[] _actionButtons;
        private CanvasGroup[] _actionGroups;
        private Vector2[] _actionPositions;
        private UnityEngine.UI.LayoutGroup _actionsLayout;
        private Sequence _actionsTransition;
        private bool _actionsMenuOpen;

        public event Action SettingsRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;
        public event Action<bool> ActionsMenuChanged;
        public bool IsActionsMenuOpen => _actionsMenuOpen;
        public bool IsActionsMenuAnimating => _actionsTransition != null;
        public RectTransform SizeMeter => sizeFill != null ? sizeFill.parent as RectTransform : null;
        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { sizeLabel, seedLabel, hintLabel };

        protected override void Awake()
        {
            if (burgerButton != null) burgerButton.onClick.AddListener(ToggleActionsMenu);
            _actionButtons = new[] { menuButton, restartButton, settingsButton };
            _actionGroups = new CanvasGroup[_actionButtons.Length];
            _actionPositions = new Vector2[_actionButtons.Length];
            if (actionsMenu != null) _actionsLayout = actionsMenu.GetComponent<UnityEngine.UI.LayoutGroup>();
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                if (_actionButtons[i] == null) continue;
                var button = _actionButtons[i];
                _actionGroups[i] = button.GetComponent<CanvasGroup>();
                if (_actionGroups[i] == null) _actionGroups[i] = button.gameObject.AddComponent<CanvasGroup>();
                _actionPositions[i] = ((RectTransform)button.transform).anchoredPosition;
            }
            SetActionsMenuOpen(false, false);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (menuButton != null) menuButton.onClick.AddListener(OnMenu);
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

        public void SetGameplayActionsVisible(bool visible)
        {
            SetActionsMenuOpen(false, false);
            if (burgerButton != null) burgerButton.gameObject.SetActive(visible);
        }

        private void ToggleActionsMenu() => SetActionsMenuOpen(!IsActionsMenuOpen);

        private void SetActionsMenuOpen(bool open, bool animate = true)
        {
            if (actionsMenu == null) return;
            var changed = _actionsMenuOpen != open;
            if (!changed && animate) return;
            _actionsMenuOpen = open;
            _actionsTransition?.Kill();
            _actionsTransition = null;
            if (animate && isActiveAndEnabled) AnimateActionsMenu(open);
            else ApplyActionsMenuInstant(open);
            if (changed) ActionsMenuChanged?.Invoke(open);
        }

        private void AnimateActionsMenu(bool open)
        {
            var wasActive = actionsMenu.activeSelf;
            actionsMenu.SetActive(true);
            if (_actionsLayout != null && _actionsLayout.enabled)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)actionsMenu.transform);
                for (var i = 0; i < _actionButtons.Length; i++)
                    if (_actionButtons[i] != null)
                        _actionPositions[i] = ((RectTransform)_actionButtons[i].transform).anchoredPosition;
                // Layout owns the resting positions; the tween owns them only during a transition.
                _actionsLayout.enabled = false;
            }

            var duration = open ? actionsOpenDuration : actionsCloseDuration;
            var stagger = open ? actionsStagger : actionsStagger * 0.5f;
            var offset = Vector2.up * actionsSlideDistance;
            var sequence = DOTween.Sequence().SetUpdate(true);
            _actionsTransition = sequence;
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                if (_actionButtons[i] == null) continue;
                var rect = (RectTransform)_actionButtons[i].transform;
                var group = _actionGroups[i];
                if (!wasActive)
                {
                    rect.anchoredPosition = _actionPositions[i] + offset;
                    group.alpha = 0f;
                }
                group.interactable = false;
                group.blocksRaycasts = open;
                var delay = (open ? i : _actionButtons.Length - 1 - i) * stagger;
                sequence.Insert(delay, rect.DOAnchorPos(_actionPositions[i] + (open ? Vector2.zero : offset), duration)
                    .SetEase(open ? Ease.OutCubic : Ease.InCubic));
                sequence.Insert(delay, group.DOFade(open ? 1f : 0f, duration * 0.85f).SetEase(Ease.InOutSine));
                if (open) sequence.InsertCallback(delay + duration, () => group.interactable = true);
            }
            sequence.OnComplete(() =>
            {
                _actionsTransition = null;
                ApplyActionsMenuInstant(_actionsMenuOpen);
            });
        }

        private void ApplyActionsMenuInstant(bool open)
        {
            if (_actionButtons != null)
                for (var i = 0; i < _actionButtons.Length; i++)
                {
                    if (_actionButtons[i] == null) continue;
                    ((RectTransform)_actionButtons[i].transform).anchoredPosition = _actionPositions[i];
                    _actionGroups[i].alpha = open ? 1f : 0f;
                    _actionGroups[i].interactable = _actionGroups[i].blocksRaycasts = open;
                }
            if (_actionsLayout != null) _actionsLayout.enabled = true;
            actionsMenu.SetActive(open);
        }

        private void OnSettings()
        {
            SetActionsMenuOpen(false);
            SettingsRequested?.Invoke();
        }

        private void OnRestart()
        {
            SetActionsMenuOpen(false);
            RestartRequested?.Invoke();
        }

        private void OnMenu()
        {
            SetActionsMenuOpen(false);
            MenuRequested?.Invoke();
        }

        protected override void OnDisable()
        {
            SetActionsMenuOpen(false, false);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            _actionsTransition?.Kill();
            _actionsTransition = null;
            if (burgerButton != null) burgerButton.onClick.RemoveListener(ToggleActionsMenu);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestart);
            if (menuButton != null) menuButton.onClick.RemoveListener(OnMenu);
            BindLocalization(null);
            base.OnDestroy();
        }
    }
}
