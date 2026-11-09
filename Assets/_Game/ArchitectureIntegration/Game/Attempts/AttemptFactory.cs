using System;
using System.Collections.Generic;
using System.Threading;
using Slime.Gameplay.Core.Levels;
using Slime.Gameplay.Core.Model;

namespace Slime.Gameplay.Presentation
{
    /// <summary>Creates the context of a new attempt for a built level.</summary>
    public interface IAttemptFactory
    {
        AttemptContext Create(BuiltLevel built, CancellationToken parent);
    }

    /// <summary>Numbers attempts, creates their model and picks the mode policy registered for the request type.</summary>
    public sealed class AttemptFactory : IAttemptFactory
    {
        private readonly Dictionary<Type, IAttemptModePolicy> _policies = new Dictionary<Type, IAttemptModePolicy>();

        private int _lastId;

        public AttemptFactory(IReadOnlyList<IAttemptModePolicy> policies)
        {
            if (policies == null)
                throw new ArgumentNullException(nameof(policies));

            foreach (var policy in policies)
            {
                if (_policies.ContainsKey(policy.RequestType))
                    throw new InvalidOperationException($"Two attempt policies handle {policy.RequestType.Name}.");

                _policies.Add(policy.RequestType, policy);
            }
        }

        public AttemptContext Create(BuiltLevel built, CancellationToken parent)
        {
            if (built == null)
                throw new ArgumentNullException(nameof(built));

            var requestType = built.Request.GetType();
            if (!_policies.TryGetValue(requestType, out var policy))
                throw new InvalidOperationException($"No attempt policy handles {requestType.Name}.");

            var id = ++_lastId;
            var model = new AttemptModel(id, built.Rules, built.Level);

            return new AttemptContext(id, built, model, policy, parent);
        }
    }
}
