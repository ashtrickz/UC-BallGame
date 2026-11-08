using System.Collections;
using System.IO;
using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>
    /// Development tool: writes every frame (with UI) as a JPEG while <see cref="Time.captureFramerate"/> fixes the
    /// game step, so the frames form a smooth video regardless of capture cost. Assemble with ffmpeg.
    /// </summary>
    public sealed class FrameRecorder : MonoBehaviour
    {
        public string Folder { get; private set; }
        public int Frames { get; private set; }
        public int FrameRate = 30;
        public int Width = 720;
        public int Height = 1560;

        private RenderTexture _scaled;
        private Texture2D _readback;
        private bool _recording;

        public void Begin(string folder)
        {
            Folder = folder;
            Directory.CreateDirectory(folder);
            foreach (var file in Directory.GetFiles(folder, "*.jpg")) File.Delete(file);
            Frames = 0;
            Time.captureFramerate = FrameRate;
            _scaled = new RenderTexture(Width, Height, 0);
            _readback = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            _recording = true;
            StartCoroutine(Capture());
        }

        public void End()
        {
            _recording = false;
            Time.captureFramerate = 0;
        }

        private IEnumerator Capture()
        {
            var wait = new WaitForEndOfFrame();
            while (_recording)
            {
                yield return wait;
                var screen = ScreenCapture.CaptureScreenshotAsTexture();
                Graphics.Blit(screen, _scaled);
                Destroy(screen);
                var previous = RenderTexture.active;
                RenderTexture.active = _scaled;
                _readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                _readback.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(Path.Combine(Folder, $"frame_{Frames:00000}.jpg"), _readback.EncodeToJPG(92));
                Frames++;
            }
        }

        private void OnDestroy()
        {
            End();
            if (_scaled != null) _scaled.Release();
            if (_readback != null) Destroy(_readback);
        }
    }
}
