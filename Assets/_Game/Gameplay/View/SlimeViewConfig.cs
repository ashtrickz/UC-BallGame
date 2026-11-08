using UnityEngine;

namespace Game.Gameplay.View
{
    /// <summary>Presentation-only tuning. Never affects rules, generation or outcomes.</summary>
    [CreateAssetMenu(menuName = "Ball Game/Slime View Config", fileName = "SlimeViewConfig")]
    public sealed class SlimeViewConfig : ScriptableObject
    {
        [Header("Hops (SlimeLab reference: 0.14 s prep, 0.78 s flight, 1.15 height)")]
        [Min(0)] public float hopPrepare = 0.12f;
        [Min(0.1f)] public float hopFlight = 0.56f;
        [Min(0)] public float hopHeight = 0.85f;
        [Min(0.2f)] public float maxHopLength = 1.35f;
        [Range(0, 0.08f)] public float surfaceWobble = 0.017f;

        [Header("Shot")]
        [Min(0.02f)] public float pinchDuration = 0.16f;
        [Min(0.1f)] public float flightMin = 0.32f;
        [Min(0)] public float flightPerMetre = 0.05f;
        [Min(0)] public float flightArc = 0.55f;

        [Header("Clearing")]
        [Min(0.1f)] public float waveSpeed = 7f;
        [Min(0.02f)] public float infectDuration = 0.12f;
        [Min(0.02f)] public float shatterDuration = 0.22f;

        [Header("Defeat")]
        [Min(0)] public float defeatHopHeight = 0.32f;
        [Min(0.1f)] public float defeatHopDuration = 0.45f;
        [Min(0.05f)] public float popSquash = 0.28f;
        [Min(0)] public float popLinger = 0.55f;

        [Header("Camera")]
        public Vector3 cameraEuler = new Vector3(42f, -32f, 0f);
        [Min(1)] public float cameraDistance = 24f;
        [Min(0)] public float lookAhead = 3.1f;
        [Min(1)] public float halfWidthAtD0 = 4.4f;
        [Range(0, 1)] public float zoomAdaptation = 0.45f;
        [Range(0.3f, 1)] public float minZoomFactor = 0.6f;
        [Min(0)] public float chargePullBack = 0.14f;
        [Min(0.05f)] public float chargeZoomDuration = 0.7f;
        [Min(0.05f)] public float releaseZoomDuration = 0.9f;
        public AnimationCurve zoomEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(0.01f)] public float followSmoothTime = 0.28f;

        [Header("Victory camera")]
        [Range(0.5f, 1)] public float victoryZoomFactor = 0.86f;
        public Vector3 victoryCameraEulerOffset = new Vector3(10f, -12f, 0f);
        [Min(0.05f)] public float victoryCameraDuration = 0.75f;
        [Min(0)] public float victoryFocusHeight = 0.8f;

        [Header("Track and preview")]
        [Min(0)] public float trackSmoothTime = 0.08f;
        public Color previewColor = new Color(0.88f, 1f, 0.53f, 0.85f);
        public Color highlightColor = new Color(0.88f, 1f, 0.53f, 1f);
        public Color infectColor = new Color(0.88f, 1f, 0.53f, 1f);
    }
}
