#if GAME_DEBUG
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Integration.Game;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Editor
{
    /// <summary>Play Mode helpers: replay a seed and drive the attempt with the autoplay (demo videos, smoke checks).</summary>
    public static class GameplayDebugMenu
    {
        private const string Menu = "Tools/Ball Game/Gameplay/Play Mode/";

        public static IGameplayDebug Find()
        {
            return Resolve<IGameplayDebug>();
        }

        private static T Resolve<T>() where T : class
        {
            var scope = Object.FindFirstObjectByType<GameScope>();
            if (scope == null || scope.Container == null)
                return null;

            return scope.Container.Resolve<T>();
        }

        [MenuItem(Menu + "Autoplay Control Plan")] private static void Control() => Run(AutoplayMode.ControlPlan);
        [MenuItem(Menu + "Autoplay Overcharge Defeat")] private static void Overcharge() => Run(AutoplayMode.Overcharge);
        [MenuItem(Menu + "Autoplay Out Of Resource Defeat")] private static void Starve() => Run(AutoplayMode.Starve);
        [MenuItem(Menu + "Restart (New Seed)")] private static void Restart() => Resolve<GameplayFlow>()?.Restart();
        [MenuItem(Menu + "Record Video/Victory (seed 424242)")] private static void RecordVictory() => Record(AutoplayMode.ControlPlan, DemoSeed, "victory").Forget();
        [MenuItem(Menu + "Record Video/Defeat (seed 424242)")] private static void RecordDefeat() => Record(AutoplayMode.Starve, DemoSeed, "defeat").Forget();

        public const int DemoSeed = 424242;
        public const string VideoFolder = "Builds/Video";

        [MenuItem(Menu + "Autoplay Control Plan", true)]
        [MenuItem(Menu + "Autoplay Overcharge Defeat", true)]
        [MenuItem(Menu + "Autoplay Out Of Resource Defeat", true)]
        [MenuItem(Menu + "Restart (New Seed)", true)]
        [MenuItem(Menu + "Record Video/Victory (seed 424242)", true)]
        [MenuItem(Menu + "Record Video/Defeat (seed 424242)", true)]
        private static bool Validate() => EditorApplication.isPlaying;

        public static void Run(AutoplayMode mode)
        {
            var gameplay = Find();
            if (gameplay == null) { Debug.LogWarning("[Gameplay] start the Game scene first."); return; }
            RunAsync(gameplay, mode).Forget();
        }

        public static async UniTaskVoid Record(AutoplayMode mode, int seed, string name)
        {
            var gameplay = Find();
            if (gameplay == null) { Debug.LogWarning("[Gameplay] start the Game scene first."); return; }
            var recorder = new GameObject("Frame Recorder").AddComponent<Game.Gameplay.View.FrameRecorder>();
            try
            {
                gameplay.PlaySeed(seed);
                recorder.Begin(System.IO.Path.Combine(VideoFolder, "frames_" + name));
                var result = await GameplayAutoplay.PlayAsync(gameplay, mode, CancellationToken.None, thinkSeconds: 0.7f);
                await UniTask.Delay(System.TimeSpan.FromSeconds(3), DelayType.DeltaTime);
                recorder.End();
                Debug.Log($"[Gameplay] recorded {name}: {result}, {recorder.Frames} frames at {recorder.FrameRate} fps in {recorder.Folder}");
            }
            finally { Object.Destroy(recorder.gameObject); }
        }

        public static async UniTaskVoid CaptureVictoryFramesAsync(string folder, float step, int count)
        {
            var gameplay = Find();
            if (gameplay == null) return;
            System.IO.Directory.CreateDirectory(folder);
            var context = gameplay.Current;
            await UniTask.WaitUntil(() => context.Model.Phase == Game.Gameplay.Model.AttemptPhase.Won);
            var camera = Resolve<Game.Gameplay.View.GameplayStage>().CameraRig.Camera;
            var target = new RenderTexture(428, 926, 24);
            var read = new Texture2D(428, 926, TextureFormat.RGB24, false);
            var elapsed = 0f;
            for (var i = 0; i < count; i++)
            {
                while (elapsed < i * step) { await UniTask.Yield(PlayerLoopTiming.PostLateUpdate); elapsed += Time.deltaTime; }
                var previous = camera.targetTexture;
                var aspect = camera.aspect;
                camera.targetTexture = target;
                camera.aspect = aspect;
                camera.Render();
                camera.targetTexture = previous;
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, 428, 926), 0, 0);
                read.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, $"v{i:00}.png"), read.EncodeToPNG());
            }
            target.Release();
            Object.Destroy(read);
            Debug.Log("[Gameplay] victory frames written to " + folder);
        }

        private static async UniTaskVoid RunAsync(IGameplayDebug gameplay, AutoplayMode mode)
        {
            try
            {
                var result = await GameplayAutoplay.PlayAsync(gameplay, mode, CancellationToken.None);
                Debug.Log($"[Gameplay] autoplay {mode}: {result}");
            }
            catch (System.OperationCanceledException) { Debug.Log("[Gameplay] autoplay stopped"); }
        }
    }
}
#endif
