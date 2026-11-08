using UnityEngine;

namespace Game.Gameplay.Config
{
    /// <summary>
    /// Balance, generation and available slime palettes (animation tuning lives in a separate view config).
    /// Registered in GameScope with RegisterInstance; each attempt takes an immutable <see cref="GameRules"/> snapshot.
    /// </summary>
    [CreateAssetMenu(menuName = "Ball Game/Slime Game Config", fileName = "SlimeGameConfig")]
    public sealed class SlimeGameConfig : ScriptableObject
    {
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
        public System.Collections.Generic.IReadOnlyList<SlimeColorPreset> SlimeColors =>
            slimeColors ?? System.Array.Empty<SlimeColorPreset>();

        public GameRules CreateSnapshot()
        {
            if (crystals == null) throw new System.InvalidOperationException("SlimeGameConfig needs a crystal catalog.");
            return new GameRules(values, crystals.ToVariantData());
        }

        private void Reset() => values = RuleValues.Defaults;
    }
}
