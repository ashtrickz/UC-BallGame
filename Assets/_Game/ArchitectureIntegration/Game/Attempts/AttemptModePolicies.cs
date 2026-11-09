using System;
using Game.App.Game;
using Game.Gameplay.Config;
using Game.Gameplay.Levels;
using Game.Gameplay.Model;
using Game.Localization;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>One result card button: its localization key and the level it plays.</summary>
    public readonly struct ResultChoice
    {
        public static readonly ResultChoice None = default;

        public ResultChoice(string labelKey, LevelRequest request)
        {
            LabelKey = labelKey;
            Request = request;
        }

        public string LabelKey { get; }

        public LevelRequest Request { get; }

        public bool IsNone => string.IsNullOrEmpty(LabelKey);
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
        protected const string RetryKey = "common.retry";

        private const string StarStatsKey = "result.star_stats";
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
            return Popped(context, new ResultChoice(RetryKey, Retry(context)), ResultChoice.None);
        }

        protected static TRequest RequestOf(AttemptContext context)
        {
            return (TRequest)context.Request;
        }

        protected string Stats(AttemptContext context)
        {
            var model = context.Model;
            var rules = model.Rules;
            var left = Mathf.RoundToInt((float)StarCriteria.RemainingMassPercent(model.Diameter, rules.D0));

            return Localization.Format(StarStatsKey, model.ShotsFired, left, context.Built.Stars.MaxShots,
                context.Built.Stars.MinRemainingMassPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
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
        private const string NextLevelKey = "action.next_level";
        private const string WallModeKey = "action.wall_mode";
        private const string ReplayLevelKey = "action.replay_level";
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
                    Stats(context),
                    new ResultChoice(NextLevelKey, Next(context)),
                    new ResultChoice(RetryKey, Retry(context)));
            }

            return new AttemptResult(
                Localization.Get(CampaignCompleteKey),
                Localization.Get(ColonyHomeKey) + Separator + Stats(context),
                new ResultChoice(WallModeKey, new WallLevelRequest(null)),
                new ResultChoice(ReplayLevelKey, Retry(context)));
        }
    }

    /// <summary>Generated crystal walls: a win or a retry builds a new wall, a defeat offers the same wall again.</summary>
    public sealed class WallAttemptPolicy : AttemptModePolicy<WallLevelRequest>
    {
        private const string LabelKey = "hud.wall_seed";
        private const string WallBrokenKey = "result.wall_broken";
        private const string NewWallKey = "action.new_wall";
        private const string SameWallKey = "action.same_wall";

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
                Stats(context),
                new ResultChoice(NewWallKey, Retry(context)),
                new ResultChoice(SameWallKey, new WallLevelRequest(context.Seed)));
        }

        public override AttemptResult Lost(AttemptContext context)
        {
            return Popped(
                context,
                new ResultChoice(SameWallKey, new WallLevelRequest(context.Seed)),
                new ResultChoice(NewWallKey, Retry(context)));
        }
    }

    /// <summary>Generated obstacle courses: every win or retry draws a new seed.</summary>
    public sealed class EndlessAttemptPolicy : AttemptModePolicy<EndlessLevelRequest>
    {
        private const string LabelKey = "hud.endless_seed";
        private const string HomeKey = "result.home";
        private const string NewLevelKey = "action.new_level";

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
                Stats(context),
                new ResultChoice(NewLevelKey, Retry(context)),
                ResultChoice.None);
        }
    }
}
