namespace Prohect_DOD_Undergird.DOD.Combat
{
    /// <summary>
    /// 개별 엔티티 슬롯의 체력 및 세대 식별 정보를 보관하는 데이터 구조체입니다.
    /// 배열 연속 메모리에 저장되어 캐시 효율을 극대화합니다.
    /// </summary>
    public struct HealthData
    {
        /// <summary>
        /// 현재 잔여 체력입니다.
        /// </summary>
        public int Current;

        /// <summary>
        /// 엔티티의 최대 체력입니다.
        /// </summary>
        public int Max;

        /// <summary>
        /// 이 체력 데이터가 등록된 시점의 EntityId Generation 값입니다.
        /// 슬롯이 재사용된 후 이전 ID가 새 데이터에 접근하거나 이전 데이터가 오염되는 것을 방어합니다.
        /// </summary>
        public int Generation;
    }
}
