using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>Stays active; toggles its children. Ring on the floor showing the predicted blast circle (centre = contact point, radius = b × S).</summary>
    public sealed class ImpactPreviewView : MonoBehaviour
    {
        [SerializeField] private MeshFilter ring;
        [SerializeField] private MeshRenderer ringRenderer;
        [SerializeField] private Transform marker;
        [SerializeField, Range(0.01f, 0.5f)] private float thickness = 0.13f;
        private const int Segments = 72;
        private Mesh _mesh;
        private Mesh _impactMesh;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _properties;
        private readonly Transform[] _waves = new Transform[2];
        private readonly MeshRenderer[] _waveRenderers = new MeshRenderer[2];
        private GameClock _clock = new GameClock();
        private SlimeViewConfig _config;
        private float _impactAge = -1, _impactRadius, _impactDuration, _impactStrength;
        private Color _impactColor;

        public bool IsImpactPlaying => _impactAge >= 0;

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            _clock = clock;
            _config = config;
        }

        private void Awake()
        {
            _mesh = BuildRing(thickness, "Blast ring (runtime)");
            ring.sharedMesh = _mesh;
            _impactMesh = BuildRing(0.055f, "Impact wave (runtime)");
            _properties = new MaterialPropertyBlock();
            for (var i = 0; i < _waves.Length; i++)
            {
                var go = new GameObject("Impact wave " + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = _impactMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ringRenderer.sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _waves[i] = go.transform;
                _waveRenderers[i] = renderer;
            }
            ResetEffects();
        }

        private static Mesh BuildRing(float width, string name)
        {
            var mesh = new Mesh { name = name };
            var vertices = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            for (var i = 0; i < Segments; i++)
            {
                var a = i * Mathf.PI * 2 / Segments;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = dir * (1 - width);
                vertices[i * 2 + 1] = dir;
                var n = (i + 1) % Segments;
                var t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = n * 2 + 1; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = n * 2; triangles[t + 5] = n * 2 + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_impactMesh != null) Destroy(_impactMesh);
        }

        /// <summary>Two pooled ground rings; neither exceeds the real blast radius or blocks the action chain.</summary>
        public void PlayImpact(Vector3 center, float radius, Color color)
        {
            _impactAge = 0;
            _impactRadius = Mathf.Max(0.02f, radius);
            _impactDuration = _config.ImpactWaveDuration(radius);
            _impactStrength = _config.ImpactStrength(radius);
            _impactColor = color;
            for (var i = 0; i < _waves.Length; i++)
                _waves[i].position = new Vector3(center.x, 0.045f + i * 0.005f, center.z);
            UpdateImpact();
        }

        private void Update()
        {
            if (_impactAge < 0) return;
            _impactAge += _clock.Delta;
            UpdateImpact();
        }

        private void UpdateImpact()
        {
            const float fade = 0.14f;
            if (_impactAge > _impactDuration + fade + 0.065f)
            {
                _impactAge = -1;
                foreach (var wave in _waves) wave.gameObject.SetActive(false);
                return;
            }
            for (var i = 0; i < _waves.Length; i++)
            {
                var age = _impactAge - i * 0.065f;
                var active = age >= 0 && age < _impactDuration + fade && (i == 0 || _impactStrength >= 0.5f);
                _waves[i].gameObject.SetActive(active);
                if (!active) continue;
                var radius = _impactRadius * Mathf.Lerp(0.08f, 1, Mathf.Clamp01(age / _impactDuration));
                _waves[i].localScale = new Vector3(radius, 1, radius);
                var color = _impactColor;
                color.a *= Mathf.Lerp(0.5f, 0.85f, _impactStrength) * (i == 0 ? 1 : 0.55f)
                    * (1 - Mathf.Clamp01((age - _impactDuration) / fade));
                _properties.Clear();
                _properties.SetColor(BaseColorId, color);
                _waveRenderers[i].SetPropertyBlock(_properties);
            }
        }

        /// <summary>Restart cancels the old attempt's secondary effects immediately.</summary>
        public void ResetEffects()
        {
            _impactAge = -1;
            Hide();
            foreach (var wave in _waves) if (wave != null) wave.gameObject.SetActive(false);
        }

        public void Show(Vector3 center, float radius, Color color)
        {
            ring.gameObject.SetActive(true);
            if (marker != null) marker.gameObject.SetActive(true);
            ring.transform.position = new Vector3(center.x, 0.03f, center.z);
            ring.transform.localScale = new Vector3(radius, 1, radius);
            if (marker != null) marker.position = new Vector3(center.x, 0.035f, center.z);
            _properties.SetColor(BaseColorId, color);
            ringRenderer.SetPropertyBlock(_properties);
        }

        public void Hide()
        {
            ring.gameObject.SetActive(false);
            if (marker != null) marker.gameObject.SetActive(false);
        }
    }
}
