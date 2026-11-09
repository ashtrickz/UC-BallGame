using System;
using Game.App.Game;
using Game.Gameplay.Config;
using Game.Gameplay.Levels;
using Game.Gameplay.View;
using UnityEngine;

namespace Game.Integration.Game
{
    /// <summary>Lays a built level out on the gameplay stage.</summary>
    public interface ILevelPresenter
    {
        void Present(BuiltLevel built);
    }

    /// <summary>Builds the scene of a level: crystals, track, doors, finish, slime start and colours, splash, camera and preview.</summary>
    public sealed class LevelPresenter : ILevelPresenter
    {
        private const float TrackLeadIn = 1.5f;
        private const float FinishMarkerOffset = 0.4f;
        private const int CorridorSides = 2;
        private const int NoColor = -1;

        private readonly SlimeGameConfig _config;
        private readonly GameplayStage _stage;
        private readonly ISlimeColorPreferences _colors;

        public LevelPresenter(SlimeGameConfig config, GameplayStage stage, ISlimeColorPreferences colors)
        {
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            _stage = stage ? stage : throw new ArgumentNullException(nameof(stage));
            _colors = colors ?? throw new ArgumentNullException(nameof(colors));
        }

        public void Present(BuiltLevel built)
        {
            if (built == null)
                throw new ArgumentNullException(nameof(built));

            var level = built.Level;
            var rules = built.Rules;
            var startZ = (float)level.StartZ;
            var doorZ = (float)level.DoorZ;
            var startDiameter = (float)rules.D0;
            var anchor = new Vector3(0, 0, startZ);
            var corridorWidth = (float)(rules.CorridorHalfWidth(rules.D0) * CorridorSides);

            _stage.Crystals.Build(level, _config.Crystals);
            _stage.Track.Build(startZ - TrackLeadIn, doorZ, corridorWidth);
            _stage.Doors.Place(doorZ);
            if (_stage.FinishMarker != null)
                _stage.FinishMarker.position = new Vector3(0, 0, (float)level.FinishZ + FinishMarkerOffset);

            _stage.Slime.ResetForAttempt(anchor, startDiameter);
            var colorIndex = _colors.SelectedIndex(_config.SlimeColors);
            if (colorIndex > NoColor)
                _stage.Slime.SetColors(_config.SlimeColors[colorIndex]);

            if (_stage.PoolSplash != null)
                _stage.PoolSplash.Clear();

            _stage.CameraRig.Snap(anchor, startDiameter, startDiameter);
            _stage.Preview.ResetEffects();
        }
    }
}
