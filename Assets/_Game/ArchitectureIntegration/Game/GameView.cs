using System;
using System.Collections.Generic;
using DG.Tweening;
using Slime.Localization;
using TMPro;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Game", sourceAssembly: "Game.Features.Game", sourceClassName: "GameView")]
    public sealed class GameView : UI.ViewBase
    {
        private const string SizeKey = "hud.size";
        private const float Percent = 100f;
        private const float DangerSize = 0.08f;
        private const float DangerBlend = 0.3f;
        private const float DefaultActionsStagger = 0.055f;
        private const float DefaultActionsSlideDistance = 54f;
        private const float CloseStaggerShare = 0.5f;
        private const float FadeShare = 0.85f;
        private const float Hidden = 0f;
        private const float Shown = 1f;
        private const float DriverSeconds = 1f;

        private static readonly Color DefaultHealthyColor = new Color(0.35f, 0.94f, 0.27f);
        private static readonly Color DefaultDangerColor = new Color(1f, 0.4f, 0.33f);

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
        [SerializeField, Min(0f)] private float actionsStagger = DefaultActionsStagger;
        [SerializeField, Min(0f)] private float actionsSlideDistance = DefaultActionsSlideDistance;
        [SerializeField] private Color healthyColor = DefaultHealthyColor;
        [SerializeField] private Color dangerColor = DefaultDangerColor;

        private UnityEngine.UI.Image _fillImage;
        private ILocalizationService _localization;
        private float _lastSize = 1f;
        private UnityEngine.UI.Button[] _actionButtons;
        private CanvasGroup[] _actionGroups;
        private Vector2[] _actionPositions;
        private UnityEngine.UI.LayoutGroup _actionsLayout;
        private Sequence _actionsTransition;
        private Vector2[] _actionFromPositions;
        private float[] _actionFromAlphas;
        private bool _actionsAnimating;
        private bool _actionsOpening;
        private float _actionsDuration;
        private float _actionsStaggerNow;
        private float _actionsTotal;
        private bool _actionsMenuOpen;

        public event Action SettingsRequested;
        public event Action RestartRequested;
        public event Action MenuRequested;
        public event Action<bool> ActionsMenuChanged;
        public bool IsActionsMenuOpen => _actionsMenuOpen;
        public bool IsActionsMenuAnimating => _actionsAnimating;
        public RectTransform SizeMeter => sizeFill != null ? sizeFill.parent as RectTransform : null;
        public IEnumerable<TMP_Text> DynamicTextLabels => new[] { sizeLabel, seedLabel, hintLabel };

        protected override void Awake()
        {
            if (burgerButton != null)
                burgerButton.onClick.AddListener(ToggleActionsMenu);
            _actionButtons = new[] { menuButton, restartButton, settingsButton };
            _actionGroups = new CanvasGroup[_actionButtons.Length];
            _actionPositions = new Vector2[_actionButtons.Length];
            _actionFromPositions = new Vector2[_actionButtons.Length];
            _actionFromAlphas = new float[_actionButtons.Length];
            if (actionsMenu != null)
                _actionsLayout = actionsMenu.GetComponent<UnityEngine.UI.LayoutGroup>();
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                if (_actionButtons[i] == null)
                    continue;

                var button = _actionButtons[i];
                _actionGroups[i] = button.GetComponent<CanvasGroup>();
                if (_actionGroups[i] == null)
                    _actionGroups[i] = button.gameObject.AddComponent<CanvasGroup>();
                _actionPositions[i] = ((RectTransform)button.transform).anchoredPosition;
            }
            SetActionsMenuOpen(false, false);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettings);
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestart);
            if (menuButton != null)
                menuButton.onClick.AddListener(OnMenu);
            if (sizeFill != null)
                _fillImage = sizeFill.GetComponent<UnityEngine.UI.Image>();
            base.Awake();
        }

        public void BindLocalization(ILocalizationService localization)
        {
            if (_localization != null)
                _localization.Changed -= RefreshSizeLabel;
            _localization = localization;
            if (_localization != null)
                _localization.Changed += RefreshSizeLabel;
            RefreshSizeLabel();
        }

        public void ShowSize(float size, float beforeCharge)
        {
            SetFill(sizeFill, size);
            if (_fillImage != null)
                _fillImage.color = Color.Lerp(dangerColor, healthyColor, Mathf.SmoothStep(0, 1, (size - DangerSize) / DangerBlend));
            SetFill(chargeFill, Mathf.Max(size, beforeCharge));
            _lastSize = size;
            RefreshSizeLabel();
        }

        public void ShowSeed(string text)
        {
            if (seedLabel != null)
                seedLabel.text = text;
        }

        public void ShowHint(string text)
        {
            if (hintLabel == null)
                return;

            hintLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text))
                hintLabel.text = text;
        }

        private void RefreshSizeLabel()
        {
            if (sizeLabel != null && _localization != null)
                sizeLabel.text = _localization.Format(SizeKey, Mathf.RoundToInt(Mathf.Clamp01(_lastSize) * Percent));
        }

        private static void SetFill(RectTransform fill, float amount)
        {
            if (fill != null)
                fill.anchorMax = new Vector2(Mathf.Clamp01(amount), fill.anchorMax.y);
        }

        public void ShowPlayer(string name)
        {
            if (playerName != null)
                playerName.text = name;
        }

        public void SetGameplayActionsVisible(bool visible)
        {
            SetActionsMenuOpen(false, false);
            if (burgerButton != null)
                burgerButton.gameObject.SetActive(visible);
        }

        private void ToggleActionsMenu() => SetActionsMenuOpen(!IsActionsMenuOpen);

        private void SetActionsMenuOpen(bool open, bool animate = true)
        {
            if (actionsMenu == null)
                return;

            var changed = _actionsMenuOpen != open;
            if (!changed && animate)
                return;

            _actionsMenuOpen = open;
            StopActionsTransition();
            if (animate && isActiveAndEnabled)
                AnimateActionsMenu(open);
            else
                ApplyActionsMenuInstant(open);
            if (changed)
                ActionsMenuChanged?.Invoke(open);
        }

        private void AnimateActionsMenu(bool open)
        {
            var wasActive = actionsMenu.activeSelf;
            actionsMenu.SetActive(true);
            if (_actionsLayout != null && _actionsLayout.enabled)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)actionsMenu.transform);
                for (var i = 0; i < _actionButtons.Length; i++)
                {
                    if (_actionButtons[i] != null)
                        _actionPositions[i] = ((RectTransform)_actionButtons[i].transform).anchoredPosition;
                }

                // Layout owns the resting positions; the transition owns them only while it runs.
                _actionsLayout.enabled = false;
            }

            _actionsOpening = open;
            _actionsDuration = open ? actionsOpenDuration : actionsCloseDuration;
            _actionsStaggerNow = open ? actionsStagger : actionsStagger * CloseStaggerShare;
            _actionsTotal = (_actionButtons.Length - 1) * _actionsStaggerNow + _actionsDuration;
            var offset = Vector2.up * actionsSlideDistance;
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                if (_actionButtons[i] == null)
                    continue;

                var rect = (RectTransform)_actionButtons[i].transform;
                var group = _actionGroups[i];
                if (!wasActive)
                {
                    rect.anchoredPosition = _actionPositions[i] + offset;
                    group.alpha = Hidden;
                }

                group.interactable = false;
                group.blocksRaycasts = open;
                _actionFromPositions[i] = rect.anchoredPosition;
                _actionFromAlphas[i] = group.alpha;
            }

            _actionsAnimating = true;
            var transition = ActionsTransition();
            transition.timeScale = DriverSeconds / Mathf.Max(Mathf.Epsilon, _actionsTotal);
            transition.Restart();
        }

        private Sequence ActionsTransition()
        {
            if (_actionsTransition != null)
                return _actionsTransition;

            _actionsTransition = DOTween.Sequence()
                .Append(DOVirtual.Float(Hidden, Shown, DriverSeconds, ApplyActionsTransition).SetEase(Ease.Linear))
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(gameObject)
                .OnComplete(OnActionsTransitionComplete)
                .Pause();

            return _actionsTransition;
        }

        private void ApplyActionsTransition(float progress)
        {
            var elapsed = progress * _actionsTotal;
            var offset = Vector2.up * actionsSlideDistance;
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                if (_actionButtons[i] == null)
                    continue;

                var order = _actionsOpening ? i : _actionButtons.Length - 1 - i;
                var local = elapsed - order * _actionsStaggerNow;
                var move = Mathf.Clamp01(local / _actionsDuration);
                var fade = Mathf.Clamp01(local / (_actionsDuration * FadeShare));
                var target = _actionPositions[i] + (_actionsOpening ? Vector2.zero : offset);
                var ease = _actionsOpening ? Ease.OutCubic : Ease.InCubic;
                var rect = (RectTransform)_actionButtons[i].transform;
                rect.anchoredPosition = Vector2.LerpUnclamped(_actionFromPositions[i], target, DOVirtual.EasedValue(Hidden, Shown, move, ease));
                var group = _actionGroups[i];
                var alpha = _actionsOpening ? Shown : Hidden;
                group.alpha = Mathf.Lerp(_actionFromAlphas[i], alpha, DOVirtual.EasedValue(Hidden, Shown, fade, Ease.InOutSine));
                if (_actionsOpening && move >= Shown)
                    group.interactable = true;
            }
        }

        private void OnActionsTransitionComplete()
        {
            _actionsAnimating = false;
            ApplyActionsMenuInstant(_actionsMenuOpen);
        }

        private void StopActionsTransition()
        {
            _actionsAnimating = false;
            if (_actionsTransition != null && _actionsTransition.IsActive())
                _actionsTransition.Pause();
        }

        private void ApplyActionsMenuInstant(bool open)
        {
            if (_actionButtons != null)
                for (var i = 0; i < _actionButtons.Length; i++)
                {
                    if (_actionButtons[i] == null)
                        continue;

                    ((RectTransform)_actionButtons[i].transform).anchoredPosition = _actionPositions[i];
                    _actionGroups[i].alpha = open ? 1f : 0f;
                    _actionGroups[i].interactable = _actionGroups[i].blocksRaycasts = open;
                }
            if (_actionsLayout != null)
                _actionsLayout.enabled = true;
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
            StopActionsTransition();
            _actionsTransition?.Kill();
            _actionsTransition = null;
            if (burgerButton != null)
                burgerButton.onClick.RemoveListener(ToggleActionsMenu);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OnSettings);
            if (restartButton != null)
                restartButton.onClick.RemoveListener(OnRestart);
            if (menuButton != null)
                menuButton.onClick.RemoveListener(OnMenu);
            BindLocalization(null);
            base.OnDestroy();
        }
    }
}
