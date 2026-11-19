namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 기존 월드 자원 종류를 일반 아이템 ID에 연결하는 Unity 비의존 매핑 값입니다.
    /// </summary>
    public readonly struct WorldResourceItemDefinition
    {
        public WorldResourceType ResourceType { get; }
        public ItemId ItemId { get; }

        public WorldResourceItemDefinition(WorldResourceType resourceType, ItemId itemId)
        {
            ResourceType = resourceType;
            ItemId = itemId;
        }
    }
}
