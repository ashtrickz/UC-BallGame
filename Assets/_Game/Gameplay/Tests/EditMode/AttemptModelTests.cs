using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Model;
using NUnit.Framework;

namespace Game.Gameplay.Tests
{
    public sealed class AttemptModelTests
    {
        private static AttemptModel Started(GameRules rules, params ConvexPolygon[] crystals)
        {
            var model = new AttemptModel(7, rules, TestRules.Level(rules, crystals));
            var move = model.Start();
            Assert.IsTrue(model.ConfirmMove(7, move.ActionId));
            return model;
        }

        private static ActionRequest Shoot(AttemptModel model, double hold)
        {
            Assert.IsTrue(model.BeginCharge());
            Assert.IsTrue(model.SetHold(hold));
            return model.ReleaseCharge();
        }

        private static void Resolve(AttemptModel model, ActionRequest shot)
        {
            Assert.IsTrue(model.ConfirmImpact(model.AttemptId, shot.ActionId));
            Assert.IsTrue(model.ConfirmClearing(model.AttemptId, model.Pending.ActionId));
            Assert.IsTrue(model.ConfirmMove(model.AttemptId, model.Pending.ActionId));
        }

        [Test]
        public void Start_moves_to_the_safe_stop_before_the_first_crystal()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
            Assert.AreEqual(5.7 - rules.CorridorHalfWidth(rules.D0), model.AnchorZ, 1e-9);
        }

        [Test]
        public void Short_tap_fires_the_minimum_shot_once()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            Assert.IsTrue(model.BeginCharge());
            Assert.AreEqual(rules.D0 - rules.Values.MinShotCost, model.Diameter, 1e-12);
            var shot = model.ReleaseCharge();
            Assert.AreEqual(rules.Values.MinShotCost, shot.Shot.Cost, 1e-12);
            Assert.AreEqual(rules.D0 - rules.Values.MinShotCost, model.Diameter, 1e-12, "release must not charge again");
            Resolve(model, shot);
            Assert.AreEqual(rules.D0 - rules.Values.MinShotCost, model.Diameter, 1e-12, "impact must not charge again");
        }

        [Test]
        public void Holding_shrinks_the_player_linearly_and_grows_the_shot()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            model.BeginCharge();
            model.TickCharge(0.5);
            var expectedCost = rules.Values.MinShotCost + rules.Values.ChargeRate * 0.5;
            Assert.AreEqual(rules.D0 - expectedCost, model.Diameter, 1e-12);
            var preview = model.PreviewShot();
            Assert.AreEqual(expectedCost / rules.Values.TransferCoefficient, preview.ProjectileDiameter, 1e-12);
            Assert.AreEqual(rules.Values.BlastCoefficient * preview.ProjectileDiameter, preview.BlastRadius, 1e-12);
        }

        [Test]
        public void Preview_and_release_produce_the_same_hit_set()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)), TestRules.Square(0.3, new Vec2(0.7, 6.5)),
                TestRules.Square(0.3, new Vec2(-0.9, 6.9)), TestRules.Square(0.3, new Vec2(0, 12)));
            model.BeginCharge();
            model.TickCharge(0.6);
            var preview = model.PreviewShot();
            var shot = model.ReleaseCharge().Shot;
            CollectionAssert.AreEqual(preview.HitIds, shot.HitIds);
            Assert.AreEqual(preview.ImpactCenter, shot.ImpactCenter);
            Assert.IsTrue(model.ConfirmImpact(7, model.Pending.ActionId));
            foreach (var id in shot.HitIds) Assert.IsFalse(model.IsAlive(id));
        }

        [Test]
        public void Shot_plan_separates_projectile_centre_and_contact_point()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            var shot = Shoot(model, 0.4).Shot;
            Assert.IsTrue(shot.Hit);
            Assert.AreEqual(5.7, shot.ImpactCenter.Z, 1e-9, "blast centre is on the contour");
            Assert.AreEqual(5.7 - shot.ProjectileDiameter / 2, shot.TouchCenter.Z, 1e-9, "flight ends at the projectile centre");
        }

        [Test]
        public void Overcharge_to_exactly_critical_size_is_a_defeat_without_refund()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            model.BeginCharge();
            var holdToCritical = (rules.D0 - rules.Dcrit - rules.Values.MinShotCost) / rules.Values.ChargeRate;
            Assert.IsTrue(model.SetHold(holdToCritical - 0.01), "just above Dcrit keeps charging");
            Assert.AreEqual(AttemptPhase.Charging, model.Phase);
            Assert.IsFalse(model.SetHold(holdToCritical));
            Assert.AreEqual(AttemptPhase.DefeatAnimation, model.Phase);
            Assert.AreEqual(DefeatReason.Overcharge, model.Defeat);
            Assert.AreEqual(rules.Dcrit, model.Diameter, 1e-12);
            model.SetPaused(true);
            Assert.AreEqual(rules.Dcrit, model.Diameter, 1e-12, "pause after a fixed defeat must not refund");
            Assert.AreEqual(AttemptPhase.DefeatAnimation, model.Phase);
            Assert.IsTrue(model.ConfirmDefeat(7, model.Pending.ActionId));
            Assert.AreEqual(AttemptPhase.Lost, model.Phase);
        }

        [Test]
        public void Pause_cancels_the_charge_and_restores_the_exact_size()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)), TestRules.Square(0.3, new Vec2(0, 15)));
            Resolve(model, Shoot(model, 0.1));
            Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
            var before = model.Diameter;
            model.BeginCharge();
            model.TickCharge(0.37);
            model.SetPaused(true);
            Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
            Assert.AreEqual(before, model.Diameter);
            Assert.IsFalse(model.BeginCharge(), "no charge while paused");
            model.SetPaused(true);
            Assert.AreEqual(before, model.Diameter, "a repeated pause does not refund twice");
            model.SetPaused(false);
            Assert.IsTrue(model.BeginCharge());
        }

        [Test]
        public void Pause_keeps_a_released_shot_spent()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            var shot = Shoot(model, 0.2);
            var after = model.Diameter;
            model.SetPaused(true);
            Assert.AreEqual(after, model.Diameter);
            Assert.AreEqual(AttemptPhase.Shooting, model.Phase);
            model.SetPaused(false);
            Assert.IsTrue(model.ConfirmImpact(7, shot.ActionId));
        }

        [Test]
        public void Stale_and_repeated_confirmations_are_rejected()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            var shot = Shoot(model, 0);
            Assert.IsFalse(model.ConfirmImpact(8, shot.ActionId), "other attempt");
            Assert.IsFalse(model.ConfirmImpact(7, shot.ActionId + 5), "unknown action");
            Assert.IsFalse(model.ConfirmClearing(7, shot.ActionId), "wrong kind");
            Assert.IsTrue(model.ConfirmImpact(7, shot.ActionId));
            Assert.IsFalse(model.ConfirmImpact(7, shot.ActionId), "applied once");
            var clearing = model.Pending.ActionId;
            Assert.IsTrue(model.ConfirmClearing(7, clearing));
            Assert.IsFalse(model.ConfirmClearing(7, clearing));
        }

        [Test]
        public void Miss_spends_the_resource_and_hits_nothing()
        {
            var rules = TestRules.Create();
            // The blocker sits to the side: the corridor touches it, the tiny projectile flies past.
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0.95, 6)));
            Assert.AreEqual(AttemptPhase.Waiting, model.Phase);
            var shot = Shoot(model, 0).Shot;
            Assert.IsFalse(shot.Hit);
            Assert.AreEqual(0, shot.HitIds.Count);
            Assert.AreEqual(rules.FinishZ, shot.TouchCenter.Z, 1e-9);
            Assert.AreEqual(rules.D0 - rules.Values.MinShotCost, model.Diameter, 1e-12);
        }

        [Test]
        public void Shrinking_can_open_a_side_passage()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0.95, 6)));
            var stop = model.AnchorZ;
            // A big miss shrinks the body so the corridor no longer touches the side crystal.
            // Leaves D = 1.07: corridor half-width 0.615 < 0.65 (crystal edge), projectile radius 0.53 still misses.
            var shot = Shoot(model, (rules.D0 - 1.07 - rules.Values.MinShotCost) / rules.Values.ChargeRate);
            Assert.IsFalse(shot.Shot.Hit);
            Resolve(model, shot);
            Assert.Greater(model.AnchorZ, stop);
            Assert.AreEqual(AttemptPhase.Won, model.Phase);
        }

        [Test]
        public void Free_path_is_walkable_with_a_tiny_reserve()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)));
            // Spend almost everything on one shot that clears the only crystal.
            var hold = (rules.D0 - rules.Dcrit - 0.03 - rules.Values.MinShotCost) / rules.Values.ChargeRate;
            var shot = Shoot(model, hold);
            Assert.IsFalse(rules.CanAffordMinShot(model.Diameter));
            Resolve(model, shot);
            Assert.AreEqual(AttemptPhase.Won, model.Phase);
        }

        [Test]
        public void Blocked_path_without_resource_is_a_defeat_after_the_move()
        {
            var rules = TestRules.Create();
            var model = Started(rules, TestRules.Square(0.3, new Vec2(0, 6)), TestRules.Square(0.3, new Vec2(0, 15)));
            var hold = (rules.D0 - rules.Dcrit - 0.03 - rules.Values.MinShotCost) / rules.Values.ChargeRate;
            var shot = Shoot(model, hold);
            // Shot only touches the first crystal; the second still blocks.
            Assert.That(shot.Shot.HitIds, Has.No.Member(1));
            Assert.IsTrue(model.ConfirmImpact(7, shot.ActionId));
            Assert.IsTrue(model.ConfirmClearing(7, model.Pending.ActionId));
            Assert.IsTrue(model.ConfirmMove(7, model.Pending.ActionId));
            Assert.AreEqual(AttemptPhase.DefeatAnimation, model.Phase);
            Assert.AreEqual(DefeatReason.OutOfResource, model.Defeat);
            Assert.AreEqual(1, model.Pending.DefeatCrystalId);
            Assert.IsTrue(model.ConfirmDefeat(7, model.Pending.ActionId));
            Assert.AreEqual(AttemptPhase.Lost, model.Phase);
        }

        [Test]
        public void Doors_open_within_five_metres_of_the_finish_threshold()
        {
            var rules = TestRules.Create();
            var model = new AttemptModel(1, rules, TestRules.Level(rules));
            var opened = 0;
            model.DoorsOpened += () => opened++;
            var move = model.Start();
            Assert.IsTrue(move.Advance.ReachesFinish);
            model.ReportProgress(1, move.ActionId, rules.FinishZ - 5.2);
            Assert.IsFalse(model.DoorsOpen);
            model.ReportProgress(1, move.ActionId, rules.FinishZ - 5.0);
            Assert.IsTrue(model.DoorsOpen);
            model.ReportProgress(1, move.ActionId, rules.FinishZ - 1);
            Assert.AreEqual(1, opened);
            Assert.IsTrue(model.ConfirmMove(1, move.ActionId));
            Assert.AreEqual(AttemptPhase.Won, model.Phase);
        }

        [Test]
        public void Snapshot_is_isolated_from_later_config_edits()
        {
            var values = RuleValues.Defaults;
            var rules = new GameRules(values, TestRules.Variants());
            values.InitialDiameter = 5;
            Assert.AreEqual(RuleValues.Defaults.InitialDiameter, rules.D0);
        }
    }
}
