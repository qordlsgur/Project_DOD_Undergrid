using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 기존 월드 자원 API를 일반 슬롯 인벤토리에 연결하는 호환 계층입니다.
    /// 수량을 별도로 저장하지 않고 SlotInventory의 실제 슬롯에서 계산합니다.
    /// </summary>
    public sealed class WorldResourceInventory
    {
        private static readonly InventoryItemDefinition[] DefaultItemDefinitions =
        {
            new InventoryItemDefinition(new ItemId(1001), 999),
            new InventoryItemDefinition(new ItemId(1002), 999)
        };

        private static readonly WorldResourceItemDefinition[] DefaultResourceMappings =
        {
            new WorldResourceItemDefinition(WorldResourceType.Copper, new ItemId(1001)),
            new WorldResourceItemDefinition(WorldResourceType.Iron, new ItemId(1002))
        };

        private readonly SlotInventory slots;
        private readonly WorldResourceItemDefinition[] resourceMappings;

        /// <summary>기존 호출부와 단위 테스트를 위한 기본 40칸 자원 인벤토리를 만듭니다.</summary>
        public WorldResourceInventory()
            : this(new SlotInventory(40, DefaultItemDefinitions), DefaultResourceMappings)
        {
        }

        /// <summary>플레이어 슬롯 데이터와 월드 자원-ID 매핑을 공유하는 조회 계층을 만듭니다.</summary>
        public WorldResourceInventory(
            SlotInventory slotInventory,
            WorldResourceItemDefinition[] mappings)
        {
            slots = slotInventory ?? throw new ArgumentNullException(nameof(slotInventory));
            if (mappings == null)
            {
                throw new ArgumentNullException(nameof(mappings));
            }

            resourceMappings = (WorldResourceItemDefinition[])mappings.Clone();
        }

        /// <summary>지정 자원에 대응하는 아이템이 가진 모든 슬롯 수량을 반환합니다.</summary>
        public int GetAmount(WorldResourceType resourceType)
        {
            return TryGetItemId(resourceType, out ItemId itemId) ? slots.GetTotalCount(itemId) : 0;
        }

        /// <summary>광물 파괴 보상을 전체 수량으로 담을 수 있는지 슬롯 변경 없이 확인합니다.</summary>
        public bool CanAdd(WorldResourceType resourceType, int amount)
        {
            return TryGetItemId(resourceType, out ItemId itemId) && slots.CanAdd(itemId, amount);
        }

        /// <summary>기존 월드 자원 종류를 매핑된 아이템 슬롯에 추가합니다.</summary>
        public bool TryAdd(WorldResourceType resourceType, int amount)
        {
            return TryGetItemId(resourceType, out ItemId itemId) && slots.TryAdd(itemId, amount);
        }

        /// <summary>채굴의 마지막 타격 중 보상 공간이 다른 슬롯 작업에 사용되지 않도록 예약합니다.</summary>
        public bool TryReserveAdd(WorldResourceType resourceType, int amount)
        {
            return TryGetItemId(resourceType, out ItemId itemId) && slots.TryReserveAdd(itemId, amount);
        }

        /// <summary>벽 파괴가 확정된 뒤 예약 수량을 실제 슬롯에 반영합니다.</summary>
        public bool CommitReservedAdd(WorldResourceType resourceType, int amount)
        {
            return TryGetItemId(resourceType, out ItemId itemId) && slots.CommitReservedAdd(itemId, amount);
        }

        /// <summary>벽 피해가 실패했거나 파괴되지 않았을 때 예약을 취소합니다.</summary>
        public bool CancelReservedAdd(WorldResourceType resourceType, int amount)
        {
            return TryGetItemId(resourceType, out ItemId itemId) && slots.CancelReservedAdd(itemId, amount);
        }

        private bool TryGetItemId(WorldResourceType resourceType, out ItemId itemId)
        {
            for (int i = 0; i < resourceMappings.Length; i++)
            {
                if (resourceMappings[i].ResourceType == resourceType)
                {
                    itemId = resourceMappings[i].ItemId;
                    return true;
                }
            }

            itemId = default;
            return false;
        }
    }
}
