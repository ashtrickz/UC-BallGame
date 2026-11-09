using UnityEngine;
using UnityEngine.UI;

namespace Game.Integration.Menu
{
    /// <summary>Resolution-independent glossy colour button and selected check mark.</summary>
    [AddComponentMenu("Ball Game/UI/Slime Color Swatch")]
    public sealed class SlimeColorSwatch : MaskableGraphic
    {
        private const int EllipseSegments = 64;
        private const float FullTurn = Mathf.PI * 2f;
        private const float Half = 0.5f;

        // Sizes as shares of the swatch size.
        private const float BodyRadius = 0.39f;
        private const float ShadowDrop = 0.038f;
        private const float ShadowGrow = 0.025f;
        private const float SelectedRing = 0.485f;
        private const float OutlineGrow = 0.027f;
        private const float GlossLift = 0.025f;
        private const float GlossShrink = 0.024f;
        private const float GlossTopBlend = 0.25f;
        private const float HighlightRotation = 30f;
        private const float CheckOuterWidth = 0.155f;
        private const float CheckInnerWidth = 0.085f;

        private static readonly Color Ink = new Color(0.13f, 0.16f, 0.06f);
        private static readonly Color Cream = new Color(1, 0.98f, 0.78f);
        private static readonly Color Shadow = new Color(0.18f, 0.1f, 0.02f, 0.25f);
        private static readonly Color BodyShade = new Color(0.72f, 0.72f, 0.72f, 1);
        private static readonly Color Highlight = new Color(1, 1, 0.92f, 0.96f);
        private static readonly Vector2 HighlightOffset = new Vector2(-0.14f, 0.19f);
        private static readonly Vector2 HighlightRadii = new Vector2(0.12f, 0.055f);
        private static readonly Vector2 CheckStart = new Vector2(-0.15f, 0);
        private static readonly Vector2 CheckCorner = new Vector2(-0.035f, -0.11f);
        private static readonly Vector2 CheckEnd = new Vector2(0.18f, 0.13f);

        private Color _baseColor;
        private Color _topColor;
        private bool _selected;

        public void SetColors(Color baseColor, Color topColor)
        {
            _baseColor = baseColor;
            _topColor = topColor;
            SetVerticesDirty();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            var radius = size * BodyRadius;
            Ellipse(mesh, center + Vector2.down * size * ShadowDrop, Vector2.one * (radius + size * ShadowGrow), Shadow);
            if (_selected)
                Ellipse(mesh, center, Vector2.one * size * SelectedRing, Cream);

            Ellipse(mesh, center, Vector2.one * (radius + size * OutlineGrow), Ink);
            Ellipse(mesh, center, Vector2.one * radius, _baseColor * BodyShade);
            Ellipse(
                mesh,
                center + Vector2.up * size * GlossLift,
                Vector2.one * (radius - size * GlossShrink),
                _baseColor,
                Color.Lerp(_topColor, Color.white, GlossTopBlend));
            Ellipse(mesh, center + HighlightOffset * size, HighlightRadii * size, Highlight, null, HighlightRotation);
            if (!_selected)
                return;

            var start = center + CheckStart * size;
            var corner = center + CheckCorner * size;
            var end = center + CheckEnd * size;
            Line(mesh, start, corner, size * CheckOuterWidth, Ink);
            Line(mesh, corner, end, size * CheckOuterWidth, Ink);
            Line(mesh, start, corner, size * CheckInnerWidth, Cream);
            Line(mesh, corner, end, size * CheckInnerWidth, Cream);
        }

        private void Ellipse(VertexHelper mesh, Vector2 center, Vector2 radii, Color bottom, Color? top = null, float rotation = 0)
        {
            var start = mesh.currentVertCount;
            var high = top ?? bottom;
            mesh.AddVert(center, Color.Lerp(bottom, high, Half) * color, Vector2.zero);
            var angleOffset = rotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleOffset);
            var sin = Mathf.Sin(angleOffset);
            for (var i = 0; i <= EllipseSegments; i++)
            {
                var angle = i * FullTurn / EllipseSegments;
                var point = new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                var rotated = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
                var shade = Color.Lerp(bottom, high, (Mathf.Sin(angle) + 1) * Half) * color;
                mesh.AddVert(center + rotated, shade, Vector2.zero);
                if (i > 0)
                    mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            var direction = (to - from).normalized;
            var normal = new Vector2(-direction.y, direction.x) * width * Half;
            var start = mesh.currentVertCount;
            var first = start;
            var second = start + 1;
            var third = second + 1;
            var fourth = third + 1;
            mesh.AddVert(from - normal, tint * color, Vector2.zero);
            mesh.AddVert(from + normal, tint * color, Vector2.zero);
            mesh.AddVert(to + normal, tint * color, Vector2.zero);
            mesh.AddVert(to - normal, tint * color, Vector2.zero);
            mesh.AddTriangle(first, second, third);
            mesh.AddTriangle(third, fourth, first);
            Ellipse(mesh, from, Vector2.one * width * Half, tint);
            Ellipse(mesh, to, Vector2.one * width * Half, tint);
        }
    }
}
