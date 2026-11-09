using System.Threading;
using System;
using Cysharp.Threading.Tasks;
using Slime.Foundation;
using Slime.Gameplay.Config;
using Slime.Gameplay.Core.Config;
using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Moments of the slime presentation that sound and haptics react to.</summary>
    public enum SlimePresentationCue
    {
        HopPreparing,
        Takeoff,
        Landed,
        ShotReleased,
        ShotImpact,
        DefeatPreparing,
        Popped,
        PoolSplash,
    }

    /// <summary>
    /// Procedural jelly slime ported from SlimeLab: hop preparation, arc, squash and stretch, damped landing
    /// spring, charging drop with a stretching neck, separation, flight, final weak hop and pop.
    /// The logical anchor (route position on the ground) is separate from the body's visual height and deformation.
    /// Every critical action is an awaitable that completes when its animation really finishes and is cancelled
    /// (never completed) by the attempt token.
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.View", sourceAssembly: "Game.Gameplay.View", sourceClassName: "SlimeView")]
    public sealed class SlimeView : MonoBehaviour, IFrameTickable
    {
        private const string WobbleProperty = "_Wobble";
        private const string ImpactProperty = "_Impact";
        private const string PullProperty = "_Pull";
        private const string PullDirectionProperty = "_PullDirection";
        private const string PhaseProperty = "_Phase";
        private const string BaseColorProperty = "_BaseColor";
        private const string TopColorProperty = "_TopColor";
        private const string BridgeMeshName = "Slime neck (runtime)";

        private const int BridgeRings = 9;
        private const int BridgeSides = 16;
        private const int IndicesPerQuad = 6;

        private const float DefaultBodyYaw = 150f;
        private const float DefaultDiameter = 1.6f;
        private const float DefaultWobble = 0.017f;
        private const float MinDiameter = 0.05f;
        private const float HalfDiameter = 0.5f;
        private const float DiameterPerRadius = 2f;
        private const float QuarterTurn = Mathf.PI * 0.5f;
        private const float FullTurn = Mathf.PI * 2f;
        private const float ParabolaPeak = 4f;
        private const float ParabolaSlope = 2f;
        private const float MinDirectionSqr = 1e-6f;
        private const float NoFlight = -1f;

        private const float LandingImpulse = -6.3f;
        private const float LandingScaleMin = 0.3f;
        private const float LandingScaleMax = 1f;
        private const float LandingSplashHeight = 0.1f;
        private const int LandingSplashCount = 6;
        private const float LandingSplashSpeed = 0.4f;
        private const float MinMoveDistance = 0.0001f;
        private const float HopScaleMin = 0.45f;
        private const float HopScaleMax = 1f;

        private const float CancelChargeImpulse = 2.5f;
        private const float ReleaseRecoil = 3.4f;
        private const int ReleaseSplashMin = 4;
        private const int ReleaseSplashMax = 8;
        private const float ReleaseSplashSpeedMin = 0.35f;
        private const float ReleaseSplashSpeedMax = 0.6f;
        private const float MinTargetAhead = 0.1f;
        private const float MinFlightHeight = 0.15f;
        private const float ArcReferenceDistance = 4f;
        private const float ArcScaleMin = 0.35f;
        private const float ArcScaleMax = 1.4f;
        private const float ImpactSplashHeight = 0.2f;
        private const int ImpactSplashMin = 7;
        private const int ImpactSplashMax = 20;
        private const float ImpactSplashSpeedMin = 0.65f;
        private const float ImpactSplashSpeedMax = 1.85f;
        private const float ImpactSplashSizeMin = 0.8f;
        private const float ImpactSplashSizeMax = 1.25f;

        private const float DefeatReachShrink = 0.25f;
        private const float DefeatCrouch = 0.6f;
        private const float DefeatLandingImpulse = -4f;
        private const float PoppedFlatten = 0.42f;
        private const float PopSplashHeight = 0.3f;
        private const int PopSplashCount = 26;
        private const float PopSplashSpeed = 1.5f;
        private const float PopSplashSize = 1.6f;

        private const float DiveApexPoint = 0.55f;
        private const float DiveApexHeight = 1.9f;
        private const float CrouchDuration = 0.22f;
        private const float CrouchSquashFrom = 0.9f;
        private const float CrouchSquashTo = 0.56f;
        private const float TakeoffSplashHeight = 0.08f;
        private const int TakeoffSplashCount = 8;
        private const float TakeoffSplashSpeed = 0.55f;
        private const float LaunchDuration = 0.36f;
        private const float LaunchStretch = 1.5f;
        private const float LaunchTilt = -12f;
        private const float HangDuration = 0.34f;
        private const float HangWobbleTurns = 3f;
        private const float HangWobble = 0.1f;
        private const float HangFloat = 0.14f;
        private const float DiveTilt = 10f;
        private const float DiveDepth = 2.2f;
        private const float DiveDuration = 0.3f;
        private const float DiveShrink = 0.9f;
        private const float DiveStretch = 1.55f;
        private const float EntrySplashHeight = 0.05f;
        private const int EntrySplashCount = 8;
        private const float EntrySplashSpeed = 1.1f;
        private const float EntrySplashSize = 1.3f;
        private const float SinkDuration = 0.2f;
        private const float SinkSquash = 1.2f;
        private const float SinkDepth = 0.3f;
        private const float PoolBounceDuration = 0.7f;
        private const float PoolBounceTurns = 3f;
        private const float PoolBounceWiden = 0.12f;
        private const float PoolBounceFlatten = 0.35f;

        private const float MinSquash = 0.2f;
        private const float BodyLift = 0.055f;
        private const float ShadowHeight = 0.015f;
        private const float ShadowScale = 2.3f;
        private const float ShadowFade = 0.7f;
        private const float IdleSquash = 0.91f;
        private const float IdleBreath = 0.015f;
        private const float IdleBreathSpeed = 2.6f;
        private const float CrouchDepth = 0.18f;
        private const float FlightSquash = 1.08f;
        private const float FlightWobble = 0.14f;
        private const float SpringStiffness = 95f;
        private const float SpringDamping = 10.5f;
        private const float SpringMin = -0.28f;
        private const float SpringMax = 0.25f;
        private const float SquashMin = 0.65f;
        private const float SquashMax = 1.35f;
        private const float ChargePull = 0.22f;
        private const float PinchPull = 0.03f;
        private const float PullSmoothTime = 0.09f;
        private const float FlightTilt = 6f;
        private const float PullTilt = 14f;
        private const float ImpactShading = 3f;

        private const float BlinkOffset = 1.4f;
        private const float BlinkPeriod = 4.3f;
        private const float BlinkDuration = 0.16f;
        private const float BlinkClosed = 0.12f;
        private const float EyeWidth = 0.105f;
        private const float EyeHeight = 0.16f;
        private const float EyeDepth = 0.065f;

        private const float DropDistance = 1.32f;
        private const float DropChargeReach = 0.18f;
        private const float PinchTravel = 0.5f;
        private const float MinDropDiameter = 0.02f;
        private const float NeckStart = 0.72f;
        private const float NeckEnd = 0.52f;
        private const float NeckRadius = 0.7f;
        private const float NeckWaist = 0.7f;
        private const float PinchedWaist = 0.015f;
        private const float DropWobbleShare = 0.65f;
        private const float DropPhase = 2.7f;

        private static readonly int WobbleId = Shader.PropertyToID(WobbleProperty);
        private static readonly int ImpactId = Shader.PropertyToID(ImpactProperty);
        private static readonly int PullId = Shader.PropertyToID(PullProperty);
        private static readonly int PullDirectionId = Shader.PropertyToID(PullDirectionProperty);
        private static readonly int PhaseId = Shader.PropertyToID(PhaseProperty);
        private static readonly int BaseColorId = Shader.PropertyToID(BaseColorProperty);
        private static readonly int TopColorId = Shader.PropertyToID(TopColorProperty);
        private static readonly Vector3 DropFlightShape = new Vector3(0.88f, 1.29f, 0.88f);

        [SerializeField] private Transform body;
        [SerializeField] private Transform[] eyes = Array.Empty<Transform>();
        [SerializeField] private Transform drop;
        [SerializeField] private MeshFilter bridge;
        [SerializeField] private SplashPool splashes;
        [SerializeField] private float bodyYaw = DefaultBodyYaw;
        [Tooltip("Soft contact shadow on the logical anchor: grounds the body for the tilted orthographic camera.")]
        [SerializeField] private Transform shadow;

        private GameClock _clock = new GameClock();
        private SlimeViewConfig _config;
        private MaterialPropertyBlock _bodyProperties;
        private MaterialPropertyBlock _dropProperties;
        private MaterialPropertyBlock _bridgeProperties;
        private MeshRenderer _bodyRenderer;
        private MeshRenderer _dropRenderer;
        private MeshRenderer _bridgeRenderer;
        private Mesh _bridgeMesh;
        private Vector3[] _bridgeVertices;
        private Vector3[] _bridgeNormals;

        private Vector3 _anchor;
        private float _diameter = DefaultDiameter;
        private float _height;
        private float _idleTime;
        private float _prepare;
        private float _flightT = NoFlight;
        private float _hopClock;
        private float _impactSpring;
        private float _impactVelocity;
        private float _pull;
        private float _pullVelocity;
        private float _flatten = 1;
        private bool _charging;
        private bool _pinching;
        private bool _flying;
        private bool _popped;
        // A scripted move (the finish dive) owns the body transform.
        private bool _scripted;
        private float _dropDiameter;
        private float _pinchT;
        private float _chargeNormalized;
        private Vector3 _pinchStart;

        public event Action<SlimePresentationCue> PresentationCue;

        public Vector3 Anchor => _anchor;

        public float Diameter => _diameter;

        public Vector3 BodyCenter => body.position;

        public SplashPool Splashes => splashes;

        private float SurfaceWobble => _config != null ? _config.surfaceWobble : DefaultWobble;

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

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            _clock = clock ?? new GameClock();
            _config = config;
        }

        public void SetColors(SlimeColorPreset preset)
        {
            ApplyColors(_bodyRenderer, _bodyProperties, preset);
            ApplyColors(_dropRenderer, _dropProperties, preset);
            ApplyColors(_bridgeRenderer, _bridgeProperties, preset);
            splashes?.SetColors(preset);
        }

        public void ResetForAttempt(Vector3 anchor, float diameter)
        {
            _anchor = anchor;
            _diameter = diameter;
            _height = 0;
            _prepare = 0;
            _flightT = NoFlight;
            _impactSpring = 0;
            _impactVelocity = 0;
            _pull = 0;
            _pullVelocity = 0;
            _flatten = 1;
            _charging = false;
            _pinching = false;
            _flying = false;
            _popped = false;
            _scripted = false;
            body.gameObject.SetActive(true);
            if (shadow != null)
                shadow.gameObject.SetActive(true);

            drop.gameObject.SetActive(false);
            bridge.gameObject.SetActive(false);
            splashes?.Clear();
            UpdateBody(0);
        }

        public void SetDiameter(float diameter)
        {
            _diameter = Mathf.Max(MinDiameter, diameter);
        }

        public void Tick(float deltaTime)
        {
            UpdateBody(deltaTime);
            if (splashes != null)
                splashes.Tick(deltaTime);
        }

        public async UniTask HopAsync(Vector3 to, float heightScale, Action<float> progress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PresentationCue?.Invoke(SlimePresentationCue.HopPreparing);
            var from = _anchor;
            try
            {
                await _clock.Animate(_config.hopPrepare, t => _prepare = Mathf.Sin(t * QuarterTurn), token);
                _prepare = 0;
                _hopClock = 0;
                PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                await _clock.Animate(_config.hopFlight, t =>
                {
                    _flightT = t;
                    _hopClock += _clock.Delta;
                    _height = ParabolaPeak * _config.hopHeight * heightScale * t * (1 - t);
                    _anchor = Vector3.Lerp(from, to, GameClock.Smooth(t));
                    progress?.Invoke(_anchor.z);
                }, token);
            }
            finally
            {
                _prepare = 0;
                _flightT = NoFlight;
                _height = 0;
            }

            _anchor = to;
            PresentationCue?.Invoke(SlimePresentationCue.Landed);
            _impactVelocity = LandingImpulse * Mathf.Clamp(heightScale, LandingScaleMin, LandingScaleMax);
            splashes?.Burst(new Vector3(_anchor.x, LandingSplashHeight, _anchor.z), LandingSplashCount, LandingSplashSpeed);
        }

        public async UniTask MoveAsync(float toZ, Action<float> progress, Func<float, CancellationToken, UniTask> gate,
            CancellationToken token)
        {
            var distance = toZ - _anchor.z;
            if (distance <= MinMoveDistance)
            {
                progress?.Invoke(toZ);
                return;
            }

            var hops = Mathf.Max(1, Mathf.CeilToInt(distance / _config.maxHopLength));
            var step = distance / hops;
            var startZ = _anchor.z;
            for (var i = 1; i <= hops; i++)
            {
                var targetZ = i == hops
                    ? toZ
                    : startZ + step * i;
                if (gate != null)
                    await gate(targetZ, token);

                var scale = Mathf.Clamp(step / _config.maxHopLength, HopScaleMin, HopScaleMax);
                await HopAsync(new Vector3(_anchor.x, 0, targetZ), scale, progress, token);
            }
        }

        public void BeginCharge()
        {
            _charging = true;
            _pinching = false;
            _dropDiameter = 0;
            drop.gameObject.SetActive(true);
            bridge.gameObject.SetActive(true);
        }

        public void UpdateCharge(float projectileDiameter, float normalized)
        {
            _dropDiameter = projectileDiameter;
            _chargeNormalized = Mathf.Clamp01(normalized);
        }

        public void CancelCharge()
        {
            if (!_charging)
                return;

            _charging = false;
            _pinching = false;
            drop.gameObject.SetActive(false);
            bridge.gameObject.SetActive(false);
            _impactVelocity += CancelChargeImpulse;
        }

        public async UniTask ShootAsync(Vector3 touchCenter, Vector3 impactPoint, float projectileDiameter,
            float feedbackStrength, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var strength = Mathf.Clamp01(feedbackStrength);
            _dropDiameter = projectileDiameter;
            _pinchStart = drop.position;
            _pinching = true;
            await _clock.Animate(_config.pinchDuration, t => _pinchT = t, token);
            _pinching = false;
            _charging = false;
            _flying = true;
            bridge.gameObject.SetActive(false);
            _impactVelocity -= ReleaseRecoil;
            PresentationCue?.Invoke(SlimePresentationCue.ShotReleased);
            var start = drop.position;
            splashes?.Burst(
                start,
                Mathf.RoundToInt(Mathf.Lerp(ReleaseSplashMin, ReleaseSplashMax, strength)),
                Mathf.Lerp(ReleaseSplashSpeedMin, ReleaseSplashSpeedMax, strength));
            var radius = projectileDiameter * HalfDiameter;
            var target = touchCenter.z > start.z + MinTargetAhead
                ? touchCenter
                : impactPoint;
            target.y = Mathf.Max(radius, MinFlightHeight);
            var distance = Vector3.Distance(start, target);
            var duration = _config.flightMin + distance * _config.flightPerMetre;
            var arc = _config.flightArc * Mathf.Clamp(distance / ArcReferenceDistance, ArcScaleMin, ArcScaleMax);
            try
            {
                await _clock.Animate(duration, t =>
                {
                    drop.position = Vector3.Lerp(start, target, t) + Vector3.up * (arc * ParabolaPeak * t * (1 - t));
                    var velocity = (target - start) + Vector3.up * (arc * ParabolaPeak * (1 - ParabolaSlope * t));
                    if (velocity.sqrMagnitude > MinDirectionSqr)
                        drop.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);

                    drop.localScale = DropFlightShape * projectileDiameter;
                }, token);
            }
            finally
            {
                _flying = false;
            }

            drop.gameObject.SetActive(false);
            PresentationCue?.Invoke(SlimePresentationCue.ShotImpact);
            splashes?.Burst(
                impactPoint + Vector3.up * ImpactSplashHeight,
                Mathf.RoundToInt(Mathf.Lerp(ImpactSplashMin, ImpactSplashMax, strength)),
                Mathf.Lerp(ImpactSplashSpeedMin, ImpactSplashSpeedMax, strength),
                null,
                Mathf.Lerp(ImpactSplashSizeMin, ImpactSplashSizeMax, strength));
        }

        public async UniTask DefeatAsync(Vector3? target, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            PresentationCue?.Invoke(SlimePresentationCue.DefeatPreparing);
            CancelCharge();
            if (target.HasValue)
                await DefeatHopAsync(target.Value, token);

            await _clock.Animate(
                _config.popSquash,
                t => _flatten = Mathf.Lerp(1, PoppedFlatten, GameClock.EaseOut(t)),
                token);
            _popped = true;
            PresentationCue?.Invoke(SlimePresentationCue.Popped);
            body.gameObject.SetActive(false);
            if (shadow != null)
                shadow.gameObject.SetActive(false);

            var center = new Vector3(_anchor.x, _diameter * PopSplashHeight, _anchor.z);
            splashes?.Burst(center, PopSplashCount, PopSplashSpeed, null, PopSplashSize);
            await _clock.Wait(_config.popLinger, token);
        }

        public async UniTask DiveIntoAsync(Transform pool, PoolSplashEffect splash, CancellationToken token)
        {
            if (pool == null)
                return;

            token.ThrowIfCancellationRequested();
            CancelCharge();
            _scripted = true;
            var radius = _diameter * HalfDiameter;
            var start = new Vector3(_anchor.x, 0, _anchor.z);
            var poolCenter = pool.position;
            var landing = new Vector3(poolCenter.x, 0, poolCenter.z);
            var surface = poolCenter.y + pool.lossyScale.y * HalfDiameter;
            var apex = Vector3.Lerp(start, landing, DiveApexPoint);
            var apexHeight = DiveApexHeight + radius;
            var splashed = false;
            try
            {
                // 1. Anticipation: a deep, wide crouch.
                PresentationCue?.Invoke(SlimePresentationCue.HopPreparing);
                await _clock.Animate(
                    CrouchDuration,
                    t => Pose(start, radius, Mathf.Lerp(CrouchSquashFrom, CrouchSquashTo, GameClock.EaseOut(t)), 0, 0),
                    token);
                splashes?.Burst(start + Vector3.up * TakeoffSplashHeight, TakeoffSplashCount, TakeoffSplashSpeed);

                // 2. Launch: stretched upwards, decelerating towards the apex.
                PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                await _clock.Animate(LaunchDuration, t =>
                {
                    var eased = GameClock.EaseOut(t);
                    Pose(Vector3.Lerp(start, apex, eased), radius, Mathf.Lerp(LaunchStretch, 1f, t), apexHeight * eased, LaunchTilt * t);
                }, token);

                // 3. Hang time: a little float and a jelly wobble in the air.
                await _clock.Animate(HangDuration, t =>
                {
                    var wobble = 1 + Mathf.Sin(t * Mathf.PI * HangWobbleTurns) * HangWobble * (1 - t);
                    var height = apexHeight + Mathf.Sin(t * Mathf.PI) * HangFloat;
                    Pose(apex, radius, wobble, height, Mathf.Lerp(LaunchTilt, DiveTilt, t));
                }, token);

                // 4. Dive: accelerating, stretched like a drop, into the pool.
                var bottom = surface - radius * DiveDepth;
                await _clock.Animate(DiveDuration, t =>
                {
                    var eased = t * t;
                    var height = Mathf.Lerp(apexHeight, bottom, eased);
                    var shrink = radius * Mathf.Lerp(1, DiveShrink, t);
                    Pose(Vector3.Lerp(apex, landing, eased), shrink, Mathf.Lerp(1f, DiveStretch, t), height, DiveTilt);
                    if (splashed || height >= surface)
                        return;

                    splashed = true;
                    PresentationCue?.Invoke(SlimePresentationCue.PoolSplash);
                    var entry = new Vector3(landing.x, surface, landing.z);
                    if (splash != null)
                        splash.Play(entry, _diameter / DefaultDiameter);

                    splashes?.Burst(entry + Vector3.up * EntrySplashHeight, EntrySplashCount, EntrySplashSpeed, null, EntrySplashSize);
                    PoolBounceAsync(pool, token).Forget();
                }, token);

                // 5. Sink and vanish under the surface.
                await _clock.Animate(
                    SinkDuration,
                    t => Pose(landing, radius * (1 - t), SinkSquash, bottom - t * SinkDepth, DiveTilt),
                    token);
            }
            finally
            {
                if (body != null)
                    body.gameObject.SetActive(false);

                if (shadow != null)
                    shadow.gameObject.SetActive(false);

                _popped = true;
            }
        }

        private static void ApplyColors(MeshRenderer renderer, MaterialPropertyBlock properties, SlimeColorPreset preset)
        {
            properties.SetColor(BaseColorId, preset.BaseColor);
            properties.SetColor(TopColorId, preset.TopColor);
            renderer.SetPropertyBlock(properties);
        }

        private async UniTask DefeatHopAsync(Vector3 target, CancellationToken token)
        {
            var flat = target - _anchor;
            flat.y = 0;
            var reach = Mathf.Max(0, flat.magnitude - _diameter * DefeatReachShrink);
            var to = _anchor + (flat.sqrMagnitude > MinDirectionSqr ? flat.normalized * reach : Vector3.zero);
            var from = _anchor;
            try
            {
                await _clock.Animate(
                    _config.hopPrepare,
                    t => _prepare = Mathf.Sin(t * QuarterTurn) * DefeatCrouch,
                    token);
                _prepare = 0;
                PresentationCue?.Invoke(SlimePresentationCue.Takeoff);
                await _clock.Animate(_config.defeatHopDuration, t =>
                {
                    _flightT = t;
                    _height = ParabolaPeak * _config.defeatHopHeight * t * (1 - t);
                    _anchor = Vector3.Lerp(from, to, GameClock.Smooth(t));
                }, token);
            }
            finally
            {
                _flightT = NoFlight;
                _height = 0;
                _prepare = 0;
            }

            _impactVelocity = DefeatLandingImpulse;
            PresentationCue?.Invoke(SlimePresentationCue.Landed);
        }

        private void OnDestroy()
        {
            if (_bridgeMesh != null)
                Destroy(_bridgeMesh);
        }

        private void Pose(Vector3 ground, float radius, float squash, float height, float tilt)
        {
            _anchor = new Vector3(ground.x, 0, ground.z);
            var wide = 1f / Mathf.Sqrt(Mathf.Max(MinSquash, squash));
            body.localScale = new Vector3(wide, squash, wide) * (radius * DiameterPerRadius);
            body.position = new Vector3(ground.x, radius * squash + height + BodyLift, ground.z);
            body.rotation = Quaternion.Euler(tilt, bodyYaw, 0);
            if (shadow == null)
                return;

            shadow.position = new Vector3(ground.x, ShadowHeight, ground.z);
            shadow.localScale = Vector3.one * (radius * ShadowScale / (1 + Mathf.Max(0, height) * ShadowFade));
        }

        private async UniTaskVoid PoolBounceAsync(Transform pool, CancellationToken token)
        {
            var baseScale = pool.localScale;
            try
            {
                await _clock.Animate(PoolBounceDuration, t =>
                {
                    var k = Mathf.Sin(t * Mathf.PI * PoolBounceTurns) * (1 - t);
                    pool.localScale = new Vector3(
                        baseScale.x * (1 + PoolBounceWiden * k),
                        baseScale.y * (1 - PoolBounceFlatten * k),
                        baseScale.z * (1 + PoolBounceWiden * k));
                }, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (pool != null)
                    pool.localScale = baseScale;
            }
        }

        private void UpdateBody(float dt)
        {
            if (_popped || _scripted)
                return;

            _idleTime += dt;
            var radius = _diameter * HalfDiameter;
            var squash = IdleSquash + Mathf.Sin(_idleTime * IdleBreathSpeed) * IdleBreath;
            if (_prepare > 0)
                squash -= _prepare * CrouchDepth;

            if (_flightT >= 0)
                squash = FlightSquash + Mathf.Sin(_flightT * FullTurn) * FlightWobble;

            _impactVelocity += (-SpringStiffness * _impactSpring - SpringDamping * _impactVelocity) * dt;
            _impactSpring += _impactVelocity * dt;
            _impactSpring = Mathf.Clamp(_impactSpring, SpringMin, SpringMax);
            squash = Mathf.Clamp(squash + _impactSpring, SquashMin, SquashMax) * _flatten;
            var targetPull = TargetPull();
            if (dt > 0)
                _pull = Mathf.SmoothDamp(_pull, targetPull, ref _pullVelocity, PullSmoothTime, Mathf.Infinity, dt);

            var wide = 1f / Mathf.Sqrt(Mathf.Max(MinSquash, squash));
            body.localScale = new Vector3(wide, squash, wide) * (radius * DiameterPerRadius);
            body.position = new Vector3(_anchor.x, radius * squash + _height + BodyLift, _anchor.z);
            if (shadow != null)
            {
                shadow.position = new Vector3(_anchor.x, ShadowHeight, _anchor.z);
                shadow.localScale = Vector3.one * (radius * ShadowScale / (1 + _height * ShadowFade));
            }

            var tilt = _flightT >= 0
                ? -Mathf.Sin(_flightT * FullTurn) * FlightTilt
                : _pull * PullTilt;
            body.rotation = Quaternion.Euler(tilt, bodyYaw, 0);
            var forward = Vector3.forward;
            _bodyProperties.SetFloat(WobbleId, SurfaceWobble);
            _bodyProperties.SetFloat(ImpactId, Mathf.Abs(_impactSpring) * ImpactShading);
            _bodyProperties.SetFloat(PullId, _pull);
            _bodyProperties.SetVector(PullDirectionId, body.InverseTransformDirection(forward));
            _bodyRenderer.SetPropertyBlock(_bodyProperties);

            var blinkClock = Mathf.Repeat(_idleTime + BlinkOffset, BlinkPeriod);
            var blink = blinkClock < BlinkDuration
                ? Mathf.Lerp(BlinkClosed, 1, Mathf.Abs(blinkClock - BlinkDuration * HalfDiameter) / (BlinkDuration * HalfDiameter))
                : 1;
            foreach (var eye in eyes)
                eye.localScale = new Vector3(EyeWidth, EyeHeight * blink, EyeDepth);

            if (_charging || _pinching)
                UpdateChargedDrop(radius, forward);
        }

        private float TargetPull()
        {
            if (_charging && !_pinching)
                return _chargeNormalized * ChargePull;

            if (_pinching)
                return Mathf.Lerp(ChargePull, PinchPull, _pinchT);

            return 0;
        }

        private void UpdateChargedDrop(float bodyRadius, Vector3 direction)
        {
            var radius = _dropDiameter * HalfDiameter;
            var center = body.position;
            var distance = bodyRadius * DropDistance + radius + DropChargeReach * _chargeNormalized;
            var position = center + direction * distance;
            if (_pinching)
                position = _pinchStart + direction * GameClock.Smooth(_pinchT) * PinchTravel;

            drop.position = position;
            drop.rotation = Quaternion.identity;
            drop.localScale = Vector3.one * Mathf.Max(MinDropDiameter, _dropDiameter);
            var start = center + direction * bodyRadius * NeckStart;
            var end = position - direction * radius * NeckEnd;
            var pinch = _pinching
                ? _pinchT
                : 0;
            UpdateBridge(start, end, Mathf.Min(radius, bodyRadius) * NeckRadius, pinch);
            _dropProperties.SetFloat(WobbleId, SurfaceWobble * DropWobbleShare);
            _dropProperties.SetFloat(PhaseId, DropPhase);
            _dropRenderer.SetPropertyBlock(_dropProperties);
        }

        private void CreateBridgeMesh()
        {
            _bridgeMesh = new Mesh { name = BridgeMeshName };
            _bridgeMesh.MarkDynamic();
            _bridgeVertices = new Vector3[BridgeRings * BridgeSides];
            _bridgeNormals = new Vector3[_bridgeVertices.Length];
            var triangles = new int[(BridgeRings - 1) * BridgeSides * IndicesPerQuad];
            var index = 0;
            for (var ring = 0; ring < BridgeRings - 1; ring++)
            {
                for (var side = 0; side < BridgeSides; side++)
                {
                    var a = ring * BridgeSides + side;
                    var b = ring * BridgeSides + (side + 1) % BridgeSides;
                    triangles[index++] = a;
                    triangles[index++] = b;
                    triangles[index++] = a + BridgeSides;
                    triangles[index++] = b;
                    triangles[index++] = b + BridgeSides;
                    triangles[index++] = a + BridgeSides;
                }
            }

            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.triangles = triangles;
            bridge.sharedMesh = _bridgeMesh;
        }

        private void UpdateBridge(Vector3 start, Vector3 end, float radius, float pinch)
        {
            var axis = (end - start).normalized;
            if (axis.sqrMagnitude < MinDirectionSqr)
                axis = Vector3.forward;

            var tangent = Vector3.Cross(axis, Vector3.up).normalized;
            var bitangent = Vector3.Cross(axis, tangent);
            var space = bridge.transform;
            for (var ring = 0; ring < BridgeRings; ring++)
            {
                var t = ring / (float)(BridgeRings - 1);
                var middle = Mathf.Sin(t * Mathf.PI);
                var width = radius * Mathf.Lerp(1, Mathf.Lerp(NeckWaist, PinchedWaist, pinch), middle);
                var center = Vector3.Lerp(start, end, t);
                for (var side = 0; side < BridgeSides; side++)
                {
                    var angle = side * FullTurn / BridgeSides;
                    var normal = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    var i = ring * BridgeSides + side;
                    _bridgeVertices[i] = space.InverseTransformPoint(center + normal * width);
                    _bridgeNormals[i] = space.InverseTransformDirection(normal);
                }
            }

            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.normals = _bridgeNormals;
            _bridgeMesh.RecalculateBounds();
        }
    }
}
