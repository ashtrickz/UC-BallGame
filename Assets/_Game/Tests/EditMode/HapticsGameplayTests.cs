using Slime.Foundation;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Slime.Audio;
using Slime.Gameplay.View;
using Slime.Gameplay.Presentation;
using Slime.Infrastructure.Audio;

namespace Slime.Tests.EditMode
{
    public sealed class HapticsGameplayTests
    {
        [Test]
        public void Charge_envelope_is_continuous_increasing_and_finite()
        {
            var pattern = ChargeHapticPattern.Build(0.05f, 0.21f, 4.5f, true);
            Assert.AreEqual(pattern.Timings.Length, pattern.Amplitudes.Length);
            Assert.AreEqual(4500, pattern.Timings.Sum());
            Assert.IsTrue(pattern.Timings.All(t => t > 0));
            Assert.IsTrue(pattern.Amplitudes.All(a => a >= 26 && a <= 184));
            for (var i = 1; i < pattern.Amplitudes.Length; i++)
                Assert.GreaterOrEqual(pattern.Amplitudes[i], pattern.Amplitudes[i - 1]);
            Assert.Greater(pattern.Amplitudes.Last(), pattern.Amplitudes.First() * 4);
        }

        [Test]
        public void On_off_motors_use_default_amplitude_with_increasing_duty_cycle()
        {
            var pattern = ChargeHapticPattern.Build(0, 0.25f, 4, false);
            Assert.AreEqual(4000, pattern.Timings.Sum());
            for (var i = 0; i < pattern.Timings.Length; i += 2)
            {
                Assert.AreEqual(0, pattern.Amplitudes[i]);
                Assert.AreEqual(-1, pattern.Amplitudes[i + 1]);
                Assert.Greater(pattern.Timings[i + 1], 0);
            }
            Assert.Greater(pattern.Timings.Last(), pattern.Timings[1]);
        }

        [Test]
        public void Invalid_envelopes_are_empty_and_duration_is_bounded()
        {
            Assert.IsEmpty(ChargeHapticPattern.Build(0, 1, float.NaN, true).Timings);
            Assert.IsEmpty(ChargeHapticPattern.Build(0, 1, -1, true).Timings);
            Assert.IsEmpty(ChargeHapticPattern.Build(float.PositiveInfinity, 1, 1, true).Timings);
            Assert.IsEmpty(ChargeHapticPattern.Build(0, -1, 1, true).Timings);
            Assert.AreEqual(10000, ChargeHapticPattern.Build(0, 0.1f, 100, true).Timings.Sum());
        }

        [Test]
        public void Every_landing_and_water_entry_pulses_but_takeoff_does_not()
        {
            var pulses = new List<int>();
            var presenter = new GameplayHapticsPresenter(new CallbackHaptics((duration, intensity) => pulses.Add(duration),
                (initial, rate, seconds) => { }, () => { }));
            presenter.OnSlimeCue(SlimePresentationCue.HopPreparing);
            presenter.OnSlimeCue(SlimePresentationCue.Takeoff);
            presenter.OnSlimeCue(SlimePresentationCue.Landed);
            presenter.OnSlimeCue(SlimePresentationCue.Landed);
            presenter.OnSlimeCue(SlimePresentationCue.PoolSplash);
            CollectionAssert.AreEqual(new[] { 22, 22, 28 }, pulses);
            presenter.SetPaused(true);
            presenter.OnSlimeCue(SlimePresentationCue.Landed);
            presenter.SetPaused(false);
            presenter.OnSlimeCue(SlimePresentationCue.Landed);
            presenter.Dispose();
            presenter.OnSlimeCue(SlimePresentationCue.Landed);
            Assert.AreEqual(4, pulses.Count);
        }

        [TestCase("release")]
        [TestCase("pause")]
        [TestCase("dispose")]
        public void Charge_stops_without_automatically_restarting(string action)
        {
            var starts = 0;
            var stops = 0;
            var presenter = new GameplayHapticsPresenter(new CallbackHaptics((duration, intensity) => { },
                (initial, rate, seconds) => starts++, () => stops++));
            presenter.BeginCharge(0.05f, 0.2f, 4.5f);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(0, stops);
            if (action == "release")
                presenter.CancelCharge();
            else if (action == "pause")
                presenter.SetPaused(true);
            else
                presenter.Dispose();
            Assert.AreEqual(1, stops);
            presenter.SetPaused(false);
            presenter.CancelCharge();
            Assert.AreEqual(1, starts);
            Assert.AreEqual(1, stops);
        }

        [Test]
        public void Replacement_charge_cancels_the_previous_envelope()
        {
            var commands = new List<string>();
            var presenter = new GameplayHapticsPresenter(new CallbackHaptics((duration, intensity) => { },
                (initial, rate, seconds) => commands.Add("start"), () => commands.Add("stop")));
            presenter.BeginCharge(0.05f, 0.2f, 4);
            presenter.BeginCharge(0.05f, 0.2f, 2);
            CollectionAssert.AreEqual(new[] { "start", "stop", "start" }, commands);
        }
        /// <summary>Adapts test callbacks to the project feedback contract.</summary>
        private sealed class CallbackHaptics : IHaptics
        {
            private readonly Action<int, float> _pulse;
            private readonly Action<float, float, float> _charge;
            private readonly Action _cancel;
            public bool Enabled { get; set; } = true;

            public CallbackHaptics(Action<int, float> pulse, Action<float, float, float> charge, Action cancel)
            {
                _pulse = pulse;
                _charge = charge;
                _cancel = cancel;
            }

            public void Play(HapticKind kind)
            {
            }

            public void Vibrate(int milliseconds, float intensity) => _pulse(milliseconds, intensity);
            public void StartCharge(float initialCharge, float chargePerSecond, float seconds) => _charge(initialCharge, chargePerSecond, seconds);
            public void Cancel() => _cancel();

            public void Dispose()
            {
            }
        }
    }
}
