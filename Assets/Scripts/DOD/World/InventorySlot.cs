namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 인벤토리의 한 슬롯을 읽기 전용으로 전달하는 값입니다.
    /// 기본값은 ID와 수량이 모두 0인 빈 슬롯입니다.
    /// </summary>
    public readonly struct InventorySlot
    {
        /// <summary>슬롯에 들어 있는 아이템의 ID입니다. 빈 슬롯은 기본 ID입니다.</summary>
        public ItemId ItemId { get; }
        /// <summary>슬롯에 들어 있는 아이템 수량입니다.</summary>
        public int Amount { get; }
        /// <summary>수량이 0이면 true입니다.</summary>
        public bool IsEmpty => Amount == 0;

        internal InventorySlot(ItemId itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }
}
