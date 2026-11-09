using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Game.Foundation;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>Finish doors: two hinged leaves that swing open when the model reports the 5 m threshold.</summary>
    public sealed class DoorsView : MonoBehaviour, IFrameTickable
    {
        [SerializeField] private Transform leftHinge;
        [SerializeField] private Transform rightHinge;
        [SerializeField, Min(0.05f)] private float openDuration = 0.65f;
        [SerializeField] private float openAngle = 100f;

        private GameClock _clock = new GameClock();
        private float _t;
        private bool _opening;

        public bool IsOpen => _t >= 1;
        public float Z => transform.position.z;
        public event Action Opening;

        public void Bind(GameClock clock) => _clock = clock;

        public void Place(float doorZ)
        {
            transform.position = new Vector3(0, 0, doorZ);
            ResetClosed();
        }

        public void ResetClosed()
        {
            _t = 0;
            _opening = false;
            Apply();
        }

        public void Open()
        {
            if (_opening)
                return;

            _opening = true;
            Opening?.Invoke();
        }

        public async UniTask WaitOpenAsync(CancellationToken token)
        {
            while (!IsOpen)
                await _clock.NextFrame(token);
        }

        public void Tick(float deltaTime)
        {
            if (!_opening || _t >= 1)
                return;

            _t = Mathf.Min(1, _t + deltaTime / openDuration);
            Apply();
        }

        private void Apply()
        {
            var angle = openAngle * GameClock.Smooth(_t);
            if (leftHinge != null)
                leftHinge.localRotation = Quaternion.Euler(0, -angle, 0);
            if (rightHinge != null)
                rightHinge.localRotation = Quaternion.Euler(0, angle, 0);
        }
    }
}
