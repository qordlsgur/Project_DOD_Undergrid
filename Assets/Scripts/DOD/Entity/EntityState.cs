namespace Prohect_DOD_Undergird.DOD.Entity
{
    /// <summary>
    /// 엔티티 슬롯의 생명주기 상태를 나타내는 열거형입니다.
    /// </summary>
    public enum EntityState
    {
        /// <summary>
        /// 엔티티가 활성화되어 정상 동작 중인 상태입니다.
        /// </summary>
        Alive,

        /// <summary>
        /// 엔티티가 사망 처리되어 해제 대기 중인 상태입니다.
        /// </summary>
        Dead,

        /// <summary>
        /// 슬롯이 완전히 해제되어 비어 있으며 재사용 대기열(FreeList)에 존재하는 상태입니다.
        /// </summary>
        Released
    }
}
