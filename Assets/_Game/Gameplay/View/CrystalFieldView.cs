using System;
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
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");

        [SerializeField] private SplashPool debris;
        [SerializeField] private bool drawContours = true;

        private sealed class CrystalView
        {
            public Transform Root;
            public MeshRenderer[] Renderers;
            public Color[] BaseColors;
            public float[] BaseBrightness;
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
        private bool _committed;
        public event Action<bool> Shattered;

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
                    BaseBrightness = new float[variant.shards.Length],
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
                    view.BaseBrightness[s] = material.HasProperty(BrightnessId) ? material.GetFloat(BrightnessId) : 1f;
                }
                _crystals.Add(view);
            }
        }

        public void Clear()
        {
            foreach (var crystal in _crystals) if (crystal.Root != null) Destroy(crystal.Root.gameObject);
            _crystals.Clear();
            _highlight.Clear();
            _committed = false;
            _time = 0;
            debris?.Clear();
            _level = null;
        }

        /// <summary>Highlights exactly the ids the shared shot rules predicted (empty clears the preview).</summary>
        public void SetPreview(IReadOnlyList<int> ids) => SetPreview(ids, false);

        /// <summary>The accepted targets stay legible throughout the projectile flight.</summary>
        public void CommitPreview(IReadOnlyList<int> ids) => SetPreview(ids, true);

        private void SetPreview(IReadOnlyList<int> ids, bool committed)
        {
            var wasActive = _highlight.Count > 0;
            var wasCommitted = _committed;
            _highlight.Clear();
            if (ids != null)
                foreach (var id in ids)
                    if (id >= 0 && id < _crystals.Count && !_crystals[id].Destroyed) _highlight.Add(id);
            _committed = committed;
            var active = _highlight.Count > 0;
            for (var i = 0; i < _crystals.Count; i++)
            {
                var crystal = _crystals[i];
                var on = _highlight.Contains(i) && !crystal.Destroyed;
                if (crystal.Destroyed || (crystal.Highlighted == on && wasActive == active && wasCommitted == committed)) continue;
                crystal.Highlighted = on;
                if (on) PreviewTint(crystal);
                else Tint(crystal, 0, active ? _config.surroundingCrystalBrightness : 1);
            }
        }

        private void PreviewTint(CrystalView crystal)
        {
            // Keep the candy colours; the brighter prism edges identify the affected set.
            var pulse = _committed ? 0f : Mathf.Sin(_time * 7f) * 0.06f;
            Tint(crystal, _config.previewTint, _config.previewBrightness + pulse, true);
        }

        private void Update()
        {
            _time += _clock.Delta;
            if (_highlight.Count == 0) return;
            foreach (var id in _highlight)
                if (id < _crystals.Count && !_crystals[id].Destroyed) PreviewTint(_crystals[id]);
        }

        /// <summary>Wave of infection and shattering outward from the blast centre. Completes when every hit crystal is gone.</summary>
        public async UniTask ClearAsync(IReadOnlyList<int> ids, Vector3 center, float radius, CancellationToken token)
        {
            _highlight.Clear();
            _committed = false;
            if (ids == null || ids.Count == 0) { SetPreview(null); return; }
            foreach (var crystal in _crystals)
                if (!crystal.Destroyed && !crystal.Highlighted) Tint(crystal, 0, 1);
            var strength = _config.ImpactStrength(radius);
            var waveDuration = _config.ImpactWaveDuration(radius);
            var tasks = new List<UniTask>(ids.Count);
            foreach (var id in ids)
            {
                var crystal = _crystals[id];
                var flat = crystal.Root.position - center;
                flat.y = 0;
                var delay = Mathf.Clamp01(flat.magnitude / Mathf.Max(0.02f, radius)) * waveDuration;
                tasks.Add(ShatterAsync(crystal, delay, strength, token));
            }
            await UniTask.WhenAll(tasks);
        }

        private async UniTask ShatterAsync(CrystalView crystal, float delay, float strength, CancellationToken token)
        {
            await _clock.Wait(delay, token);
            crystal.Highlighted = false;
            await _clock.Animate(_config.infectDuration,
                t => Tint(crystal, Mathf.Lerp(_config.previewTint, 1, t), Mathf.Lerp(_config.previewBrightness, 1.4f, t)), token);
            var top = crystal.Root.position + Vector3.up * (0.45f * crystal.Scale.y);
            var material = crystal.Materials.Length > 0 ? crystal.Materials[0] : null;
            Shattered?.Invoke(crystal.Scale.y >= 1.1f);
            debris?.Burst(top, Mathf.RoundToInt(Mathf.Lerp(4, 8, strength)), Mathf.Lerp(0.65f, 1.35f, strength),
                material, Mathf.Lerp(0.85f, 1.2f, strength));
            await _clock.Animate(_config.shatterDuration, t =>
            {
                var s = t < 0.3f ? 1 + 0.18f * GameClock.Smooth(t / 0.3f) : (1.18f * (1 - GameClock.Smooth((t - 0.3f) / 0.7f)));
                crystal.Root.localScale = crystal.Scale * Mathf.Max(0, s);
            }, token);
            crystal.Destroyed = true;
            crystal.Root.gameObject.SetActive(false);
            debris?.Burst(crystal.Root.position + Vector3.up * 0.15f, Mathf.RoundToInt(Mathf.Lerp(2, 4, strength)),
                Mathf.Lerp(0.3f, 0.65f, strength), material, 0.75f);
        }

        private void Tint(CrystalView crystal, float amount, float brightness, bool preview = false)
        {
            var target = _config != null ? _config.infectColor : Color.white;
            for (var i = 0; i < crystal.Renderers.Length; i++)
            {
                _properties.Clear();
                if (amount > 0 || brightness != 1)
                {
                    _properties.SetColor(BaseColorId, Color.Lerp(crystal.BaseColors[i], target, Mathf.Clamp01(amount)));
                    _properties.SetFloat(BrightnessId, crystal.BaseBrightness[i] * brightness);
                }
                if (preview)
                {
                    _properties.SetColor(EdgeColorId, _config.highlightColor);
                    _properties.SetFloat(EdgeWidthId, _config.previewEdgeWidth);
                    _properties.SetFloat(EdgeStrengthId, 0.95f);
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
