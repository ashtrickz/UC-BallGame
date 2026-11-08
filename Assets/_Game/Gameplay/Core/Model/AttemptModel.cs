using System;
using System.Collections.Generic;
using Game.Gameplay.Config;
using Game.Gameplay.Geometry;
using Game.Gameplay.Level;

namespace Game.Gameplay.Model
{
    public enum AttemptPhase { Preparing, Moving, Waiting, Charging, Shooting, Clearing, DefeatAnimation, Won, Lost }
    public enum DefeatReason { None, Overcharge, OutOfResource }
    public enum ActionKind { None, Advance, Shot, Clearing, Defeat }

    /// <summary>A command for the view. Its completion is confirmed back with the same attempt and action ids.</summary>
    public sealed class ActionRequest
    {
        public int AttemptId { get; }
        public int ActionId { get; }
        public ActionKind Kind { get; }
        public ShotPlan Shot { get; }
        public AdvancePlan Advance { get; }
        /// <summary>For <see cref="ActionKind.Defeat"/>: the blocking crystal to hop into, or −1 to pop in place.</summary>
        public int DefeatCrystalId { get; }
        public Vec2 DefeatTarget { get; }

        internal ActionRequest(int attemptId, int actionId, ActionKind kind, ShotPlan shot = null, AdvancePlan advance = null,
            int defeatCrystalId = -1, Vec2 defeatTarget = default)
        {
            AttemptId = attemptId; ActionId = actionId; Kind = kind; Shot = shot; Advance = advance;
            DefeatCrystalId = defeatCrystalId; DefeatTarget = defeatTarget;
        }
    }

    /// <summary>
    /// The rules and phases of one attempt. Plain C#: no scene objects and no timers. Every critical phase ends
    /// only when the presenter confirms the matching action; confirmations are accepted once.
    /// </summary>
    public sealed class AttemptModel
    {
        private readonly CrystalField _field;
        private int _nextActionId;
        private double _chargeStartDiameter;

        public int AttemptId { get; }
        public GameRules Rules { get; }
        public LevelData Level { get; }
        public AttemptPhase Phase { get; private set; } = AttemptPhase.Preparing;
        public DefeatReason Defeat { get; private set; }
        public double Diameter { get; private set; }
        /// <summary>Confirmed anchor position along the route (Z).</summary>
        public double AnchorZ { get; private set; }
        /// <summary>Latest confirmed position, including progress reported during movement.</summary>
        public double ProgressZ { get; private set; }
        public double HoldSeconds { get; private set; }
        /// <summary>Diameter when the current hold began (equals <see cref="Diameter"/> outside a charge).</summary>
        public double ChargeStartDiameter => Phase == AttemptPhase.Charging ? _chargeStartDiameter : Diameter;
        public bool IsPaused { get; private set; }
        public bool DoorsOpen { get; private set; }
        public ActionRequest Pending { get; private set; }
        public int ShotsFired { get; private set; }
        public double TotalSpent => Rules.D0 - Diameter;
        public bool IsFinished => Phase == AttemptPhase.Won || Phase == AttemptPhase.Lost;
        public bool CanBeginCharge => Phase == AttemptPhase.Waiting && !IsPaused;
        public double ChargeCost => Phase == AttemptPhase.Charging ? Rules.ChargeCost(HoldSeconds) : 0;

        public event Action DoorsOpened;
        public event Action<int> CrystalsKilled;

        public AttemptModel(int attemptId, GameRules rules, LevelData level)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Level = level ?? throw new ArgumentNullException(nameof(level));
            if (level.RulesFingerprint != rules.Fingerprint)
                throw new ArgumentException("Level was generated with different rules.", nameof(level));
            AttemptId = attemptId;
            _field = level.CreateField();
            Diameter = rules.D0;
            AnchorZ = ProgressZ = level.StartZ;
        }

        public bool IsAlive(int crystalId) => _field.IsAlive(crystalId);
        public int AliveCount => _field.AliveCount;

        /// <summary>Leaves preparation with the first automatic advance.</summary>
        public ActionRequest Start()
        {
            if (Phase != AttemptPhase.Preparing) throw new InvalidOperationException("Attempt already started.");
            return BeginAdvance();
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            if (paused && Phase == AttemptPhase.Charging) CancelCharge();
        }

        public bool BeginCharge()
        {
            if (!CanBeginCharge || !Rules.CanAffordMinShot(Diameter)) return false;
            _chargeStartDiameter = Diameter;
            HoldSeconds = 0;
            Phase = AttemptPhase.Charging;
            ApplyCharge();
            return true;
        }

        /// <summary>Grows the charge. Returns false once the charge has caused a defeat.</summary>
        public bool TickCharge(double deltaSeconds)
        {
            if (Phase != AttemptPhase.Charging) return false;
            if (IsPaused || !(deltaSeconds > 0)) return true;
            HoldSeconds += deltaSeconds;
            return ApplyCharge();
        }

        /// <summary>Validator entry: sets the exact hold duration of the current charge.</summary>
        public bool SetHold(double holdSeconds)
        {
            if (Phase != AttemptPhase.Charging) return false;
            HoldSeconds = Math.Max(0, holdSeconds);
            return ApplyCharge();
        }

        private bool ApplyCharge()
        {
            Diameter = _chargeStartDiameter - Rules.ChargeCost(HoldSeconds);
            if (!Rules.IsCritical(Diameter)) return true;
            // Reaching Dcrit while holding is a defeat at once; nothing is refunded.
            Diameter = Rules.Dcrit;
            BeginDefeat(DefeatReason.Overcharge);
            return false;
        }

        /// <summary>Restores exactly the size invested since this hold began.</summary>
        public bool CancelCharge()
        {
            if (Phase != AttemptPhase.Charging) return false;
            Diameter = _chargeStartDiameter;
            HoldSeconds = 0;
            Phase = AttemptPhase.Waiting;
            return true;
        }

        public ShotPlan PreviewShot() =>
            Phase == AttemptPhase.Charging ? ShotRules.ComputeShot(Rules, _field, AnchorZ, _chargeStartDiameter, HoldSeconds) : null;

        /// <summary>Fixes the shot outcome. The resource is spent from here on.</summary>
        public ActionRequest ReleaseCharge()
        {
            if (Phase != AttemptPhase.Charging || IsPaused) return null;
            var plan = ShotRules.ComputeShot(Rules, _field, AnchorZ, _chargeStartDiameter, HoldSeconds);
            Diameter = plan.DiameterAfter;
            ShotsFired++;
            Phase = AttemptPhase.Shooting;
            return Issue(ActionKind.Shot, shot: plan);
        }

        public bool ConfirmImpact(int attemptId, int actionId)
        {
            if (!Accept(attemptId, actionId, ActionKind.Shot, AttemptPhase.Shooting)) return false;
            var plan = Pending.Shot;
            foreach (var id in plan.HitIds) _field.Kill(id);
            if (plan.HitIds.Count > 0) CrystalsKilled?.Invoke(plan.HitIds.Count);
            Phase = AttemptPhase.Clearing;
            Pending = Issue(ActionKind.Clearing, shot: plan);
            return true;
        }

        public bool ConfirmClearing(int attemptId, int actionId)
        {
            if (!Accept(attemptId, actionId, ActionKind.Clearing, AttemptPhase.Clearing)) return false;
            Pending = BeginAdvance();
            return true;
        }

        /// <summary>Progress along the current advance, measured on the route (body height is ignored).</summary>
        public bool ReportProgress(int attemptId, int actionId, double z)
        {
            if (attemptId != AttemptId || Pending == null || Pending.ActionId != actionId || Pending.Kind != ActionKind.Advance) return false;
            var plan = Pending.Advance;
            ProgressZ = Math.Max(ProgressZ, Math.Min(Math.Max(z, plan.FromZ), plan.ToZ));
            UpdateDoors();
            return true;
        }

        public bool ConfirmMove(int attemptId, int actionId)
        {
            if (!Accept(attemptId, actionId, ActionKind.Advance, AttemptPhase.Moving)) return false;
            var plan = Pending.Advance;
            AnchorZ = ProgressZ = plan.ToZ;
            UpdateDoors();
            if (plan.ReachesFinish)
            {
                Phase = AttemptPhase.Won;
                Pending = null;
            }
            else if (!Rules.CanAffordMinShot(Diameter)) BeginDefeat(DefeatReason.OutOfResource, plan);
            else
            {
                Phase = AttemptPhase.Waiting;
                Pending = null;
            }
            return true;
        }

        public bool ConfirmDefeat(int attemptId, int actionId)
        {
            if (!Accept(attemptId, actionId, ActionKind.Defeat, AttemptPhase.DefeatAnimation)) return false;
            Phase = AttemptPhase.Lost;
            Pending = null;
            return true;
        }

        /// <summary>Current corridor check from the confirmed position; used by previews and the defeat hop.</summary>
        public AdvancePlan PeekAdvance() => ShotRules.ComputeAdvance(Rules, _field, AnchorZ, Diameter);

        private ActionRequest BeginAdvance()
        {
            var plan = ShotRules.ComputeAdvance(Rules, _field, AnchorZ, Diameter);
            Phase = AttemptPhase.Moving;
            Pending = Issue(ActionKind.Advance, advance: plan);
            return Pending;
        }

        private void BeginDefeat(DefeatReason reason, AdvancePlan blocking = null)
        {
            blocking = blocking ?? ShotRules.ComputeAdvance(Rules, _field, AnchorZ, Diameter);
            Defeat = reason;
            Phase = AttemptPhase.DefeatAnimation;
            HoldSeconds = 0;
            Pending = Issue(ActionKind.Defeat, defeatCrystalId: blocking.BlockingCrystalId, defeatTarget: blocking.BlockingContact);
        }

        private ActionRequest Issue(ActionKind kind, ShotPlan shot = null, AdvancePlan advance = null, int defeatCrystalId = -1,
            Vec2 defeatTarget = default)
        {
            Pending = new ActionRequest(AttemptId, ++_nextActionId, kind, shot, advance, defeatCrystalId, defeatTarget);
            return Pending;
        }

        private bool Accept(int attemptId, int actionId, ActionKind kind, AttemptPhase phase) =>
            attemptId == AttemptId && Pending != null && Pending.ActionId == actionId && Pending.Kind == kind && Phase == phase;

        private void UpdateDoors()
        {
            if (DoorsOpen || Level.FinishZ - ProgressZ > Rules.Values.DoorOpenDistance + Rules.Tolerance) return;
            DoorsOpen = true;
            DoorsOpened?.Invoke();
        }

        /// <summary>Ids of live crystals, ascending; used by tests and diagnostics.</summary>
        public List<int> AliveIds()
        {
            var ids = new List<int>();
            for (var i = 0; i < _field.Count; i++) if (_field.IsAlive(i)) ids.Add(i);
            return ids;
        }
    }
}
