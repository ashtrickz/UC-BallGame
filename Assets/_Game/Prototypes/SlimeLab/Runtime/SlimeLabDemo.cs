using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Prototypes.SlimeLab
{
    [DisallowMultipleComponent]
    public sealed class SlimeLabDemo : MonoBehaviour
    {
        public enum MotionState { Idle, Charging, Pinching, Flying }

        [Header("Scene references")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform drop;
        [SerializeField] private MeshFilter bridge;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Transform[] crystals;
        [SerializeField] private Transform[] eyes;
        [SerializeField] private SlimeLabHud hud;
        [SerializeField] private Material jellyMaterial;
        [SerializeField] private Camera viewCamera;

        [Header("Motion - tune during Play Mode")]
        [Min(0.1f)] public float initialRadius = 0.95f;
        [Min(0.1f)] public float jumpHeight = 1.15f;
        [Min(0.3f)] public float jumpDuration = 0.78f;
        [Range(0, 0.08f)] public float surfaceWobble = 0.017f;
        [Range(0.1f, 0.6f)] public float massPerSecond = 0.19f;
        [Range(0.1f, 0.5f)] public float minimumMass = 0.25f;
        [Range(0.1f, 0.5f)] public float maximumShotMass = 0.34f;
        [Min(0.1f)] public float flightDuration = 0.65f;
        public bool autoDemo = true;
        [Min(2)] public float viewHalfWidth = 6.5f;
        public Vector3 startPosition = new Vector3(-2.45f, 0, 0);

        public MotionState State { get; private set; }
        public float RemainingMass { get; private set; } = 1;
        public float ChargedMass { get; private set; }
        public int ShotsFired { get; private set; }
        public int Impacts { get; private set; }
        public int Landings { get; private set; }
        public float BodyRadius => initialRadius * Mathf.Pow(RemainingMass, 1f / 3f);
        public float DropRadius => initialRadius * Mathf.Pow(ChargedMass, 1f / 3f);
        public bool IsJumping => _jumpClock >= 0;
        public Transform Body => body;
        public Transform Drop => drop;
        public string Readout => RemainingMass <= minimumMass + 0.01f && State == MotionState.Idle
            ? "LOW MASS / RESET TO REFILL"
            : State == MotionState.Idle ? IsJumping ? "HOP / SQUASH + STRETCH" : "IDLE / SOFT JELLY"
            : State == MotionState.Charging ? "HOLD / TRANSFER MASS"
            : State == MotionState.Pinching ? "SPLIT / BREAK THE NECK" : "RELEASE / FLYING DROP";

        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int ImpactId = Shader.PropertyToID("_Impact");
        private static readonly int PullId = Shader.PropertyToID("_Pull");
        private static readonly int PullDirectionId = Shader.PropertyToID("_PullDirection");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");
        private MaterialPropertyBlock _bodyProperties, _dropProperties, _bridgeProperties;
        private MeshRenderer _bodyRenderer, _dropRenderer;
        private Mesh _bridgeMesh;
        private Vector3[] _bridgeVertices, _bridgeNormals;
        private const int Rings = 9, Sides = 16;
        private Vector3[] _crystalScales;
        private float[] _crystalHitTimes;
        private MeshRenderer[] _crystalRenderers;
        private Color[] _crystalBaseColors;
        private MaterialPropertyBlock _crystalProperties;
        private readonly Speck[] _specks = new Speck[28];
        private float _jumpClock = -1, _groundX, _jumpFromX, _jumpToX;
        private float _impactSpring, _impactVelocity, _pull, _pullVelocity;
        private float _massAtChargeStart, _chargeClock, _stateClock, _demoClock;
        private int _demoStep, _speckCursor;
        private bool _wasHeld, _inputCharging;
        private Vector3 _flightStart, _flightEnd, _pinchStart;
        private Vector3 _bodyHome;

        private sealed class Speck
        {
            public Transform Transform;
            public Vector3 Origin, Velocity;
            public float Started, Duration, Size;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _bodyRenderer = body.GetComponent<MeshRenderer>();
            _dropRenderer = drop.GetComponent<MeshRenderer>();
            _bodyProperties = new MaterialPropertyBlock();
            _dropProperties = new MaterialPropertyBlock();
            _bridgeProperties = new MaterialPropertyBlock();
            _bridgeProperties.SetFloat(WobbleId, 0);
            bridge.GetComponent<MeshRenderer>().SetPropertyBlock(_bridgeProperties);
            _bodyHome = startPosition;
            _groundX = _bodyHome.x;
            _crystalScales = new Vector3[crystals.Length];
            _crystalHitTimes = new float[crystals.Length];
            _crystalRenderers = new MeshRenderer[crystals.Length];
            _crystalBaseColors = new Color[crystals.Length];
            _crystalProperties = new MaterialPropertyBlock();
            for (var i = 0; i < crystals.Length; i++)
            {
                _crystalScales[i] = Vector3.one;
                _crystalHitTimes[i] = -100;
                _crystalRenderers[i] = crystals[i].GetComponentInChildren<MeshRenderer>();
                _crystalBaseColors[i] = _crystalRenderers[i].sharedMaterial.GetColor("_BaseColor");
            }
            CreateBridgeMesh();
            CreateSpecks();
            ResetDemo();
        }

        private void Update()
        {
            if (viewCamera != null)
                viewCamera.orthographicSize = Mathf.Max(3.9f, viewHalfWidth / Mathf.Max(0.3f, viewCamera.aspect));
            ReadInput();
            if (autoDemo) UpdateAutoDemo();
            var dt = Time.deltaTime;
            _stateClock += dt;
            if (State == MotionState.Charging)
            {
                _chargeClock += dt;
                ChargedMass = Mathf.Min(0.008f + _chargeClock * massPerSecond,
                    Mathf.Min(maximumShotMass, _massAtChargeStart - minimumMass));
                RemainingMass = _massAtChargeStart - ChargedMass;
            }
            else if (State == MotionState.Pinching && _stateClock >= 0.16f)
            {
                State = MotionState.Flying;
                _stateClock = 0;
                _flightStart = drop.position;
                _flightEnd = aimPoint.position;
                bridge.gameObject.SetActive(false);
                ShotsFired++;
                _impactVelocity -= 3.4f;
                Burst(_flightStart, 7, 0.55f);
            }
            else if (State == MotionState.Flying && _stateClock >= flightDuration)
            {
                Impact();
            }
            UpdateBody(dt);
            UpdateDrop();
            UpdateCrystals();
            UpdateSpecks();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ResetDemo();
                if (keyboard.aKey.wasPressedThisFrame) ToggleAutoDemo();
                if (keyboard.jKey.wasPressedThisFrame) ManualJump();
            }
            var held = keyboard != null && keyboard.spaceKey.isPressed;
            var pointerHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            var point = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                pointerHeld = true;
                point = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            if (pointerHeld && (hud == null || !hud.IsOverControls(point))) held = true;
            if (held && !_wasHeld)
            {
                autoDemo = false;
                // Let an existing hop settle; the held input starts charging on landing.
                _inputCharging = true;
            }
            if (held && _inputCharging && !IsJumping && State == MotionState.Idle) BeginCharge();
            if (!held && _wasHeld && _inputCharging)
            {
                if (State == MotionState.Charging) ReleaseCharge();
                _inputCharging = false;
            }
            _wasHeld = held;
        }

        private void UpdateAutoDemo()
        {
            _demoClock += Time.deltaTime;
            switch (_demoStep)
            {
                case 0 when _demoClock > 0.5f: Jump(); _demoStep++; break;
                case 1 when _demoClock > 1.8f: BeginCharge(); _demoStep++; break;
                case 2 when _demoClock > 3.25f: ReleaseCharge(); _demoStep++; break;
                case 3 when _demoClock > 4.6f: Jump(); _demoStep++; break;
                case 4 when _demoClock > 5.9f: BeginCharge(); _demoStep++; break;
                case 5 when _demoClock > 6.55f: ReleaseCharge(); _demoStep++; break;
                case 6 when _demoClock > 8.8f: ResetDemo(); break;
            }
        }

        public void ToggleAutoDemo()
        {
            autoDemo = !autoDemo;
            if (autoDemo) ResetDemo();
            else if (State == MotionState.Charging) ReleaseCharge();
        }

        public void ManualJump()
        {
            autoDemo = false;
            Jump();
        }

        public void Jump()
        {
            if (IsJumping || State == MotionState.Charging || State == MotionState.Pinching) return;
            _jumpClock = 0;
            _jumpFromX = _groundX;
            _jumpToX = Mathf.Min(_groundX + 0.6f, 0.1f);
        }

        public void BeginCharge()
        {
            if (State != MotionState.Idle || IsJumping || RemainingMass <= minimumMass + 0.009f) return;
            State = MotionState.Charging;
            _massAtChargeStart = RemainingMass;
            _chargeClock = 0;
            _stateClock = 0;
            ChargedMass = Mathf.Min(0.008f, RemainingMass - minimumMass);
            RemainingMass = _massAtChargeStart - ChargedMass;
            drop.gameObject.SetActive(true);
            bridge.gameObject.SetActive(true);
        }

        public void ReleaseCharge()
        {
            if (State != MotionState.Charging) return;
            State = MotionState.Pinching;
            _stateClock = 0;
            _pinchStart = drop.position;
        }

        public void ResetDemo()
        {
            State = MotionState.Idle;
            RemainingMass = 1;
            ChargedMass = 0;
            _demoClock = 0;
            _demoStep = 0;
            _stateClock = 0;
            _jumpClock = -1;
            _groundX = _bodyHome.x;
            _impactSpring = 0;
            _impactVelocity = 0;
            _pull = 0;
            _pullVelocity = 0;
            _inputCharging = false;
            drop.gameObject.SetActive(false);
            bridge.gameObject.SetActive(false);
            if (_crystalHitTimes != null)
                for (var i = 0; i < _crystalHitTimes.Length; i++) _crystalHitTimes[i] = -100;
            foreach (var speck in _specks)
                if (speck != null) speck.Transform.gameObject.SetActive(false);
            UpdateBody(0);
        }

        private void UpdateBody(float dt)
        {
            var radius = BodyRadius;
            var height = 0f;
            var squash = 0.91f + Mathf.Sin(Time.time * 2.6f) * 0.015f;
            if (IsJumping)
            {
                _jumpClock += dt;
                var prep = 0.14f;
                if (_jumpClock < prep)
                    squash -= Mathf.Sin(_jumpClock / prep * Mathf.PI * 0.5f) * 0.18f;
                else
                {
                    var t = Mathf.Clamp01((_jumpClock - prep) / jumpDuration);
                    height = 4 * jumpHeight * t * (1 - t);
                    _groundX = Mathf.Lerp(_jumpFromX, _jumpToX, Smooth(t));
                    squash = 1.08f + Mathf.Sin(t * Mathf.PI * 2) * 0.14f;
                    if (t >= 1)
                    {
                        _jumpClock = -1;
                        _impactVelocity = -6.3f;
                        Landings++;
                        Burst(new Vector3(_groundX, 0.1f, _bodyHome.z), 6, 0.4f);
                    }
                }
            }
            // A damped spring makes both landing and recoil settle into the idle wobble.
            _impactVelocity += (-95 * _impactSpring - 10.5f * _impactVelocity) * dt;
            _impactSpring += _impactVelocity * dt;
            _impactSpring = Mathf.Clamp(_impactSpring, -0.28f, 0.25f);
            squash = Mathf.Clamp(squash + _impactSpring, 0.65f, 1.35f);
            var targetPull = State == MotionState.Charging ? Mathf.Clamp01(ChargedMass / maximumShotMass) * 0.22f
                : State == MotionState.Pinching ? Mathf.Lerp(0.22f, 0.03f, _stateClock / 0.16f) : 0;
            _pull = dt > 0 ? Mathf.SmoothDamp(_pull, targetPull, ref _pullVelocity, 0.09f, Mathf.Infinity, dt) : targetPull;
            var wide = 1f / Mathf.Sqrt(squash);
            body.localScale = new Vector3(wide, squash, wide) * (radius * 2);
            body.position = new Vector3(_groundX, radius * squash + height + 0.055f, _bodyHome.z);
            body.rotation = Quaternion.Euler(0, 0, IsJumping ? -Mathf.Sin(_jumpClock * 5) * 5 : -_pull * 12);
            var direction = (aimPoint.position - body.position).normalized;
            _bodyProperties.SetFloat(WobbleId, surfaceWobble);
            _bodyProperties.SetFloat(ImpactId, Mathf.Abs(_impactSpring) * 3);
            _bodyProperties.SetFloat(PullId, _pull);
            _bodyProperties.SetVector(PullDirectionId, body.InverseTransformDirection(direction));
            _bodyRenderer.SetPropertyBlock(_bodyProperties);

            var blinkClock = Mathf.Repeat(Time.time + 1.4f, 4.3f);
            var blink = blinkClock < 0.16f ? Mathf.Lerp(0.12f, 1, Mathf.Abs(blinkClock - 0.08f) / 0.08f) : 1;
            foreach (var eye in eyes)
                eye.localScale = new Vector3(0.105f, 0.16f * blink, 0.065f);
        }

        private void UpdateDrop()
        {
            if (State == MotionState.Idle) return;
            var radius = DropRadius;
            var direction = (aimPoint.position - body.position).normalized;
            if (State == MotionState.Charging || State == MotionState.Pinching)
            {
                var distance = BodyRadius * 1.32f + radius + 0.18f * Mathf.Clamp01(ChargedMass / maximumShotMass);
                var position = body.position + direction * distance;
                if (State == MotionState.Pinching)
                    position = _pinchStart + direction * Smooth(_stateClock / 0.16f) * 0.5f;
                drop.position = position;
                drop.rotation = Quaternion.identity;
                drop.localScale = Vector3.one * (radius * 2);
                var start = body.position + direction * BodyRadius * 0.72f;
                var end = position - direction * radius * 0.52f;
                var pinch = State == MotionState.Pinching ? Mathf.Clamp01(_stateClock / 0.16f) : 0;
                UpdateBridge(start, end, Mathf.Min(radius, BodyRadius) * 0.7f, pinch);
            }
            else
            {
                var t = Mathf.Clamp01(_stateClock / flightDuration);
                drop.position = Vector3.Lerp(_flightStart, _flightEnd, t) + Vector3.up * (0.6f * 4 * t * (1 - t));
                var velocity = (_flightEnd - _flightStart) + Vector3.up * (2.4f * (1 - 2 * t));
                drop.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
                drop.localScale = new Vector3(0.88f, 1.29f, 0.88f) * (radius * 2);
            }
            _dropProperties.SetFloat(WobbleId, surfaceWobble * 0.65f);
            _dropProperties.SetFloat(PhaseId, 2.7f);
            _dropRenderer.SetPropertyBlock(_dropProperties);
        }

        private void Impact()
        {
            var radius = DropRadius;
            Burst(aimPoint.position, 16, 1.1f + radius);
            for (var i = 0; i < crystals.Length; i++)
            {
                var delta = crystals[i].position - aimPoint.position;
                delta.y = 0;
                if (delta.magnitude < 0.6f + radius * 2.2f) _crystalHitTimes[i] = Time.time;
            }
            Impacts++;
            State = MotionState.Idle;
            ChargedMass = 0;
            drop.gameObject.SetActive(false);
        }

        private void UpdateCrystals()
        {
            for (var i = 0; i < crystals.Length; i++)
            {
                var t = Time.time - _crystalHitTimes[i];
                var size = t < 0.22f ? 1 - Smooth(t / 0.22f) : t < 2.6f ? 0 : Smooth((t - 2.6f) / 0.5f);
                crystals[i].localScale = _crystalScales[i] * size;
                _crystalProperties.SetColor("_BaseColor", t < 2.6f
                    ? Color.Lerp(_crystalBaseColors[i], SlimeLabPalette.JellyLight, Mathf.Clamp01(t / 0.07f))
                    : _crystalBaseColors[i]);
                _crystalRenderers[i].SetPropertyBlock(_crystalProperties);
            }
        }

        private void CreateBridgeMesh()
        {
            if (bridge.sharedMesh != null && bridge.sharedMesh.name == "Slime neck (runtime)")
                Destroy(bridge.sharedMesh);
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
                    triangles[index++] = a;
                    triangles[index++] = b;
                    triangles[index++] = a + Sides;
                    triangles[index++] = b;
                    triangles[index++] = b + Sides;
                    triangles[index++] = a + Sides;
                }
            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.triangles = triangles;
            bridge.sharedMesh = _bridgeMesh;
        }

        private void UpdateBridge(Vector3 start, Vector3 end, float radius, float pinch)
        {
            var axis = (end - start).normalized;
            var tangent = Vector3.Cross(axis, Vector3.up).normalized;
            var bitangent = Vector3.Cross(axis, tangent);
            for (var ring = 0; ring < Rings; ring++)
            {
                var t = ring / (float)(Rings - 1);
                var middle = Mathf.Sin(t * Mathf.PI);
                var width = radius * Mathf.Lerp(1, Mathf.Lerp(0.7f, 0.015f, pinch), middle);
                var center = Vector3.Lerp(start, end, t);
                for (var side = 0; side < Sides; side++)
                {
                    var angle = side * Mathf.PI * 2 / Sides;
                    var n = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
                    var i = ring * Sides + side;
                    _bridgeVertices[i] = bridge.transform.InverseTransformPoint(center + n * width);
                    _bridgeNormals[i] = bridge.transform.InverseTransformDirection(n);
                }
            }
            _bridgeMesh.vertices = _bridgeVertices;
            _bridgeMesh.normals = _bridgeNormals;
            _bridgeMesh.RecalculateBounds();
        }

        private void CreateSpecks()
        {
            var previous = transform.Find("Pooled jelly splashes");
            if (previous != null) Destroy(previous.gameObject);
            var root = new GameObject("Pooled jelly splashes").transform;
            root.SetParent(transform, false);
            for (var i = 0; i < _specks.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Splash " + i;
                go.transform.SetParent(root, false);
                var collider = go.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = jellyMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var properties = new MaterialPropertyBlock();
                properties.SetFloat(WobbleId, 0);
                renderer.SetPropertyBlock(properties);
                go.SetActive(false);
                _specks[i] = new Speck { Transform = go.transform };
            }
        }

        private void Burst(Vector3 origin, int count, float strength)
        {
            for (var i = 0; i < count; i++)
            {
                var speck = _specks[_speckCursor++ % _specks.Length];
                var angle = i * 2.399963f + _speckCursor * 0.6f;
                speck.Origin = origin;
                speck.Velocity = new Vector3(Mathf.Cos(angle), 1.4f + i % 3 * 0.3f, Mathf.Sin(angle)) * strength;
                speck.Started = Time.time;
                speck.Duration = 0.42f + i % 4 * 0.075f;
                speck.Size = 0.055f + i % 3 * 0.025f;
                speck.Transform.gameObject.SetActive(true);
            }
        }

        private void UpdateSpecks()
        {
            foreach (var speck in _specks)
            {
                if (!speck.Transform.gameObject.activeSelf) continue;
                var age = Time.time - speck.Started;
                if (age > speck.Duration) { speck.Transform.gameObject.SetActive(false); continue; }
                speck.Transform.position = speck.Origin + speck.Velocity * age + Vector3.down * (4.5f * age * age);
                speck.Transform.localScale = Vector3.one * (speck.Size * (1 - age / speck.Duration));
            }
        }

        private void OnDestroy()
        {
            if (_bridgeMesh != null) Destroy(_bridgeMesh);
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
    }
}
