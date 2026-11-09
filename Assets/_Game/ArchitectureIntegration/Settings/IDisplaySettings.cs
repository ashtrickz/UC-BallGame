using System.Collections.Generic;
using Game.Settings;

namespace Game.Integration.Settings
{
    /// <summary>Isolates desktop display controls from the shared settings runtime.</summary>
    public interface IDisplaySettings
    {
        ResolutionOption Resolution
        {
            get;
        }
        WindowMode WindowMode
        {
            get;
        }
        IReadOnlyList<ResolutionOption> Resolutions
        {
            get;
        }
        void SetDisplay(ResolutionOption resolution, WindowMode mode);
        void Reset();
    }
}
