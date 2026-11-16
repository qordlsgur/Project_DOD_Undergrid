using UnityEngine;
using Prohect_DOD_Undergird.DOD.Entity;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 플레이어 GameObject를 공유 DOD 전투 시스템에 등록합니다.
    /// </summary>
    [RequireComponent(typeof(CombatReceiver))]
    public class PlayerCombatInitializer : MonoBehaviour
    {
        [Header("전투 초기화")]
        // 씬에서 공유 매니저를 명시적으로 주입해 개체별 CombatManager 생성을 막습니다.
        [SerializeField] private CombatBootstrap combatBootstrap;
        // 플레이어의 초기 체력 설정이며 런타임 체력은 CombatManager만 보관합니다.
        [SerializeField, Min(1)] private int maxHealth = 100;

        private CombatReceiver combatReceiver;
        // 재호출 시 새 Entity를 만들지 않도록 초기화 여부를 식별합니다.
        private EntityId entityId = EntityId.Invalid;

        /// <summary>
        /// 현재 플레이어에게 발급된 Entity 식별자입니다.
        /// </summary>
        public EntityId EntityId => entityId;

        private void Awake()
        {
            combatReceiver = GetComponent<CombatReceiver>();
        }

        private void Start()
        {
            // Bootstrap의 Awake에서 공유 매니저가 만들어진 뒤 플레이어를 등록합니다.
            Initialize();
        }

        /// <summary>
        /// 공유 매니저에 플레이어 Entity와 최대 체력을 한 번 등록합니다.
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public bool Initialize()
        {
            if (entityId != EntityId.Invalid || combatBootstrap == null ||
                combatBootstrap.EntityManager == null || combatBootstrap.CombatManager == null ||
                maxHealth <= 0)
            {
                return false;
            }

            if (combatReceiver == null)
            {
                combatReceiver = GetComponent<CombatReceiver>();
            }

            if (combatReceiver == null)
            {
                return false;
            }

            entityId = combatBootstrap.EntityManager.Create();
            combatReceiver.Initialize(entityId, combatBootstrap.CombatManager, maxHealth);
            return combatReceiver.EntityId == entityId && combatReceiver.MaxHealth == maxHealth;
        }

        private void OnDestroy()
        {
            // EntityManager 규칙에 맞춰 사망 상태에서만 슬롯을 해제합니다.
            if (combatBootstrap == null || entityId == EntityId.Invalid ||
                combatBootstrap.EntityManager == null ||
                combatBootstrap.EntityManager.GetState(entityId) != EntityState.Dead)
            {
                return;
            }

            combatBootstrap.EntityManager.Release(entityId);
        }
    }
}
