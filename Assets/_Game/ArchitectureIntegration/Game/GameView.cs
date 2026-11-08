using System;
using TMPro;
using UnityEngine;

namespace Game.Integration.Game
{
    public sealed class GameView : UI.ViewBase
    {
        [SerializeField] private UnityEngine.UI.Button settingsButton;
        [SerializeField] private TMP_Text playerName;
        public event Action SettingsRequested;

        protected override void Awake()
        {
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            base.Awake();
        }

        public void ShowPlayer(string name)
        {
            if (playerName != null) playerName.text = name;
        }

        private void OnSettings() => SettingsRequested?.Invoke();

        protected override void OnDestroy()
        {
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
            base.OnDestroy();
        }
    }
}