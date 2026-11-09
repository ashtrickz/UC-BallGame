using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Level;
using Game.Gameplay.Model;

namespace Game.Gameplay.Generation
{
    public readonly struct ReplayResult
    {
        public readonly bool Success;
        public readonly string Failure;
        public readonly double Spent;
        public readonly double FinalDiameter;
        public readonly int Shots;
        public readonly bool Won;

        public ReplayResult(bool success, string failure, double spent, double finalDiameter, int shots, bool won)
        {
            Success = success;
            Failure = failure;
            Spent = spent;
            FinalDiameter = finalDiameter;
            Shots = shots;
            Won = won;
        }
    }

    /// <summary>
    /// Plays a control plan through the real <see cref="AttemptModel"/> on the final layout, confirming every
    /// critical action at once instead of waiting for animations.
    /// </summary>
    public static class ControlReplay
    {
        public static ReplayResult Run(GameRules rules, LevelData level, IReadOnlyList<double> holds)
        {
            var model = new AttemptModel(0, rules, level);
            var request = model.Start();
            if (!Finish(model, request))
                return Fail(model, "initial advance was not accepted");

            for (var i = 0; i < holds.Count; i++)
            {
                if (model.Phase == AttemptPhase.Won)
                    return Fail(model, $"finish reached before shot {i}");

                if (model.Phase != AttemptPhase.Waiting)
                    return Fail(model, $"shot {i}: expected Waiting, got {model.Phase}");

                if (!model.BeginCharge())
                    return Fail(model, $"shot {i}: charge refused");

                if (!model.SetHold(holds[i]))
                    return Fail(model, $"shot {i}: hold {holds[i]:0.###}s reached the critical size");

                var shot = model.ReleaseCharge();
                if (shot == null || !model.ConfirmImpact(model.AttemptId, shot.ActionId))
                    return Fail(model, $"shot {i}: impact rejected");

                if (!model.ConfirmClearing(model.AttemptId, model.Pending.ActionId))
                    return Fail(model, $"shot {i}: clearing rejected");

                if (!Finish(model, model.Pending))
                    return Fail(model, $"shot {i}: advance rejected");
            }
            if (model.Phase != AttemptPhase.Won)
                return Fail(model, $"plan ended in {model.Phase} at z={model.AnchorZ:0.###}");

            var spent = model.TotalSpent;
            if (spent > rules.ControlBudgetLimit + rules.Tolerance)
                return Fail(model, $"control spend {spent:0.####} exceeds B/1.2 = {rules.ControlBudgetLimit:0.####}");

            if (rules.IsCritical(model.Diameter))
                return Fail(model, "control plan reached the critical size");

            return new ReplayResult(true, null, spent, model.Diameter, model.ShotsFired, true);
        }

        private static bool Finish(AttemptModel model, ActionRequest request)
        {
            if (request == null || request.Kind != ActionKind.Advance)
                return false;

            model.ReportProgress(model.AttemptId, request.ActionId, request.Advance.ToZ);
            return model.ConfirmMove(model.AttemptId, request.ActionId);
        }

        private static ReplayResult Fail(AttemptModel model, string why) =>
            new ReplayResult(false, why, model.TotalSpent, model.Diameter, model.ShotsFired, model.Phase == AttemptPhase.Won);
    }
}
