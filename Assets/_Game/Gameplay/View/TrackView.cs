using Game.Foundation;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Visual lane on the floor whose width is the corridor the slime currently needs (D + 2 × gap).
    /// It narrows with the slime, so the player can read which crystals still block the way.
    /// </summary>
    public sealed class TrackView : MonoBehaviour, IFrameTickable
    {
        private const float DefaultEdgeWidth = 0.05f;
        private const float MinLength = 0.1f;
        private const float Half = 0.5f;
        private const float LaneHeight = 0.004f;
        private const float LaneThickness = 0.008f;
        private const float EdgeHeight = 0.008f;
        private const float EdgeThickness = 0.016f;

        [SerializeField] private Transform lane;
        [SerializeField] private Transform leftEdge;
        [SerializeField] private Transform rightEdge;
        [SerializeField, Min(0.005f)] private float edgeWidth = DefaultEdgeWidth;

        private SlimeViewConfig _config;
        private float _width = 1;
        private float _target = 1;
        private float _velocity;
        private float _startZ;
        private float _endZ;

        public void Bind(SlimeViewConfig config)
        {
            _config = config;
        }

        public void Build(float startZ, float endZ, float width)
        {
            _startZ = startZ;
            _endZ = endZ;
            _width = width;
            _target = width;
            _velocity = 0;
            Apply();
        }

        public void SetCorridorWidth(float width)
        {
            _target = width;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0 && _config != null)
                _width = Mathf.SmoothDamp(_width, _target, ref _velocity, _config.trackSmoothTime, Mathf.Infinity, deltaTime);

            Apply();
        }

        private void Apply()
        {
            var length = Mathf.Max(MinLength, _endZ - _startZ);
            var center = (_startZ + _endZ) * Half;
            if (lane != null)
            {
                lane.localPosition = new Vector3(0, LaneHeight, center);
                lane.localScale = new Vector3(_width, LaneThickness, length);
            }

            var half = _width * Half + edgeWidth * Half;
            if (leftEdge != null)
            {
                leftEdge.localPosition = new Vector3(-half, EdgeHeight, center);
                leftEdge.localScale = new Vector3(edgeWidth, EdgeThickness, length);
            }

            if (rightEdge != null)
            {
                rightEdge.localPosition = new Vector3(half, EdgeHeight, center);
                rightEdge.localScale = new Vector3(edgeWidth, EdgeThickness, length);
            }
        }
    }
}
