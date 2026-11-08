using System;
using System.Collections.Generic;
using Game.Gameplay.Level;
using UnityEngine;

namespace Game.Gameplay.Config
{
    /// <summary>
    /// The ten fixed campaign levels: crystal placements, route length and the stored control sequence.
    /// Authored by Tools/Ball Game/Gameplay/Campaign/Write Campaign Levels; every level is proven by tests.
    /// </summary>
    [CreateAssetMenu(menuName = "Ball Game/Campaign", fileName = "Campaign")]
    public sealed class CampaignAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Crystal
        {
            public int variant;
            [Tooltip("x = lateral offset, y = distance along the route (metres)")]
            public Vector2 position;
            [Range(0, 11)] public int rotationStep;
            public float scale = 1;
            public CrystalRole role;
        }

        [Serializable]
        public sealed class Level
        {
            public string title;
            [TextArea] public string idea;
            public float routeLength = 18;
            public Crystal[] crystals = Array.Empty<Crystal>();
            [Tooltip("Control sequence: hold seconds per shot, proven to win with the 20% reserve.")]
            public double[] controlHolds = Array.Empty<double>();
        }

        public Level[] levels = Array.Empty<Level>();

        public int Count => levels.Length;

        public CampaignLevelSpec Spec(int index)
        {
            if (index < 0 || index >= levels.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var level = levels[index];
            var crystals = new List<CrystalSpec>(level.crystals.Length);
            foreach (var c in level.crystals)
                crystals.Add(new CrystalSpec(c.variant, c.position.x, c.position.y, c.rotationStep, c.scale, c.role));
            return new CampaignLevelSpec(index + 1, level.title, level.idea, level.routeLength, crystals, level.controlHolds);
        }
    }
}
