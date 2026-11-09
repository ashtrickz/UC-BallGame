using System;

namespace Slime.Gameplay.Presentation
{
    /// <summary>Result copy and actions chosen by the level controller.</summary>
    public sealed class ResultViewModel
    {
        public string Title { get; }
        public string Subtitle { get; }
        public Action PrimaryAction { get; }
        public Action SecondaryAction { get; }
        public Action MenuAction { get; }

        public ResultViewModel(string title, string subtitle, Action primaryAction,
            Action secondaryAction = null, Action menuAction = null)
        {
            Title = title;
            Subtitle = subtitle;
            PrimaryAction = primaryAction;
            SecondaryAction = secondaryAction;
            MenuAction = menuAction;
        }
    }
}
