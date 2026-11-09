namespace Game.UI
{
    /// <summary>UI feedback identifiers retain the values authored on existing button prefabs.</summary>
    public enum UiFeedbackSound
    {
        Click,
        Confirm,
        Back,
        PanelOpen,
        PanelClose,
        ToggleOn,
        ToggleOff,
        SliderTick,
        Coin = 31
    }

    /// <summary>Connects a view's interaction feedback to platform integration without service dependencies.</summary>
    public interface IUiFeedback
    {
        void Play(UiFeedbackSound sound);
    }
}
