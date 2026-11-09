using System;
using System.Collections.Generic;

namespace Slime.Gameplay.Core.Geometry
{
    public readonly struct SweepHit
    {
        public readonly bool Hit;
        public readonly double Distance;
        public readonly int CrystalId;
        public readonly Vec2 Center;
        public readonly Vec2 Contact;

        public SweepHit(double distance, int crystalId, Vec2 center, Vec2 contact)
        {
            Hit = true;
            Distance = distance;
            CrystalId = crystalId;
            Center = center;
            Contact = contact;
        }

        public static SweepHit None => default;
    }

    /// <summary>
    /// Static crystal contours plus an alive mask. Ids are dense indices (0..Count-1), so ties resolve by id
    /// and iteration order is part of the reproducibility contract.
    /// </summary>
    public sealed class CrystalField
    {
        private readonly ConvexPolygon[] _polygons;
        private readonly bool[] _alive;

        public CrystalField(IReadOnlyList<ConvexPolygon> polygons)
        {
            _polygons = new ConvexPolygon[polygons.Count];
            _alive = new bool[polygons.Count];
            for (var i = 0; i < _polygons.Length; i++)
            {
                _polygons[i] = polygons[i] ?? throw new ArgumentNullException(nameof(polygons));
                _alive[i] = true;
            }
        }

        private CrystalField(ConvexPolygon[] polygons, bool[] alive)
        {
            _polygons = polygons;
            _alive = alive;
        }

        public int Count => _polygons.Length;
        public ConvexPolygon Polygon(int id) => _polygons[id];
        public bool IsAlive(int id) => _alive[id];
        public int AliveCount
        {
            get
            {
                var count = 0;
                foreach (var alive in _alive)
                {
                    if (alive)
                        count++;
                }

                return count;
            }
        }

        public void Kill(int id) => _alive[id] = false;

        public CrystalField Clone() => new CrystalField(_polygons, (bool[])_alive.Clone());

        public SweepHit SweepCircle(Vec2 origin, Vec2 direction, double radius, double maxDistance, double tolerance)
        {
            var best = SweepHit.None;
            for (var id = 0; id < _polygons.Length; id++)
            {
                if (!_alive[id])
                    continue;

                var polygon = _polygons[id];
                // Broad phase: the polygon lies within its bounding circle.
                var rel = polygon.Center - origin;
                var along = Vec2.Dot(rel, direction);
                var reach = polygon.BoundingRadius + radius + tolerance;
                if (along < -reach || along > maxDistance + reach)
                    continue;

                var side = Math.Abs(Vec2.Cross(direction, rel));
                if (side > reach)
                    continue;

                var t = polygon.SweepCircle(origin, direction, radius, maxDistance, tolerance);
                if (double.IsPositiveInfinity(t))
                    continue;

                // Equal distances within tolerance resolve to the lower id, independent of float noise.
                if (best.Hit && !(t < best.Distance - tolerance))
                    continue;

                var center = origin + direction * t;
                best = new SweepHit(t, id, center, polygon.ClosestPoint(center));
            }
            return best;
        }

        public List<int> Overlapping(Vec2 center, double radius, double tolerance, List<int> into = null)
        {
            var result = into ?? new List<int>();
            result.Clear();
            for (var id = 0; id < _polygons.Length; id++)
            {
                if (!_alive[id])
                    continue;

                var polygon = _polygons[id];
                if (Vec2.Distance(polygon.Center, center) > polygon.BoundingRadius + radius + tolerance)
                    continue;

                if (polygon.DistanceTo(center) <= radius + tolerance)
                    result.Add(id);
            }
            return result;
        }
    }
}
