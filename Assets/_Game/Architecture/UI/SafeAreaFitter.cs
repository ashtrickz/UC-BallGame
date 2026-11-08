using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            var screenSize = new Vector2Int(
                Screen.width,
                Screen.height
            );

            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize != screenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safeArea = Screen.safeArea;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(
                Screen.width,
                Screen.height
            );

            _rectTransform.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height
            );

            _rectTransform.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height
            );

            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}