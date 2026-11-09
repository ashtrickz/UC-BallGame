using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>Fits its anchors to the screen service snapshot when layout changes.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private IScreenService _screen;
        private bool _subscribed;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void BindScreen(IScreenService screen)
        {
            if (screen == null)
                throw new ArgumentNullException(nameof(screen));

            if (ReferenceEquals(_screen, screen))
                return;

            Unsubscribe();
            _screen = screen;
            if (isActiveAndEnabled)
                Subscribe();
            Apply();
        }

        private void OnEnable()
        {
            Subscribe();
            Apply();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (_screen == null || _subscribed)
                return;

            _screen.SafeAreaChanged += Apply;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _screen.SafeAreaChanged -= Apply;
            _subscribed = false;
        }

        private void Apply()
        {
            if (_screen == null || _screen.Size.x <= 0 || _screen.Size.y <= 0)
                return;

            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            var safeArea = _screen.SafeArea;
            var size = _screen.Size;
            _rectTransform.anchorMin = new Vector2(safeArea.xMin / size.x, safeArea.yMin / size.y);
            _rectTransform.anchorMax = new Vector2(safeArea.xMax / size.x, safeArea.yMax / size.y);
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
