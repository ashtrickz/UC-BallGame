using UnityEngine;

namespace Slime.UI
{
    /// <summary>Three authored star slots shared by result cards and level selection cells.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Slime.UI", sourceAssembly: "Game.UI", sourceClassName: "StarRowView")]
    public sealed class StarRowView : MonoBehaviour
    {
        public const int SlotCount = 3;
        private const string ShimmerResource = "UI/Star Shimmer";

        [SerializeField] private UnityEngine.UI.Image[] slots = new UnityEngine.UI.Image[SlotCount];
        [SerializeField] private Sprite emptyStar;
        [SerializeField] private Sprite filledStar;
        [SerializeField] private Material filledStarMaterial;

        public int FilledCount { get; private set; }

        public RectTransform Target(int index)
        {
            return slots[index].rectTransform;
        }

        public void Show(int count)
        {
            FilledCount = Mathf.Clamp(count, 0, SlotCount);
            for (var i = 0; i < slots.Length; i++)
            {
                ApplySlot(i, i < FilledCount);
                slots[i].rectTransform.localScale = Vector3.one;
            }
        }

        public void Fill(int index)
        {
            ApplySlot(index, true);
            FilledCount = Mathf.Max(FilledCount, index + 1);
        }

        private void ApplySlot(int index, bool filled)
        {
            var slot = slots[index];
            slot.sprite = filled ? filledStar : emptyStar;
            if (!filled)
            {
                slot.material = null;
                return;
            }

            if (filledStarMaterial == null)
                filledStarMaterial = Resources.Load<Material>(ShimmerResource);

            if (!slot.TryGetComponent<ShimmerCoordinates>(out _))
                slot.gameObject.AddComponent<ShimmerCoordinates>();

            slot.material = filledStarMaterial;
        }
    }
}
