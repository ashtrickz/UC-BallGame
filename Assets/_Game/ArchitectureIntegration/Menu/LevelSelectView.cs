using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Integration.Menu
{
    /// <summary>
    /// Grid of campaign levels: completed levels, the next level to play and locked ones.
    /// Cells are cloned from a template child, so the grid follows the campaign length.
    /// </summary>
    public sealed class LevelSelectView : MonoBehaviour
    {
        [SerializeField] private RectTransform grid;
        [SerializeField] private Button cellTemplate;
        [SerializeField] private Button backButton;
        [SerializeField] private Color completedColor = new Color(0.35f, 0.94f, 0.27f);
        [SerializeField] private Color nextColor = new Color(1f, 0.4f, 0.33f);
        [SerializeField] private Color lockedColor = new Color(0.2f, 0.29f, 0.32f);

        public event Action<int> LevelChosen;
        public event Action BackRequested;

        private readonly List<Button> _cells = new List<Button>();

        public bool IsVisible => gameObject.activeSelf;
        public IReadOnlyList<Button> Cells => _cells;

        private void Awake()
        {
            if (cellTemplate != null) cellTemplate.gameObject.SetActive(false);
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBack);
            foreach (var cell in _cells) if (cell != null) cell.onClick.RemoveAllListeners();
        }

        /// <param name="unlocked">Highest playable level (count + 1 when everything is completed).</param>
        public void Show(int count, int unlocked)
        {
            while (_cells.Count < count)
            {
                var level = _cells.Count + 1;
                var cell = Instantiate(cellTemplate, grid);
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
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnBack() => BackRequested?.Invoke();
    }
}
