using System;
using System.Collections.Generic;

namespace Game.Integration.Game
{
    public enum TutorialStepKind
    {
        Tour,
        Explain,
        Act,
        AwaitStop,
        ExplainWhenDoorsOpen,
    }

    [Flags]
    public enum SpotlightTarget
    {
        None = 0,
        Slime = 1,
        Meter = 2,
        Blocker = 4,
        Preview = 8,
        Doors = 16,
    }

    /// <summary>One tutorial step as data: what to wait for, what to say and what to light up.</summary>
    public sealed class TutorialStep
    {
        public TutorialStepKind Kind { get; set; }

        public string Id { get; set; }

        public string TextKey { get; set; }

        public SpotlightTarget Targets { get; set; }

        public int Shots { get; set; }

        public string ChargingTextKey { get; set; }

        public SpotlightTarget ChargingTargets { get; set; }
    }

    /// <summary>The level-1 tutorial as data. A new step is a new entry here, not new runner code.</summary>
    public static class TutorialScript
    {
        private const string TourStep = "tour";
        private const string SlimeStep = "slime";
        private const string MeterStep = "meter";
        private const string CrystalStep = "crystal";
        private const string FirstShotStep = "shot1";
        private const string HopStep = "hop";
        private const string SecondShotStep = "shot2";
        private const string DoorsStep = "doors";

        private const string TourKey = "tutorial.tour";
        private const string SlimeKey = "tutorial.slime";
        private const string MeterKey = "tutorial.meter";
        private const string CrystalKey = "tutorial.crystal";
        private const string TapFirstKey = "tutorial.tap_first";
        private const string HopKey = "tutorial.hop";
        private const string TapAgainKey = "tutorial.tap_again";
        private const string DoorsKey = "tutorial.doors";
        private const string HoldKey = "tutorial.hold";

        private const int FirstShots = 1;
        private const int SecondShots = 2;
        private const SpotlightTarget AimTargets = SpotlightTarget.Slime | SpotlightTarget.Blocker;
        private const SpotlightTarget ChargeTargets = SpotlightTarget.Slime | SpotlightTarget.Preview | SpotlightTarget.Meter;

        public static readonly IReadOnlyList<TutorialStep> FirstLevel = new[]
        {
            Step(TutorialStepKind.Tour, TourStep, TourKey, SpotlightTarget.Doors),
            Step(TutorialStepKind.Explain, SlimeStep, SlimeKey, SpotlightTarget.Slime),
            Step(TutorialStepKind.Explain, MeterStep, MeterKey, SpotlightTarget.Meter),
            Step(TutorialStepKind.Explain, CrystalStep, CrystalKey, AimTargets),
            Shoot(FirstShotStep, TapFirstKey, FirstShots),
            new TutorialStep { Kind = TutorialStepKind.AwaitStop },
            Step(TutorialStepKind.Explain, HopStep, HopKey, AimTargets),
            Shoot(SecondShotStep, TapAgainKey, SecondShots),
            Step(TutorialStepKind.ExplainWhenDoorsOpen, DoorsStep, DoorsKey, SpotlightTarget.Doors),
        };

        private static TutorialStep Step(TutorialStepKind kind, string id, string textKey, SpotlightTarget targets)
        {
            return new TutorialStep { Kind = kind, Id = id, TextKey = textKey, Targets = targets };
        }

        private static TutorialStep Shoot(string id, string textKey, int shots)
        {
            var step = Step(TutorialStepKind.Act, id, textKey, AimTargets);
            step.Shots = shots;
            step.ChargingTextKey = HoldKey;
            step.ChargingTargets = ChargeTargets;

            return step;
        }
    }
}
