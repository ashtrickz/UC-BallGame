using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Foundation;
using Slime.Users;

namespace Slime.Infrastructure.Bootstrap
{
    public sealed class UserInitializationStep : IInitializationStep
    {
        private const string LoadingKey = "loading.user";

        private readonly IUserService _users;
        public string Name => LoadingKey;
        public bool CanRetry => true;
        public UserInitializationStep(IUserService users) => _users = users;
        public UniTask InitializeAsync(CancellationToken cancellationToken) => _users.InitializeAsync(cancellationToken);
    }
}
