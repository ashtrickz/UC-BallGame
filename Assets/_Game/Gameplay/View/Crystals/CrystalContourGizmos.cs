using Slime.Gameplay.Core.Level;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Slime.Gameplay.View
{
    /// <summary>Editor diagnostic: draws the logical crystal contours over the visible variants. Disable the component to hide them.</summary>
    [RequireComponent(typeof(CrystalFieldView))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.View", sourceAssembly: "Game.Gameplay.View", sourceClassName: "CrystalContourGizmos")]
    public sealed class CrystalContourGizmos : MonoBehaviour
    {
#if UNITY_EDITOR
        private const float Height = 0.03f;

        private static readonly Color DecorColor = new Color(1, 1, 1, 0.35f);
        private static readonly Color ObstacleColor = Color.yellow;

        private CrystalFieldView _field;

        private void OnDrawGizmos()
        {
            if (!enabled)
                return;

            if (_field == null)
                _field = GetComponent<CrystalFieldView>();

            var level = _field != null ? _field.Level : null;
            if (level == null)
                return;

            foreach (var placement in level.Crystals)
            {
                if (_field.IsDestroyed(placement.Id))
                    continue;

                Gizmos.color = placement.Role == CrystalRole.Decor
                    ? DecorColor
                    : ObstacleColor;
                var vertices = placement.Contour.Vertices;
                for (var i = 0; i < vertices.Count; i++)
                {
                    var a = vertices[i];
                    var b = vertices[(i + 1) % vertices.Count];
                    Gizmos.DrawLine(new Vector3((float)a.X, Height, (float)a.Z), new Vector3((float)b.X, Height, (float)b.Z));
                }
            }
        }
#endif
    }
}
