using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// 아이템 종류별 고정 콘텐츠 값을 보관합니다.
    /// 슬롯의 현재 수량은 이 에셋에 저장하지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryItemData", menuName = "Game/Inventory/Item Data")]
    public sealed class InventoryItemData : ScriptableObject
    {
        [SerializeField, Min(1)] private int itemId = 1001;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int maxStack = 999;

        public ItemId Id => new ItemId(itemId);
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int MaxStack => maxStack;
        public bool IsValid => itemId > 0 && maxStack > 0;
    }
}
