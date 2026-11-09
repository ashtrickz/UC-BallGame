using UnityEngine;

namespace Slime.UI
{
    /// <summary>Sprite-local coordinates for the shared shimmer material, independent of atlas UVs and icon size.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.UI.Graphic))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.UI", sourceAssembly: "Game.UI", sourceClassName: "ShimmerCoordinates")]
    public sealed class ShimmerCoordinates : UnityEngine.UI.BaseMeshEffect
    {
        private const float MinExtent = 0.001f;

        public override void ModifyMesh(UnityEngine.UI.VertexHelper vertices)
        {
            if (!IsActive() || vertices.currentVertCount == 0)
                return;

            var vertex = new UIVertex();
            vertices.PopulateUIVertex(ref vertex, 0);
            var minimum = (Vector2)vertex.position;
            var maximum = minimum;
            for (var i = 1; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                minimum = Vector2.Min(minimum, vertex.position);
                maximum = Vector2.Max(maximum, vertex.position);
            }

            var extent = maximum - minimum;
            extent.x = Mathf.Max(MinExtent, extent.x);
            extent.y = Mathf.Max(MinExtent, extent.y);
            for (var i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4((vertex.position.x - minimum.x) / extent.x,
                    (vertex.position.y - minimum.y) / extent.y, 0, 0);
                vertices.SetUIVertex(vertex, i);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableChannel();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnableChannel();
        }

        private void EnableChannel()
        {
            if (graphic == null || graphic.canvas == null)
                return;

            graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            graphic.canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }
    }
}
