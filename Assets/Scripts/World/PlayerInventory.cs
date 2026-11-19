using System;
using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// Unity의 아이템 설정 에셋을 DOD 슬롯 데이터로 초기화하고 플레이어에 연결합니다.
    /// 실제 슬롯 수량은 내부 SlotInventory 한 곳에서만 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("인벤토리 설정")]
        [SerializeField, Min(1)] private int slotCapacity = 40;
        [SerializeField] private InventoryItemData[] itemDefinitions = Array.Empty<InventoryItemData>();
        [SerializeField] private InventoryResourceBinding[] resourceBindings = Array.Empty<InventoryResourceBinding>();

        private SlotInventory inventory;
        private WorldResourceInventory resourceInventory;

        public SlotInventory Inventory => inventory;
        public WorldResourceInventory ResourceInventory => resourceInventory;
        public bool IsInitialized => inventory != null;

        private void Awake()
        {
            Initialize();
        }

        /// <summary>
        /// Inspector의 고정 에셋 정의를 검증한 뒤 슬롯 데이터와 자원 조회 계층을 만듭니다.
        /// 이미 초기화되었다면 재호출로 런타임 슬롯 상태를 초기화하지 않습니다.
        /// </summary>
        public bool Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (slotCapacity <= 0 || itemDefinitions == null || resourceBindings == null)
            {
                return false;
            }

            var definitions = new InventoryItemDefinition[itemDefinitions.Length];
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                InventoryItemData item = itemDefinitions[i];
                if (item == null || !item.IsValid)
                {
                    return false;
                }

                definitions[i] = new InventoryItemDefinition(item.Id, item.MaxStack);
            }

            var mappings = new WorldResourceItemDefinition[resourceBindings.Length];
            for (int i = 0; i < resourceBindings.Length; i++)
            {
                InventoryResourceBinding binding = resourceBindings[i];
                if (binding.ResourceType == WorldResourceType.None ||
                    !Enum.IsDefined(typeof(WorldResourceType), binding.ResourceType) ||
                    binding.ItemData == null || !ContainsItem(binding.ItemData))
                {
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (resourceBindings[j].ResourceType == binding.ResourceType)
                    {
                        return false;
                    }
                }

                mappings[i] = new WorldResourceItemDefinition(binding.ResourceType, binding.ItemData.Id);
            }

            try
            {
                var initializedInventory = new SlotInventory(slotCapacity, definitions);
                inventory = initializedInventory;
                resourceInventory = new WorldResourceInventory(initializedInventory, mappings);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public bool TryGetSlot(int index, out InventorySlot slot)
        {
            slot = default;
            return inventory != null && inventory.TryGetSlot(index, out slot);
        }

        public bool TryAdd(ItemId itemId, int amount)
        {
            return inventory != null && inventory.TryAdd(itemId, amount);
        }

        public bool TryRemove(ItemId itemId, int amount)
        {
            return inventory != null && inventory.TryRemove(itemId, amount);
        }

        public int GetItemCount(ItemId itemId)
        {
            return inventory != null ? inventory.GetTotalCount(itemId) : 0;
        }

        /// <summary>슬롯 UI가 같은 콘텐츠 정의의 이름과 아이콘을 표시하도록 아이템 ID를 조회합니다.</summary>
        public bool TryGetItemData(ItemId itemId, out InventoryItemData itemData)
        {
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                InventoryItemData candidate = itemDefinitions[i];
                if (candidate != null && candidate.Id == itemId)
                {
                    itemData = candidate;
                    return true;
                }
            }

            itemData = null;
            return false;
        }

        private bool ContainsItem(InventoryItemData itemData)
        {
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                if (itemDefinitions[i] == itemData)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
