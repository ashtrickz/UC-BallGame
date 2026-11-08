using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Game.UI
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Architecture.UI", sourceAssembly: "Assembly-CSharp", sourceClassName: "ViewBase")]
    public abstract class ViewBase : MonoBehaviour, IView
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private bool _initiallyVisible;
        [SerializeField] private bool _animateFade = true;
        [SerializeField, Min(0f)] protected float _transitionDuration = 0.2f;

        public bool IsVisible => _isVisible;

        private CancellationTokenSource _transitionCancellation;
        private Tween _transitionTween;
        private bool _tweenFinishing;   // the tween is inside its own OnComplete: it despawns itself right after, and must not be killed from under that
        private bool _isVisible;
        private bool _initialized;

        protected virtual void Awake()
        {
            if (_initialized)
                return;

            if (_initiallyVisible)
                ShowInstant();
            else
                HideInstant();
        }

        public virtual UniTask ShowAsync(CancellationToken cancellationToken = default) => TransitionAsync(true, cancellationToken);
        public virtual UniTask HideAsync(CancellationToken cancellationToken = default) => TransitionAsync(false, cancellationToken);
        public virtual void ShowInstant() => SetInstant(true);
        public virtual void HideInstant() => SetInstant(false);

        protected virtual Tween CreateTransitionTween(bool visible)
        {
            if (!_animateFade || !_canvasGroup)
                return null;

            return _canvasGroup.DOFade(visible ? 1f : 0f, _transitionDuration)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true);
        }

        protected virtual void KillTransitionTween()
        {
            if (!_tweenFinishing) _transitionTween?.Kill();
            _transitionTween = null;
        }

        protected virtual void OnDisable() => CancelTransition();
        protected virtual void OnDestroy() => CancelTransition();

        private async UniTask TransitionAsync(bool visible, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelTransition();
            EnsureCanvasGroup();

            if (_transitionDuration <= 0f)
            {
                SetInstant(visible);
                return;
            }

            _isVisible = visible;
            gameObject.SetActive(true);

            if (_canvasGroup)
            {
                if (!_animateFade)
                    // A custom transition (e.g. the loading shader) owns visibility until it finishes.
                    _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;
            }

            var transition = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, this.GetCancellationTokenOnDestroy());
            _transitionCancellation = transition;
            var transitionToken = transition.Token;

            try
            {
                _transitionTween = CreateTransitionTween(visible);
                if (_transitionTween != null)
                {
                    var completion = new UniTaskCompletionSource();
                    // UniTask continues inline, so the code after the await (which may deactivate this object and thereby cancel the transition)
                    // runs inside this callback: flag it so the cancellation does not kill a tween that is already finishing.
                    _transitionTween.OnComplete(() =>
                        {
                            _tweenFinishing = true;
                            try { completion.TrySetResult(); }
                            finally { _tweenFinishing = false; }
                        })
                        .OnKill(() => completion.TrySetCanceled());
                    await completion.Task.AttachExternalCancellation(transitionToken);
                }

                transitionToken.ThrowIfCancellationRequested();
                FinishVisibility(visible);
            }
            finally
            {
                if (ReferenceEquals(_transitionCancellation, transition))
                {
                    _transitionCancellation = null;
                    KillTransitionTween();
                    transition.Dispose();
                }
            }
        }

        private void SetInstant(bool visible)
        {
            CancelTransition();
            EnsureCanvasGroup();
            FinishVisibility(visible);
        }

        private void FinishVisibility(bool visible)
        {
            _isVisible = visible;

            if (_canvasGroup)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.interactable = visible;
                _canvasGroup.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup == null)
                TryGetComponent<CanvasGroup>(out _canvasGroup);

            _initialized = true;
        }

        private void CancelTransition()
        {
            var transition = _transitionCancellation;
            _transitionCancellation = null;
            transition?.Cancel();
            KillTransitionTween();
            transition?.Dispose();
        }
    }
}
