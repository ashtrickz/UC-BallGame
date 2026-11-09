using Game.Foundation;
using Game.Gameplay.Config;
using UnityEngine.Rendering;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>Pooled jelly and crystal specks (secondary effect: never awaited, never gameplay).</summary>
    public sealed class SplashPool : MonoBehaviour, IFrameTickable
    {
        private const string WobbleProperty = "_Wobble";
        private const string BaseColorProperty = "_BaseColor";
        private const string TopColorProperty = "_TopColor";
        private const string SpeckName = "Speck ";
        private const int DefaultCapacity = 64;
        private const int RandomSeed = 17;
        private const float GoldenAngle = 2.399963f;
        private const float AngleJitter = 0.8f;
        private const float Lift = 1.4f;
        private const int LiftVariants = 3;
        private const float LiftStep = 0.3f;
        private const float Duration = 0.42f;
        private const int DurationVariants = 4;
        private const float DurationStep = 0.075f;
        private const float SpeckSize = 0.055f;
        private const int SizeVariants = 3;
        private const float SizeStep = 0.025f;
        private const float HalfGravity = 4.5f;

        private static readonly int WobbleId = Shader.PropertyToID(WobbleProperty);
        private static readonly int BaseColorId = Shader.PropertyToID(BaseColorProperty);
        private static readonly int TopColorId = Shader.PropertyToID(TopColorProperty);

        [SerializeField] private Material defaultMaterial;
        [SerializeField] private Mesh speckMesh;
        [SerializeField, Min(4)] private int capacity = DefaultCapacity;

        // Visual variation only: its own stream, never the generator's.
        private readonly System.Random _random = new System.Random(RandomSeed);

        private MaterialPropertyBlock _properties;
        private SlimeColorPreset? _colors;
        private Speck[] _specks;
        private int _cursor;

        private sealed class Speck
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Origin;
            public Vector3 Velocity;
            public float Age;
            public float Duration;
            public float Size;
        }

        public void SetColors(SlimeColorPreset preset)
        {
            _colors = preset;
        }

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
            _specks = new Speck[capacity];
            var properties = new MaterialPropertyBlock();
            properties.SetFloat(WobbleId, 0);
            var sphere = speckMesh;
            for (var i = 0; i < capacity; i++)
            {
                var go = new GameObject(SpeckName + i, typeof(MeshFilter), typeof(MeshRenderer));
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
            if (_specks == null)
                return;

            for (var i = 0; i < count; i++)
            {
                var speck = _specks[_cursor++ % _specks.Length];
                var angle = i * GoldenAngle + (float)_random.NextDouble() * AngleJitter;
                var lift = Lift + i % LiftVariants * LiftStep;
                speck.Origin = origin;
                speck.Velocity = new Vector3(Mathf.Cos(angle), lift, Mathf.Sin(angle)) * strength;
                speck.Age = 0;
                speck.Duration = Duration + i % DurationVariants * DurationStep;
                speck.Size = (SpeckSize + i % SizeVariants * SizeStep) * size;
                speck.Renderer.sharedMaterial = material != null
                    ? material
                    : defaultMaterial;
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
            if (_specks == null)
                return;

            foreach (var speck in _specks)
                speck.Transform.gameObject.SetActive(false);
        }

        public void Tick(float deltaTime)
        {
            if (_specks == null)
                return;

            foreach (var speck in _specks)
            {
                if (!speck.Transform.gameObject.activeSelf)
                    continue;

                speck.Age += deltaTime;
                if (speck.Age > speck.Duration)
                {
                    speck.Transform.gameObject.SetActive(false);
                    continue;
                }

                var age = speck.Age;
                speck.Transform.position = speck.Origin + speck.Velocity * age + Vector3.down * (HalfGravity * age * age);
                speck.Transform.localScale = Vector3.one * (speck.Size * (1 - age / speck.Duration));
            }
        }
    }
}
