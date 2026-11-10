using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Slime.Gameplay.View;
using Slime.UI;
using UnityEngine;

namespace Slime.Gameplay.Presentation
{
    /// <summary>Three reusable world-canvas stars: spring out of the well, follow a sine arc, then light popup slots.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Integration.Game", sourceAssembly: "Game.Features.Game", sourceClassName: "WorldStarsView")]
    public sealed class WorldStarsView : MonoBehaviour
    {
        private const float FullTurn = 360f;
        private const float Half = 0.5f;
        private const float EndScale = 1f;
        private const float Two = 2f;
        private const float ScreenDepth = 4f;
        private const float SourceHeight = 0.3f;
        private const float PopFraction = 0.28f;
        private const float ClaimPop = 0.3f;
        private const float ClaimDuration = 0.22f;
        private const int CornerCount = 4;

        [SerializeField] private UnityEngine.UI.Image[] stars = new UnityEngine.UI.Image[StarRowView.SlotCount];
        [SerializeField, Min(0.1f)] private float flightDuration = 1.35f;
        [SerializeField, Min(0)] private float stagger = 0.2f;
        [SerializeField, Min(0)] private float burstHeight = 1.6f;
        [SerializeField, Min(0)] private float waveWidth = 0.32f;
        [SerializeField, Min(0.1f)] private float launchSize = 0.65f;

        private readonly bool[] _launched = new bool[StarRowView.SlotCount];
        private readonly bool[] _arrived = new bool[StarRowView.SlotCount];
        private readonly Vector3[] _corners = new Vector3[CornerCount];
        private int _generation;

        public event Action<int> Launched;

        public event Action<int> Arrived;

        public int FlyingCount { get; private set; }

        public bool IsPlaying { get; private set; }

        public async UniTask PlayAsync(int count, Vector3 source, Camera camera, StarRowView targets, IGameClock clock,
            CancellationToken token)
        {
            Stop();
            token.ThrowIfCancellationRequested();
            var canvas = GetComponent<Canvas>();
            var renderCamera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : camera;
            var generation = _generation;
            count = Mathf.Clamp(count, 0, StarRowView.SlotCount);
            Array.Clear(_launched, 0, _launched.Length);
            Array.Clear(_arrived, 0, _arrived.Length);
            targets.Show(0);
            IsPlaying = true;
            var duration = flightDuration + stagger * Mathf.Max(0, count - 1) + ClaimDuration;
            try
            {
                await clock.Animate(duration, progress =>
                {
                    if (generation != _generation || token.IsCancellationRequested)
                        return;

                    transform.rotation = renderCamera.transform.rotation;
                    var elapsed = progress * duration;
                    FlyingCount = 0;
                    for (var i = 0; i < count; i++)
                    {
                        var local = elapsed - stagger * i;
                        if (local < 0)
                            continue;

                        if (!_launched[i])
                        {
                            _launched[i] = true;
                            Launched?.Invoke(i);
                        }

                        var t = Mathf.Clamp01(local / flightDuration);
                        if (t < 1)
                        {
                            stars[i].gameObject.SetActive(true);
                            Place(stars[i].rectTransform, targets.Target(i), camera, renderCamera, source, t, i);
                            FlyingCount++;
                        }
                        else
                        {
                            stars[i].gameObject.SetActive(false);
                            if (!_arrived[i])
                            {
                                _arrived[i] = true;
                                targets.Fill(i);
                                Arrived?.Invoke(i);
                            }

                            var pulse = Mathf.Clamp01((local - flightDuration) / ClaimDuration);
                            targets.Target(i).localScale = Vector3.one * (1 + ClaimPop * Mathf.Sin(Mathf.PI * pulse));
                        }
                    }
                }, token);
            }
            finally
            {
                if (generation == _generation)
                    Stop();
            }
        }

        public void Stop()
        {
            _generation++;
            FlyingCount = 0;
            IsPlaying = false;
            foreach (var star in stars)
                if (star != null)
                    star.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Place(RectTransform star, RectTransform target, Camera sourceCamera, Camera renderCamera, Vector3 source, float t, int index)
        {
            var targetCanvas = target.GetComponentInParent<Canvas>();
            var uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            var screenTarget = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);
            var end = renderCamera.ScreenToWorldPoint(new Vector3(screenTarget.x, screenTarget.y, ScreenDepth));
            var screenSource = sourceCamera.WorldToScreenPoint(source + Vector3.up * SourceHeight);
            var start = renderCamera.ScreenToWorldPoint(new Vector3(screenSource.x, screenSource.y, ScreenDepth));
            var side = index - 1;
            var pop = start + renderCamera.transform.up * burstHeight + renderCamera.transform.right * (side * waveWidth);
            Vector3 position;
            if (t < PopFraction)
            {
                var popT = t / PopFraction;
                position = Vector3.LerpUnclamped(start, pop, GameClock.EaseOut(popT));
            }
            else
            {
                var flyT = (t - PopFraction) / (1 - PopFraction);
                position = Vector3.Lerp(pop, end, GameClock.Smooth(flyT))
                    + renderCamera.transform.up * (Mathf.Sin(Mathf.PI * flyT) * burstHeight * Half)
                    + renderCamera.transform.right * (Mathf.Sin(Mathf.PI * Two * flyT) * waveWidth);
            }

            target.GetWorldCorners(_corners);
            var left = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[0]);
            var right = RectTransformUtility.WorldToScreenPoint(uiCamera, _corners[CornerCount - 1]);
            var endSize = Vector3.Distance(renderCamera.ScreenToWorldPoint(new Vector3(left.x, left.y, ScreenDepth)),
                renderCamera.ScreenToWorldPoint(new Vector3(right.x, right.y, ScreenDepth)));
            star.position = position;
            star.rotation = renderCamera.transform.rotation * Quaternion.Euler(0, 0, Mathf.Sin(Mathf.PI * t) * FullTurn);
            star.sizeDelta = Vector2.one * Mathf.Lerp(launchSize, endSize, t);
            star.localScale = Vector3.one * Mathf.Lerp(Half, EndScale, Mathf.Clamp01(t / PopFraction));
        }
    }
}
