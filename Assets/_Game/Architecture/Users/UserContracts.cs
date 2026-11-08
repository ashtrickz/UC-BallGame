using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Foundation;

namespace Game.Users
{
    [Serializable]
    public sealed class UserSaveData
    {
        public int FormatVersion = 1;
        public string UserId;
        public int Revision;
        public string PlayerName = "Player";
        public long Gold;

        public UserSaveData Copy() => (UserSaveData)MemberwiseClone();

        public void Validate(string userId)
        {
            if (FormatVersion != 1 || string.IsNullOrWhiteSpace(userId) || UserId != userId ||
                Revision < 0 || string.IsNullOrWhiteSpace(PlayerName) || PlayerName.Length > 64 || Gold < 0)
                throw new InvalidOperationException("Invalid local user profile.");
        }
    }

    public sealed class UserChange
    {
        public string PlayerName { get; set; }
        public long GoldDelta { get; set; }
    }

    public interface IUserCache
    {
        string LastUserId { get; }
        UserSaveData Load(string userId);
        void Save(UserSaveData data);
    }

    public interface IUserService : IAsyncInitializable
    {
        event Action Changed;
        string UserId { get; }
        bool HasCachedState { get; }
        void SelectUser(string userId);
        void Submit(UserChange change);
    }
}
