using Slime.Foundation;
using UnityEngine;

namespace Slime.Infrastructure.Frame
{
    /// <summary>Reads Unity's unscaled frame interval at the project loop boundary.</summary>
    public sealed class UnityFrameTimeSource : IFrameTimeSource
    {
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
    }
}
