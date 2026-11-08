using TMPro;
using UnityEngine;

namespace Game.Prototypes.SlimeLab
{
    public sealed class SlimeLabHud : MonoBehaviour
    {
        [SerializeField] private SlimeLabDemo demo;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private TMP_Text massLabel;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private RectTransform massFill;
        [SerializeField] private RectTransform controls;
        [SerializeField] private UnityEngine.UI.Button autoButton;
        [SerializeField] private UnityEngine.UI.Button hopButton;
        [SerializeField] private UnityEngine.UI.Button resetButton;
        private string _lastReadout;
        private Canvas _canvas;
        private RectTransform _canvasRect;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _canvas = GetComponentInParent<Canvas>();
            _canvasRect = _canvas.GetComponent<RectTransform>();
            autoButton.onClick.AddListener(demo.ToggleAutoDemo);
            hopButton.onClick.AddListener(demo.ManualJump);
            resetButton.onClick.AddListener(demo.ResetDemo);
        }

        private void OnDisable()
        {
            autoButton.onClick.RemoveListener(demo.ToggleAutoDemo);
            hopButton.onClick.RemoveListener(demo.ManualJump);
            resetButton.onClick.RemoveListener(demo.ResetDemo);
        }

        private void Update()
        {
            controls.anchoredPosition = new Vector2(0, _canvasRect.rect.width < 1100 ? 90 : 0);
            if (_lastReadout != demo.Readout)
            {
                _lastReadout = demo.Readout;
                stateLabel.text = _lastReadout;
            }
            massLabel.SetText("BODY  {0:0}%", demo.RemainingMass * 100);
            massFill.anchorMax = new Vector2(demo.RemainingMass, 1);
            autoLabel.text = demo.autoDemo ? "AUTO DEMO / ON" : "AUTO DEMO / OFF";
        }

        public bool IsOverControls(Vector2 point)
        {
            return controls != null && RectTransformUtility.RectangleContainsScreenPoint(controls, point,
                _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null);
        }
    }
}
