using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.View;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Integration.Menu
{
    public interface IMenuView : IView
    {
        event Action PlayRequested;
        event Action WallRequested;
        event Action SettingsRequested;
        event Action<int> SlimeColorRequested;
        void SetSlimeColors(IReadOnlyList<SlimeColorPreset> presets, int selectedIndex);
        void SetSelectedSlimeColor(int index);
        void SetInteractable(bool interactable);
    }

    public sealed class MenuView : ViewBase, IMenuView
    {
        private const string SwatchName = "Color ";

        [SerializeField] private UnityEngine.UI.Button playButton;
        [SerializeField] private UnityEngine.UI.Button settingsButton;
        [SerializeField] private UnityEngine.UI.Button wallButton;
        [SerializeField] private RectTransform slimeColorRoot;
        [SerializeField] private SlimeView slimePreview;
        private readonly List<SlimeColorSwatch> _swatches = new List<SlimeColorSwatch>();
        private IReadOnlyList<SlimeColorPreset> _presets;
        private bool _interactable = true;
        private IUiElementFactory _elements;

        [Inject]
        public void Construct(IUiElementFactory elements)
        {
            _elements = elements;
        }
        public event Action PlayRequested;
        public event Action WallRequested;
        public event Action SettingsRequested;
        public event Action<int> SlimeColorRequested;

        public void SetSlimeColors(IReadOnlyList<SlimeColorPreset> presets, int selectedIndex)
        {
            _presets = presets;
            foreach (var swatch in _swatches)
                Destroy(swatch.gameObject);
            _swatches.Clear();
            if (slimeColorRoot != null)
            {
                slimeColorRoot.parent.gameObject.SetActive(presets.Count > 0);
                for (var i = 0; i < presets.Count; i++)
                {
                    var index = i;
                    var go = _elements.Create(SwatchName + presets[i].Id, slimeColorRoot, typeof(RectTransform),
                        typeof(CanvasRenderer), typeof(SlimeColorSwatch), typeof(FeedbackButton));
                    var swatch = go.GetComponent<SlimeColorSwatch>();
                    swatch.SetColors(presets[i].BaseColor, presets[i].TopColor);
                    var button = go.GetComponent<FeedbackButton>();
                    button.targetGraphic = swatch;
                    button.interactable = _interactable;
                    var navigation = button.navigation;
                    navigation.mode = UnityEngine.UI.Navigation.Mode.Automatic;
                    button.navigation = navigation;
                    button.onClick.AddListener(() => SlimeColorRequested?.Invoke(index));
                    _swatches.Add(swatch);
                }
            }
            SetSelectedSlimeColor(selectedIndex);
        }

        public void SetSelectedSlimeColor(int index)
        {
            for (var i = 0; i < _swatches.Count; i++)
                _swatches[i].SetSelected(i == index);
            if (slimePreview != null && _presets != null && index >= 0 && index < _presets.Count)
                slimePreview.SetColors(_presets[index]);
        }

        protected override void Awake()
        {
            if (playButton != null)
                playButton.onClick.AddListener(OnPlay);
            if (settingsButton != null)
                settingsButton.onClick.AddListener(OnSettings);
            if (wallButton != null)
                wallButton.onClick.AddListener(OnWall);
            base.Awake();
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            if (playButton != null)
                playButton.interactable = interactable;
            if (settingsButton != null)
                settingsButton.interactable = interactable;
            if (wallButton != null)
                wallButton.interactable = interactable;
            foreach (var swatch in _swatches)
                swatch.GetComponent<UnityEngine.UI.Button>().interactable = interactable;
        }

        private void OnPlay() => PlayRequested?.Invoke();
        private void OnSettings() => SettingsRequested?.Invoke();
        private void OnWall() => WallRequested?.Invoke();

        protected override void OnDestroy()
        {
            if (playButton != null)
                playButton.onClick.RemoveListener(OnPlay);
            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OnSettings);
            if (wallButton != null)
                wallButton.onClick.RemoveListener(OnWall);
            base.OnDestroy();
        }
    }
}
