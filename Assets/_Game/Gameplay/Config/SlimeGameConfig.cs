using System;
using System.Collections.Generic;
using Game.Gameplay.Level;
using Game.Gameplay.Levels;
using UnityEngine;

namespace Game.Gameplay.Config
{
    /// <summary>
    /// Balance, generation and available slime palettes (animation tuning lives in a separate view config).
    /// Registered in GameScope with RegisterInstance; each attempt takes an immutable <see cref="GameRules"/> snapshot.
    /// </summary>
    [CreateAssetMenu(menuName = "Ball Game/Slime Game Config", fileName = "SlimeGameConfig")]
    public sealed class SlimeGameConfig : ScriptableObject, ILevelConfig
    {
        private const string MissingCatalogMessage = "SlimeGameConfig needs a crystal catalog.";

        [SerializeField] private RuleValues values = RuleValues.Defaults;
        [SerializeField] private CrystalCatalog crystals;
        [SerializeField] private CampaignAsset campaign;
        [SerializeField] private WallValues wall = WallValues.Defaults;
        [Header("Slime colours")]
        [SerializeField] private SlimeColorPreset[] slimeColors = SlimeColorPreset.Defaults();
        [Tooltip("Debug: when non-zero, every attempt uses this seed instead of a new one.")]
        [SerializeField] private int debugFixedSeed;

        public CrystalCatalog Crystals => crystals;

        public CampaignAsset Campaign => campaign;

        public WallValues Wall => wall;

        public int DebugFixedSeed => debugFixedSeed;

        public IReadOnlyList<SlimeColorPreset> SlimeColors => slimeColors ?? Array.Empty<SlimeColorPreset>();

        public int CampaignCount => campaign != null ? campaign.Count : 0;

        public GameRules CreateSnapshot()
        {
            if (crystals == null)
                throw new InvalidOperationException(MissingCatalogMessage);

            return new GameRules(values, crystals.ToVariantData());
        }

        public CampaignLevelSpec CampaignSpec(int index)
        {
            return campaign.Spec(index);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            values = RuleValues.Defaults;
        }
#endif
    }
}
