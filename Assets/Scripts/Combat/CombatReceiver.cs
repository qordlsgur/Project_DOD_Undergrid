using System;
using UnityEngine;
using Prohect_DOD_Undergird.DOD.Entity;
using Prohect_DOD_Undergird.DOD.Combat;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// GameObject와 DOD CombatManager를 연결하는 공통 수신 컴포넌트입니다.
    /// 플레이어, 일반 몬스터, 보스 프리팹에 공통으로 부착되어 Unity 라이프사이클 및 이벤트를 중계합니다.
    /// </summary>
    public class CombatReceiver : MonoBehaviour
    {
        private EntityId entityId = EntityId.Invalid;
        private CombatManager combatManager;
        private bool isSubscribed;

        /// <summary>
        /// 이 컴포넌트가 담당하는 EntityId입니다.
        /// </summary>
        public EntityId EntityId => entityId;

        /// <summary>
        /// 현재 잔여 체력입니다.
        /// </summary>
        public int CurrentHealth => combatManager != null ? combatManager.GetCurrentHealth(entityId) : 0;

        /// <summary>
        /// 최대 체력입니다.
        /// </summary>
        public int MaxHealth => combatManager != null ? combatManager.GetMaxHealth(entityId) : 0;

        /// <summary>
        /// 사망 여부입니다.
        /// </summary>
        public bool IsDead => combatManager == null || combatManager.IsDead(entityId);

        /// <summary>
        /// 피격 시 발생하는 Unity 측 C# 이벤트입니다. (적용된 피해량)
        /// </summary>
        public event Action<int> OnDamaged;

        /// <summary>
        /// 회복 시 발생하는 Unity 측 C# 이벤트입니다. (적용된 회복량)
        /// </summary>
        public event Action<int> OnHealed;

        /// <summary>
        /// 사망 확정 시 1회 발생하는 Unity 측 C# 이벤트입니다.
        /// </summary>
        public event Action OnDied;

        /// <summary>
        /// 공유 CombatManager 인스턴스와 EntityId를 주입받아 초기화합니다.
        /// 각 개체가 개별 CombatManager를 생성하지 않고 외부 단일 관리자로부터 주입받습니다.
        /// </summary>
        /// <param name="id">할당된 유효한 EntityId</param>
        /// <param name="manager">게임 전체에서 공유되는 CombatManager 단일 인스턴스</param>
        /// <param name="maxHealth">초기 최대 체력</param>
        public void Initialize(EntityId id, CombatManager manager, int maxHealth)
        {
            if (manager == null)
            {
                Debug.LogError("[CombatReceiver] 전달된 CombatManager가 null입니다.", this);
                return;
            }

            // 기존 구독이 있다면 먼저 안전하게 해제
            UnsubscribeEvents();

            entityId = id;
            combatManager = manager;

            // CombatManager에 초기 체력 등록
            combatManager.Register(entityId, maxHealth);

            // 활성화 상태라면 즉시 이벤트 구독 시작
            if (isActiveAndEnabled)
            {
                SubscribeEvents();
            }
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// 외부(투사체 충돌, 피격 판정 등)에서 이 개체에 피해를 입힙니다.
        /// </summary>
        /// <param name="amount">피해량</param>
        /// <returns>피해 적용 성공 여부</returns>
        public bool ReceiveDamage(int amount)
        {
            if (combatManager == null || entityId == EntityId.Invalid)
            {
                return false;
            }

            return combatManager.TakeDamage(entityId, new DamageData(amount));
        }

        /// <summary>
        /// 외부(힐 포션, 버프 스킬 등)에서 이 개체의 체력을 회복합니다.
        /// </summary>
        /// <param name="amount">회복량</param>
        /// <returns>회복 성공 여부</returns>
        public bool ReceiveHeal(int amount)
        {
            if (combatManager == null || entityId == EntityId.Invalid)
            {
                return false;
            }

            return combatManager.Heal(entityId, amount);
        }

        /// <summary>
        /// CombatManager의 전역 이벤트를 안전하게 구독합니다.
        /// </summary>
        private void SubscribeEvents()
        {
            if (isSubscribed || combatManager == null || entityId == EntityId.Invalid)
            {
                return;
            }

            combatManager.OnDamaged += HandleDamaged;
            combatManager.OnHealed += HandleHealed;
            combatManager.OnDied += HandleDied;
            isSubscribed = true;
        }

        /// <summary>
        /// CombatManager의 전역 이벤트 구독을 해제하여 메모리 누수를 방지합니다.
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (!isSubscribed || combatManager == null)
            {
                return;
            }

            combatManager.OnDamaged -= HandleDamaged;
            combatManager.OnHealed -= HandleHealed;
            combatManager.OnDied -= HandleDied;
            isSubscribed = false;
        }

        private void HandleDamaged(EntityId targetId, int damageAmount)
        {
            if (targetId == entityId)
            {
                OnDamaged?.Invoke(damageAmount);
            }
        }

        private void HandleHealed(EntityId targetId, int healAmount)
        {
            if (targetId == entityId)
            {
                OnHealed?.Invoke(healAmount);
            }
        }

        private void HandleDied(EntityId targetId)
        {
            if (targetId == entityId)
            {
                OnDied?.Invoke();
            }
        }
    }
}
