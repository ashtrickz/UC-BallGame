using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Foundation;
using Game.Users;

namespace Game.Integration.Bootstrap
{
    public sealed class UserInitializationStep : IInitializationStep
    {
        private readonly IUserService _users;
        public string Name => "Loading user profile...";
        public bool CanRetry => true;
        public UserInitializationStep(IUserService users) => _users = users;
        public UniTask InitializeAsync(CancellationToken cancellationToken) => _users.InitializeAsync(cancellationToken);
    }
}
