using System;
using System.Collections.Generic;
using Slime.Foundation;
using UnityEngine;
using VContainer.Unity;

namespace Slime.Infrastructure.Frame
{
    /// <summary>Ticks enabled dynamic clients while deferring additions until the next frame.</summary>
    public sealed class TickRegistry : ITickRegistry, ITickable, IDisposable
    {
        private const int InitialCapacity = 32;
        private const string ReentrantTickMessage = "A tick registry cannot tick recursively.";
        private readonly IFrameTimeSource _time;
        private readonly List<Registration> _clients = new List<Registration>(InitialCapacity);
        private readonly Dictionary<IFrameTickable, int> _indices = new Dictionary<IFrameTickable, int>(InitialCapacity);
        private bool _ticking;
        private bool _needsCompaction;
        private bool _disposed;

        /// <summary>Keeps a removed registration distinct from a new registration of the same client.</summary>
        private struct Registration
        {
            public readonly IFrameTickable Client;
            public bool Active;

            public Registration(IFrameTickable client)
            {
                Client = client;
                Active = true;
            }
        }

        public TickRegistry(IFrameTimeSource time)
        {
            _time = time;
        }

        public void Register(IFrameTickable client)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TickRegistry));

            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (_indices.ContainsKey(client))
                return;

            _indices.Add(client, _clients.Count);
            _clients.Add(new Registration(client));
        }

        public void Unregister(IFrameTickable client)
        {
            if (_disposed || client == null || !_indices.TryGetValue(client, out var index))
                return;

            _indices.Remove(client);
            if (_ticking)
            {
                var registration = _clients[index];
                registration.Active = false;
                _clients[index] = registration;
                _needsCompaction = true;
                return;
            }

            _clients.RemoveAt(index);
            for (var i = index; i < _clients.Count; i++)
                _indices[_clients[i].Client] = i;
        }

        public void Tick()
        {
            if (_disposed)
                return;

            if (_ticking)
                throw new InvalidOperationException(ReentrantTickMessage);

            var deltaTime = _time.UnscaledDeltaTime;
            _ticking = true;
            var count = _clients.Count;
            try
            {
                for (var i = 0; i < count && !_disposed; i++)
                {
                    var registration = _clients[i];
                    if (!registration.Active)
                        continue;

                    try
                    {
                        registration.Client.Tick(deltaTime);
                    }
                    catch (Exception error)
                    {
                        // One broken client must not prevent other views from receiving the frame.
                        Debug.LogException(error);
                    }
                }
            }
            finally
            {
                _ticking = false;
                if (!_disposed && _needsCompaction)
                    Compact();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _clients.Clear();
            _indices.Clear();
        }

        private void Compact()
        {
            var write = 0;
            for (var read = 0; read < _clients.Count; read++)
            {
                var registration = _clients[read];
                if (!registration.Active)
                    continue;

                _clients[write] = registration;
                _indices[registration.Client] = write;
                write++;
            }

            _clients.RemoveRange(write, _clients.Count - write);
            _needsCompaction = false;
        }
    }
}
