using System;
using System.Collections;
using System.Collections.Generic;
using Game.Integration.Localization;
using Game.Settings;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Integration.Gameplay
{
    public interface ISettingsPopupView : IPopupView
    {
        event Action<VolumeChannel, float> VolumeChanged;
        event Action<int> FpsChosen;
        event Action CopyKeyClicked;
        event Action ResetClicked;
        event Action<string> LanguageChosen;

        /// <summary>Shows a value without reporting it back as a change.</summary>
        void ShowVolume(VolumeChannel channel, float value);

        /// <summary>Marks the button of this frame rate as the chosen one.</summary>
        void ShowFps(int fps);

        void ShowBuild(string version, bool canCopyKey);
        void ShowLanguages(IReadOnlyList<LanguageOption> languages, string selectedLanguage);

        /// <summary>Puts the player's key on the clipboard and says so on the button for a moment.</summary>
        void CopyKey(string key);
    }

    /// <summary>The settings popup: the volume sliders, the frame rate, a reset, the build's version and a button that copies the player's key. Passive; the controller talks to the settings service.</summary>
    public sealed class SettingsPopupView : PopupBase, ISettingsPopupView
    {
        [Serializable]
        public sealed class VolumeRow
        {
            public VolumeChannel channel;
            public Slider slider;
        }

        [Serializable]
        public sealed class FpsChoice
        {
            public int fps;
            public Button button;
        }

        [Serializable]
        public sealed class LanguageChoice
        {
            public string code;
            public UnityEngine.UI.Button button;
        }

        public event Action<VolumeChannel, float> VolumeChanged;
        public event Action<int> FpsChosen;
        public event Action CopyKeyClicked;
        public event Action ResetClicked;
        public event Action<string> LanguageChosen;

        [SerializeField] private VolumeRow[] volumes = Array.Empty<VolumeRow>();
        [SerializeField] private FpsChoice[] fpsChoices = Array.Empty<FpsChoice>();
        [SerializeField] private Sprite chosenFpsSprite;
        [SerializeField] private Sprite otherFpsSprite;
        [SerializeField] private TMP_Text buildText;
        [SerializeField] private Button copyKeyButton;
        [SerializeField] private TMP_Text copyKeyLabel;
        [SerializeField, Min(0.1f)] private float copiedSeconds = 1.5f;
        [SerializeField] private Button resetButton;
        [SerializeField] private LanguageChoice[] languageChoices = Array.Empty<LanguageChoice>();

        private LocalizedText _copyKeyText;
        private Coroutine _copied;

        protected override void Awake()
        {
            foreach (var row in volumes)
            {
                if (row.slider == null) continue;
                var channel = row.channel;
                row.slider.minValue = 0f; row.slider.maxValue = 1f;
                row.slider.onValueChanged.AddListener(value => VolumeChanged?.Invoke(channel, value));
            }
            foreach (var choice in fpsChoices)
            {
                if (choice.button == null) continue;
                var fps = choice.fps;
                choice.button.onClick.AddListener(() => FpsChosen?.Invoke(fps));
            }
            foreach (var choice in languageChoices)
            {
                if (choice.button == null) continue;
                var code = choice.code;
                choice.button.onClick.AddListener(() => LanguageChosen?.Invoke(code));
            }
            if (copyKeyButton != null) copyKeyButton.onClick.AddListener(OnCopyKey);
            if (copyKeyLabel != null) _copyKeyText = copyKeyLabel.GetComponent<LocalizedText>();
            if (resetButton != null) resetButton.onClick.AddListener(OnReset);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            foreach (var row in volumes) if (row.slider != null) row.slider.onValueChanged.RemoveAllListeners();
            foreach (var choice in fpsChoices) if (choice.button != null) choice.button.onClick.RemoveAllListeners();
            foreach (var choice in languageChoices) if (choice.button != null) choice.button.onClick.RemoveAllListeners();
            if (copyKeyButton != null) copyKeyButton.onClick.RemoveListener(OnCopyKey);
            if (resetButton != null) resetButton.onClick.RemoveListener(OnReset);
            base.OnDestroy();
        }

        private void OnReset() => ResetClicked?.Invoke();
        private void OnCopyKey() => CopyKeyClicked?.Invoke();

        public void ShowVolume(VolumeChannel channel, float value)
        {
            foreach (var row in volumes)
                if (row.channel == channel && row.slider != null) row.slider.SetValueWithoutNotify(value);
        }

        public void ShowFps(int fps)
        {
            foreach (var choice in fpsChoices)
                if (choice.button != null && choice.button.targetGraphic is Image image)
                {
                    if (chosenFpsSprite != null && otherFpsSprite != null)
                        image.sprite = choice.fps == fps ? chosenFpsSprite : otherFpsSprite;
                    else image.color = choice.fps == fps ? new Color(0.18f, 0.55f, 0.7f) : new Color(0.2f, 0.29f, 0.4f);
                }
        }

        public void ShowBuild(string version, bool canCopyKey)
        {
            if (buildText != null) buildText.text = version;
            if (copyKeyButton != null) copyKeyButton.interactable = canCopyKey;
        }

        public void ShowLanguages(IReadOnlyList<LanguageOption> languages, string selectedLanguage)
        {
            foreach (var choice in languageChoices)
            {
                if (choice.button == null) continue;
                var supported = false;
                foreach (var language in languages) if (language.Code == choice.code) { supported = true; break; }
                choice.button.interactable = supported;
                if (choice.button.targetGraphic is UnityEngine.UI.Image image)
                {
                    var selected = string.Equals(choice.code, selectedLanguage, StringComparison.OrdinalIgnoreCase);
                    if (chosenFpsSprite != null && otherFpsSprite != null) image.sprite = selected ? chosenFpsSprite : otherFpsSprite;
                    else image.color = selected ? new Color(0.18f, 0.55f, 0.7f) : new Color(0.2f, 0.29f, 0.4f);
                }
            }
        }

        public void CopyKey(string key)
        {
            GUIUtility.systemCopyBuffer = key;
            if (copyKeyLabel == null) return;
            if (_copied != null) StopCoroutine(_copied);
            _copied = StartCoroutine(SayCopied());
        }

        private IEnumerator SayCopied()
        {
            if (_copyKeyText != null) _copyKeyText.SetKey("settings.copied");
            yield return new WaitForSecondsRealtime(copiedSeconds);
            if (_copyKeyText != null) _copyKeyText.SetKey("settings.copy_key");
            _copied = null;
        }
    }
}
