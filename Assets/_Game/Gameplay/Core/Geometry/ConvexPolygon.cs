using System;
using System.Collections.Generic;

namespace Game.Gameplay.Geometry
{
    /// <summary>
    /// Immutable convex polygon on the XZ plane, counter-clockwise by <see cref="Vec2.Cross"/>.
    /// Used for baked crystal footprints; all queries are allocation-free.
    /// </summary>
    public sealed class ConvexPolygon
    {
        public const int RotationSteps = 12;
        private const double Sqrt3Half = 0.86602540378443864676;
        private const int MinVertices = 3;
        private const double HalfArea = 0.5;
        private const double DuplicateDistance = 1e-9;
        private const double CollinearEpsilon = 1e-12;
        private const int HullSlack = 2;
        private const float FullTurnDegrees = 360f;
        // Exact constant table: generation never calls trigonometry, so seeds replay bit-for-bit on any CPU.
        private static readonly double[] StepCos = { 1, Sqrt3Half, 0.5, 0, -0.5, -Sqrt3Half, -1, -Sqrt3Half, -0.5, 0, 0.5, Sqrt3Half };
        private static readonly double[] StepSin = { 0, 0.5, Sqrt3Half, 1, Sqrt3Half, 0.5, 0, -0.5, -Sqrt3Half, -1, -Sqrt3Half, -0.5 };

        private readonly Vec2[] _vertices;
        public IReadOnlyList<Vec2> Vertices => _vertices;
        public int Count => _vertices.Length;
        public Vec2 Center { get; }
        public double BoundingRadius { get; }

        public ConvexPolygon(IReadOnlyList<Vec2> counterClockwiseVertices)
        {
            if (counterClockwiseVertices == null || counterClockwiseVertices.Count < MinVertices)
                throw new ArgumentException("A convex polygon needs at least three vertices.");

            _vertices = new Vec2[counterClockwiseVertices.Count];
            for (var i = 0; i < _vertices.Length; i++)
                _vertices[i] = counterClockwiseVertices[i];
            if (SignedArea(_vertices) <= 0)
                throw new ArgumentException("Polygon must be counter-clockwise with positive area.");

            double cx = 0, cz = 0;
            foreach (var v in _vertices)
            {
                cx += v.X;
                cz += v.Z;
            }
            Center = new Vec2(cx / _vertices.Length, cz / _vertices.Length);
            double r = 0;
            foreach (var v in _vertices)
                r = Math.Max(r, Vec2.Distance(v, Center));
            BoundingRadius = r;
        }

        public static double SignedArea(IReadOnlyList<Vec2> points)
        {
            double area = 0;
            for (var i = 0; i < points.Count; i++)
                area += Vec2.Cross(points[i], points[(i + 1) % points.Count]);
            return area * HalfArea;
        }

        public static ConvexPolygon Hull(IEnumerable<Vec2> points)
        {
            var list = new List<Vec2>(points);
            list.Sort((a, b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z));
            var unique = new List<Vec2>(list.Count);
            foreach (var p in list)
                if (unique.Count == 0 || Vec2.Distance(unique[unique.Count - 1], p) > DuplicateDistance)
                    unique.Add(p);
            if (unique.Count < MinVertices)
                throw new ArgumentException("Hull needs at least three distinct points.");

            var hull = new Vec2[unique.Count * HullSlack];
            var k = 0;
            for (var i = 0; i < unique.Count; i++)
            {
                while (k >= HullSlack && Vec2.Cross(hull[k - 1] - hull[k - HullSlack], unique[i] - hull[k - HullSlack]) <= CollinearEpsilon)
                    k--;
                hull[k++] = unique[i];
            }
            for (int i = unique.Count - HullSlack, lower = k + 1; i >= 0; i--)
            {
                while (k >= lower && Vec2.Cross(hull[k - 1] - hull[k - HullSlack], unique[i] - hull[k - HullSlack]) <= CollinearEpsilon)
                    k--;
                hull[k++] = unique[i];
            }
            var result = new Vec2[k - 1];
            Array.Copy(hull, result, k - 1);
            return new ConvexPolygon(result);
        }

        public static float YawDegrees(int rotationStep) => Mod(rotationStep, RotationSteps) * (FullTurnDegrees / RotationSteps);

        public static Vec2 TransformPoint(Vec2 local, int rotationStep, double scale, Vec2 offset)
        {
            var i = Mod(rotationStep, RotationSteps);
            double c = StepCos[i], s = StepSin[i];
            // Unity is left-handed: yaw θ maps (x, z) to (x·cosθ + z·sinθ, −x·sinθ + z·cosθ).
            return new Vec2((local.X * c + local.Z * s) * scale + offset.X, (-local.X * s + local.Z * c) * scale + offset.Z);
        }

        public ConvexPolygon Transformed(int rotationStep, double scale, Vec2 offset)
        {
            if (!(scale > 0))
                throw new ArgumentOutOfRangeException(nameof(scale));

            var points = new Vec2[_vertices.Length];
            for (var i = 0; i < points.Length; i++)
                points[i] = TransformPoint(_vertices[i], rotationStep, scale, offset);
            // Rotation and uniform positive scale keep the winding, so the order stays counter-clockwise.
            return new ConvexPolygon(points);
        }

        public bool Contains(Vec2 p)
        {
            for (var i = 0; i < _vertices.Length; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) % _vertices.Length];
                if (Vec2.Cross(b - a, p - a) < 0)
                    return false;
            }
            return true;
        }

        public Vec2 ClosestPoint(Vec2 p)
        {
            if (Contains(p))
                return p;

            var best = _vertices[0];
            var bestDistance = double.MaxValue;
            for (var i = 0; i < _vertices.Length; i++)
            {
                var candidate = ClosestOnSegment(p, _vertices[i], _vertices[(i + 1) % _vertices.Length]);
                var d = (candidate - p).LengthSquared;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }
            return best;
        }

        public double DistanceTo(Vec2 p) => Vec2.Distance(ClosestPoint(p), p);

        public double SweepCircle(Vec2 origin, Vec2 direction, double radius, double maxDistance, double tolerance)
        {
            if (DistanceTo(origin) <= radius + tolerance)
                return 0;

            var best = double.PositiveInfinity;
            for (var i = 0; i < _vertices.Length; i++)
            {
                var a = _vertices[i];
                var b = _vertices[(i + 1) % _vertices.Length];
                var edge = b - a;
                var length = edge.Length;
                if (length <= 0)
                    continue;

                var normal = new Vec2(edge.Z / length, -edge.X / length); // outward for a CCW polygon
                var approach = Vec2.Dot(direction, normal);
                if (approach < 0)
                {
                    var shiftedA = a + normal * radius;
                    var t = Vec2.Dot(shiftedA - origin, normal) / approach;
                    if (t >= 0 && t < best)
                    {
                        var hit = origin + direction * t;
                        var u = Vec2.Dot(hit - shiftedA, edge) / (length * length);
                        if (u >= 0 && u <= 1)
                            best = t;
                    }
                }
                // Rounded corner of the Minkowski sum.
                var toOrigin = origin - a;
                var bq = Vec2.Dot(toOrigin, direction);
                var cq = toOrigin.LengthSquared - radius * radius;
                var discriminant = bq * bq - cq;
                if (discriminant >= 0)
                {
                    var t = -bq - Math.Sqrt(discriminant);
                    if (t >= 0 && t < best)
                        best = t;
                }
            }
            return best <= maxDistance + tolerance ? Math.Min(best, Math.Max(0, maxDistance)) : double.PositiveInfinity;
        }

        public static Vec2 ClosestOnSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.LengthSquared;
            if (lengthSquared <= 0)
                return a;

            var t = Vec2.Dot(p - a, ab) / lengthSquared;
            if (t < 0)
                t = 0;
            else if (t > 1)
                t = 1;
            return a + ab * t;
        }

        private static int Mod(int value, int modulus)
        {
            var r = value % modulus;
            return r < 0 ? r + modulus : r;
        }
    }
}
