using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Game.Gameplay.Config;
using Game.Gameplay.Generation;
using Game.Gameplay.Level;
using UnityEditor;
using UnityEngine;

namespace Game.Gameplay.Editor
{
    /// <summary>
    /// Source of the ten campaign layouts (metres; x = lateral, z = along the route) and the design analysis:
    /// cheapest sequence, alternatives, control sequence placed inside a comfortable hold window, reserve.
    /// "Write Campaign Levels" bakes the result into Config/Campaign.asset, which the game plays.
    /// </summary>
    public static class CampaignDesigns
    {
        public const string AssetPath = GameplayAssetsBuilder.ConfigFolder + "/Campaign.asset";
        private const int Spire = 0, Stout = 1, Twin = 2, Cluster = 3;
        public const double MinWindow = 0.15;

        public sealed class Design
        {
            public string Title, Idea;
            public double Route;
            public List<CrystalSpec> Crystals = new List<CrystalSpec>();
            /// <summary>Named alternative strategies to compare (solver constraints).</summary>
            public List<(string name, int maxShots, double maxHold, double firstMaxHold)> Alternatives =
                new List<(string, int, double, double)>();
            /// <summary>Named fixed hold sequences to price (e.g. a tempting but wasteful shot).</summary>
            public List<(string name, double[] holds)> Fixed = new List<(string, double[])>();
            /// <summary>Limit for the control sequence when the lesson is about a specific number of shots.</summary>
            public int ControlMaxShots = 8;
        }

        private static CrystalSpec C(double x, double z, double s = 1, int rot = 0, int v = Spire, CrystalRole role = CrystalRole.Single) =>
            new CrystalSpec(v, x, z, rot, s, role);

        private static IEnumerable<CrystalSpec> Group(double z, params (double x, double dz, double s)[] members)
        {
            var i = 0;
            foreach (var m in members) yield return C(m.x, z + m.dz, m.s, (i * 5) % 12, i++ % 3 == 2 ? Cluster : Spire, CrystalRole.Group);
        }

        private static IEnumerable<CrystalSpec> Walls(double z, double innerEdge, int rows = 2, double spacing = 0.7)
        {
            // Spire at rotation 0 reaches 0.278 m to each side of its anchor.
            var x = innerEdge + 0.278;
            for (var r = 0; r < rows; r++)
            {
                yield return C(-x, z + r * spacing, 1, 0, Spire, CrystalRole.Pair);
                yield return C(x, z + r * spacing, 1, 0, Spire, CrystalRole.Pair);
            }
        }

        /// <summary>Atmosphere on the sides, far outside any corridor (never blocks, may be caught by big blasts).</summary>
        private static IEnumerable<CrystalSpec> Decor(int seed, double route)
        {
            var state = (uint)(seed * 2654435761u + 12345u);
            double Next() { state = state * 1664525u + 1013904223u; return (state >> 8) / 16777216.0; }
            var side = 1;
            for (var z = -1.0; z < route - 2.2; z += 1.1 + Next() * 1.2)
            {
                side = -side;
                var x = side * (2.4 + Next() * 2.0);
                yield return C(x, z, 0.8 + Next() * 0.45, (int)(Next() * 12), (int)(Next() * 4), CrystalRole.Decor);
            }
        }

        private static readonly (double x, double dz, double s)[] DenseGroup =
        {
            (-0.55, 0, 0.9), (0, 0, 0.9), (0.55, 0, 0.9),
            (-0.85, 0.55, 0.85), (-0.3, 0.55, 0.85), (0.3, 0.55, 0.85), (0.85, 0.55, 0.85),
            (-0.55, 1.1, 0.9), (0.05, 1.1, 0.9), (0.6, 1.1, 0.9),
        };

        private static readonly (double x, double dz, double s)[] WideRow =
        {
            (-0.9, 0, 0.85), (-0.3, 0, 0.85), (0.3, 0, 0.85), (0.9, 0, 0.85),
            (-0.6, 0.45, 0.8), (0, 0.45, 0.8), (0.6, 0.45, 0.8),
        };

        public static List<Design> All()
        {
            var levels = new List<Design>();
            Design L(string title, string idea, double route)
            {
                var d = new Design { Title = title, Idea = idea, Route = route };
                levels.Add(d);
                return d;
            }

            var l1 = L("First Taps", "Short taps are enough for single crystals; extra charge only wastes size.", 16);
            l1.Crystals.Add(C(0, 5));
            l1.Crystals.Add(C(0.05, 10.5, 1, 3));
            l1.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            var l2 = L("The Cluster", "One bigger shot clears a dense group cheaper than a series of taps.", 14);
            l2.Crystals.AddRange(Group(5.4, DenseGroup));
            l2.Alternatives.Add(("one shot", 1, double.PositiveInfinity, double.PositiveInfinity));
            l2.Alternatives.Add(("small shots only (<=0.3 s, up to 12)", 12, 0.3, double.PositiveInfinity));
            l2.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));
            l2.ControlMaxShots = 1;

            var l3 = L("Mixed Strength", "Change the charge per obstacle: tap, big shot, tap.", 22);
            l3.Crystals.Add(C(0, 4.5));
            l3.Crystals.AddRange(Group(9.4, DenseGroup));
            l3.Crystals.Add(C(-0.05, 16, 1, 2));
            l3.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            var l4 = L("Squeeze Through", "Clear only the middle; the smaller slime fits between the side crystals.", 16);
            l4.Crystals.Add(C(0, 5.0, 0.8));
            l4.Crystals.Add(C(0.02, 5.55, 0.8, 4));
            l4.Crystals.AddRange(Walls(6.7, 0.842));
            l4.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            var l5 = L("Two Rows", "Compare one shot through both groups with clearing them one by one.", 18);
            l5.Crystals.AddRange(Group(5.3, WideRow));
            l5.Crystals.AddRange(Group(7.1, WideRow));
            l5.Alternatives.Add(("one shot", 1, double.PositiveInfinity, double.PositiveInfinity));
            l5.Alternatives.Add(("two shots", 2, double.PositiveInfinity, double.PositiveInfinity));
            l5.Alternatives.Add(("any", 8, double.PositiveInfinity, double.PositiveInfinity));

            var l6 = L("Tempting Crowd", "Many crystals on the side do not block the way: tap the blocker only.", 18);
            l6.Crystals.Add(C(0, 5.6, 0.9));
            foreach (var p in new[] { (1.35, 5.0), (1.95, 5.2), (2.55, 5.0), (1.5, 5.75), (2.15, 5.85), (1.4, 6.5), (2.0, 6.55), (2.6, 6.35) })
                l6.Crystals.Add(C(p.Item1, p.Item2, 0.9, (int)(p.Item1 * 10) % 12, Spire, CrystalRole.Group));
            l6.Crystals.Add(C(0.05, 10.8, 0.95, 3));
            foreach (var p in new[] { (-1.35, 10.3), (-1.95, 10.5), (-1.45, 11.05), (-2.1, 11.2), (-1.5, 11.8), (-2.2, 11.95) })
                l6.Crystals.Add(C(p.Item1, p.Item2, 0.9, (int)(-p.Item1 * 10) % 12, Spire, CrystalRole.Group));
            l6.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));
            l6.Fixed.Add(("two 1 s holds that also wipe the crowds", new[] { 1.0, 1.0 }));

            var l7 = L("Tight Corridors", "Narrow passages need a smaller slime: save size for the passages ahead.", 26);
            l7.Crystals.Add(C(0, 4.5));
            l7.Crystals.AddRange(Walls(7.2, 0.80));
            l7.Crystals.Add(C(0, 11, 1, 5));
            l7.Crystals.AddRange(Walls(13.4, 0.72));
            l7.Crystals.Add(C(0.05, 17.3, 1, 8));
            l7.Crystals.AddRange(Walls(19.6, 0.68));
            l7.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            var l8 = L("Two Layers", "A small first shot moves you closer; the second shot then hits the back layer up close.", 18);
            l8.Crystals.Add(C(0, 4.8));
            l8.Crystals.AddRange(Group(7.0, WideRow));
            l8.Alternatives.Add(("big shot from the start", 1, double.PositiveInfinity, double.PositiveInfinity));
            l8.Alternatives.Add(("small first shot", 8, double.PositiveInfinity, 0.1));

            var l9 = L("Everything Together", "Big shot on the cluster, enough shrink for the passage, cheap taps after.", 26);
            l9.Crystals.AddRange(Group(5.0, DenseGroup));
            l9.Crystals.AddRange(Walls(9.6, 0.78));
            l9.Crystals.Add(C(0, 13.5, 1, 1));
            l9.Crystals.Add(C(-0.05, 17, 0.9, 6));
            l9.Crystals.Add(C(0.05, 20.5, 1, 9));
            l9.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            var l10 = L("The Way Home", "Plan your size all the way to the doors; the last passage is the narrowest.", 32);
            l10.Crystals.Add(C(0, 4.2));
            l10.Crystals.AddRange(Group(7.6, WideRow));
            l10.Crystals.Add(C(0, 12.2, 1, 4));
            l10.Crystals.AddRange(Group(14.6, WideRow));
            l10.Crystals.AddRange(Walls(19.0, 0.76));
            l10.Crystals.Add(C(0.04, 22.0, 1, 7));
            l10.Crystals.AddRange(Walls(25.2, 0.62));
            l10.Alternatives.Add(("taps only", 8, 0.1, double.PositiveInfinity));

            for (var i = 0; i < levels.Count; i++) levels[i].Crystals.AddRange(Decor(i + 1, levels[i].Route));
            return levels;
        }

        // ------------------------------------------------------------------ analysis

        public sealed class Result
        {
            public CampaignLevelSpec Spec;
            public double Cheapest, ControlCost;
            public List<double> Control = new List<double>();
            public List<CampaignSolver.Window> Windows = new List<CampaignSolver.Window>();
            public List<(string name, double cost)> Alternatives = new List<(string, double)>();
            public string Problem;
        }

        public static GameRules CampaignRules()
        {
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(GameplayAssetsBuilder.GameConfigPath);
            return config.CreateSnapshot();
        }

        /// <summary>Solves a design and places each control hold inside its window (so the release is forgiving).</summary>
        public static Result Analyse(GameRules campaignRules, Design design, int number)
        {
            var draft = new CampaignLevelSpec(number, design.Title, design.Idea, design.Route, design.Crystals, Array.Empty<double>());
            var rules = CampaignLevels.RulesFor(campaignRules, draft);
            var layout = CampaignLevels.BuildLayout(rules, draft);
            var result = new Result();
            var best = CampaignSolver.Cheapest(rules, layout);
            result.Cheapest = best.Cost;
            if (!best.Found) { result.Problem = "no winning sequence"; result.Spec = draft; return result; }

            // Control: the cheapest sequence among outcomes that stay the same over at least 0.15 s of hold.
            var robust = CampaignSolver.Cheapest(rules, layout, design.ControlMaxShots, minWindow: MinWindow);
            if (!robust.Found) { result.Problem = "no sequence with forgiving holds"; result.Spec = draft; return result; }
            var holds = robust.Holds;
            for (var i = 0; i < holds.Count; i++) result.Windows.Add(CampaignSolver.HoldWindow(rules, layout, holds, i));
            result.Control = holds;
            result.ControlCost = CampaignSolver.CostOf(rules, layout, holds);
            foreach (var alt in design.Alternatives)
                result.Alternatives.Add((alt.name, CampaignSolver.Cheapest(rules, layout, alt.maxShots, double.PositiveInfinity, alt.maxHold, alt.firstMaxHold).Cost));
            result.Spec = new CampaignLevelSpec(number, design.Title, design.Idea, design.Route, design.Crystals, holds);
            if (result.ControlCost > rules.ControlBudgetLimit + rules.Tolerance) result.Problem = "control exceeds B/1.2";
            foreach (var alt in design.Fixed)
                result.Alternatives.Add((alt.name, CampaignSolver.CostOf(rules, layout, alt.holds)));
            foreach (var w in result.Windows) if (w.Width < MinWindow - 1e-6) result.Problem = (result.Problem ?? "") + $" narrow window {w}";
            return result;
        }

        public static string Report()
        {
            var rules = CampaignRules();
            var sb = new StringBuilder();
            var designs = All();
            for (var i = 0; i < designs.Count; i++)
            {
                var r = Analyse(rules, designs[i], i + 1);
                sb.Append($"L{i + 1} {designs[i].Title}: cheapest {Fmt(r.Cheapest)} | control {Fmt(r.ControlCost)} ({Fmt(r.ControlCost / rules.Budget * 100)}% of B) holds ");
                sb.Append(string.Join("/", r.Control.ConvertAll(h => h.ToString("0.00", CultureInfo.InvariantCulture))));
                sb.Append(" windows ").Append(string.Join(" ", r.Windows));
                foreach (var a in r.Alternatives) sb.Append($" | {a.name} {Fmt(a.cost)}");
                if (r.Problem != null) sb.Append("  !! ").Append(r.Problem);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string Fmt(double v) => double.IsInfinity(v) ? "—" : v.ToString("0.###", CultureInfo.InvariantCulture);

        [MenuItem("Tools/Ball Game/Gameplay/Campaign/Analyse Designs")]
        public static void AnalyseFromMenu() => Debug.Log("[Campaign]\n" + Report());

        [MenuItem("Tools/Ball Game/Gameplay/Campaign/Write Campaign Levels")]
        public static string Write()
        {
            var rules = CampaignRules();
            var designs = All();
            var asset = AssetDatabase.LoadAssetAtPath<CampaignAsset>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<CampaignAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            var levels = new CampaignAsset.Level[designs.Count];
            var problems = new StringBuilder();
            for (var i = 0; i < designs.Count; i++)
            {
                var r = Analyse(rules, designs[i], i + 1);
                if (r.Problem != null) problems.AppendLine($"L{i + 1}: {r.Problem}");
                var crystals = new CampaignAsset.Crystal[designs[i].Crystals.Count];
                for (var c = 0; c < crystals.Length; c++)
                {
                    var s = designs[i].Crystals[c];
                    crystals[c] = new CampaignAsset.Crystal
                    {
                        variant = s.Variant, position = new Vector2((float)s.X, (float)s.Z), rotationStep = s.RotationStep,
                        scale = (float)s.Scale, role = s.Role,
                    };
                }
                levels[i] = new CampaignAsset.Level
                {
                    title = designs[i].Title, idea = designs[i].Idea, routeLength = (float)designs[i].Route, crystals = crystals,
                    controlHolds = r.Control.ToArray(),
                };
            }
            asset.levels = levels;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            // Re-prove from the stored (float) data exactly as the game will load it.
            for (var i = 0; i < asset.Count; i++)
            {
                try { CampaignLevels.Build(rules, asset.Spec(i), out _); }
                catch (InvalidLevelException e) { problems.AppendLine(e.Message); }
            }
            var message = problems.Length == 0 ? "[Campaign] written, all levels proven" : "[Campaign] written with problems:\n" + problems;
            Debug.Log(message);
            return message;
        }
    }
}
