using UnityEngine;

namespace Game.Integration.Menu
{
    /// <summary>Resolution-independent glossy colour button and selected check mark.</summary>
    [AddComponentMenu("Ball Game/UI/Slime Color Swatch")]
    public sealed class SlimeColorSwatch : UnityEngine.UI.MaskableGraphic
    {
        private Color _baseColor, _topColor;
        private bool _selected;
        private static readonly Color Ink = new Color(0.13f, 0.16f, 0.06f);
        private static readonly Color Cream = new Color(1, 0.98f, 0.78f);

        public void SetColors(Color baseColor, Color topColor)
        {
            _baseColor = baseColor;
            _topColor = topColor;
            SetVerticesDirty();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected) return;
            _selected = selected;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var size = Mathf.Min(rect.width, rect.height);
            var center = rect.center;
            var radius = size * 0.39f;
            Ellipse(mesh, center + Vector2.down * size * 0.038f, Vector2.one * (radius + size * 0.025f),
                new Color(0.18f, 0.1f, 0.02f, 0.25f));
            if (_selected) Ellipse(mesh, center, Vector2.one * size * 0.485f, Cream);
            Ellipse(mesh, center, Vector2.one * (radius + size * 0.027f), Ink);
            Ellipse(mesh, center, Vector2.one * radius, _baseColor * new Color(0.72f, 0.72f, 0.72f, 1));
            Ellipse(mesh, center + Vector2.up * size * 0.025f, Vector2.one * (radius - size * 0.024f),
                _baseColor, Color.Lerp(_topColor, Color.white, 0.25f));
            Ellipse(mesh, center + new Vector2(-0.14f, 0.19f) * size,
                new Vector2(0.12f, 0.055f) * size, new Color(1, 1, 0.92f, 0.96f), null, 30);
            if (!_selected) return;
            var a = center + new Vector2(-0.15f, 0) * size;
            var b = center + new Vector2(-0.035f, -0.11f) * size;
            var c = center + new Vector2(0.18f, 0.13f) * size;
            Line(mesh, a, b, size * 0.155f, Ink);
            Line(mesh, b, c, size * 0.155f, Ink);
            Line(mesh, a, b, size * 0.085f, Cream);
            Line(mesh, b, c, size * 0.085f, Cream);
        }

        private void Ellipse(UnityEngine.UI.VertexHelper mesh, Vector2 center, Vector2 radii,
            Color bottom, Color? top = null, float rotation = 0)
        {
            const int segments = 64;
            var start = mesh.currentVertCount;
            var high = top ?? bottom;
            mesh.AddVert(center, Color.Lerp(bottom, high, 0.5f) * color, Vector2.zero);
            var angleOffset = rotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(angleOffset);
            var sin = Mathf.Sin(angleOffset);
            for (var i = 0; i <= segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                var point = new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                var rotated = new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
                var shade = Color.Lerp(bottom, high, (Mathf.Sin(angle) + 1) * 0.5f) * color;
                mesh.AddVert(center + rotated, shade, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private void Line(UnityEngine.UI.VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            var direction = (to - from).normalized;
            var normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
            var start = mesh.currentVertCount;
            mesh.AddVert(from - normal, tint * color, Vector2.zero);
            mesh.AddVert(from + normal, tint * color, Vector2.zero);
            mesh.AddVert(to + normal, tint * color, Vector2.zero);
            mesh.AddVert(to - normal, tint * color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
            Ellipse(mesh, from, Vector2.one * width * 0.5f, tint);
            Ellipse(mesh, to, Vector2.one * width * 0.5f, tint);
        }
    }
}
