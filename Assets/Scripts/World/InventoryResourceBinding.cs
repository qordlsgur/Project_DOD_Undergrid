using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// 월드 채굴 보상 종류를 인벤토리 아이템 에셋에 연결하는 Inspector 설정입니다.
    /// </summary>
    [System.Serializable]
    public struct InventoryResourceBinding
    {
        [SerializeField] private WorldResourceType resourceType;
        [SerializeField] private InventoryItemData itemData;

        public WorldResourceType ResourceType => resourceType;
        public InventoryItemData ItemData => itemData;

        public InventoryResourceBinding(WorldResourceType type, InventoryItemData data)
        {
            resourceType = type;
            itemData = data;
        }
    }
}
