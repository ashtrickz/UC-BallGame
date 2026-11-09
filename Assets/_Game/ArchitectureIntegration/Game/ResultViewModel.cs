using System;

namespace Game.Integration.Game
{
    /// <summary>Result copy and actions chosen by the level controller.</summary>
    public sealed class ResultViewModel
    {
        public string Title { get; }
        public string Subtitle { get; }
        public string PrimaryLabel { get; }
        public string SecondaryLabel { get; }
        public Action PrimaryAction { get; }
        public Action SecondaryAction { get; }
        public Action MenuAction { get; }

        public ResultViewModel(string title, string subtitle, string primaryLabel, Action primaryAction,
            string secondaryLabel = null, Action secondaryAction = null, Action menuAction = null)
        {
            Title = title;
            Subtitle = subtitle;
            PrimaryLabel = primaryLabel;
            PrimaryAction = primaryAction;
            SecondaryLabel = secondaryLabel;
            SecondaryAction = secondaryAction;
            MenuAction = menuAction;
        }
    }
}
