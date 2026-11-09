using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Slime.UI
{
    /// <summary>Popup view: the base fade plus a scale punch in the same reusable transition, and a close button.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Game.Architecture.UI", sourceAssembly: "Assembly-CSharp", sourceClassName: "PopupBase")]
    public abstract class PopupBase : ViewBase, IPopupView
    {
        private const float DefaultPunchStrength = 0.08f;
        private const int PunchVibrato = 4;
        private const float PunchElasticity = 0.5f;

        [SerializeField, FormerlySerializedAs("_scaleRoot")] private RectTransform scaleRoot;
        [SerializeField, FormerlySerializedAs("_closeButton")] private UnityEngine.UI.Button closeButton;
        [SerializeField, FormerlySerializedAs("_animatePunch")] private bool animatePunch = true;
        [SerializeField, Min(0f), FormerlySerializedAs("_punchStrength")] private float punchStrength = DefaultPunchStrength;

        private Vector3 _restingScale;
        private bool _hasPunchScale;

        public event Action CloseRequested;

        protected override bool HasTransition => base.HasTransition || HasPunch;

        private bool HasPunch => animatePunch && punchStrength > 0f && ScaleRoot != null;

        private RectTransform ScaleRoot
        {
            get
            {
                if (scaleRoot == null)
                    scaleRoot = transform as RectTransform;

                return scaleRoot;
            }
        }

        public void Dispose()
        {
            HideInstant();
            Destroy(gameObject);
        }

        protected override void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseRequested);

            base.Awake();
        }

        protected override void BuildTransition(Sequence sequence)
        {
            if (!HasPunch)
                return;

            _restingScale = ScaleRoot.localScale;
            _hasPunchScale = true;
            sequence.Join(ScaleRoot.DOPunchScale(Vector3.one * punchStrength, TransitionDuration, PunchVibrato, PunchElasticity));
        }

        protected override void StopTransition()
        {
            base.StopTransition();
            if (_hasPunchScale && scaleRoot != null)
                scaleRoot.localScale = _restingScale;
        }

        protected override void OnDestroy()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseRequested);

            base.OnDestroy();
        }

        protected void OnCloseRequested()
        {
            CloseRequested?.Invoke();
        }

        protected void SetCloseButton(bool visible, bool interactable)
        {
            if (closeButton == null)
                return;

            closeButton.gameObject.SetActive(visible);
            closeButton.interactable = interactable;
        }
    }
}
