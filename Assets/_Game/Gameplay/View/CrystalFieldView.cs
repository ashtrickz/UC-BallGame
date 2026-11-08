using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Builds the level's crystals from the catalog variants (same shards the contour was baked from), shows the
    /// preview highlight and plays the clearing wave. Destroyed crystals stay gone until the next attempt; debris is
    /// a separate secondary effect that never becomes an obstacle.
    /// </summary>
    public sealed class CrystalFieldView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

        [SerializeField] private SplashPool debris;
        [SerializeField] private bool drawContours = true;

        private sealed class CrystalView
        {
            public Transform Root;
            public MeshRenderer[] Renderers;
            public Color[] BaseColors;
            public Material[] Materials;
            public Vector3 Scale;
            public bool Highlighted, Destroyed;
        }

        private readonly List<CrystalView> _crystals = new List<CrystalView>();
        private readonly HashSet<int> _highlight = new HashSet<int>();
        private MaterialPropertyBlock _properties;
        private GameClock _clock = new GameClock();
        private SlimeViewConfig _config;
        private LevelData _level;
        private float _time;

        public void Bind(GameClock clock, SlimeViewConfig config)
        {
            _clock = clock;
            _config = config;
            debris?.Bind(clock);
        }

        public void Build(LevelData level, CrystalCatalog catalog)
        {
            Clear();
            _level = level;
            _properties ??= new MaterialPropertyBlock();
            foreach (var placement in level.Crystals)
            {
                var variant = catalog.variants[placement.VariantId];
                var root = new GameObject($"Crystal {placement.Id} ({variant.name})").transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3((float)placement.Position.X, 0, (float)placement.Position.Z);
                root.localRotation = Quaternion.Euler(0, ConvexPolygon.YawDegrees(placement.RotationStep), 0);
                var scale = Vector3.one * (float)placement.Scale;
                root.localScale = scale;
                var view = new CrystalView
                {
                    Root = root, Scale = scale,
                    Renderers = new MeshRenderer[variant.shards.Length],
                    BaseColors = new Color[variant.shards.Length],
                    Materials = new Material[variant.shards.Length],
                };
                for (var s = 0; s < variant.shards.Length; s++)
                {
                    var shard = variant.shards[s];
                    var go = new GameObject("Shard", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = shard.localPosition;
                    go.transform.localRotation = Quaternion.Euler(shard.localEuler);
                    go.transform.localScale = shard.localScale;
                    go.GetComponent<MeshFilter>().sharedMesh = shard.mesh;
                    // Alternate the two candy materials so neighbours read as separate crystals.
                    var material = catalog.materials[(shard.materialIndex + placement.Id) % catalog.materials.Length];
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    view.Renderers[s] = renderer;
                    view.Materials[s] = material;
                    view.BaseColors[s] = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                }
                _crystals.Add(view);
            }
        }

        public void Clear()
        {
            foreach (var crystal in _crystals) if (crystal.Root != null) Destroy(crystal.Root.gameObject);
            _crystals.Clear();
            _highlight.Clear();
            debris?.Clear();
            _level = null;
        }

        /// <summary>Highlights exactly the ids the shared shot rules predicted (empty clears the preview).</summary>
        public void SetPreview(IReadOnlyList<int> ids)
        {
            _highlight.Clear();
            if (ids != null) foreach (var id in ids) _highlight.Add(id);
            for (var i = 0; i < _crystals.Count; i++)
            {
                var crystal = _crystals[i];
                var on = _highlight.Contains(i) && !crystal.Destroyed;
                if (crystal.Highlighted == on) continue;
                crystal.Highlighted = on;
                if (!on) Tint(crystal, 0, 1);
            }
        }

        private void Update()
        {
            _time += _clock.Delta;
            if (_highlight.Count == 0) return;
            var pulse = 0.45f + 0.25f * Mathf.Sin(_time * 9f);
            foreach (var id in _highlight)
                if (id < _crystals.Count && !_crystals[id].Destroyed) Tint(_crystals[id], pulse, 1.18f);
        }

        /// <summary>Wave of infection and shattering outward from the blast centre. Completes when every hit crystal is gone.</summary>
        public async UniTask ClearAsync(IReadOnlyList<int> ids, Vector3 center, CancellationToken token)
        {
            _highlight.Clear();
            if (ids == null || ids.Count == 0) return;
            var tasks = new List<UniTask>(ids.Count);
            foreach (var id in ids)
            {
                var crystal = _crystals[id];
                crystal.Highlighted = false;
                var flat = crystal.Root.position - center;
                flat.y = 0;
                var delay = Mathf.Min(0.45f, flat.magnitude / _config.waveSpeed);
                tasks.Add(ShatterAsync(crystal, delay, token));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask ShatterAsync(CrystalView crystal, float delay, CancellationToken token)
        {
            await _clock.Wait(delay, token);
            await _clock.Animate(_config.infectDuration, t => Tint(crystal, t, 1 + 0.35f * t), token);
            var top = crystal.Root.position + Vector3.up * (0.45f * crystal.Scale.y);
            debris?.Burst(top, 8, 0.8f, crystal.Materials.Length > 0 ? crystal.Materials[0] : null, 1.2f);
            await _clock.Animate(_config.shatterDuration, t =>
            {
                var s = t < 0.3f ? 1 + 0.18f * GameClock.Smooth(t / 0.3f) : (1.18f * (1 - GameClock.Smooth((t - 0.3f) / 0.7f)));
                crystal.Root.localScale = crystal.Scale * Mathf.Max(0, s);
            }, token);
            crystal.Destroyed = true;
            crystal.Root.gameObject.SetActive(false);
            debris?.Burst(crystal.Root.position + Vector3.up * 0.15f, 6, 0.6f, null, 0.9f);
        }

        private void Tint(CrystalView crystal, float amount, float brightness)
        {
            var target = _config != null ? _config.infectColor : Color.white;
            for (var i = 0; i < crystal.Renderers.Length; i++)
            {
                _properties.Clear();
                if (amount > 0 || brightness != 1)
                {
                    _properties.SetColor(BaseColorId, Color.Lerp(crystal.BaseColors[i], target, Mathf.Clamp01(amount)));
                    _properties.SetFloat(BrightnessId, 1.05f * brightness);
                }
                crystal.Renderers[i].SetPropertyBlock(_properties);
            }
        }

        private void OnDrawGizmos()
        {
            // Logical contours on top of the visible variants (Editor diagnostic for G03–G06).
            if (!drawContours || _level == null) return;
            foreach (var placement in _level.Crystals)
            {
                if (placement.Id < _crystals.Count && _crystals[placement.Id].Destroyed) continue;
                Gizmos.color = placement.Role == CrystalRole.Decor ? new Color(1, 1, 1, 0.35f) : Color.yellow;
                var vertices = placement.Contour.Vertices;
                for (var i = 0; i < vertices.Count; i++)
                {
                    var a = vertices[i];
                    var b = vertices[(i + 1) % vertices.Count];
                    Gizmos.DrawLine(new Vector3((float)a.X, 0.03f, (float)a.Z), new Vector3((float)b.X, 0.03f, (float)b.Z));
                }
            }
        }
    }
}
