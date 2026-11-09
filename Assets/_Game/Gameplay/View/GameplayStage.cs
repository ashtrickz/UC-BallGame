using System.Collections.Generic;
using System;
using Game.Foundation;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Scene root of the gameplay presentation. Holds the views the controller drives and reports application
    /// pause and focus changes. Contains no rules.
    /// </summary>
    public sealed class GameplayStage : MonoBehaviour
    {
        [SerializeField] private SlimeViewConfig viewConfig;
        [SerializeField] private SlimeView slime;
        [SerializeField] private CrystalFieldView crystals;
        [SerializeField] private TrackView track;
        [SerializeField] private DoorsView doors;
        [SerializeField] private ImpactPreviewView preview;
        [SerializeField] private CameraRig cameraRig;
        [SerializeField] private PointerChargeInput input;
        [SerializeField] private Transform finishMarker;
        [SerializeField] private TutorialOverlay tutorial;
        [SerializeField] private PoolSplashEffect poolSplash;

        public GameClock Clock { get; } = new GameClock();
        public SlimeViewConfig ViewConfig => viewConfig;
        public SlimeView Slime => slime;
        public CrystalFieldView Crystals => crystals;
        public TrackView Track => track;
        public DoorsView Doors => doors;
        public ImpactPreviewView Preview => preview;
        public CameraRig CameraRig => cameraRig;
        public PointerChargeInput Input => input;
        public Transform FinishMarker => finishMarker;
        public TutorialOverlay Tutorial => tutorial;
        public PoolSplashEffect PoolSplash => poolSplash;
        public IReadOnlyList<IFrameTickable> FrameViews => _frameViews;
        public Transform FinishPool => finishMarker != null ? finishMarker.Find("Living pool") : null;

        public event Action<bool> BackgroundChanged;

        private bool _paused, _unfocused;
        private IFrameTickable[] _frameViews = Array.Empty<IFrameTickable>();

        private void Awake()
        {
            if (viewConfig == null || slime == null || crystals == null || track == null || doors == null || preview == null
                || cameraRig == null || input == null)
                throw new InvalidOperationException("GameplayStage has unassigned references in the Game scene.");
            slime.Bind(Clock, viewConfig);
            crystals.Bind(Clock, viewConfig);
            preview.Bind(viewConfig);
            track.Bind(viewConfig);
            doors.Bind(Clock);
            cameraRig.Bind(Clock, viewConfig);
            _frameViews = new IFrameTickable[] { slime, crystals, doors, preview, track };
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            Report();
        }
        private void OnApplicationFocus(bool focused)
        {
            _unfocused = !focused;
            Report();
        }

        private void Report()
        {
#if UNITY_EDITOR
            // In the Editor, clicking another window should not pause play testing; real pauses still apply.
            BackgroundChanged?.Invoke(_paused);
#else
            BackgroundChanged?.Invoke(_paused || _unfocused);
#endif
        }
    }
}
