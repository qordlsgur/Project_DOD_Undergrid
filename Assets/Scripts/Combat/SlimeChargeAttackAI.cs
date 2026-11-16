using UnityEngine;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// Slime의 준비, 방향 고정 돌진, 최초 접촉 피해와 공격 대기시간을 관리합니다.
    /// 이동 속도 적용은 EnemyChaseAI에 요청해 Rigidbody2D 제어자를 하나로 유지합니다.
    /// </summary>
    [RequireComponent(typeof(EnemyCombatInitializer))]
    [RequireComponent(typeof(EnemyChaseAI))]
    public class SlimeChargeAttackAI : MonoBehaviour
    {
        private enum ChargeState
        {
            Ready,
            Preparing,
            Charging,
            Returning,
            ReturnWait,
            Cooldown
        }

        private const float ReturnWaitDuration = 1f;

        private EnemyData enemyData;
        private EnemyChaseAI chaseAI;
        private CombatReceiver playerReceiver;
        private ChargeState currentState;
        private float stateEndTime;
        private float nextAttackTime;
        private Vector2 returnPosition;
        private bool hasReturnPosition;
        private bool hasHitPlayerThisCharge;

        private void Awake()
        {
            EnemyCombatInitializer initializer = GetComponent<EnemyCombatInitializer>();
            enemyData = initializer != null ? initializer.Data : null;
            chaseAI = GetComponent<EnemyChaseAI>();
        }

        /// <summary>
        /// 스포너가 공유 플레이어 전투 수신기를 전달합니다.
        /// </summary>
        public void SetPlayerTarget(CombatReceiver target)
        {
            playerReceiver = target;
        }

        private void Update()
        {
            if (enemyData == null || chaseAI == null || playerReceiver == null)
            {
                CancelCharge();
                return;
            }

            if (playerReceiver.IsDead)
            {
                CancelCharge();
                return;
            }

            switch (currentState)
            {
                case ChargeState.Ready:
                    TryBeginPreparation();
                    break;
                case ChargeState.Preparing:
                    UpdatePreparation();
                    break;
                case ChargeState.Charging:
                    UpdateCharge();
                    break;
                case ChargeState.Returning:
                    UpdateReturn();
                    break;
                case ChargeState.ReturnWait:
                    UpdateReturnWait();
                    break;
                case ChargeState.Cooldown:
                    UpdateCooldown();
                    break;
            }
        }

        private void TryBeginPreparation()
        {
            if (!IsPlayerInAttackRange())
            {
                return;
            }

            currentState = ChargeState.Preparing;
            stateEndTime = Time.time + Mathf.Max(0f, enemyData.ChargePreparationTime);
        }

        private void UpdatePreparation()
        {
            if (!IsPlayerInAttackRange())
            {
                currentState = ChargeState.Ready;
                return;
            }

            if (Time.time < stateEndTime)
            {
                return;
            }

            Vector2 direction = (Vector2)playerReceiver.transform.position - (Vector2)transform.position;
            if (!chaseAI.BeginChargeMovement(direction, enemyData.ChargeSpeed))
            {
                currentState = ChargeState.Ready;
                return;
            }

            // 첫 돌진을 실제로 시작할 때만 인스턴스별 복귀 기준점을 기록합니다.
            // 이후 반복 공격은 매번 복귀 위치를 덮어쓰지 않고 이 기준점을 재사용합니다.
            if (!hasReturnPosition)
            {
                returnPosition = transform.position;
                hasReturnPosition = true;
            }

            hasHitPlayerThisCharge = false;
            currentState = ChargeState.Charging;
            stateEndTime = Time.time + Mathf.Max(0.01f, enemyData.ChargeDuration);
        }

        private void UpdateCharge()
        {
            if (Time.time < stateEndTime)
            {
                return;
            }

            FinishCharge();
        }

        private void UpdateReturn()
        {
            if (!chaseAI.HasReachedReturnPosition)
            {
                return;
            }

            currentState = ChargeState.ReturnWait;
            stateEndTime = Time.time + ReturnWaitDuration;
        }

        private void UpdateReturnWait()
        {
            if (Time.time < stateEndTime)
            {
                return;
            }

            chaseAI.EndReturnMovement();
            if (Time.time >= nextAttackTime)
            {
                currentState = ChargeState.Ready;
                return;
            }

            currentState = ChargeState.Cooldown;
            stateEndTime = nextAttackTime;
        }

        private void UpdateCooldown()
        {
            if (Time.time >= stateEndTime)
            {
                currentState = ChargeState.Ready;
            }
        }

        private bool IsPlayerInAttackRange()
        {
            float attackRange = Mathf.Max(0f, enemyData.AttackRange);
            Vector2 toPlayer = (Vector2)playerReceiver.transform.position - (Vector2)transform.position;
            return toPlayer.sqrMagnitude <= attackRange * attackRange;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandlePlayerCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            HandlePlayerCollision(collision);
        }

        /// <summary>
        /// 접촉이 돌진 시작 전에 이미 이루어졌더라도 현재 돌진에서의 최초 접촉을 놓치지 않습니다.
        /// Enter와 Stay가 같은 물리 프레임에 모두 불려도 내부 적중 상태가 한 번만 처리합니다.
        /// </summary>
        private void HandlePlayerCollision(Collision2D collision)
        {
            if (currentState != ChargeState.Charging || hasHitPlayerThisCharge || playerReceiver == null ||
                playerReceiver.IsDead)
            {
                return;
            }

            CombatReceiver hitReceiver = collision.collider.GetComponentInParent<CombatReceiver>();
            if (hitReceiver != playerReceiver)
            {
                return;
            }

            // 같은 돌진에서 콜백이 반복되거나 여러 Collider에 닿아도 피해는 한 번만 전달합니다.
            hasHitPlayerThisCharge = true;
            playerReceiver.ReceiveDamage(enemyData.AttackPower);
            FinishCharge();
        }

        private void FinishCharge()
        {
            chaseAI.BeginReturnMovement(returnPosition);
            currentState = ChargeState.Returning;
            nextAttackTime = Time.time + Mathf.Max(0.01f, enemyData.AttackInterval);
        }

        private void CancelCharge()
        {
            if (chaseAI != null)
            {
                chaseAI.EndChargeMovement();
                chaseAI.EndReturnMovement();
            }

            currentState = ChargeState.Ready;
            hasHitPlayerThisCharge = false;
        }

        private void OnDisable()
        {
            if (chaseAI != null)
            {
                chaseAI.EndChargeMovement();
                chaseAI.EndReturnMovement();
            }

            currentState = ChargeState.Ready;
            hasHitPlayerThisCharge = false;
        }
    }
}
