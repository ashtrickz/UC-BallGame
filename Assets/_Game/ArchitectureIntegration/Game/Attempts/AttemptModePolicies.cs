using System;
using Slime.App.Game;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using Slime.Gameplay.Core.Levels;
using Slime.Gameplay.Core.Model;
using Slime.Localization;

namespace Slime.Gameplay.Presentation
{
    /// <summary>The destination of one icon action on the result card.</summary>
    public readonly struct ResultChoice
    {
        public static readonly ResultChoice None = default;

        public ResultChoice(LevelRequest request)
        {
            Request = request;
        }

        public LevelRequest Request { get; }

        public bool IsNone => Request == null;
    }

    /// <summary>Localized copy and choices of the result card for a finished attempt.</summary>
    public sealed class AttemptResult
    {
        public AttemptResult(string title, string subtitle, ResultChoice primary, ResultChoice secondary)
        {
            Title = title;
            Subtitle = subtitle;
            Primary = primary;
            Secondary = secondary;
        }

        public string Title { get; }

        public string Subtitle { get; }

        public ResultChoice Primary { get; }

        public ResultChoice Secondary { get; }
    }

    /// <summary>Everything that differs between level modes: progress, retry, next level, HUD label and result card.</summary>
    public interface IAttemptModePolicy
    {
        Type RequestType { get; }

        bool OffersTutorial(AttemptContext context);

        string Label(AttemptContext context);

        LevelRequest Retry(AttemptContext context);

        LevelRequest Next(AttemptContext context);

        void RecordWin(AttemptContext context);

        AttemptResult Won(AttemptContext context);

        AttemptResult Lost(AttemptContext context);
    }

    /// <summary>Shared result copy of all modes; a mode overrides only what differs.</summary>
    public abstract class AttemptModePolicy<TRequest> : IAttemptModePolicy where TRequest : LevelRequest
    {
        private const string PerfectHintKey = "result.hint_perfect";
        private const string ShotsHintKey = "result.hint_shots";
        private const string MassHintKey = "result.hint_mass";
        private const string BothHintKey = "result.hint_both";
        private const string PopTitleKey = "result.pop";
        private const string OverchargeKey = "result.overcharge";
        private const string NoAmmoKey = "result.no_ammo";
        private const int FirstCampaignLevel = 1;

        protected AttemptModePolicy(ILocalizationService localization)
        {
            Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        public Type RequestType => typeof(TRequest);

        protected ILocalizationService Localization { get; }

        public virtual bool OffersTutorial(AttemptContext context)
        {
            return false;
        }

        public abstract string Label(AttemptContext context);

        public abstract LevelRequest Retry(AttemptContext context);

        public virtual LevelRequest Next(AttemptContext context)
        {
            return new CampaignLevelRequest(FirstCampaignLevel);
        }

        public virtual void RecordWin(AttemptContext context)
        {
        }

        public abstract AttemptResult Won(AttemptContext context);

        public virtual AttemptResult Lost(AttemptContext context)
        {
            return Popped(context, new ResultChoice(Retry(context)), ResultChoice.None);
        }

        protected static TRequest RequestOf(AttemptContext context)
        {
            return (TRequest)context.Request;
        }

        protected string WinFeedback(AttemptContext context)
        {
            var model = context.Model;
            var criteria = context.Built.Stars;
            var shots = criteria.MeetsShotLimit(model.ShotsFired);
            var mass = criteria.MeetsMassTarget(StarCriteria.RemainingMassPercent(model.Diameter, model.Rules.D0));
            var key = shots && mass ? PerfectHintKey
                : !shots && !mass ? BothHintKey
                : !shots ? ShotsHintKey : MassHintKey;

            return Localization.Get(key);
        }

        protected AttemptResult Popped(AttemptContext context, ResultChoice primary, ResultChoice secondary)
        {
            var reasonKey = context.Model.Defeat == DefeatReason.Overcharge
                ? OverchargeKey
                : NoAmmoKey;

            return new AttemptResult(Localization.Get(PopTitleKey), Localization.Get(reasonKey), primary, secondary);
        }
    }

    /// <summary>Fixed campaign levels: saves progress on a win, "next" is the following level, the last one completes the campaign.</summary>
    public sealed class CampaignAttemptPolicy : AttemptModePolicy<CampaignLevelRequest>
    {
        private const int TutorialLevel = 1;
        private const string LabelKey = "hud.level";
        private const string LevelClearKey = "result.level_clear";
        private const string CampaignCompleteKey = "result.campaign_complete";
        private const string ColonyHomeKey = "result.colony_home";
        private const string Separator = " ";

        private readonly SlimeGameConfig _config;
        private readonly ICampaignProgress _progress;

        public CampaignAttemptPolicy(SlimeGameConfig config, ICampaignProgress progress, ILocalizationService localization)
            : base(localization)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        private int CampaignLength => _config.Campaign != null ? _config.Campaign.Count : 0;

        public override bool OffersTutorial(AttemptContext context)
        {
            return RequestOf(context).Number == TutorialLevel;
        }

        public override string Label(AttemptContext context)
        {
            return Localization.Format(LabelKey, RequestOf(context).Number);
        }

        public override LevelRequest Retry(AttemptContext context)
        {
            return new CampaignLevelRequest(RequestOf(context).Number);
        }

        public override LevelRequest Next(AttemptContext context)
        {
            var next = RequestOf(context).Number + 1;
            if (next <= CampaignLength)
                return new CampaignLevelRequest(next);

            return new EndlessLevelRequest(null);
        }

        public override void RecordWin(AttemptContext context)
        {
            _progress.MarkCompleted(RequestOf(context).Number, CampaignLength);
        }

        public override AttemptResult Won(AttemptContext context)
        {
            var number = RequestOf(context).Number;
            if (number < CampaignLength)
            {
                return new AttemptResult(
                    Localization.Format(LevelClearKey, number),
                    WinFeedback(context),
                    new ResultChoice(Next(context)),
                    new ResultChoice(Retry(context)));
            }

            return new AttemptResult(
                Localization.Get(CampaignCompleteKey),
                Localization.Get(ColonyHomeKey) + Separator + WinFeedback(context),
                new ResultChoice(new WallLevelRequest(null)),
                new ResultChoice(Retry(context)));
        }
    }

    /// <summary>Generated crystal walls: a win or a retry builds a new wall, a defeat offers the same wall again.</summary>
    public sealed class WallAttemptPolicy : AttemptModePolicy<WallLevelRequest>
    {
        private const string LabelKey = "hud.wall_seed";
        private const string WallBrokenKey = "result.wall_broken";

        public WallAttemptPolicy(ILocalizationService localization)
            : base(localization)
        {
        }

        public override string Label(AttemptContext context)
        {
            return Localization.Format(LabelKey, context.Seed);
        }

        public override LevelRequest Retry(AttemptContext context)
        {
            return new WallLevelRequest(null);
        }

        public override AttemptResult Won(AttemptContext context)
        {
            return new AttemptResult(
                Localization.Get(WallBrokenKey),
                WinFeedback(context),
                new ResultChoice(Retry(context)),
                new ResultChoice(new WallLevelRequest(context.Seed)));
        }

        public override AttemptResult Lost(AttemptContext context)
        {
            return Popped(
                context,
                new ResultChoice(new WallLevelRequest(context.Seed)),
                new ResultChoice(Retry(context)));
        }
    }

    /// <summary>Generated obstacle courses: every win or retry draws a new seed.</summary>
    public sealed class EndlessAttemptPolicy : AttemptModePolicy<EndlessLevelRequest>
    {
        private const string LabelKey = "hud.endless_seed";
        private const string HomeKey = "result.home";

        public EndlessAttemptPolicy(ILocalizationService localization)
            : base(localization)
        {
        }

        public override string Label(AttemptContext context)
        {
            return Localization.Format(LabelKey, context.Seed);
        }

        public override LevelRequest Retry(AttemptContext context)
        {
            return new EndlessLevelRequest(null);
        }

        public override AttemptResult Won(AttemptContext context)
        {
            return new AttemptResult(
                Localization.Get(HomeKey),
                WinFeedback(context),
                new ResultChoice(Retry(context)),
                ResultChoice.None);
        }
    }
}
