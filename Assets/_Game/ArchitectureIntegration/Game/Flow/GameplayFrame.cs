using System;
using Slime.Gameplay.View;
using UnityEngine;
using VContainer.Unity;

namespace Slime.Gameplay.Presentation
{
    /// <summary>
    /// The single per-frame pass of the gameplay scene, in an explicit order:
    /// 1) the clock (resumes animations waiting for the frame), 2) input and charge with the HUD,
    /// 3) the stage views. The camera follows on LateUpdate; tutorial cut-outs are computed after it.
    /// </summary>
    public sealed class GameplayFrame : ITickable, IDisposable
    {
        private readonly GameClock _clock;
        private readonly ChargeInputController _charge;
        private readonly GameplayStage _stage;

        public GameplayFrame(GameplayStage stage, ChargeInputController charge)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _charge = charge ?? throw new ArgumentNullException(nameof(charge));
            _clock = stage.Clock;
        }

        public void Tick()
        {
            _clock.Tick(Time.deltaTime);
            _charge.Tick();
            var delta = _clock.Delta;
            var views = _stage.FrameViews;
            for (var i = 0; i < views.Count; i++)
                views[i].Tick(delta);
        }

        public void Dispose()
        {
            _clock.Stop();
        }
    }
}
