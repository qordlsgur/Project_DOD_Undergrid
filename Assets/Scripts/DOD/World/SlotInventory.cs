using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 고정 크기 슬롯 배열을 소유하고 아이템 중첩 및 수량 변경을 처리합니다.
    /// 슬롯 상태의 유일한 런타임 원본이며 외부에는 읽기 전용 값만 전달합니다.
    /// </summary>
    public sealed class SlotInventory
    {
        private readonly InventorySlot[] slots;
        private readonly InventoryItemDefinition[] definitions;
        private bool hasReservedAdd;
        private ItemId reservedItemId;
        private int reservedAmount;

        /// <summary>생성 시 고정된 전체 슬롯 수입니다.</summary>
        public int Capacity => slots.Length;

        /// <summary>슬롯 값이 실제로 바뀐 뒤 변경된 인덱스와 새 값을 전달합니다.</summary>
        public event Action<int, InventorySlot> SlotChanged;

        public SlotInventory(int capacity, InventoryItemDefinition[] itemDefinitions)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "슬롯 수는 1 이상이어야 합니다.");
            }

            if (itemDefinitions == null)
            {
                throw new ArgumentNullException(nameof(itemDefinitions));
            }

            ValidateDefinitions(itemDefinitions);
            slots = new InventorySlot[capacity];
            definitions = (InventoryItemDefinition[])itemDefinitions.Clone();
        }

        /// <summary>유효한 슬롯 번호의 현재 값을 복사해 반환합니다.</summary>
        public bool TryGetSlot(int index, out InventorySlot slot)
        {
            slot = default;
            if (index < 0 || index >= slots.Length)
            {
                return false;
            }

            slot = slots[index];
            return true;
        }

        /// <summary>아이템의 모든 슬롯 수량을 합산하며 int 범위를 넘으면 최댓값으로 제한합니다.</summary>
        public int GetTotalCount(ItemId itemId)
        {
            if (!TryGetDefinition(itemId, out _))
            {
                return 0;
            }

            long total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemId == itemId)
                {
                    total += slots[i].Amount;
                }
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary>기존 스택과 빈 슬롯을 합쳐 요청 수량 전체를 담을 수 있는지 변경 없이 확인합니다.</summary>
        public bool CanAdd(ItemId itemId, int amount)
        {
            if (hasReservedAdd || amount <= 0 || !TryGetDefinition(itemId, out InventoryItemDefinition definition))
            {
                return false;
            }

            long available = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.IsEmpty)
                {
                    available += definition.MaxStack;
                }
                else if (slot.ItemId == itemId)
                {
                    available += definition.MaxStack - slot.Amount;
                }

                if (available >= amount)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>기존 동일 아이템 스택을 먼저 채우고 나머지를 빈 슬롯에 분배합니다.</summary>
        public bool TryAdd(ItemId itemId, int amount)
        {
            if (hasReservedAdd || !CanAdd(itemId, amount) ||
                !TryGetDefinition(itemId, out InventoryItemDefinition definition))
            {
                return false;
            }

            AddUnchecked(itemId, amount, definition.MaxStack);
            return true;
        }

        /// <summary>
        /// 외부 월드 변경을 처리하는 동안 다른 슬롯 변경이 예약 공간을 차지하지 못하게 합니다.
        /// 예약은 슬롯 수량을 바꾸지 않으며 Commit 또는 Cancel에서 반드시 종료해야 합니다.
        /// </summary>
        public bool TryReserveAdd(ItemId itemId, int amount)
        {
            if (hasReservedAdd || !CanAdd(itemId, amount))
            {
                return false;
            }

            hasReservedAdd = true;
            reservedItemId = itemId;
            reservedAmount = amount;
            return true;
        }

        /// <summary>예약된 수량을 슬롯에 확정하고 예약 상태를 해제합니다.</summary>
        public bool CommitReservedAdd(ItemId itemId, int amount)
        {
            if (!hasReservedAdd || reservedItemId != itemId || reservedAmount != amount ||
                !TryGetDefinition(itemId, out InventoryItemDefinition definition))
            {
                return false;
            }

            try
            {
                AddUnchecked(itemId, amount, definition.MaxStack);
                return true;
            }
            finally
            {
                ClearReservation();
            }
        }

        /// <summary>월드 변경이 실패했을 때 예약 공간을 되돌려 다른 인벤토리 작업을 허용합니다.</summary>
        public bool CancelReservedAdd(ItemId itemId, int amount)
        {
            if (!hasReservedAdd || reservedItemId != itemId || reservedAmount != amount)
            {
                return false;
            }

            ClearReservation();
            return true;
        }

        private void AddUnchecked(ItemId itemId, int amount, int maxStack)
        {
            int remaining = amount;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.ItemId != itemId)
                {
                    continue;
                }

                int added = Math.Min(remaining, maxStack - slot.Amount);
                if (added > 0)
                {
                    SetSlot(i, new InventorySlot(itemId, slot.Amount + added));
                    remaining -= added;
                }
            }

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    continue;
                }

                int added = Math.Min(remaining, maxStack);
                SetSlot(i, new InventorySlot(itemId, added));
                remaining -= added;
            }

        }

        private void ClearReservation()
        {
            hasReservedAdd = false;
            reservedItemId = default;
            reservedAmount = 0;
        }

        /// <summary>요청 수량을 모두 보유한 경우에만 앞 슬롯부터 제거하고 빈 슬롯을 복원합니다.</summary>
        public bool TryRemove(ItemId itemId, int amount)
        {
            if (hasReservedAdd || amount <= 0 || GetTotalCount(itemId) < amount)
            {
                return false;
            }

            int remaining = amount;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                InventorySlot slot = slots[i];
                if (slot.ItemId != itemId)
                {
                    continue;
                }

                int removed = Math.Min(remaining, slot.Amount);
                int newAmount = slot.Amount - removed;
                SetSlot(i, newAmount == 0 ? default : new InventorySlot(itemId, newAmount));
                remaining -= removed;
            }

            return remaining == 0;
        }

        private bool TryGetDefinition(ItemId itemId, out InventoryItemDefinition definition)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].Id == itemId)
                {
                    definition = definitions[i];
                    return true;
                }
            }

            definition = default;
            return false;
        }

        private static void ValidateDefinitions(InventoryItemDefinition[] itemDefinitions)
        {
            for (int i = 0; i < itemDefinitions.Length; i++)
            {
                if (!itemDefinitions[i].IsValid)
                {
                    throw new ArgumentException("아이템 ID와 최대 중첩 수량은 유효한 양수여야 합니다.", nameof(itemDefinitions));
                }

                for (int j = 0; j < i; j++)
                {
                    if (itemDefinitions[i].Id == itemDefinitions[j].Id)
                    {
                        throw new ArgumentException("아이템 ID는 중복될 수 없습니다.", nameof(itemDefinitions));
                    }
                }
            }
        }

        private void SetSlot(int index, InventorySlot value)
        {
            slots[index] = value;
            SlotChanged?.Invoke(index, value);
        }
    }
}
