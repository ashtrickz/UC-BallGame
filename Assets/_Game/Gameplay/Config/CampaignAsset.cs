using System;
using System.Collections.Generic;
using Game.Gameplay.Level;
using Game.Gameplay.Levels;
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
            private const float DefaultRouteLength = 18f;

            public string title;
            [TextArea] public string idea;
            public float routeLength = DefaultRouteLength;
            public Crystal[] crystals = Array.Empty<Crystal>();
            [Tooltip("Control sequence: hold seconds per shot, proven to win with the 20% reserve.")]
            public double[] controlHolds = Array.Empty<double>();
            [Header("Bonus star criteria (independent)")]
            [Min(0)] public int maxShotsForStar;
            [Range(0, 100)] public float minRemainingMassPercent;
        }

        public Level[] levels = Array.Empty<Level>();
        [SerializeField, HideInInspector] private bool starCriteriaInitialized;

        public int Count => levels.Length;

        public CampaignLevelSpec Spec(int index)
        {
            if (index < 0 || index >= levels.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var level = levels[index];
            var crystals = new List<CrystalSpec>(level.crystals.Length);
            foreach (var c in level.crystals)
                crystals.Add(new CrystalSpec(c.variant, c.position.x, c.position.y, c.rotationStep, c.scale, c.role));
            return new CampaignLevelSpec(index + 1, level.title, level.idea, level.routeLength, crystals, level.controlHolds,
                new StarCriteria(level.maxShotsForStar, level.minRemainingMassPercent));
        }

        public string StarsKey(int index, GameRules rules)
        {
            var spec = Spec(index);
            var layout = CampaignLevels.BuildLayout(CampaignLevels.RulesFor(rules, spec), spec);

            return spec.Stars.ResultKey(nameof(CampaignLevelRequest), layout);
        }

        public void InitializeStarCriteria(GameRules rules)
        {
            if (starCriteriaInitialized)
                return;

            for (var i = 0; i < Count; i++)
            {
                var level = CampaignLevels.Build(rules, Spec(i), out var levelRules);
                var criteria = StarCriteria.FromControl(level, levelRules.D0);
                levels[i].maxShotsForStar = criteria.MaxShots;
                levels[i].minRemainingMassPercent = (float)Math.Floor(criteria.MinRemainingMassPercent);
            }

            starCriteriaInitialized = true;
        }
    }
}
