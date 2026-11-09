using System;
using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using Game.Gameplay.Level;

namespace Game.Gameplay.Levels
{
    /// <summary>Builds levels for one request type.</summary>
    public interface ILevelSource
    {
        Type RequestType { get; }

        BuiltLevel Build(LevelRequest request);
    }

    /// <summary>Typed base of a level source: checks the request type once and hands over the concrete request.</summary>
    public abstract class LevelSource<TRequest> : ILevelSource where TRequest : LevelRequest
    {
        public Type RequestType => typeof(TRequest);

        public BuiltLevel Build(LevelRequest request)
        {
            if (request is not TRequest typed)
                throw new ArgumentException($"{GetType().Name} cannot build {request?.GetType().Name}.", nameof(request));

            return Build(typed);
        }

        protected abstract BuiltLevel Build(TRequest request);
    }

    /// <summary>Fixed campaign levels from the campaign asset, each proven by its stored control sequence.</summary>
    public sealed class CampaignLevelSource : LevelSource<CampaignLevelRequest>
    {
        private const int FirstLevel = 1;

        private readonly ILevelConfig _config;

        public CampaignLevelSource(ILevelConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int Count => _config.CampaignCount;

        protected override BuiltLevel Build(CampaignLevelRequest request)
        {
            if (request.Number < FirstLevel || request.Number > Count)
                throw new ArgumentOutOfRangeException(nameof(request), $"Campaign has no level {request.Number}.");

            var spec = _config.CampaignSpec(request.Number - FirstLevel);
            var level = CampaignLevels.Build(_config.CreateSnapshot(), spec, out var rules);

            return new BuiltLevel(request, level, rules, level.Seed, spec.Stars);
        }
    }

    /// <summary>Generated crystal walls; a request without a seed draws one from the seed provider.</summary>
    public sealed class WallLevelSource : LevelSource<WallLevelRequest>
    {
        private readonly ILevelConfig _config;
        private readonly WallGenerator _generator;
        private readonly ISeedProvider _seeds;

        public WallLevelSource(ILevelConfig config, WallGenerator generator, ISeedProvider seeds)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        }

        protected override BuiltLevel Build(WallLevelRequest request)
        {
            var seed = request.Seed ?? _seeds.Next();
            var level = _generator.Generate(_config.CreateSnapshot(), _config.Wall, seed, out var rules);

            var stars = StarCriteria.FromControl(level, rules.D0, _config.Wall.StarExtraShots, _config.Wall.StarMassAllowancePercent);

            return new BuiltLevel(request, level, rules, seed, stars);
        }
    }

    /// <summary>Generated obstacle courses; a request without a seed draws one from the seed provider.</summary>
    public sealed class EndlessLevelSource : LevelSource<EndlessLevelRequest>
    {
        private readonly ILevelConfig _config;
        private readonly LevelGenerator _generator;
        private readonly ISeedProvider _seeds;

        public EndlessLevelSource(ILevelConfig config, LevelGenerator generator, ISeedProvider seeds)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        }

        protected override BuiltLevel Build(EndlessLevelRequest request)
        {
            var seed = request.Seed ?? _seeds.Next();
            var rules = _config.CreateSnapshot();
            var level = _generator.Generate(rules, seed);

            return new BuiltLevel(request, level, rules, seed);
        }
    }
}
