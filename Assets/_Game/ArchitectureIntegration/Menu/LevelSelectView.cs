using System;
using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Integration.Menu
{
    /// <summary>
    /// Grid of campaign levels: completed levels, the next level to play and locked ones.
    /// Cells are cloned from a template child, so the grid follows the campaign length.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LevelSelectView : ViewBase
    {
        [SerializeField] private RectTransform grid;
        [SerializeField] private UnityEngine.UI.Button cellTemplate;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private Color completedColor = new Color(0.35f, 0.94f, 0.27f);
        [SerializeField] private Color nextColor = new Color(1f, 0.4f, 0.33f);
        [SerializeField] private Color lockedColor = new Color(0.2f, 0.29f, 0.32f);

        public event Action<int> LevelChosen;
        public event Action BackRequested;

        private readonly List<UnityEngine.UI.Button> _cells = new List<UnityEngine.UI.Button>();
        private IObjectResolver _resolver;

        [Inject] public void Construct(IObjectResolver resolver) => _resolver = resolver;

        public IReadOnlyList<UnityEngine.UI.Button> Cells => _cells;

        protected override void Awake()
        {
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
            base.Awake();
        }

        protected override void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBack);
            foreach (var cell in _cells) if (cell != null) cell.onClick.RemoveAllListeners();
            base.OnDestroy();
        }

        /// <param name="unlocked">Highest playable level (count + 1 when everything is completed).</param>
        public void SetLevels(int count, int unlocked)
        {
            while (_cells.Count < count)
            {
                var level = _cells.Count + 1;
                var cell = Instantiate(cellTemplate, grid);
                _resolver?.InjectGameObject(cell.gameObject);
                cell.name = "Level " + level;
                cell.gameObject.SetActive(true);
                cell.onClick.AddListener(() => LevelChosen?.Invoke(level));
                _cells.Add(cell);
            }
            for (var i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                var level = i + 1;
                var open = i < count && level <= unlocked;
                cell.gameObject.SetActive(i < count);
                cell.interactable = open;
                var color = level < unlocked ? completedColor : level == unlocked ? nextColor : lockedColor;
                if (cell.targetGraphic != null) cell.targetGraphic.color = color;
                var label = cell.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = level.ToString();
                    label.alpha = open ? 1f : 0.35f;
                }
            }
        }

        private void OnBack() => BackRequested?.Invoke();
    }
}
