using Slime.Settings;

namespace Slime.Infrastructure.Settings
{
    /// <summary>Selects the settings supported by the compiled player platform.</summary>
    public sealed class SettingsPackProvider
    {
#if UNITY_ANDROID || UNITY_IOS
        public SettingsPack Value => SettingsPack.Mobile;
#else
        public SettingsPack Value => SettingsPack.Desktop;
#endif
    }
}
