namespace Slime.Infrastructure.Settings
{
    /// <summary>Owns the existing persisted settings keys across platform implementations.</summary>
    public static class SettingsStorageKeys
    {
        public const string TargetFps = "Settings.TargetFps";
        public const string Language = "Settings.Language";
        public const string VolumePrefix = "Settings.Volume.";
        public const string TogglePrefix = "Settings.Toggle.";
        public const string Width = "Settings.Width";
        public const string Height = "Settings.Height";
        public const string RefreshRate = "Settings.RefreshRate";
        public const string WindowMode = "Settings.WindowMode";
    }
}
