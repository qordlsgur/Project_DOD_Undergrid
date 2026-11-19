namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 슬롯 처리에 필요한 Unity 비의존 아이템 규칙입니다.
    /// 표시 이름과 아이콘은 이 런타임 데이터에 포함하지 않습니다.
    /// </summary>
    public readonly struct InventoryItemDefinition
    {
        /// <summary>아이템을 식별하는 안정적인 ID입니다.</summary>
        public ItemId Id { get; }
        /// <summary>슬롯 하나에 보관할 수 있는 최대 수량입니다.</summary>
        public int MaxStack { get; }
        /// <summary>ID와 중첩 수량이 모두 유효한 설정이면 true입니다.</summary>
        public bool IsValid => Id.IsValid && MaxStack > 0;

        /// <summary>아이템 ID와 슬롯별 최대 중첩 수량을 설정합니다.</summary>
        public InventoryItemDefinition(ItemId id, int maxStack)
        {
            Id = id;
            MaxStack = maxStack;
        }
    }
}
