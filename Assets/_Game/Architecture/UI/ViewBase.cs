using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.UI
{
    /// <summary>
    /// Base of every shown and hidden view. A transition is one sequence built on first use and replayed afterwards:
    /// it drives a normalized progress, and each transition starts from the current values, so an interrupted
    /// show or hide continues without a jump and nothing is allocated per transition after warm-up.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Architecture.UI", sourceAssembly: "Assembly-CSharp", sourceClassName: "ViewBase")]
    public abstract class ViewBase : MonoBehaviour, IView
    {
        private const float DefaultTransitionDuration = 0.2f;
        private const float Start = 0f;
        private const float End = 1f;

        [SerializeField, FormerlySerializedAs("_canvasGroup")] private CanvasGroup canvasGroup;
        [SerializeField, FormerlySerializedAs("_initiallyVisible")] private bool initiallyVisible;
        [SerializeField, FormerlySerializedAs("_animateFade")] private bool animateFade = true;
        [SerializeField, Min(0f), FormerlySerializedAs("_transitionDuration")] private float transitionDuration = DefaultTransitionDuration;

        private CancellationTokenSource _transitionCancellation;
        private Sequence _transition;
        private UniTaskCompletionSource _completion;
        private float _alphaFrom;
        private bool _isVisible;
        private bool _initialized;

        public bool IsVisible => _isVisible;

        protected bool TransitionVisible { get; private set; }

        protected float TransitionDuration => transitionDuration;

        protected virtual bool HasTransition => animateFade && canvasGroup;

        protected virtual void Awake()
        {
            if (_initialized)
                return;

            if (initiallyVisible)
                ShowInstant();
            else
                HideInstant();
        }

        public virtual UniTask ShowAsync(CancellationToken cancellationToken = default)
        {
            return TransitionAsync(true, cancellationToken);
        }

        public virtual UniTask HideAsync(CancellationToken cancellationToken = default)
        {
            return TransitionAsync(false, cancellationToken);
        }

        public virtual void ShowInstant()
        {
            SetInstant(true);
        }

        public virtual void HideInstant()
        {
            SetInstant(false);
        }

        protected virtual void BuildTransition(Sequence sequence)
        {
        }

        protected virtual void BeginTransition(bool visible)
        {
            if (canvasGroup)
                _alphaFrom = canvasGroup.alpha;
        }

        protected virtual void ApplyTransition(float progress)
        {
            if (!animateFade || !canvasGroup)
                return;

            var target = TransitionVisible ? End : Start;
            canvasGroup.alpha = Mathf.Lerp(_alphaFrom, target, DOVirtual.EasedValue(Start, End, progress, Ease.InOutSine));
        }

        protected virtual void StopTransition()
        {
            if (_transition != null && _transition.IsActive())
                _transition.Pause();
        }

        protected virtual void OnDisable()
        {
            CancelTransition();
        }

        protected virtual void OnDestroy()
        {
            CancelTransition();
            _transition?.Kill();
            _transition = null;
        }

        private async UniTask TransitionAsync(bool visible, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancelTransition();
            EnsureCanvasGroup();

            if (transitionDuration <= 0f)
            {
                SetInstant(visible);
                return;
            }

            _isVisible = visible;
            gameObject.SetActive(true);
            if (canvasGroup)
            {
                // A custom transition (e.g. the loading shader) owns visibility until it finishes.
                if (!animateFade)
                    canvasGroup.alpha = End;

                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = true;
            }

            if (!HasTransition)
            {
                FinishVisibility(visible);
                return;
            }

            var transition = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                this.GetCancellationTokenOnDestroy());
            _transitionCancellation = transition;
            var transitionToken = transition.Token;

            try
            {
                TransitionVisible = visible;
                BeginTransition(visible);
                _completion = new UniTaskCompletionSource();
                Transition().Restart();
                await _completion.Task.AttachExternalCancellation(transitionToken);
                transitionToken.ThrowIfCancellationRequested();
                FinishVisibility(visible);
            }
            finally
            {
                if (ReferenceEquals(_transitionCancellation, transition))
                {
                    _transitionCancellation = null;
                    StopTransition();
                    transition.Dispose();
                }
            }
        }

        private Sequence Transition()
        {
            if (_transition != null)
                return _transition;

            _transition = DOTween.Sequence()
                .Append(DOVirtual.Float(Start, End, transitionDuration, ApplyTransition).SetEase(Ease.Linear))
                .SetUpdate(true)
                .SetAutoKill(false)
                .SetLink(gameObject)
                .OnComplete(OnTransitionComplete)
                .Pause();
            BuildTransition(_transition);

            return _transition;
        }

        private void OnTransitionComplete()
        {
            _completion?.TrySetResult();
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
            if (canvasGroup)
            {
                canvasGroup.alpha = visible ? End : Start;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            gameObject.SetActive(visible);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
                TryGetComponent<CanvasGroup>(out canvasGroup);

            _initialized = true;
        }

        private void CancelTransition()
        {
            var transition = _transitionCancellation;
            _transitionCancellation = null;
            transition?.Cancel();
            StopTransition();
            _completion?.TrySetCanceled();
            transition?.Dispose();
        }
    }
}
