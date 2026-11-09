using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Gameplay.Model;
using Game.Integration.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Gameplay.PlayModeTests
{
    public sealed partial class GameplayLifecycleTests
    {
        private static MeshRenderer FeedbackCrystal(GameplayHandle gameplay, int id) =>
            gameplay.Stage.Crystals.transform.Cast<Transform>()
                .First(t => t.name.StartsWith("Crystal " + id + " (", StringComparison.Ordinal))
                .GetComponentInChildren<MeshRenderer>();

        [UnityTest]
        public IEnumerator Shot_feedback_keeps_accepted_targets_until_the_projectile_hits() => UniTask.ToCoroutine(async () =>
        {
            GameplayHandle gameplay = null;
            try
            {
                gameplay = await StartGameAsync();
                gameplay.PlayWall(Seed);
                var model = gameplay.Current.Model;
                await WaitPhase(model, AttemptPhase.Waiting);
                var input = gameplay.Stage.Input;
                input.SetVirtualPointer(false);
                await UniTask.Yield();
                input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging);
                await UniTask.WaitUntil(() => model.HoldSeconds >= 0.6).Timeout(TimeSpan.FromSeconds(5));
                var plan = model.PreviewShot();
                Assert.Greater(plan.HitIds.Count, 0);
                var selected = FeedbackCrystal(gameplay, plan.HitIds[0]);
                var otherId = gameplay.Current.Level.Crystals.First(c => !plan.HitIds.Contains(c.Id)).Id;
                var other = FeedbackCrystal(gameplay, otherId);
                var properties = new MaterialPropertyBlock();
                other.GetPropertyBlock(properties);
                Assert.Less(properties.GetFloat("_Brightness"), other.sharedMaterial.GetFloat("_Brightness"),
                    "Surrounding crystals become quieter while choosing the shot.");
                input.SetVirtualPointer(false);
                await WaitPhase(model, AttemptPhase.Shooting);
                selected.GetPropertyBlock(properties);
                Assert.Greater(properties.GetFloat("_EdgeStrength"), 0.8f,
                    "The committed set remains visible after release.");
                Assert.IsFalse(gameplay.Stage.Preview.IsImpactPlaying, "The wave must not precede contact.");
                await WaitPhase(model, AttemptPhase.Clearing);
                Assert.IsTrue(gameplay.Stage.Preview.IsImpactPlaying, "Actual contact starts the ground wave.");
                Assert.AreEqual(1, model.ShotsFired);
            }
            finally
            {
                await TearDownAsync();
            }
        });

        [UnityTest]
        public IEnumerator Shot_feedback_cancel_restores_material_properties_without_a_shot() => UniTask.ToCoroutine(async () =>
        {
            GameplayHandle gameplay = null;
            try
            {
                gameplay = await StartGameAsync();
                gameplay.PlayWall(Seed);
                var model = gameplay.Current.Model;
                await WaitPhase(model, AttemptPhase.Waiting);
                gameplay.Stage.Input.SetVirtualPointer(false);
                await UniTask.Yield();
                gameplay.Stage.Input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging);
                await UniTask.WaitUntil(() => model.HoldSeconds >= 0.3).Timeout(TimeSpan.FromSeconds(5));
                gameplay.SetPaused(PauseReason.Settings, true);
                var properties = new MaterialPropertyBlock();
                foreach (var renderer in gameplay.Stage.Crystals.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.GetPropertyBlock(properties);
                    Assert.IsTrue(properties.isEmpty, renderer.transform.parent.name + " must return to its authored material.");
                }
                Assert.IsFalse(gameplay.Stage.Preview.IsImpactPlaying);
                Assert.AreEqual(0, model.ShotsFired);
                Assert.AreEqual(model.Rules.D0, model.Diameter, 1e-9);
            }
            finally
            {
                await TearDownAsync();
            }
        });

        [UnityTest]
        public IEnumerator Shot_feedback_waves_scale_pause_reuse_and_reset_with_the_attempt() => UniTask.ToCoroutine(async () =>
        {
            GameplayHandle gameplay = null;
            try
            {
                gameplay = await StartGameAsync();
                var preview = gameplay.Stage.Preview;
                var waves = preview.transform.Cast<Transform>().Where(t => t.name.StartsWith("Impact wave "))
                    .OrderBy(t => t.name).ToArray();
                Assert.AreEqual(2, waves.Length);
                var instanceIds = waves.Select(t => t.GetInstanceID()).ToArray();
                preview.PlayImpact(Vector3.zero, 0.22f, gameplay.Stage.ViewConfig.infectColor);
                Assert.AreEqual(1, waves.Count(t => t.gameObject.activeSelf), "A minimum hit uses one compact wave.");
                preview.PlayImpact(Vector3.zero, 2.2f, gameplay.Stage.ViewConfig.infectColor);
                await UniTask.Delay(TimeSpan.FromSeconds(0.08));
                Assert.AreEqual(2, waves.Count(t => t.gameObject.activeSelf), "A strong hit gets a second wave.");
                gameplay.SetPaused(PauseReason.Settings, true);
                var scale = waves[0].localScale;
                await UniTask.Delay(TimeSpan.FromSeconds(0.15), DelayType.UnscaledDeltaTime);
                Assert.AreEqual(scale, waves[0].localScale, "Effects share the gameplay pause clock.");
                gameplay.SetPaused(PauseReason.Settings, false);
                await UniTask.WaitUntil(() => !preview.IsImpactPlaying).Timeout(TimeSpan.FromSeconds(3));
                for (var i = 0; i < 10; i++) preview.PlayImpact(Vector3.zero, 2.2f, gameplay.Stage.ViewConfig.infectColor);
                CollectionAssert.AreEqual(instanceIds, waves.Select(t => t.GetInstanceID()).ToArray());
                Assert.AreEqual(2, preview.transform.Cast<Transform>().Count(t => t.name.StartsWith("Impact wave ")),
                    "Repeated impacts must not allocate more effect objects.");
                gameplay.Restart();
                Assert.IsFalse(preview.IsImpactPlaying);
                Assert.IsTrue(waves.All(t => !t.gameObject.activeSelf), "Restart removes the previous attempt's wave.");
            }
            finally
            {
                await TearDownAsync();
            }
        });
    }
}
