using System;
using System.Collections.Generic;
using Game.Gameplay.Model;
using Game.Gameplay.View;
using UnityEngine;
using VContainer.Unity;

namespace Game.Integration.Game
{
    /// <summary>
    /// Screen cut-outs of the tutorial overlay. Computed after the camera has moved this frame (post late tick),
    /// so the holes stay glued to what they show.
    /// </summary>
    public sealed class SpotlightTargets : IPostLateTickable
    {
        private const float SlimeHoleScale = 1.45f;
        private const float MeterPadding = 18f;
        private const float BlockerPadding = 22f;
        private const float BlockerHeight = 0.95f;
        private const float PreviewPadding = 16f;
        private const float PreviewRing = 0.6f;
        private const float DoorsPadding = 20f;
        private const float DoorsHalfWidth = 2.1f;
        private const float DoorsHeight = 3.3f;
        private const float DoorsFront = -0.3f;
        private const float DoorsBack = 0.4f;
        private const float HalfDiameter = 0.5f;
        private const int CornerCount = 4;
        private const int BottomLeft = 0;
        private const int TopRight = 2;
        private const int BlockerHeightSamples = 2;

        private static readonly float[] DoorsX = { -DoorsHalfWidth, DoorsHalfWidth };
        private static readonly float[] DoorsY = { 0f, DoorsHeight };
        private static readonly float[] DoorsZ = { DoorsFront, DoorsBack };

        private readonly GameplayStage _stage;
        private readonly GameView _view;
        private readonly List<SpotlightHole> _holes = new List<SpotlightHole>();
        private readonly Vector3[] _corners = new Vector3[CornerCount];
        private readonly float[] _blockerHeights = new float[BlockerHeightSamples];

        private AttemptContext _context;
        private SpotlightTarget _targets;

        public SpotlightTargets(GameplayStage stage, GameView view)
        {
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _view = view ? view : throw new ArgumentNullException(nameof(view));
        }

        public void Show(AttemptContext context, SpotlightTarget targets)
        {
            _context = context;
            _targets = targets;
        }

        public void Clear()
        {
            _context = null;
            _targets = SpotlightTarget.None;
        }

        public void PostLateTick()
        {
            if (_context == null)
                return;

            // The scene may unload before the attempt is cancelled: never touch a destroyed overlay.
            var overlay = _stage ? _stage.Tutorial : null;
            if (!overlay || !_stage.CameraRig || !overlay.IsVisible)
                return;

            _holes.Clear();
            Add(SpotlightTarget.Doors, DoorsHole());
            Add(SpotlightTarget.Slime, SlimeHole());
            Add(SpotlightTarget.Blocker, BlockerHole());
            Add(SpotlightTarget.Preview, PreviewHole());
            Add(SpotlightTarget.Meter, MeterHole());
            overlay.SetHoles(_holes);
        }

        private Camera Camera => _stage.CameraRig.Camera;

        private void Add(SpotlightTarget target, SpotlightHole? hole)
        {
            if ((_targets & target) != 0 && hole.HasValue)
                _holes.Add(hole.Value);
        }

        private SpotlightHole? SlimeHole()
        {
            if ((_targets & SpotlightTarget.Slime) == 0)
                return null;

            var slime = _stage.Slime;
            var center = Camera.WorldToScreenPoint(slime.BodyCenter);
            var edge = Camera.WorldToScreenPoint(slime.BodyCenter + Camera.transform.right * (slime.Diameter * HalfDiameter));

            return SpotlightHole.Around(center, Vector2.Distance(center, edge) * SlimeHoleScale);
        }

        private SpotlightHole? MeterHole()
        {
            var meter = _view.SizeMeter;
            if ((_targets & SpotlightTarget.Meter) == 0 || meter == null)
                return null;

            // Overlay canvas: world corners are screen pixels.
            meter.GetWorldCorners(_corners);
            var rect = Rect.MinMaxRect(_corners[BottomLeft].x, _corners[BottomLeft].y, _corners[TopRight].x, _corners[TopRight].y);

            return new SpotlightHole(Expand(rect, MeterPadding), false);
        }

        private SpotlightHole? BlockerHole()
        {
            if ((_targets & SpotlightTarget.Blocker) == 0)
                return null;

            var id = _context.Model.PeekAdvance().BlockingCrystalId;
            if (id < 0)
                return null;

            var placement = _context.Level.Crystals[id];
            _blockerHeights[BlockerHeightSamples - 1] = BlockerHeight * (float)placement.Scale;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var vertex in placement.Contour.Vertices)
            {
                foreach (var height in _blockerHeights)
                {
                    Vector2 point = Camera.WorldToScreenPoint(new Vector3((float)vertex.X, height, (float)vertex.Z));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }

            return new SpotlightHole(Expand(Rect.MinMaxRect(min.x, min.y, max.x, max.y), BlockerPadding), false);
        }

        private SpotlightHole? PreviewHole()
        {
            if ((_targets & SpotlightTarget.Preview) == 0)
                return null;

            var plan = _context.Model.PreviewShot();
            if (plan == null)
                return null;

            var center = new Vector3((float)plan.ImpactCenter.X, 0, (float)plan.ImpactCenter.Z);
            var screenCenter = Camera.WorldToScreenPoint(center);
            var screenEdge = Camera.WorldToScreenPoint(center + Camera.transform.right * (float)plan.BlastRadius);

            return SpotlightHole.Around(screenCenter, Vector2.Distance(screenCenter, screenEdge) + PreviewPadding, PreviewRing);
        }

        private SpotlightHole? DoorsHole()
        {
            if ((_targets & SpotlightTarget.Doors) == 0)
                return null;

            var z = (float)_context.Level.DoorZ;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var x in DoorsX)
            {
                foreach (var y in DoorsY)
                {
                    foreach (var dz in DoorsZ)
                    {
                        Vector2 point = Camera.WorldToScreenPoint(new Vector3(x, y, z + dz));
                        min = Vector2.Min(min, point);
                        max = Vector2.Max(max, point);
                    }
                }
            }

            return new SpotlightHole(Expand(Rect.MinMaxRect(min.x, min.y, max.x, max.y), DoorsPadding), false);
        }

        private static Rect Expand(Rect rect, float by)
        {
            return Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);
        }
    }
}
