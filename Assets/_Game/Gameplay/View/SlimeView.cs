using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Config;
using UnityEngine;

namespace Game.Gameplay.View
{
    public enum SlimePresentationCue { HopPreparing, Takeoff, Landed, ShotReleased, ShotImpact, DefeatPreparing, Popped, PoolSplash }

    /// <summary>
    /// Procedural jelly slime ported from SlimeLab: hop preparation, arc, squash and stretch, damped landing
    /// spring, charging drop with a stretching neck, separation, flight, final weak hop and pop.
    /// The logical anchor (route position on the ground) is separate from the body's visual height and deformation.
    /// Every critical action is an awaitable that completes when its animation really finishes and is cancelled
    /// (never completed) by the attempt token.
    /// </summary>
    public sealed class SlimeView : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform[] eyes = Array.Empty<Transform>();
        [SerializeField] private Transform drop;
        [SerializeField] private MeshFilter bridge;
        [SerializeField] private SplashPool splashes;
        [SerializeField] private float bodyYaw = 150f;
        [Tooltip("Soft contact shadow on the logical anchor: grounds the body for the tilted orthographic camera.")]
        [SerializeField] private Transform shadow;

        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");
        private static readonly int PullId = Shader.PropertyToID("_Pull");
        private static readonly int PullDirectionId = Shader.PropertyToID("_PullDirection");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private const int Rings = 9, Sides = 16;

        private GameClock _clock = new GameClock();
        private SlimeViewConfig _config;
        private MaterialPropertyBlock _bodyProperties, _dropProperties, _bridgeProperties;
        private MeshRenderer _bodyRenderer, _dropRenderer, _bridgeRenderer;
        private Mesh _bridgeMesh;
        private Vector3[] _bridgeVertices, _bridgeNormals;

        private Vector3 _anchor;
        private float _diameter = 1.6f, _height, _idleTime;
        private float _prepare;            // 0..1 crouch before a hop
        private float _flightT = -1;       // 0..1 during the airborne part of a hop
        private float _hopClock;
        private float _impactSpring, _impactVelocity, _pull, _pullVelocity;
        private float _flatten = 1;        // defeat squash
        private bool _charging, _pinching, _flying, _popped;
        private bool _scripted;            // a scripted move (the finish dive) owns the body transform
        private float _dropDiameter, _pinchT, _chargeNormalized;
        private Vector3 _pinchStart;

        public Vector3 Anchor => _anchor;
        public float Diameter => _diameter;
        public Vector3 BodyCenter => body.position;
        public SplashPool Splashes => splashes;
        public event Action<SlimePresentationCue> PresentationCue;

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            _clock = clock ?? new GameClock();
            _config = config;
            splashes?.Bind(_clock);
        }

        private void Awake()
        {
            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _dropRenderer = drop.GetComponent<MeshRenderer>();
            _bodyProperties = new MaterialPropertyBlock();
            _dropProperties = new MaterialPropertyBlock();
            _bridgeRenderer = bridge.GetComponent<MeshRenderer>();
            _bridgeProperties = new MaterialPropertyBlock();
            _bridgeProperties.SetFloat(WobbleId, 0);
            _bridgeRenderer.SetPropertyBlock(_bridgeProperties);
            CreateBridgeMesh();
        }

        /// <summary>Overrides only this slime's colours, preserving its animated shader properties.</summary>
        public void SetColors(SlimeColorPreset preset)
        {
            void Apply(MeshRenderer renderer, MaterialPropertyBlock properties)
            {
                properties.SetColor(BaseColorId, preset.BaseColor);
                properties.SetColor(TopColorId, preset.TopColor);
                renderer.SetPropertyBlock(properties);
            }
            Apply(_bodyRenderer, _bodyProperties);
            Apply(_dropRenderer, _dropProperties);
            Apply(_bridgeRenderer, _bridgeProperties);
            splashes?.SetColors(preset);
        }

        private void OnDestroy()
        {
            if (_bridgeMesh != null) Destroy(_bridgeMesh);
        }

        /// <summary>Puts the slime, unhurt and idle, at the start of a new attempt.</summary>
        public void ResetForAttempt(Vector3 anchor, float diameter)
        {
            _anchor = anchor;
            _diameter = diameter;
            _height = 0;
            _prepare = 0;
            _flightT = -1;
            _impactSpring = _impactVelocity = _pull = _pullVelocity = 0;
            _flatten = 1;
            _charging = _pinching = _flying = _popped = _scripted = false;
            body.gameObject.SetActive(true);
            if (shadow != null) shadow.gameObject.SetActive(true);
            drop.gameObject.SetActive(false);
            bridge.gameObject.SetActive(false);
            splashes?.Clear();
            UpdateBody(0);
        }

        /// <summary>Logical diameter from the model (the visual body always shows the real resource).</summary>
        public void SetDiameter(float diameter) => _diameter = Mathf.Max(0.05f, diameter);

        // ---------------------------------------------------------------- hops

        public async UniTask HopAsync(Vector3 to, float heightScale, Action<float> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PresentationCue?.Invoke(SlimePresentationCue.HopPreparing);
            var from = _anchor;
            try
            {
                await _clock.Animate(_config.hopPrepare, t => _prepare = Mathf.Sin(t * Mathf.PI * 0.5f), token);
                _prepare = 0;
                _hopClock = 0;
                PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                await _clock.Animate(_config.hopFlight, t =>
                {
                    _flightT = t;
                    _hopClock += _clock.Delta;
                    _height = 4 * _config.hopHeight * heightScale * t * (1 - t);
                    _anchor = Vector3.Lerp(from, to, GameClock.Smooth(t));
                    progress?.Invoke(_anchor.z);
                }, token);
            }
            finally
            {
                _prepare = 0;
                _flightT = -1;
                _height = 0;
            }
            _anchor = to;
            PresentationCue?.Invoke(SlimePresentationCue.Landed);
            _impactVelocity = -6.3f * Mathf.Clamp(heightScale, 0.3f, 1f);
            splashes?.Burst(new Vector3(_anchor.x, 0.1f, _anchor.z), 6, 0.4f);
        }

        /// <summary>Hops along the route in equal steps no longer than the configured hop.</summary>
        public async UniTask MoveAsync(float toZ, Action<float> progress, Func<float, CancellationToken, UniTask> gate,
            CancellationToken token)
        {
            var distance = toZ - _anchor.z;
            if (distance <= 0.0001f) { progress?.Invoke(toZ); return; }
            var hops = Mathf.Max(1, Mathf.CeilToInt(distance / _config.maxHopLength));
            var step = distance / hops;
            var startZ = _anchor.z;
            for (var i = 1; i <= hops; i++)
            {
                var targetZ = i == hops ? toZ : startZ + step * i;
                if (gate != null) await gate(targetZ, token);
                var scale = Mathf.Clamp(step / _config.maxHopLength, 0.45f, 1f);
                await HopAsync(new Vector3(_anchor.x, 0, targetZ), scale, progress, token);
            }
        }

        // ---------------------------------------------------------------- charge and shot

        public void BeginCharge()
        {
            _charging = true;
            _pinching = false;
            _dropDiameter = 0;
            drop.gameObject.SetActive(true);
            bridge.gameObject.SetActive(true);
        }

        /// <summary>Per-frame charge visual: projectile diameter S and its share of the maximum useful charge.</summary>
        public void UpdateCharge(float projectileDiameter, float normalized)
        {
            _dropDiameter = projectileDiameter;
            _chargeNormalized = Mathf.Clamp01(normalized);
        }

        /// <summary>Unreleased charge returned to the body (pause): the drop is reabsorbed at once.</summary>
        public void CancelCharge()
        {
            if (!_charging) return;
            _charging = _pinching = false;
            drop.gameObject.SetActive(false);
            bridge.gameObject.SetActive(false);
            _impactVelocity += 2.5f;
        }

        /// <summary>Pinches the drop off and flies it to the first contact. Completes at the moment of impact.</summary>
        public async UniTask ShootAsync(Vector3 touchCenter, Vector3 impactPoint, float projectileDiameter, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _dropDiameter = projectileDiameter;
            _pinchStart = drop.position;
            _pinching = true;
            await _clock.Animate(_config.pinchDuration, t => _pinchT = t, token);
            _pinching = false;
            _charging = false;
            _flying = true;
            bridge.gameObject.SetActive(false);
            _impactVelocity -= 3.4f;
            PresentationCue?.Invoke(SlimePresentationCue.ShotReleased);
            var start = drop.position;
            splashes?.Burst(start, 7, 0.55f);
            var radius = projectileDiameter * 0.5f;
            var target = touchCenter.z > start.z + 0.1f ? touchCenter : impactPoint;
            target.y = Mathf.Max(radius, 0.15f);
            var distance = Vector3.Distance(start, target);
            var duration = _config.flightMin + distance * _config.flightPerMetre;
            var arc = _config.flightArc * Mathf.Clamp(distance / 4f, 0.35f, 1.4f);
            try
            {
                await _clock.Animate(duration, t =>
                {
                    drop.position = Vector3.Lerp(start, target, t) + Vector3.up * (arc * 4 * t * (1 - t));
                    var velocity = (target - start) + Vector3.up * (arc * 4 * (1 - 2 * t));
                    if (velocity.sqrMagnitude > 1e-6f) drop.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
                    drop.localScale = new Vector3(0.88f, 1.29f, 0.88f) * projectileDiameter;
                }, token);
            }
            finally { _flying = false; }
            drop.gameObject.SetActive(false);
            PresentationCue?.Invoke(SlimePresentationCue.ShotImpact);
            splashes?.Burst(impactPoint + Vector3.up * 0.2f, 14, 0.9f + radius);
        }

        // ---------------------------------------------------------------- defeat and victory

        /// <summary>Final weak hop into the blocking crystal (if any), squash and pop.</summary>
        public async UniTask DefeatAsync(Vector3? target, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PresentationCue?.Invoke(SlimePresentationCue.DefeatPreparing);
            CancelCharge();
            if (target.HasValue)
            {
                var flat = target.Value - _anchor;
                flat.y = 0;
                var reach = Mathf.Max(0, flat.magnitude - _diameter * 0.25f);
                var to = _anchor + (flat.sqrMagnitude > 1e-6f ? flat.normalized * reach : Vector3.zero);
                var from = _anchor;
                try
                {
                    await _clock.Animate(_config.hopPrepare, t => _prepare = Mathf.Sin(t * Mathf.PI * 0.5f) * 0.6f, token);
                    _prepare = 0;
                    PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                    await _clock.Animate(_config.defeatHopDuration, t =>
                    {
                        _flightT = t;
                        _height = 4 * _config.defeatHopHeight * t * (1 - t);
                        _anchor = Vector3.Lerp(from, to, GameClock.Smooth(t));
                    }, token);
                }
                finally { _flightT = -1; _height = 0; _prepare = 0; }
                _impactVelocity = -4f;
                PresentationCue?.Invoke(SlimePresentationCue.Landed);
            }
            await _clock.Animate(_config.popSquash, t => _flatten = Mathf.Lerp(1, 0.42f, GameClock.EaseOut(t)), token);
            _popped = true;
            PresentationCue?.Invoke(SlimePresentationCue.Popped);
            body.gameObject.SetActive(false);
            if (shadow != null) shadow.gameObject.SetActive(false);
            var center = new Vector3(_anchor.x, _diameter * 0.3f, _anchor.z);
            splashes?.Burst(center, 26, 1.5f, null, 1.6f);
            await _clock.Wait(_config.popLinger, token);
        }

        /// <summary>
        /// Victory: a cartoon jump into the colony pool. Deep crouch, launch with stretch, a short hang at the apex
        /// with a wobble, a stretched dive, splash and sink. Completes when the slime is under the surface.
        /// </summary>
        public async UniTask DiveIntoAsync(Transform pool, PoolSplashEffect splash, CancellationToken token)
        {
            if (pool == null) return;
            token.ThrowIfCancellationRequested();
            CancelCharge();
            _scripted = true;
            var radius = _diameter * 0.5f;
            var start = new Vector3(_anchor.x, 0, _anchor.z);
            var poolCenter = pool.position;
            var landing = new Vector3(poolCenter.x, 0, poolCenter.z);
            var surface = poolCenter.y + pool.lossyScale.y * 0.5f;
            var apex = Vector3.Lerp(start, landing, 0.55f);
            var apexHeight = 1.9f + radius;
            var splashed = false;
            try
            {
                // 1. Anticipation: a deep, wide crouch.
                PresentationCue?.Invoke(SlimePresentationCue.HopPreparing);
                await _clock.Animate(0.22f, t => Pose(start, radius, Mathf.Lerp(0.9f, 0.56f, GameClock.EaseOut(t)), 0, 0), token);
                splashes?.Burst(start + Vector3.up * 0.08f, 8, 0.55f);
                // 2. Launch: stretched upwards, decelerating towards the apex.
                PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                await _clock.Animate(0.36f, t =>
                {
                    var e = GameClock.EaseOut(t);
                    Pose(Vector3.Lerp(start, apex, e), radius, Mathf.Lerp(1.5f, 1.0f, t), apexHeight * e, -12 * t);
                }, token);
                // 3. Hang time: a little float and a jelly wobble in the air.
                await _clock.Animate(0.34f, t =>
                {
                    var wobble = 1 + Mathf.Sin(t * Mathf.PI * 3) * 0.1f * (1 - t);
                    Pose(apex, radius, wobble, apexHeight + Mathf.Sin(t * Mathf.PI) * 0.14f, Mathf.Lerp(-12, 10, t));
                }, token);
                // 4. Dive: accelerating, stretched like a drop, into the pool.
                var bottom = surface - radius * 2.2f;
                await _clock.Animate(0.3f, t =>
                {
                    var e = t * t;
                    var height = Mathf.Lerp(apexHeight, bottom, e);
                    Pose(Vector3.Lerp(apex, landing, e), radius * Mathf.Lerp(1, 0.9f, t), Mathf.Lerp(1.0f, 1.55f, t), height, 10);
                    if (!splashed && height < surface)
                    {
                        splashed = true;
                        PresentationCue?.Invoke(SlimePresentationCue.PoolSplash);
                        var entry = new Vector3(landing.x, surface, landing.z);
                        if (splash != null) splash.Play(entry, _diameter / 1.6f);
                        splashes?.Burst(entry + Vector3.up * 0.05f, 8, 1.1f, null, 1.3f);
                        PoolBounceAsync(pool, token).Forget();
                    }
                }, token);
                // 5. Sink and vanish under the surface.
                await _clock.Animate(0.2f, t => Pose(landing, radius * (1 - t), 1.2f, bottom - t * 0.3f, 10), token);
            }
            finally
            {
                if (body != null) body.gameObject.SetActive(false);
                if (shadow != null) shadow.gameObject.SetActive(false);
                _popped = true;
            }
        }

        /// <summary>Sets the body directly during a scripted move. <paramref name="height"/> is the body bottom above the floor.</summary>
        private void Pose(Vector3 ground, float radius, float squash, float height, float tilt)
        {
            _anchor = new Vector3(ground.x, 0, ground.z);
            var wide = 1f / Mathf.Sqrt(Mathf.Max(0.2f, squash));
            body.localScale = new Vector3(wide, squash, wide) * (radius * 2);
            body.position = new Vector3(ground.x, radius * squash + height + 0.055f, ground.z);
            body.rotation = Quaternion.Euler(tilt, bodyYaw, 0);
            if (shadow != null)
            {
                shadow.position = new Vector3(ground.x, 0.015f, ground.z);
                shadow.localScale = Vector3.one * (radius * 2.3f / (1 + Mathf.Max(0, height) * 0.7f));
            }
        }

        /// <summary>Secondary: the pool squashes and springs back after the splash.</summary>
        private async UniTaskVoid PoolBounceAsync(Transform pool, CancellationToken token)
        {
            var baseScale = pool.localScale;
            try
            {
                await _clock.Animate(0.7f, t =>
                {
                    var k = Mathf.Sin(t * Mathf.PI * 3) * (1 - t);
                    pool.localScale = new Vector3(baseScale.x * (1 + 0.12f * k), baseScale.y * (1 - 0.35f * k), baseScale.z * (1 + 0.12f * k));
                }, token);
            }
            catch (OperationCanceledException) { }
            finally { if (pool != null) pool.localScale = baseScale; }
        }

        // ---------------------------------------------------------------- per-frame body

        private void Update() => UpdateBody(_clock.Delta);

        private void UpdateBody(float dt)
        {
            if (_popped || _scripted) return;
            _idleTime += dt;
            var radius = _diameter * 0.5f;
            var squash = 0.91f + Mathf.Sin(_idleTime * 2.6f) * 0.015f;
            if (_prepare > 0) squash -= _prepare * 0.18f;
            if (_flightT >= 0) squash = 1.08f + Mathf.Sin(_flightT * Mathf.PI * 2) * 0.14f;
            _impactVelocity += (-95 * _impactSpring - 10.5f * _impactVelocity) * dt;
            _impactSpring += _impactVelocity * dt;
            _impactSpring = Mathf.Clamp(_impactSpring, -0.28f, 0.25f);
            squash = Mathf.Clamp(squash + _impactSpring, 0.65f, 1.35f) * _flatten;
            var targetPull = _charging && !_pinching ? _chargeNormalized * 0.22f
                : _pinching ? Mathf.Lerp(0.22f, 0.03f, _pinchT) : 0;
            _pull = dt > 0 ? Mathf.SmoothDamp(_pull, targetPull, ref _pullVelocity, 0.09f, Mathf.Infinity, dt) : _pull;
            var wide = 1f / Mathf.Sqrt(Mathf.Max(0.2f, squash));
            body.localScale = new Vector3(wide, squash, wide) * (radius * 2);
            body.position = new Vector3(_anchor.x, radius * squash + _height + 0.055f, _anchor.z);
            if (shadow != null)
            {
                shadow.position = new Vector3(_anchor.x, 0.015f, _anchor.z);
                shadow.localScale = Vector3.one * (radius * 2.3f / (1 + _height * 0.7f));
            }
            var tilt = _flightT >= 0 ? -Mathf.Sin(_flightT * Mathf.PI * 2) * 6 : _pull * 14;
            body.rotation = Quaternion.Euler(tilt, bodyYaw, 0);
            var forward = Vector3.forward;
            _bodyProperties.SetFloat(WobbleId, _config != null ? _config.surfaceWobble : 0.017f);
            _bodyProperties.SetFloat(ImpactId, Mathf.Abs(_impactSpring) * 3);
            _bodyProperties.SetFloat(PullId, _pull);
            _bodyProperties.SetVector(PullDirectionId, body.InverseTransformDirection(forward));
            _bodyRenderer.SetPropertyBlock(_bodyProperties);

            var blinkClock = Mathf.Repeat(_idleTime + 1.4f, 4.3f);
            var blink = blinkClock < 0.16f ? Mathf.Lerp(0.12f, 1, Mathf.Abs(blinkClock - 0.08f) / 0.08f) : 1;
            foreach (var eye in eyes) eye.localScale = new Vector3(0.105f, 0.16f * blink, 0.065f);

            if (_charging || _pinching) UpdateChargedDrop(radius, forward);
        }

        private void UpdateChargedDrop(float bodyRadius, Vector3 direction)
        {
            var radius = _dropDiameter * 0.5f;
            var center = body.position;
            var distance = bodyRadius * 1.32f + radius + 0.18f * _chargeNormalized;
            var position = center + direction * distance;
            if (_pinching) position = _pinchStart + direction * GameClock.Smooth(_pinchT) * 0.5f;
            drop.position = position;
            drop.rotation = Quaternion.identity;
            drop.localScale = Vector3.one * Mathf.Max(0.02f, _dropDiameter);
            var start = center + direction * bodyRadius * 0.72f;
            var end = position - direction * radius * 0.52f;
            UpdateBridge(start, end, Mathf.Min(radius, bodyRadius) * 0.7f, _pinching ? _pinchT : 0);
            _dropProperties.SetFloat(WobbleId, (_config != null ? _config.surfaceWobble : 0.017f) * 0.65f);
            _dropProperties.SetFloat(PhaseId, 2.7f);
            _dropRenderer.SetPropertyBlock(_dropProperties);
        }

        private void CreateBridgeMesh()
        {
            _bridgeMesh = new Mesh { name = "Slime neck (runtime)" };
            _bridgeMesh.MarkDynamic();
            _bridgeVertices = new Vector3[Rings * Sides];
            _bridgeNormals = new Vector3[_bridgeVertices.Length];
            var triangles = new int[(Rings - 1) * Sides * 6];
            var index = 0;
            for (var ring = 0; ring < Rings - 1; ring++)
                for (var side = 0; side < Sides; side++)
                {
                    var a = ring * Sides + side;
                    var b = ring * Sides + (side + 1) % Sides;
                    triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + Sides;
                    triangles[index++] = b; triangles[index++] = b + Sides; triangles[index++] = a + Sides;
                }
            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.triangles = triangles;
            bridge.sharedMesh = _bridgeMesh;
        }

        private void UpdateBridge(Vector3 start, Vector3 end, float radius, float pinch)
        {
            var axis = (end - start).normalized;
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.forward;
            var tangent = Vector3.Cross(axis, Vector3.up).normalized;
            var bitangent = Vector3.Cross(axis, tangent);
            var t0 = bridge.transform;
            for (var ring = 0; ring < Rings; ring++)
            {
                var t = ring / (float)(Rings - 1);
                var middle = Mathf.Sin(t * Mathf.PI);
                var width = radius * Mathf.Lerp(1, Mathf.Lerp(0.7f, 0.015f, pinch), middle);
                var c = Vector3.Lerp(start, end, t);
                for (var side = 0; side < Sides; side++)
                {
                    var angle = side * Mathf.PI * 2 / Sides;
                    var n = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    var i = ring * Sides + side;
                    _bridgeVertices[i] = t0.InverseTransformPoint(c + n * width);
                    _bridgeNormals[i] = t0.InverseTransformDirection(n);
                }
            }
            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.normals = _bridgeNormals;
            _bridgeMesh.RecalculateBounds();
        }
    }
}
