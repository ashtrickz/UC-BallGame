using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Game.Users
{
    public sealed class FileUserCache : IUserCache
    {
        private readonly string _directory;
        public FileUserCache(string directory) => _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        public string LastUserId
        {
            get
            {
                var path = Path.Combine(_directory, "last-user");
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
        }

        public UserSaveData Load(string userId)
        {
            var path = AccountPath(userId);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return null;
            try { return Read(path, userId); }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is InvalidOperationException)
            {
                if (File.Exists(path + ".bak")) return Read(path + ".bak", userId);
                throw;
            }
        }

        public void Save(UserSaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.Validate(data.UserId);
            Directory.CreateDirectory(_directory);
            WriteAtomic(AccountPath(data.UserId), JsonUtility.ToJson(data));
            try { WriteAtomic(Path.Combine(_directory, "last-user"), data.UserId); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static UserSaveData Read(string path, string userId)
        {
            var data = JsonUtility.FromJson<UserSaveData>(File.ReadAllText(path));
            if (data == null) throw new InvalidOperationException("Empty user profile.");
            data.Validate(userId);
            return data;
        }

        private string AccountPath(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("A user ID is required.", nameof(userId));
            using var hash = SHA256.Create();
            var key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(userId))).Replace("-", "");
            return Path.Combine(_directory, key + ".json");
        }

        private static void WriteAtomic(string path, string value)
        {
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}
