using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Visual lane on the floor whose width is the corridor the slime currently needs (D + 2 × gap).
    /// It narrows with the slime, so the player can read which crystals still block the way.
    /// </summary>
    public sealed class TrackView : MonoBehaviour
    {
        [SerializeField] private Transform lane;
        [SerializeField] private Transform leftEdge;
        [SerializeField] private Transform rightEdge;
        [SerializeField, Min(0.005f)] private float edgeWidth = 0.05f;

        private GameClock _clock = new GameClock();
        private SlimeViewConfig _config;
        private float _width = 1, _target = 1, _velocity, _startZ, _endZ;

        public void Bind(GameClock clock, SlimeViewConfig config) { _clock = clock; _config = config; }

        public void Build(float startZ, float endZ, float width)
        {
            _startZ = startZ;
            _endZ = endZ;
            _width = _target = width;
            _velocity = 0;
            Apply();
        }

        public void SetCorridorWidth(float width) => _target = width;

        private void Update()
        {
            var dt = _clock.Delta;
            if (dt > 0 && _config != null)
                _width = Mathf.SmoothDamp(_width, _target, ref _velocity, _config.trackSmoothTime, Mathf.Infinity, dt);
            Apply();
        }

        private void Apply()
        {
            var length = Mathf.Max(0.1f, _endZ - _startZ);
            var center = (_startZ + _endZ) * 0.5f;
            if (lane != null)
            {
                lane.localPosition = new Vector3(0, 0.004f, center);
                lane.localScale = new Vector3(_width, 0.008f, length);
            }
            var half = _width * 0.5f + edgeWidth * 0.5f;
            if (leftEdge != null)
            {
                leftEdge.localPosition = new Vector3(-half, 0.008f, center);
                leftEdge.localScale = new Vector3(edgeWidth, 0.016f, length);
            }
            if (rightEdge != null)
            {
                rightEdge.localPosition = new Vector3(half, 0.008f, center);
                rightEdge.localScale = new Vector3(edgeWidth, 0.016f, length);
            }
        }
    }
}
