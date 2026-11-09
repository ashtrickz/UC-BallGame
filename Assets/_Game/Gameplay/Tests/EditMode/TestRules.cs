using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;

namespace Game.Gameplay.Tests
{
    internal static class TestRules
    {
        public static ConvexPolygon Hexagon(double radius)
        {
            var points = new Vec2[6];
            for (var i = 0; i < 6; i++)
            {
                var a = i * Math.PI / 3;
                points[i] = new Vec2(Math.Cos(a) * radius, Math.Sin(a) * radius);
            }
            return new ConvexPolygon(points);
        }

        public static ConvexPolygon Square(double half, Vec2 center) => new ConvexPolygon(new[]
        {
            new Vec2(center.X - half, center.Z - half), new Vec2(center.X + half, center.Z - half),
            new Vec2(center.X + half, center.Z + half), new Vec2(center.X - half, center.Z + half),
        });

        public static IReadOnlyList<CrystalVariantData> Variants() => new[]
        {
            new CrystalVariantData(0, "hex", Hexagon(0.32)),
            new CrystalVariantData(1, "wide", new ConvexPolygon(new[]
            {
                new Vec2(-0.4, -0.2), new Vec2(0.3, -0.3), new Vec2(0.45, 0.15), new Vec2(0.0, 0.38), new Vec2(-0.38, 0.2),
            })),
        };

        public static GameRules Create(Func<RuleValues, RuleValues> tweak = null)
        {
            var values = RuleValues.Defaults;
            if (tweak != null) values = tweak(values);
            return new GameRules(values, Variants());
        }

        public static LevelData Level(GameRules rules, params ConvexPolygon[] contours)
        {
            var crystals = new List<CrystalPlacement>();
            for (var i = 0; i < contours.Length; i++)
                crystals.Add(new CrystalPlacement(i, 0, contours[i].Center, 0, 1, CrystalRole.Single, i, contours[i]));
            return new LevelData(1, 0, rules, crystals, null, null);
        }
    }
}
