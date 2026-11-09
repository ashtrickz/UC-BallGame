using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.View
{
    /// <summary>A screen-space cut-out: a rounded rectangle (or circle) in screen pixels, with an optional pulsing ring.</summary>
    public readonly struct SpotlightHole
    {
        public readonly Rect Screen;
        public readonly bool Circle;
        public readonly float Ring;

        public SpotlightHole(Rect screen, bool circle, float ring = 1f) { Screen = screen; Circle = circle; Ring = ring; }

        public static SpotlightHole Around(Vector2 center, float radius, float ring = 1f) =>
            new SpotlightHole(new Rect(center.x - radius, center.y - radius, radius * 2, radius * 2), true, ring);
    }

    /// <summary>
    /// Tutorial layer: a full-screen dimmer drawn by the spotlight shader with cut-outs around what matters, a message
    /// card placed away from the cut-outs, "tap to continue" and SKIP. While it waits for a tap it blocks the game;
    /// during action steps it lets touches through to the game.
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private const int MaxHoles = 6;
        private static readonly int HolesId = Shader.PropertyToID("_Holes");
        private static readonly int StylesId = Shader.PropertyToID("_HoleStyles");
        private static readonly int CountId = Shader.PropertyToID("_HoleCount");

        [SerializeField] private Image dimmer;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button skipButton;

        public event Action Continue;
        public event Action Skip;

        private Material _material;
        private RectTransform _canvasRect;
        private readonly Vector4[] _holes = new Vector4[MaxHoles];
        private readonly Vector4[] _styles = new Vector4[MaxHoles];

        public bool IsVisible => gameObject.activeSelf;
        public string Message => message != null ? message.text : "";
        public TMP_Text MessageLabel => message;
        public bool WaitsForTap => continueButton != null && continueButton.gameObject.activeSelf;

        private void Awake()
        {
            _material = new Material(dimmer.material) { name = "Tutorial Spotlight (runtime)" };
            dimmer.material = _material;
            _canvasRect = dimmer.canvas.rootCanvas.GetComponent<RectTransform>();
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(OnContinue);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkip);
            if (_material != null) Destroy(_material);
        }

        private void OnContinue() => Continue?.Invoke();
        private void OnSkip() => Skip?.Invoke();

        /// <param name="waitForTap">True: the dimmer blocks the game and a tap anywhere continues. False: action step.</param>
        public void Show(string text, bool waitForTap)
        {
            gameObject.SetActive(true);
            if (message != null) message.text = text;
            if (continueButton != null) continueButton.gameObject.SetActive(waitForTap);
            if (continueLabel != null) continueLabel.gameObject.SetActive(waitForTap);
            // Action steps: touches must reach the game, so the dimmer stops catching rays.
            dimmer.raycastTarget = waitForTap;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Updates the cut-outs (screen pixels) and moves the card to the half of the screen without holes.</summary>
        public void SetHoles(IReadOnlyList<SpotlightHole> holes)
        {
            if (_material == null) return;
            var count = Mathf.Min(MaxHoles, holes?.Count ?? 0);
            var lowestTop = float.MaxValue;
            var highestBottom = float.MinValue;
            for (var i = 0; i < count; i++)
            {
                var hole = holes[i];
                var min = ToCanvas(hole.Screen.min);
                var max = ToCanvas(hole.Screen.max);
                var center = (min + max) * 0.5f;
                var half = (max - min) * 0.5f;
                _holes[i] = new Vector4(center.x, center.y, Mathf.Abs(half.x), Mathf.Abs(half.y));
                _styles[i] = new Vector4(hole.Circle ? 10000f : 26f, hole.Ring, 0, 0);
                lowestTop = Mathf.Min(lowestTop, hole.Screen.yMax);
                highestBottom = Mathf.Max(highestBottom, hole.Screen.yMin);
            }
            for (var i = count; i < MaxHoles; i++) { _holes[i] = Vector4.zero; _styles[i] = Vector4.zero; }
            _material.SetVectorArray(HolesId, _holes);
            _material.SetVectorArray(StylesId, _styles);
            _material.SetFloat(CountId, count);
            PlaceCard(count == 0 ? 0 : (lowestTop + highestBottom) * 0.5f / Mathf.Max(1, Screen.height));
        }

        private void PlaceCard(float holesHeight01)
        {
            if (card == null) return;
            // Holes mostly in the lower half: card at the top, otherwise at the bottom.
            var top = holesHeight01 < 0.55f;
            card.anchorMin = new Vector2(0.5f, top ? 1 : 0);
            card.anchorMax = card.anchorMin;
            card.pivot = new Vector2(0.5f, top ? 1 : 0);
            card.anchoredPosition = new Vector2(0, top ? -230 : 300);
        }

        private Vector2 ToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
            return local;
        }
    }
}
