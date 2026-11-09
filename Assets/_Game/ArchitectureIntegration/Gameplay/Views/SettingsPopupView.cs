using System;
using System.Collections.Generic;
using Game.Settings;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Integration.Gameplay
{
    /// <summary>Passive settings presentation and input events.</summary>
    public interface ISettingsPopupView : IPopupView
    {
        event Action<VolumeChannel, float> VolumeChanged;
        event Action<int> FpsChosen;
        event Action ResetClicked;
        event Action<string> LanguageChosen;

        void ShowVolume(VolumeChannel channel, float value);

        void ShowFps(int fps);

        void ShowBuild(string version);
        void ShowLanguages(IReadOnlyList<LanguageOption> languages, string selectedLanguage);

    }

    /// <summary>Displays settings and reports input without retaining application services.</summary>
    public sealed class SettingsPopupView : PopupBase, ISettingsPopupView
    {
        /// <summary>Authored association between a volume channel and its slider.</summary>
        [Serializable]
        public sealed class VolumeRow
        {
            public VolumeChannel channel;
            public UnityEngine.UI.Slider slider;
        }

        /// <summary>Authored frame-rate choice.</summary>
        [Serializable]
        public sealed class FpsChoice
        {
            public int fps;
            public UnityEngine.UI.Button button;
        }

        /// <summary>Authored language choice.</summary>
        [Serializable]
        public sealed class LanguageChoice
        {
            public string code;
            public UnityEngine.UI.Button button;
        }

        private static readonly Color SelectedColor = Color.white;
        private static readonly Color UnselectedColor = new Color(0.8f, 0.8f, 0.8f);

        public event Action<VolumeChannel, float> VolumeChanged;
        public event Action<int> FpsChosen;
        public event Action ResetClicked;
        public event Action<string> LanguageChosen;

        [SerializeField] private VolumeRow[] volumes = Array.Empty<VolumeRow>();
        [SerializeField] private FpsChoice[] fpsChoices = Array.Empty<FpsChoice>();
        [SerializeField] private Sprite chosenFpsSprite;
        [SerializeField] private Sprite otherFpsSprite;
        [SerializeField] private TMP_Text buildText;
        [SerializeField] private UnityEngine.UI.Button resetButton;
        [SerializeField] private LanguageChoice[] languageChoices = Array.Empty<LanguageChoice>();

        protected override void Awake()
        {
            foreach (var row in volumes)
            {
                if (row.slider == null)
                    continue;

                var channel = row.channel;
                row.slider.minValue = 0f;
                row.slider.maxValue = 1f;
                row.slider.onValueChanged.AddListener(value => VolumeChanged?.Invoke(channel, value));
            }
            foreach (var choice in fpsChoices)
            {
                if (choice.button == null)
                    continue;

                var fps = choice.fps;
                choice.button.onClick.AddListener(() => FpsChosen?.Invoke(fps));
            }
            foreach (var choice in languageChoices)
            {
                if (choice.button == null)
                    continue;

                var code = choice.code;
                choice.button.onClick.AddListener(() => LanguageChosen?.Invoke(code));
            }
            if (resetButton != null)
                resetButton.onClick.AddListener(OnReset);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            foreach (var row in volumes)
                if (row.slider != null)
                    row.slider.onValueChanged.RemoveAllListeners();
            foreach (var choice in fpsChoices)
                if (choice.button != null)
                    choice.button.onClick.RemoveAllListeners();
            foreach (var choice in languageChoices)
                if (choice.button != null)
                    choice.button.onClick.RemoveAllListeners();
            if (resetButton != null)
                resetButton.onClick.RemoveListener(OnReset);
            base.OnDestroy();
        }

        private void OnReset() => ResetClicked?.Invoke();

        public void ShowVolume(VolumeChannel channel, float value)
        {
            foreach (var row in volumes)
                if (row.channel == channel && row.slider != null)
                    row.slider.SetValueWithoutNotify(value);
        }

        public void ShowFps(int fps)
        {
            foreach (var choice in fpsChoices)
                if (choice.button != null && choice.button.targetGraphic is UnityEngine.UI.Image image)
                {
                    if (chosenFpsSprite != null && otherFpsSprite != null)
                        image.sprite = choice.fps == fps ? chosenFpsSprite : otherFpsSprite;
                    else
                        image.color = choice.fps == fps ? SelectedColor : UnselectedColor;
                }
        }

        public void ShowBuild(string version)
        {
            if (buildText != null)
                buildText.text = version;
        }

        public void ShowLanguages(IReadOnlyList<LanguageOption> languages, string selectedLanguage)
        {
            foreach (var choice in languageChoices)
            {
                if (choice.button == null)
                    continue;

                var supported = false;
                foreach (var language in languages)
                    if (language.Code == choice.code)
                    {
                        supported = true;
                        break;
                    }
                choice.button.interactable = supported;
                if (choice.button.targetGraphic is UnityEngine.UI.Image image)
                {
                    var selected = string.Equals(choice.code, selectedLanguage, StringComparison.OrdinalIgnoreCase);
                    if (chosenFpsSprite != null && otherFpsSprite != null)
                        image.sprite = selected ? chosenFpsSprite : otherFpsSprite;
                    else
                        image.color = selected ? SelectedColor : UnselectedColor;
                }
            }
        }
    }
}
