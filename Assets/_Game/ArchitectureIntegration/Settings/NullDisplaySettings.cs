using System;
using System.Collections.Generic;
using Game.Settings;

namespace Game.Integration.Settings
{
    /// <summary>Retains requested display values without applying unsupported platform controls.</summary>
    public sealed class NullDisplaySettings : IDisplaySettings
    {
        public ResolutionOption Resolution
        {
            get;
            private set;
        }
        public WindowMode WindowMode
        {
            get;
            private set;
        }
        public IReadOnlyList<ResolutionOption> Resolutions => Array.Empty<ResolutionOption>();

        public void SetDisplay(ResolutionOption resolution, WindowMode mode)
        {
            Resolution = resolution;
            WindowMode = mode;
        }

        public void Reset()
        {
            Resolution = default;
            WindowMode = default;
        }
    }
}
