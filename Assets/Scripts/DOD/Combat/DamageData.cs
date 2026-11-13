namespace Prohect_DOD_Undergird.DOD.Combat
{
    /// <summary>
    /// 전투 시스템에서 적용되는 피해량 정보를 보관하는 불변 데이터 구조체입니다.
    /// 값 복사 비용이 적고 GC Alloc이 발생하지 않는 readonly struct로 구현되었습니다.
    /// </summary>
    public readonly struct DamageData
    {
        /// <summary>
        /// 적용될 피해 수치입니다. 0 이상의 정수만 허용됩니다.
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 피해량 데이터를 생성합니다. 음수 값이 들어오면 안전하게 0으로 보정합니다.
        /// </summary>
        /// <param name="amount">피해량 정수값</param>
        public DamageData(int amount)
        {
            Amount = amount > 0 ? amount : 0;
        }
    }
}
