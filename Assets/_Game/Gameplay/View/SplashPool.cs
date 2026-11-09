using UnityEngine;
using UnityEngine.Rendering;
using Game.Gameplay.Config;

namespace Game.Gameplay.View
{
    /// <summary>Pooled jelly and crystal specks (secondary effect: never awaited, never gameplay).</summary>
    public sealed class SplashPool : MonoBehaviour
    {
        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Mesh speckMesh;
        [SerializeField, Min(4)] private int capacity = 64;

        private sealed class Speck
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Origin, Velocity;
            public float Age, Duration, Size;
        }

        private static readonly int WobbleId = Shader.PropertyToID("_Wobble");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TopColorId = Shader.PropertyToID("_TopColor");
        private MaterialPropertyBlock _properties;
        private SlimeColorPreset? _colors;
        private Speck[] _specks;
        private int _cursor;
        private GameClock _clock;
        // Visual variation only: its own stream, never the generator's.
        private System.Random _random = new System.Random(17);

        public void Bind(GameClock clock) => _clock = clock;
        public void SetColors(SlimeColorPreset preset) => _colors = preset;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _specks = new Speck[capacity];
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(WobbleId, 0);
            var sphere = speckMesh;
            for (var i = 0; i < capacity; i++)
            {
                var go = new GameObject("Speck " + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = sphere;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = defaultMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.SetPropertyBlock(properties);
                go.SetActive(false);
                _specks[i] = new Speck { Transform = go.transform, Renderer = renderer };
            }
        }

        public void Burst(Vector3 origin, int count, float strength, Material material = null, float size = 1f)
        {
            if (_specks == null) return;
            for (var i = 0; i < count; i++)
            {
                var speck = _specks[_cursor++ % _specks.Length];
                var angle = i * 2.399963f + (float)_random.NextDouble() * 0.8f;
                speck.Origin = origin;
                speck.Velocity = new Vector3(Mathf.Cos(angle), 1.4f + i % 3 * 0.3f, Mathf.Sin(angle)) * strength;
                speck.Age = 0;
                speck.Duration = 0.42f + i % 4 * 0.075f;
                speck.Size = (0.055f + i % 3 * 0.025f) * size;
                speck.Renderer.sharedMaterial = material != null ? material : defaultMaterial;
                _properties.Clear();
                _properties.SetFloat(WobbleId, 0);
                if (material == null && _colors.HasValue)
                {
                    _properties.SetColor(BaseColorId, _colors.Value.BaseColor);
                    _properties.SetColor(TopColorId, _colors.Value.TopColor);
                }
                speck.Renderer.SetPropertyBlock(_properties);
                speck.Transform.position = origin;
                speck.Transform.localScale = Vector3.one * speck.Size;
                speck.Transform.gameObject.SetActive(true);
            }
        }

        public void Clear()
        {
            if (_specks == null) return;
            foreach (var speck in _specks) speck.Transform.gameObject.SetActive(false);
        }

        private void Update()
        {
            var dt = _clock?.Delta ?? Time.deltaTime;
            foreach (var speck in _specks)
            {
                if (!speck.Transform.gameObject.activeSelf) continue;
                speck.Age += dt;
                if (speck.Age > speck.Duration) { speck.Transform.gameObject.SetActive(false); continue; }
                var age = speck.Age;
                speck.Transform.position = speck.Origin + speck.Velocity * age + Vector3.down * (4.5f * age * age);
                speck.Transform.localScale = Vector3.one * (speck.Size * (1 - age / speck.Duration));
            }
        }
    }
}
