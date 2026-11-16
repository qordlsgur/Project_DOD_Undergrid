using UnityEngine;
using Prohect_DOD_Undergird.DOD.Entity;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 적 GameObject를 공유 DOD 매니저에 등록하고 CombatReceiver를 초기화합니다.
    /// </summary>
    [RequireComponent(typeof(CombatReceiver))]
    public class EnemyCombatInitializer : MonoBehaviour
    {
        [SerializeField] private EnemyData enemyData;

        private CombatBootstrap combatBootstrap;
        private CombatReceiver combatReceiver;
        private EntityId entityId = EntityId.Invalid;
        private bool isDeathSubscribed;
        private bool isDeathHandled;
        private bool isReleaseAttempted;

        /// <summary>
        /// 이 적에 발급된 EntityId를 반환합니다. 초기화 전에는 무효 ID입니다.
        /// </summary>
        public EntityId EntityId => entityId;

        /// <summary>
        /// 이 적 종류의 공통 설정 데이터를 반환합니다.
        /// </summary>
        public EnemyData Data => enemyData;

        private void Awake()
        {
            combatReceiver = GetComponent<CombatReceiver>();
        }

        private void OnEnable()
        {
            SubscribeToDeath();
        }

        private void OnDisable()
        {
            UnsubscribeFromDeath();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDeath();
            ReleaseDeadEntity();
        }

        /// <summary>
        /// 공유 Bootstrap에서 매니저를 받아 ID를 발급하고 최대 체력을 등록합니다.
        /// </summary>
        /// <param name="bootstrap">씬에서 공유 매니저를 보관하는 CombatBootstrap</param>
        /// <returns>초기화 성공 여부</returns>
        public bool Initialize(CombatBootstrap bootstrap)
        {
            if (entityId != EntityId.Invalid || bootstrap == null ||
                bootstrap.EntityManager == null || bootstrap.CombatManager == null ||
                enemyData == null || enemyData.MaxHealth <= 0)
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

            combatBootstrap = bootstrap;
            entityId = combatBootstrap.EntityManager.Create();
            combatReceiver.Initialize(entityId, combatBootstrap.CombatManager, enemyData.MaxHealth);
            SubscribeToDeath();
            return true;
        }

        private void SubscribeToDeath()
        {
            if (!isActiveAndEnabled || isDeathSubscribed || combatReceiver == null || entityId == EntityId.Invalid)
            {
                return;
            }

            combatReceiver.OnDied += HandleDeath;
            isDeathSubscribed = true;

            if (combatReceiver.IsDead)
            {
                HandleDeath();
            }
        }

        private void UnsubscribeFromDeath()
        {
            if (!isDeathSubscribed || combatReceiver == null)
            {
                return;
            }

            combatReceiver.OnDied -= HandleDeath;
            isDeathSubscribed = false;
        }

        private void HandleDeath()
        {
            if (isDeathHandled)
            {
                return;
            }

            isDeathHandled = true;
            Destroy(gameObject);
        }

        private void ReleaseDeadEntity()
        {
            if (isReleaseAttempted || combatBootstrap == null || entityId == EntityId.Invalid)
            {
                return;
            }

            if (combatBootstrap.EntityManager.GetState(entityId) != EntityState.Dead)
            {
                return;
            }

            isReleaseAttempted = true;
            combatBootstrap.EntityManager.Release(entityId);
        }
    }
}
