using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Audio;
using Game.Gameplay.Model;
using Game.Integration.Audio;
using Game.Integration.Composition;
using Game.Integration.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Game.Gameplay.PlayModeTests
{
    public sealed partial class GameplayLifecycleTests
    {
        [UnityTest]
        public IEnumerator Audio_selected_jump_shot_impacts_and_result_follow_the_real_gameplay() => UniTask.ToCoroutine(async () =>
        {
            AudioService audio = null;
            Action<GameSound, AudioPlaybackHandle> observer = null;
            try
            {
                var gameplay = await StartGameAsync();
                audio = ProjectScope.Instance.Container.Resolve<AudioService>();
                var heard = new List<GameSound>();
                var jumpClips = new List<string>();
                var shotClips = new List<string>();
                observer = (id, handle) =>
                {
                    heard.Add(id);
                    if (id == GameSound.SlimeJump) jumpClips.Add(handle.Clip.name);
                    if (id == GameSound.SlimeShoot) shotClips.Add(handle.Clip.name);
                };
                audio.SoundPlayed += observer;
                gameplay.RestartWithSeed(Seed);
                var context = gameplay.Current;
                Time.timeScale = 2;
                var result = await GameplayAutoplay.PlayAsync(gameplay, AutoplayMode.ControlPlan, CancellationToken.None)
                    .Timeout(TimeSpan.FromSeconds(90));
                Assert.AreEqual(AttemptPhase.Won, result);
                await UniTask.WaitUntil(() => heard.Contains(GameSound.Victory)).Timeout(TimeSpan.FromSeconds(8));
                foreach (var id in new[] { GameSound.RoundStart, GameSound.SlimeSquash, GameSound.SlimeJump,
                    GameSound.SlimeLand, GameSound.ChargeStart, GameSound.SlimeShoot, GameSound.ProjectileImpact,
                    GameSound.GateOpen, GameSound.Victory })
                    CollectionAssert.Contains(heard, id, id.ToString());
                Assert.IsTrue(heard.Contains(GameSound.CrystalBreakSmall) || heard.Contains(GameSound.CrystalBreakLarge));
                Assert.AreEqual(context.Model.ShotsFired, shotClips.Count, "Each accepted projectile produces one release sound.");
                Assert.IsTrue(jumpClips.TrueForAll(name => name.StartsWith("slime_jump_")), "Use the selected jump WAVs.");
                Assert.IsTrue(shotClips.TrueForAll(name => name.StartsWith("slime_shoot_")), "Use the selected slime-shot WAVs.");
                Debug.Log($"[Audio validation] {jumpClips.Count} jumps, {shotClips.Count} shots, {heard.Count} cue starts through a real winning attempt.");
            }
            finally
            {
                if (audio != null && observer != null) audio.SoundPlayed -= observer;
                await TearDownAsync();
            }
        });

        [UnityTest]
        public IEnumerator Audio_charge_loops_stop_on_pause_and_restart_without_a_stale_release() => UniTask.ToCoroutine(async () =>
        {
            AudioService audio = null;
            Action<GameSound, AudioPlaybackHandle> observer = null;
            try
            {
                var gameplay = await StartGameAsync();
                audio = ProjectScope.Instance.Container.Resolve<AudioService>();
                var loops = new List<AudioPlaybackHandle>();
                var releases = 0;
                observer = (id, handle) =>
                {
                    if (id == GameSound.ChargeLoop || id == GameSound.ChargeTensionLoop) loops.Add(handle);
                    if (id == GameSound.SlimeShoot) releases++;
                };
                audio.SoundPlayed += observer;
                var model = gameplay.Current.Model;
                var input = gameplay.Stage.Input;
                await WaitPhase(model, AttemptPhase.Waiting);
                input.SetVirtualPointer(false);
                await UniTask.Yield();
                input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging);
                Assert.AreEqual(2, loops.Count);
                gameplay.SetPaused(PauseReason.Settings, true);
                Assert.IsTrue(loops.TrueForAll(handle => !handle.IsActive), "Cancelled hold owns no surviving charge sources.");
                gameplay.SetPaused(PauseReason.Settings, false);
                input.SetVirtualPointer(false);
                await UniTask.Yield();
                await UniTask.Yield();
                Assert.AreEqual(0, releases, "The old pointer release is not a shot.");
                input.SetVirtualPointer(true);
                await WaitPhase(model, AttemptPhase.Charging);
                Assert.AreEqual(4, loops.Count);
                gameplay.Restart();
                input.SetVirtualPointer(null);
                Assert.IsTrue(loops.TrueForAll(handle => !handle.IsActive), "Restart stops both old charge layers.");
                Assert.AreEqual(0, releases);
                Assert.AreEqual(0, gameplay.Current.Model.ShotsFired);
            }
            finally
            {
                if (audio != null && observer != null) audio.SoundPlayed -= observer;
                await TearDownAsync();
            }
        });
    }
}
