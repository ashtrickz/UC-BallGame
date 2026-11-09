using System;
using System.Collections.Generic;

namespace Game.Gameplay.Levels
{
    /// <summary>Builds a playable level for any request.</summary>
    public interface ILevelFactory
    {
        BuiltLevel Build(LevelRequest request);
    }

    /// <summary>
    /// Picks the level source registered for the request type. A new level type is a new request and source,
    /// with no change here or in the game flow.
    /// </summary>
    public sealed class LevelFactory : ILevelFactory
    {
        private readonly Dictionary<Type, ILevelSource> _sources = new Dictionary<Type, ILevelSource>();

        public LevelFactory(IReadOnlyList<ILevelSource> sources)
        {
            if (sources == null)
                throw new ArgumentNullException(nameof(sources));

            foreach (var source in sources)
            {
                if (_sources.ContainsKey(source.RequestType))
                    throw new InvalidOperationException($"Two level sources build {source.RequestType.Name}.");

                _sources.Add(source.RequestType, source);
            }
        }

        public BuiltLevel Build(LevelRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!_sources.TryGetValue(request.GetType(), out var source))
                throw new InvalidOperationException($"No level source builds {request.GetType().Name}.");

            return source.Build(request);
        }
    }
}
