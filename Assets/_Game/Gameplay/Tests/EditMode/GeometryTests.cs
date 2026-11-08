using System.Collections.Generic;
using Game.Gameplay.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace Game.Gameplay.Tests
{
    public sealed class GeometryTests
    {
        private const double Tol = 1e-4;

        [Test]
        public void Circle_touching_edge_counts_as_hit()
        {
            var field = new CrystalField(new[] { TestRules.Square(0.5, new Vec2(0, 5)) });
            Assert.AreEqual(1, field.Overlapping(new Vec2(0, 4.5 - 0.3), 0.3, Tol).Count);
            Assert.AreEqual(0, field.Overlapping(new Vec2(0, 4.5 - 0.31), 0.3, Tol).Count);
        }

        [Test]
        public void Circle_touching_vertex_counts_as_hit()
        {
            var field = new CrystalField(new[] { TestRules.Square(0.5, Vec2.Zero) });
            var d = 0.4 / System.Math.Sqrt(2);
            Assert.AreEqual(1, field.Overlapping(new Vec2(0.5 + d, 0.5 + d), 0.4, Tol).Count);
            Assert.AreEqual(0, field.Overlapping(new Vec2(0.5 + d + 0.01, 0.5 + d + 0.01), 0.4, Tol).Count);
        }

        [Test]
        public void Crystal_is_hit_even_when_its_centre_is_outside_the_circle()
        {
            var field = new CrystalField(new[] { TestRules.Square(1, Vec2.Zero) });
            var hits = field.Overlapping(new Vec2(1.2, 0), 0.3, Tol);
            Assert.AreEqual(1, hits.Count);
            Assert.Greater(Vec2.Distance(field.Polygon(0).Center, new Vec2(1.2, 0)), 0.3);
        }

        [Test]
        public void Rotation_and_scale_match_unity_transform()
        {
            var hull = TestRules.Variants()[1].Hull;
            for (var step = 0; step < ConvexPolygon.RotationSteps; step++)
            {
                var placed = hull.Transformed(step, 1.7, new Vec2(2, -3));
                var rotation = Quaternion.Euler(0, ConvexPolygon.YawDegrees(step), 0);
                for (var i = 0; i < hull.Count; i++)
                {
                    var local = hull.Vertices[i];
                    var world = rotation * new Vector3((float)local.X, 0, (float)local.Z) * 1.7f + new Vector3(2, 0, -3);
                    Assert.AreEqual(world.x, placed.Vertices[i].X, 1e-4, $"step {step} vertex {i} x");
                    Assert.AreEqual(world.z, placed.Vertices[i].Z, 1e-4, $"step {step} vertex {i} z");
                }
            }
        }

        [Test]
        public void Hull_drops_interior_and_collinear_points()
        {
            var hull = ConvexPolygon.Hull(new List<Vec2>
            {
                new Vec2(0, 0), new Vec2(1, 0), new Vec2(2, 0), new Vec2(2, 2), new Vec2(0, 2), new Vec2(1, 1), new Vec2(0, 1),
            });
            Assert.AreEqual(4, hull.Count);
            Assert.Greater(ConvexPolygon.SignedArea(hull.Vertices), 0);
        }

        [Test]
        public void Sweep_returns_the_nearest_crystal_and_contact()
        {
            var field = new CrystalField(new[] { TestRules.Square(0.5, new Vec2(0, 8)), TestRules.Square(0.5, new Vec2(0.2, 4)) });
            var hit = field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.25, 20, Tol);
            Assert.IsTrue(hit.Hit);
            Assert.AreEqual(1, hit.CrystalId);
            Assert.AreEqual(3.5 - 0.25, hit.Distance, 1e-9);
            Assert.AreEqual(3.5, hit.Contact.Z, 1e-9);
            Assert.AreEqual(3.25, hit.Center.Z, 1e-9);
        }

        [Test]
        public void Sweep_hits_rounded_corner_beside_the_line()
        {
            // Square entirely to the right of the line; only the corner of the Minkowski sum touches.
            var field = new CrystalField(new[] { TestRules.Square(0.5, new Vec2(0.8, 5)) });
            var hit = field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.4, 20, Tol);
            Assert.IsTrue(hit.Hit);
            var expected = 4.5 - System.Math.Sqrt(0.4 * 0.4 - 0.3 * 0.3);
            Assert.AreEqual(expected, hit.Distance, 1e-9);
            Assert.IsFalse(field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.29, 20, Tol).Hit);
        }

        [Test]
        public void Narrow_passage_depends_on_the_corridor_width()
        {
            // Inner edges at x = ±0.7: a corridor half-width below 0.7 passes, above is blocked.
            var field = new CrystalField(new[] { TestRules.Square(0.3, new Vec2(-1, 5)), TestRules.Square(0.3, new Vec2(1, 5)) });
            Assert.IsFalse(field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.69, 20, Tol).Hit);
            Assert.IsTrue(field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.71, 20, Tol).Hit);
        }

        [Test]
        public void Equal_distances_resolve_to_the_lower_id()
        {
            var left = TestRules.Square(0.3, new Vec2(-0.5, 5));
            var right = TestRules.Square(0.3, new Vec2(0.5, 5));
            Assert.AreEqual(0, new CrystalField(new[] { left, right }).SweepCircle(Vec2.Zero, Vec2.Forward, 0.4, 20, Tol).CrystalId);
            Assert.AreEqual(0, new CrystalField(new[] { right, left }).SweepCircle(Vec2.Zero, Vec2.Forward, 0.4, 20, Tol).CrystalId);
        }

        [Test]
        public void Dead_crystals_are_ignored_and_clone_is_independent()
        {
            var field = new CrystalField(new[] { TestRules.Square(0.5, new Vec2(0, 3)), TestRules.Square(0.5, new Vec2(0, 6)) });
            var clone = field.Clone();
            clone.Kill(0);
            Assert.AreEqual(1, clone.SweepCircle(Vec2.Zero, Vec2.Forward, 0.2, 20, Tol).CrystalId);
            Assert.AreEqual(0, field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.2, 20, Tol).CrystalId);
        }

        [Test]
        public void Start_overlapping_returns_zero_distance()
        {
            var field = new CrystalField(new[] { TestRules.Square(0.5, new Vec2(0, 0.6)) });
            var hit = field.SweepCircle(Vec2.Zero, Vec2.Forward, 0.2, 20, Tol);
            Assert.IsTrue(hit.Hit);
            Assert.AreEqual(0, hit.Distance);
        }
    }
}
