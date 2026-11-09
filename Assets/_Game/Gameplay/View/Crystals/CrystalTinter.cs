using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Applies infection tint, brightness and the preview edge to a crystal through one shared property block.</summary>
    public sealed class CrystalTinter
    {
        public const float Neutral = 1f;

        private const float PreviewEdgeStrength = 0.95f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int EdgeStrengthId = Shader.PropertyToID("_EdgeStrength");

        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly SlimeViewConfig _config;

        public CrystalTinter(SlimeViewConfig config)
        {
            _config = config;
        }

        public void Tint(CrystalView crystal, float amount, float brightness, bool preview = false)
        {
            var target = _config != null
                ? _config.infectColor
                : Color.white;

            for (var i = 0; i < crystal.Renderers.Length; i++)
            {
                _properties.Clear();
                if (amount > 0 || brightness != Neutral)
                {
                    _properties.SetColor(BaseColorId, Color.Lerp(crystal.BaseColors[i], target, Mathf.Clamp01(amount)));
                    _properties.SetFloat(BrightnessId, crystal.BaseBrightness[i] * brightness);
                }

                if (preview)
                {
                    _properties.SetColor(EdgeColorId, _config.highlightColor);
                    _properties.SetFloat(EdgeWidthId, _config.previewEdgeWidth);
                    _properties.SetFloat(EdgeStrengthId, PreviewEdgeStrength);
                }

                crystal.Renderers[i].SetPropertyBlock(_properties);
            }
        }
    }
}
