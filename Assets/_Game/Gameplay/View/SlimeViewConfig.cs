using UnityEngine;

namespace Slime.Gameplay.View
{
    /// <summary>Presentation-only tuning. Never affects rules, generation or outcomes.</summary>
    [CreateAssetMenu(menuName = "Ball Game/Slime View Config", fileName = "SlimeViewConfig")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.Gameplay.View", sourceAssembly: "Game.Gameplay.View", sourceClassName: "SlimeViewConfig")]
    public sealed class SlimeViewConfig : ScriptableObject
    {
        private const float WeakImpactRadius = 0.2f;
        private const float MinStrongImpactRadius = 0.3f;
        private const float MinWaveSpeed = 0.1f;
        private const float MinWaveDuration = 0.12f;
        private const float MaxWaveDuration = 0.4f;

        private static readonly Vector3 DefaultCameraEuler = new Vector3(42f, -32f, 0f);
        private static readonly Vector3 DefaultVictoryCameraEulerOffset = new Vector3(10f, -12f, 0f);
        private static readonly Color DefaultPreviewColor = new Color(0.88f, 1f, 0.53f, 0.85f);
        private static readonly Color DefaultHighlightColor = new Color(0.88f, 1f, 0.53f, 1f);
        private static readonly Color DefaultInfectColor = new Color(0.88f, 1f, 0.53f, 1f);

        private const float DefaultHopPrepare = 0.12f;
        private const float DefaultHopFlight = 0.56f;
        private const float DefaultHopHeight = 0.85f;
        private const float DefaultMaxHopLength = 1.35f;
        private const float DefaultSurfaceWobble = 0.017f;
        private const float DefaultPinchDuration = 0.16f;
        private const float DefaultFlightMin = 0.32f;
        private const float DefaultFlightPerMetre = 0.05f;
        private const float DefaultFlightArc = 0.55f;
        private const float DefaultWaveSpeed = 7f;
        private const float DefaultInfectDuration = 0.12f;
        private const float DefaultShatterDuration = 0.22f;
        private const float DefaultPreviewTint = 0.18f;
        private const float DefaultPreviewBrightness = 1.28f;
        private const float DefaultSurroundingCrystalBrightness = 0.8f;
        private const float DefaultPreviewEdgeWidth = 0.018f;
        private const float DefaultStrongImpactRadius = 2.2f;
        private const float DefaultDefeatHopHeight = 0.32f;
        private const float DefaultDefeatHopDuration = 0.45f;
        private const float DefaultPopSquash = 0.28f;
        private const float DefaultPopLinger = 0.55f;
        private const float DefaultCameraDistance = 24f;
        private const float DefaultLookAhead = 3.1f;
        private const float DefaultHalfWidthAtD0 = 4.4f;
        private const float DefaultZoomAdaptation = 0.45f;
        private const float DefaultMinZoomFactor = 0.6f;
        private const float DefaultChargePullBack = 0.14f;
        private const float DefaultChargeZoomDuration = 0.7f;
        private const float DefaultReleaseZoomDuration = 0.9f;
        private const float DefaultFollowSmoothTime = 0.28f;
        private const float DefaultVictoryZoomFactor = 0.86f;
        private const float DefaultVictoryCameraDuration = 0.75f;
        private const float DefaultVictoryFocusHeight = 0.8f;
        private const float DefaultTrackSmoothTime = 0.08f;

        [Header("Hops (SlimeLab reference: 0.14 s prep, 0.78 s flight, 1.15 height)")]
        [Min(0)] public float hopPrepare = DefaultHopPrepare;
        [Min(0.1f)] public float hopFlight = DefaultHopFlight;
        [Min(0)] public float hopHeight = DefaultHopHeight;
        [Min(0.2f)] public float maxHopLength = DefaultMaxHopLength;
        [Range(0, 0.08f)] public float surfaceWobble = DefaultSurfaceWobble;

        [Header("Shot")]
        [Min(0.02f)] public float pinchDuration = DefaultPinchDuration;
        [Min(0.1f)] public float flightMin = DefaultFlightMin;
        [Min(0)] public float flightPerMetre = DefaultFlightPerMetre;
        [Min(0)] public float flightArc = DefaultFlightArc;

        [Header("Clearing")]
        [Min(0.1f)] public float waveSpeed = DefaultWaveSpeed;
        [Min(0.02f)] public float infectDuration = DefaultInfectDuration;
        [Min(0.02f)] public float shatterDuration = DefaultShatterDuration;

        [Header("Shot feedback (presentation only)")]
        [Range(0, 0.5f)] public float previewTint = DefaultPreviewTint;
        [Range(1, 1.5f)] public float previewBrightness = DefaultPreviewBrightness;
        [Range(0.5f, 1)] public float surroundingCrystalBrightness = DefaultSurroundingCrystalBrightness;
        [Range(0.006f, 0.04f)] public float previewEdgeWidth = DefaultPreviewEdgeWidth;
        [Min(0.3f)] public float strongImpactRadius = DefaultStrongImpactRadius;


        [Header("Defeat")]
        [Min(0)] public float defeatHopHeight = DefaultDefeatHopHeight;
        [Min(0.1f)] public float defeatHopDuration = DefaultDefeatHopDuration;
        [Min(0.05f)] public float popSquash = DefaultPopSquash;
        [Min(0)] public float popLinger = DefaultPopLinger;

        [Header("Camera")]
        public Vector3 cameraEuler = DefaultCameraEuler;
        [Min(1)] public float cameraDistance = DefaultCameraDistance;
        [Min(0)] public float lookAhead = DefaultLookAhead;
        [Min(1)] public float halfWidthAtD0 = DefaultHalfWidthAtD0;
        [Range(0, 1)] public float zoomAdaptation = DefaultZoomAdaptation;
        [Range(0.3f, 1)] public float minZoomFactor = DefaultMinZoomFactor;
        [Min(0)] public float chargePullBack = DefaultChargePullBack;
        [Min(0.05f)] public float chargeZoomDuration = DefaultChargeZoomDuration;
        [Min(0.05f)] public float releaseZoomDuration = DefaultReleaseZoomDuration;
        public AnimationCurve zoomEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(0.01f)] public float followSmoothTime = DefaultFollowSmoothTime;

        [Header("Victory camera")]
        [Range(0.5f, 1)] public float victoryZoomFactor = DefaultVictoryZoomFactor;
        public Vector3 victoryCameraEulerOffset = DefaultVictoryCameraEulerOffset;
        [Min(0.05f)] public float victoryCameraDuration = DefaultVictoryCameraDuration;
        [Min(0)] public float victoryFocusHeight = DefaultVictoryFocusHeight;

        [Header("Track and preview")]
        [Min(0)] public float trackSmoothTime = DefaultTrackSmoothTime;
        public Color previewColor = DefaultPreviewColor;
        public Color highlightColor = DefaultHighlightColor;
        public Color infectColor = DefaultInfectColor;

        public float ImpactStrength(float radius)
        {
            return Mathf.InverseLerp(WeakImpactRadius, Mathf.Max(MinStrongImpactRadius, strongImpactRadius), radius);
        }

        public float ImpactWaveDuration(float radius)
        {
            return Mathf.Clamp(radius / Mathf.Max(MinWaveSpeed, waveSpeed), MinWaveDuration, MaxWaveDuration);
        }
    }
}
