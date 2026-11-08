using System;
using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Architecture.UI", sourceAssembly: "Assembly-CSharp", sourceClassName: "PopupBase")]
    public abstract class PopupBase : ViewBase, IPopupView
    {
        public event Action CloseRequested;

        [SerializeField] private RectTransform _scaleRoot;
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private bool _animatePunch = true;
        [SerializeField, Min(0f)] private float _punchStrength = 0.08f;

        private Vector3 _restingScale;
        private bool _hasPunchScale;

        protected override void Awake()
        {
            if (_scaleRoot == null)
                _scaleRoot = transform as RectTransform;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnCloseRequested);

            base.Awake();
        }

        protected override Tween CreateTransitionTween(bool visible)
        {
            var fade = base.CreateTransitionTween(visible);
            if (!_animatePunch || _punchStrength <= 0f)
                return fade;

            if (_scaleRoot == null)
                _scaleRoot = transform as RectTransform;
            if (_scaleRoot == null)
                return fade;

            _restingScale = _scaleRoot.localScale;
            _hasPunchScale = true;
            var punch = _scaleRoot.DOPunchScale(Vector3.one * _punchStrength, _transitionDuration, 4, 0.5f)
                .SetUpdate(true);
            if (fade == null)
                return punch;

            return DOTween.Sequence().Join(fade).Join(punch).SetUpdate(true);
        }

        protected override void KillTransitionTween()
        {
            base.KillTransitionTween();
            if (_hasPunchScale && _scaleRoot != null)
                _scaleRoot.localScale = _restingScale;
            _hasPunchScale = false;
        }

        protected override void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnCloseRequested);

            base.OnDestroy();
        }

        protected void OnCloseRequested() => CloseRequested?.Invoke();

        /// <summary>Shows or hides the close button, or leaves it visible but inert (while something must not be walked away from).</summary>
        protected void SetCloseButton(bool visible, bool interactable)
        {
            if (_closeButton == null) return;
            _closeButton.gameObject.SetActive(visible);
            _closeButton.interactable = interactable;
        }

        public void Dispose()
        {
            HideInstant();
            Destroy(gameObject);
        }
    }
}
