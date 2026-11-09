using System.Collections.Generic;
using Slime.Settings;

namespace Slime.Infrastructure.Settings
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
