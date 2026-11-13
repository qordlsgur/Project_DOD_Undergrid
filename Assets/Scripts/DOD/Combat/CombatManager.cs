using System;
using Prohect_DOD_Undergird.DOD.Entity;

namespace Prohect_DOD_Undergird.DOD.Combat
{
    /// <summary>
    /// DOD 아키텍처 기반의 전투 연산 및 체력 상태를 총괄 관리하는 매니저 클래스입니다.
    /// UnityEngine에 의존하지 않으며, 연속 배열과 세대(Generation) 검증을 통해 안전하고 빠른 전투 처리를 제공합니다.
    /// </summary>
    public class CombatManager
    {
        private readonly EntityManager entityManager;
        private HealthData[] healths;

        /// <summary>
        /// 피격 시 발생하는 이벤트입니다. (대상 EntityId, 실제 적용된 피해량)
        /// </summary>
        public event Action<EntityId, int> OnDamaged;

        /// <summary>
        /// 회복 시 발생하는 이벤트입니다. (대상 EntityId, 실제 적용된 회복량)
        /// </summary>
        public event Action<EntityId, int> OnHealed;

        /// <summary>
        /// 엔티티 체력이 0이 되어 사망 확정 시 1회 발생하는 이벤트입니다. (사망한 EntityId)
        /// </summary>
        public event Action<EntityId> OnDied;

        /// <summary>
        /// CombatManager의 생성자입니다.
        /// 연동할 EntityManager를 주입받아 초기 슬롯 크기만큼 체력 배열을 할당합니다.
        /// </summary>
        /// <param name="entityManager">연동할 EntityManager 인스턴스</param>
        public CombatManager(EntityManager entityManager)
        {
            if (entityManager == null)
            {
                throw new ArgumentNullException(nameof(entityManager), "EntityManager 인스턴스는 null일 수 없습니다.");
            }

            this.entityManager = entityManager;
            this.healths = new HealthData[entityManager.Capacity];
        }

        /// <summary>
        /// 엔티티 슬롯에 초기 체력 정보를 등록합니다.
        /// 살아있는 유효한 엔티티만 등록 가능하며, 슬롯 재사용 시 이전 체력 데이터를 새 값으로 덮어씁니다.
        /// </summary>
        /// <param name="id">등록할 EntityId</param>
        /// <param name="maxHealth">최대 체력 (1 이상이어야 함)</param>
        /// <returns>등록 성공 시 true, 무효한 조건이거나 중복 등록이면 false</returns>
        public bool Register(EntityId id, int maxHealth)
        {
            if (maxHealth <= 0)
            {
                return false;
            }

            if (!entityManager.IsAlive(id))
            {
                return false;
            }

            EnsureCapacity(id.Index);

            // 동일 엔티티(동일 인덱스, 동일 세대)가 이미 정상 등록되어 있다면 중복 등록 거부
            if (healths[id.Index].Generation == id.Generation && healths[id.Index].Max > 0)
            {
                return false;
            }

            // 슬롯 재사용 시 이전 잔여 데이터를 완전히 덮어써 초기화
            healths[id.Index] = new HealthData
            {
                Current = maxHealth,
                Max = maxHealth,
                Generation = id.Generation
            };

            return true;
        }

        /// <summary>
        /// 대상 엔티티에 피해를 적용합니다.
        /// 체력이 0 이하가 되면 EntityManager.MarkDead를 호출하고 OnDied 이벤트를 1회 발생시킵니다.
        /// </summary>
        /// <param name="id">피해를 받을 EntityId</param>
        /// <param name="damage">적용할 피해량 데이터</param>
        /// <returns>피해 적용 성공 시 true, 무효한 대상이거나 이미 사망한 상태면 false</returns>
        public bool TakeDamage(EntityId id, DamageData damage)
        {
            if (damage.Amount <= 0)
            {
                return false;
            }

            if (!IsValidAndRegistered(id))
            {
                return false;
            }

            healths[id.Index].Current = Math.Max(0, healths[id.Index].Current - damage.Amount);
            OnDamaged?.Invoke(id, damage.Amount);

            // 체력이 0에 도달한 경우 사망 처리
            if (healths[id.Index].Current == 0)
            {
                // MarkDead가 성공(Alive -> Dead 전이 성공)한 최초 1회에만 OnDied 이벤트 발행
                bool marked = entityManager.MarkDead(id);
                if (marked)
                {
                    OnDied?.Invoke(id);
                }
            }

            return true;
        }

        /// <summary>
        /// 대상 엔티티의 체력을 회복합니다.
        /// 최대 체력을 초과하지 않으며, 정수 오버플로를 방어합니다.
        /// </summary>
        /// <param name="id">회복할 EntityId</param>
        /// <param name="amount">회복량 (1 이상)</param>
        /// <returns>회복 성공 시 true, 이미 사망했거나 이미 최대 체력이면 false</returns>
        public bool Heal(EntityId id, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (!IsValidAndRegistered(id))
            {
                return false;
            }

            int current = healths[id.Index].Current;
            int max = healths[id.Index].Max;

            // 이미 최대 체력이면 회복 불필요
            if (current >= max)
            {
                return false;
            }

            // 잔여 회복 가능 수치를 계산하여 int.MaxValue 정수 오버플로 원천 방지
            int healable = max - current;
            int actualHeal = Math.Min(amount, healable);

            healths[id.Index].Current += actualHeal;
            OnHealed?.Invoke(id, actualHeal);

            return true;
        }

        /// <summary>
        /// 대상 엔티티의 현재 체력을 반환합니다.
        /// 유효하지 않거나 등록되지 않은 ID면 0을 반환합니다.
        /// </summary>
        public int GetCurrentHealth(EntityId id)
        {
            if (!IsValidAndRegistered(id))
            {
                return 0;
            }

            return healths[id.Index].Current;
        }

        /// <summary>
        /// 대상 엔티티의 최대 체력을 반환합니다.
        /// 유효하지 않거나 등록되지 않은 ID면 0을 반환합니다.
        /// </summary>
        public int GetMaxHealth(EntityId id)
        {
            if (!IsValidAndRegistered(id))
            {
                return 0;
            }

            return healths[id.Index].Max;
        }

        /// <summary>
        /// 대상 엔티티가 사망했는지 여부를 반환합니다.
        /// </summary>
        public bool IsDead(EntityId id)
        {
            if (id.Index < 0 || id.Index >= healths.Length || healths[id.Index].Generation != id.Generation)
            {
                return true;
            }

            return entityManager.GetState(id) != EntityState.Alive || healths[id.Index].Current == 0;
        }

        /// <summary>
        /// EntityManager가 확장되었거나 요청 인덱스가 체력 배열 범위를 초과할 때 안전하게 2배 동반 확장합니다.
        /// </summary>
        private void EnsureCapacity(int requiredIndex)
        {
            if (requiredIndex >= healths.Length || entityManager.Capacity > healths.Length)
            {
                int targetCapacity = Math.Max(entityManager.Capacity, requiredIndex + 1);
                Array.Resize(ref healths, targetCapacity);
            }
        }

        /// <summary>
        /// ID가 유효한 범위에 있고, 등록된 세대와 일치하며, 현재 Alive 상태인지 종합 검증합니다.
        /// </summary>
        private bool IsValidAndRegistered(EntityId id)
        {
            if (id.Index < 0 || id.Index >= healths.Length)
            {
                return false;
            }

            // 체력 데이터에 기록된 세대와 EntityId의 세대가 다르면 구세대/무효 참조로 차단
            if (healths[id.Index].Generation != id.Generation)
            {
                return false;
            }

            // 최대 체력이 설정되지 않은 빈 슬롯 차단
            if (healths[id.Index].Max <= 0)
            {
                return false;
            }

            // EntityManager 기준으로도 살아있는지 확인
            return entityManager.IsAlive(id);
        }
    }
}
