using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Portrait orthographic follow camera. Following owns the position (XZ of the slime's logical anchor, so hops
    /// never shake the frame); victory reframes the pool with a gentle zoom and orbit. A zoom transition starts from the
    /// current value: a charge pull-back is one continuous animation, and the release eases to the new base frame.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        private const float DefaultTourSmoothTime = 0.55f;
        private const float MinSmoothTime = 0.05f;
        private const float MinDiameter = 0.01f;
        private const float MaxZoomFactor = 1.2f;
        private const float MinZoomDuration = 0.01f;
        private const float ArrivedDistanceSqr = 0.0025f;
        private const float MinAspect = 0.3f;

        private Camera _camera;
        private SlimeViewConfig _config;
        private GameClock _clock = new GameClock();
        private Vector3 _focus, _focusVelocity, _target;
        private float _zoom = 1, _zoomFrom = 1, _zoomTo = 1, _zoomT = 1, _zoomDuration = 1;
        private float _d0 = 1;
        private Vector3? _tour;
        private bool _returning;
        private float _tourSmoothTime = DefaultTourSmoothTime;
        private Vector3? _victoryPoint;
        private Vector3 _victoryFromLookAt;
        private float _victoryT;

        public Camera Camera => _camera;
        public bool IsTouring => _tour.HasValue || _returning;

        public float Zoom => _zoom;

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            _clock = clock;
            _config = config;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        public void Snap(Vector3 anchor, float diameter, float initialDiameter)
        {
            _tour = null;
            _returning = false;
            _victoryPoint = null;
            _victoryT = 0;
            _d0 = Mathf.Max(MinDiameter, initialDiameter);
            _target = _focus = anchor;
            _focusVelocity = Vector3.zero;
            _zoom = _zoomFrom = _zoomTo = BaseZoom(diameter);
            _zoomT = 1;
            Apply();
        }

        public void Follow(Vector3 anchor) => _target = new Vector3(anchor.x, 0, anchor.z);

        public void ShowPoint(Vector3? point, float smoothTime = DefaultTourSmoothTime)
        {
            _tourSmoothTime = Mathf.Max(MinSmoothTime, smoothTime);
            if (point.HasValue)
            {
                _tour = new Vector3(point.Value.x, 0, point.Value.z);
                _returning = false;
            }
            else if (_tour.HasValue)
            {
                _tour = null;
                _returning = true;
            }
        }

        public Vector3 Focus => _focus;

        public float BaseZoom(float diameter)
        {
            if (_config == null)
                return 1;

            var factor = Mathf.Lerp(1, diameter / _d0, _config.zoomAdaptation);
            return Mathf.Clamp(factor, _config.minZoomFactor, MaxZoomFactor);
        }

        public void OnChargeStarted(float diameterAtStart) =>
            Transition(BaseZoom(diameterAtStart) * (1 + _config.chargePullBack), _config.chargeZoomDuration);

        public void SettleTo(float diameter) => Transition(BaseZoom(diameter), _config.releaseZoomDuration);

        public void ShowVictory(Vector3 poolPoint)
        {
            if (_config == null || _victoryPoint.HasValue)
                return;

            _tour = null;
            _returning = false;
            _victoryFromLookAt = _focus + Vector3.forward * (_config.lookAhead * _zoom);
            _victoryPoint = poolPoint + Vector3.up * _config.victoryFocusHeight;
            _victoryT = 0;
            Transition(_zoom * _config.victoryZoomFactor, _config.victoryCameraDuration);
        }

        private void Transition(float to, float duration)
        {
            if (Mathf.Approximately(to, _zoomTo) && _zoomT < 1)
                return;

            _zoomFrom = _zoom;
            _zoomTo = to;
            _zoomT = 0;
            _zoomDuration = Mathf.Max(MinZoomDuration, duration);
        }

        private void LateUpdate()
        {
            if (_config == null)
                return;

            var dt = _clock.Delta;
            if (_victoryPoint.HasValue)
            {
                if (dt > 0)
                    _victoryT = Mathf.Min(1, _victoryT + dt / Mathf.Max(MinSmoothTime, _config.victoryCameraDuration));
            }
            else if (_tour.HasValue || _returning)
            {
                // The tour runs in real time so it can play over a paused game.
                var goal = _tour ?? _target;
                _focus = Vector3.SmoothDamp(_focus, goal, ref _focusVelocity, _tourSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
                if (_returning && (_focus - _target).sqrMagnitude < ArrivedDistanceSqr)
                    _returning = false;
            }
            else if (dt > 0)
                _focus = Vector3.SmoothDamp(_focus, _target, ref _focusVelocity, _config.followSmoothTime, Mathf.Infinity, dt);
            if (dt > 0)
            {
                if (_zoomT < 1)
                {
                    _zoomT = Mathf.Min(1, _zoomT + dt / _zoomDuration);
                    _zoom = Mathf.LerpUnclamped(_zoomFrom, _zoomTo, _config.zoomEase.Evaluate(_zoomT));
                }
            }
            Apply();
        }

        private void Apply()
        {
            if (_config == null || _camera == null)
                return;

            var rotation = Quaternion.Euler(_config.cameraEuler);
            // Keep the slime in the lower part of the portrait frame: look ahead along the route.
            var lookAt = _focus + Vector3.forward * (_config.lookAhead * _zoom);
            if (_victoryPoint.HasValue)
            {
                var blend = GameClock.Smooth(_victoryT);
                lookAt = Vector3.Lerp(_victoryFromLookAt, _victoryPoint.Value, blend);
                rotation = Quaternion.Slerp(rotation, Quaternion.Euler(_config.cameraEuler + _config.victoryCameraEulerOffset), blend);
            }
            transform.rotation = rotation;
            transform.position = lookAt - rotation * Vector3.forward * _config.cameraDistance;
            var aspect = Mathf.Max(MinAspect, _camera.aspect);
            // Width-driven framing keeps the same lane width on every portrait aspect ratio.
            _camera.orthographicSize = _config.halfWidthAtD0 * _zoom / aspect;
        }
    }
}
