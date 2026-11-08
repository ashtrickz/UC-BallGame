using System;

namespace Game.Users
{
    public sealed class UserProfile
    {
        public string UserId { get; }
        public int Revision { get; }
        public string PlayerName { get; }
        public long Gold { get; }

        internal UserProfile(UserSaveData data)
        {
            UserId = data.UserId;
            Revision = data.Revision;
            PlayerName = data.PlayerName;
            Gold = data.Gold;
        }
    }

    public interface IUserRepository
    {
        event Action Changed;
        bool HasProfile { get; }
        UserProfile Profile { get; }
    }

    public sealed class UserRepository : IUserRepository
    {
        public event Action Changed;
        public bool HasProfile => Profile != null;
        public UserProfile Profile { get; private set; }

        internal void Publish(UserSaveData data)
        {
            Profile = new UserProfile(data);
            Changed?.Invoke();
        }
    }
}
