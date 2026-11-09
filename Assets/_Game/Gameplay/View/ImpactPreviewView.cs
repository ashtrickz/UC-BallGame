using Game.Foundation;
using UnityEngine.Rendering;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>Stays active; toggles its children. Ring on the floor showing the predicted blast circle (centre = contact point, radius = b × S).</summary>
    public sealed class ImpactPreviewView : MonoBehaviour, IFrameTickable
    {
        private const string BaseColorProperty = "_BaseColor";
        private const string RingMeshName = "Blast ring (runtime)";
        private const string WaveMeshName = "Impact wave (runtime)";
        private const string WaveObjectName = "Impact wave ";

        private const int Segments = 72;
        private const int VerticesPerSegment = 2;
        private const int IndicesPerSegment = 6;
        private const int WaveCount = 2;
        private const float FullTurn = Mathf.PI * 2f;
        private const float DefaultThickness = 0.13f;
        private const float WaveThickness = 0.055f;
        private const float MinImpactRadius = 0.02f;
        private const float NotPlaying = -1f;

        private const float RingHeight = 0.03f;
        private const float MarkerHeight = 0.035f;
        private const float WaveHeight = 0.045f;
        private const float WaveHeightStep = 0.005f;
        private const float WaveFade = 0.14f;
        private const float WaveDelay = 0.065f;
        private const float SecondWaveStrength = 0.5f;
        private const float WaveStartScale = 0.08f;
        private const float WaveAlphaMin = 0.5f;
        private const float WaveAlphaMax = 0.85f;
        private const float SecondWaveAlpha = 0.55f;

        private static readonly int BaseColorId = Shader.PropertyToID(BaseColorProperty);

        [SerializeField] private MeshFilter ring;
        [SerializeField] private MeshRenderer ringRenderer;
        [SerializeField] private Transform marker;
        [SerializeField, Range(0.01f, 0.5f)] private float thickness = DefaultThickness;

        private readonly Transform[] _waves = new Transform[WaveCount];
        private readonly MeshRenderer[] _waveRenderers = new MeshRenderer[WaveCount];

        private Mesh _mesh;
        private Mesh _impactMesh;
        private MaterialPropertyBlock _properties;
        private SlimeViewConfig _config;
        private float _impactAge = NotPlaying;
        private float _impactRadius;
        private float _impactDuration;
        private float _impactStrength;
        private Color _impactColor;

        public bool IsImpactPlaying => _impactAge >= 0;

        private void Awake()
        {
            _mesh = BuildRing(thickness, RingMeshName);
            ring.sharedMesh = _mesh;
            _impactMesh = BuildRing(WaveThickness, WaveMeshName);
            _properties = new MaterialPropertyBlock();
            for (var i = 0; i < _waves.Length; i++)
            {
                var wave = new GameObject(WaveObjectName + i, typeof(MeshFilter), typeof(MeshRenderer));
                wave.transform.SetParent(transform, false);
                wave.GetComponent<MeshFilter>().sharedMesh = _impactMesh;
                var renderer = wave.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ringRenderer.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _waves[i] = wave.transform;
                _waveRenderers[i] = renderer;
            }

            ResetEffects();
        }

        public void Bind(SlimeViewConfig config)
        {
            _config = config;
        }

        public void PlayImpact(Vector3 center, float radius, Color color)
        {
            _impactAge = 0;
            _impactRadius = Mathf.Max(MinImpactRadius, radius);
            _impactDuration = _config.ImpactWaveDuration(radius);
            _impactStrength = _config.ImpactStrength(radius);
            _impactColor = color;
            for (var i = 0; i < _waves.Length; i++)
                _waves[i].position = new Vector3(center.x, WaveHeight + i * WaveHeightStep, center.z);

            UpdateImpact();
        }

        public void Tick(float deltaTime)
        {
            if (_impactAge < 0)
                return;

            _impactAge += deltaTime;
            UpdateImpact();
        }

        public void ResetEffects()
        {
            _impactAge = NotPlaying;
            Hide();
            foreach (var wave in _waves)
            {
                if (wave != null)
                    wave.gameObject.SetActive(false);
            }
        }

        public void Show(Vector3 center, float radius, Color color)
        {
            ring.gameObject.SetActive(true);
            if (marker != null)
                marker.gameObject.SetActive(true);

            ring.transform.position = new Vector3(center.x, RingHeight, center.z);
            ring.transform.localScale = new Vector3(radius, 1, radius);
            if (marker != null)
                marker.position = new Vector3(center.x, MarkerHeight, center.z);

            _properties.SetColor(BaseColorId, color);
            ringRenderer.SetPropertyBlock(_properties);
        }

        public void Hide()
        {
            ring.gameObject.SetActive(false);
            if (marker != null)
                marker.gameObject.SetActive(false);
        }

        private static Mesh BuildRing(float width, string name)
        {
            var mesh = new Mesh { name = name };
            var vertices = new Vector3[Segments * VerticesPerSegment];
            var triangles = new int[Segments * IndicesPerSegment];
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * FullTurn / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var inner = i * VerticesPerSegment;
                var next = (i + 1) % Segments * VerticesPerSegment;
                vertices[inner] = direction * (1 - width);
                vertices[inner + 1] = direction;
                var t = i * IndicesPerSegment;
                triangles[t++] = inner;
                triangles[t++] = next + 1;
                triangles[t++] = inner + 1;
                triangles[t++] = inner;
                triangles[t++] = next;
                triangles[t] = next + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);

            if (_impactMesh != null)
                Destroy(_impactMesh);
        }

        private void UpdateImpact()
        {
            if (_impactAge > _impactDuration + WaveFade + WaveDelay)
            {
                _impactAge = NotPlaying;
                foreach (var wave in _waves)
                    wave.gameObject.SetActive(false);

                return;
            }

            for (var i = 0; i < _waves.Length; i++)
            {
                var age = _impactAge - i * WaveDelay;
                var strongEnough = i == 0 || _impactStrength >= SecondWaveStrength;
                var active = age >= 0 && age < _impactDuration + WaveFade && strongEnough;
                _waves[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                var radius = _impactRadius * Mathf.Lerp(WaveStartScale, 1, Mathf.Clamp01(age / _impactDuration));
                _waves[i].localScale = new Vector3(radius, 1, radius);
                var color = _impactColor;
                var waveAlpha = i == 0
                    ? 1
                    : SecondWaveAlpha;
                color.a *= Mathf.Lerp(WaveAlphaMin, WaveAlphaMax, _impactStrength)
                    * waveAlpha
                    * (1 - Mathf.Clamp01((age - _impactDuration) / WaveFade));
                _properties.Clear();
                _properties.SetColor(BaseColorId, color);
                _waveRenderers[i].SetPropertyBlock(_properties);
            }
        }
    }
}
