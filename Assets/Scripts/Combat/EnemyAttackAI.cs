using UnityEngine;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 적 설정에 따라 사거리 안의 플레이어에게 일정 간격으로 피해를 적용합니다.
    /// </summary>
    [RequireComponent(typeof(EnemyCombatInitializer))]
    public class EnemyAttackAI : MonoBehaviour
    {
        // 적 종류별 공격 설정은 초기화 컴포넌트가 참조하는 공유 ScriptableObject에서 읽습니다.
        private EnemyData enemyData;
        // 공격 피해를 Unity 오브젝트가 아닌 공통 DOD 전투 경로로 전달합니다.
        private CombatReceiver playerReceiver;
        // Update마다 피해를 주지 않도록 다음 공격 가능 시각을 보관합니다.
        private float nextAttackTime;

        private void Awake()
        {
            EnemyCombatInitializer enemyInitializer = GetComponent<EnemyCombatInitializer>();
            enemyData = enemyInitializer != null ? enemyInitializer.Data : null;
        }

        /// <summary>
        /// 공격 대상으로 사용할 플레이어의 전투 수신기를 설정합니다.
        /// </summary>
        /// <param name="target">공유 CombatManager에 등록된 플레이어 수신기</param>
        public void SetPlayerTarget(CombatReceiver target)
        {
            playerReceiver = target;
            nextAttackTime = 0f;
        }

        private void Update()
        {
            if (enemyData == null || playerReceiver == null || playerReceiver.IsDead)
            {
                nextAttackTime = 0f;
                return;
            }

            float attackRange = Mathf.Max(0f, enemyData.AttackRange);
            float distanceSquared = ((Vector2)playerReceiver.transform.position -
                (Vector2)transform.position).sqrMagnitude;

            if (distanceSquared > attackRange * attackRange)
            {
                // 사거리 밖에서는 대기 시간을 초기화해 재진입 시 즉시 공격하게 합니다.
                nextAttackTime = 0f;
                return;
            }

            if (Time.time < nextAttackTime)
            {
                return;
            }

            playerReceiver.ReceiveDamage(enemyData.AttackPower);
            // Update는 프레임마다 호출되므로 다음 공격 시각으로 중복 피해를 제한합니다.
            nextAttackTime = Time.time + Mathf.Max(enemyData.AttackInterval, 0.01f);
        }
    }
}
