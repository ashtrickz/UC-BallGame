using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Users
{
    // Main-thread service. Persist before publishing; a failed write never changes the visible profile.
    public sealed class UserService : IUserService
    {
        public event Action Changed;
        public string UserId => _data?.UserId;
        public bool HasCachedState => _data != null;
        private readonly UserRepository _repository;
        private readonly IUserCache _cache;
        private UserSaveData _data;

        public UserService(UserRepository repository, IUserCache cache)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_data == null)
                SelectUser(_cache.LastUserId ?? Guid.NewGuid().ToString("N"));
            return UniTask.CompletedTask;
        }

        public void SelectUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("A user ID is required.", nameof(userId));

            if (UserId == userId)
                return;

            var next = _cache.Load(userId)?.Copy() ?? new UserSaveData { UserId = userId };
            next.Validate(userId);
            Commit(next);
        }

        public void Submit(UserChange change)
        {
            if (_data == null)
                throw new InvalidOperationException("Initialize or select a user first.");

            if (change == null)
                throw new ArgumentNullException(nameof(change));

            var next = _data.Copy();
            if (change.PlayerName != null)
                next.PlayerName = change.PlayerName;
            next.Gold = checked(next.Gold + change.GoldDelta);
            next.Revision = checked(next.Revision + 1);
            next.Validate(UserId);
            Commit(next);
        }

        private void Commit(UserSaveData next)
        {
            _cache.Save(next.Copy());
            _data = next;
            _repository.Publish(next);
            Changed?.Invoke();
        }
    }
}
