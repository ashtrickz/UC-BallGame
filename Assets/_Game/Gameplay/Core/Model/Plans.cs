using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;

namespace Game.Gameplay.Model
{
    /// <summary>Immutable outcome of a shot. Fixed at release; the impact applies exactly this.</summary>
    public sealed class ShotPlan
    {
        public double HoldSeconds { get; }
        public double Cost { get; }
        public double DiameterBefore { get; }
        public double DiameterAfter { get; }
        public double ProjectileDiameter { get; }
        public double BlastRadius { get; }
        /// <summary>Logical start of the projectile sweep (the body anchor).</summary>
        public Vec2 Origin { get; }
        public bool Hit { get; }
        /// <summary>Projectile centre at first touch (or at the end of its range on a miss): where the flight ends.</summary>
        public Vec2 TouchCenter { get; }
        /// <summary>Contact point on the first crystal contour: the centre of the blast circle.</summary>
        public Vec2 ImpactCenter { get; }
        public int FirstCrystalId { get; }
        /// <summary>Every crystal destroyed by the blast, ascending id order.</summary>
        public IReadOnlyList<int> HitIds { get; }
        public bool IsOvercharged { get; }

        public ShotPlan(double holdSeconds, double cost, double diameterBefore, double diameterAfter, double projectileDiameter,
            double blastRadius, Vec2 origin, bool hit, Vec2 touchCenter, Vec2 impactCenter, int firstCrystalId,
            IReadOnlyList<int> hitIds, bool isOvercharged)
        {
            HoldSeconds = holdSeconds; Cost = cost; DiameterBefore = diameterBefore; DiameterAfter = diameterAfter;
            ProjectileDiameter = projectileDiameter; BlastRadius = blastRadius; Origin = origin; Hit = hit;
            TouchCenter = touchCenter; ImpactCenter = impactCenter; FirstCrystalId = firstCrystalId; HitIds = hitIds;
            IsOvercharged = isOvercharged;
        }
    }

    /// <summary>Automatic movement along the centre line to the next safe point.</summary>
    public sealed class AdvancePlan
    {
        public double FromZ { get; }
        public double ToZ { get; }
        public double Diameter { get; }
        public bool ReachesFinish { get; }
        /// <summary>Crystal that stops the corridor, or −1 when the way to the finish is free.</summary>
        public int BlockingCrystalId { get; }
        public Vec2 BlockingContact { get; }
        public double Distance => ToZ - FromZ;

        public AdvancePlan(double fromZ, double toZ, double diameter, bool reachesFinish, int blockingCrystalId, Vec2 blockingContact)
        {
            FromZ = fromZ; ToZ = toZ; Diameter = diameter; ReachesFinish = reachesFinish;
            BlockingCrystalId = blockingCrystalId; BlockingContact = blockingContact;
        }
    }

    /// <summary>The shared rules used by the preview, the release, the movement and the generator's validator.</summary>
    public static class ShotRules
    {
        public static ShotPlan ComputeShot(GameRules rules, CrystalField field, double anchorZ, double diameterBefore, double holdSeconds)
        {
            var cost = rules.ChargeCost(holdSeconds);
            var after = diameterBefore - cost;
            var overcharged = rules.IsCritical(after);
            var projectile = rules.ProjectileDiameter(cost);
            var radius = rules.BlastRadius(cost);
            // The shot is swept from the body anchor: the drop is pinched out of the body, so its logical path starts
            // inside the cleared corridor and the first contact always lies on a crystal contour.
            var origin = new Vec2(0, anchorZ);
            var range = Math.Max(0, rules.FinishZ - origin.Z);
            var hit = field.SweepCircle(origin, Vec2.Forward, projectile * 0.5, range, rules.Tolerance);
            if (!hit.Hit)
            {
                var end = origin + Vec2.Forward * range;
                return new ShotPlan(holdSeconds, cost, diameterBefore, after, projectile, radius, origin, false, end, end, -1,
                    Array.Empty<int>(), overcharged);
            }
            var ids = field.Overlapping(hit.Contact, radius, rules.Tolerance);
            return new ShotPlan(holdSeconds, cost, diameterBefore, after, projectile, radius, origin, true, hit.Center,
                hit.Contact, hit.CrystalId, ids.ToArray(), overcharged);
        }

        public static AdvancePlan ComputeAdvance(GameRules rules, CrystalField field, double fromZ, double diameter)
        {
            var range = rules.FinishZ - fromZ;
            if (range <= rules.Tolerance) return new AdvancePlan(fromZ, Math.Max(fromZ, rules.FinishZ), diameter, true, -1, default);
            var hit = field.SweepCircle(new Vec2(0, fromZ), Vec2.Forward, rules.CorridorHalfWidth(diameter), range, rules.Tolerance);
            if (!hit.Hit) return new AdvancePlan(fromZ, rules.FinishZ, diameter, true, -1, default);
            var distance = hit.Distance < rules.Values.MinAdvance ? 0 : hit.Distance;
            return new AdvancePlan(fromZ, fromZ + distance, diameter, false, hit.CrystalId, hit.Contact);
        }
    }
}
