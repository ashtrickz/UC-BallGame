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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _properties;

        private void Awake()
        {
            _mesh = new Mesh { name = "Blast ring (runtime)" };
            var vertices = new Vector3[Segments * 2];
            var triangles = new int[Segments * 6];
            for (var i = 0; i < Segments; i++)
            {
                var a = i * Mathf.PI * 2 / Segments;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                vertices[i * 2] = dir * (1 - thickness);
                vertices[i * 2 + 1] = dir;
                var n = (i + 1) % Segments;
                var t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = n * 2 + 1; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = n * 2; triangles[t + 5] = n * 2 + 1;
            }
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            ring.sharedMesh = _mesh;
            _properties = new MaterialPropertyBlock();
            Hide();
        }

        private void OnDestroy() { if (_mesh != null) Destroy(_mesh); }

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
