using UnityEngine;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 몬스터의 대기, 플레이어 추적, 마지막 발견 위치 조사를 관리합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyCombatInitializer))]
    public class EnemyChaseAI : MonoBehaviour
    {
        private enum ChaseState
        {
            Idle,
            Chase,
            Investigate
        }

        private const float InvestigationArrivalDistance = 0.1f;
        private const float ReturnCastOriginTolerance = 0.001f;

        private Rigidbody2D rb;
        private EnemyData enemyData;
        private Transform playerTarget;
        private Collider2D movementCollider;
        private Collider2D playerCollider;
        private ContactFilter2D returnContactFilter;
        private readonly RaycastHit2D[] returnCastResults = new RaycastHit2D[8];
        private ChaseState currentState;
        private Vector2 lastKnownPlayerPosition;
        private float investigationElapsedTime;
        private bool hasReachedLastKnownPosition;
        private bool hasChargeMovement;
        private Vector2 chargeDirection;
        private float chargeSpeed;
        private bool hasReturnMovement;
        private bool hasReachedReturnPosition;
        private Vector2 returnPosition;

        /// <summary>
        /// 복귀 이동이 물리 도착 판정에 도달했는지 반환합니다.
        /// </summary>
        public bool HasReachedReturnPosition => hasReachedReturnPosition;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            movementCollider = GetComponent<Collider2D>();
            EnemyCombatInitializer initializer = GetComponent<EnemyCombatInitializer>();
            enemyData = initializer != null ? initializer.Data : null;
        }

        /// <summary>
        /// 추적 대상으로 사용할 플레이어 Transform을 전달합니다.
        /// </summary>
        /// <param name="target">추적할 플레이어 Transform</param>
        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            playerCollider = target != null ? target.GetComponent<Collider2D>() : null;
            returnContactFilter = new ContactFilter2D();
            returnContactFilter.useTriggers = false;

            if (target != null)
            {
                returnContactFilter.SetLayerMask(1 << target.gameObject.layer);
            }
        }

        /// <summary>
        /// 돌진 공격이 정한 방향과 속도를 이동 우선 동작으로 등록합니다.
        /// 실제 Rigidbody2D 변경은 이 컴포넌트의 FixedUpdate에서만 수행합니다.
        /// </summary>
        public bool BeginChargeMovement(Vector2 direction, float speed)
        {
            if (rb == null || direction.sqrMagnitude <= Mathf.Epsilon || speed <= 0f)
            {
                return false;
            }

            chargeDirection = direction.normalized;
            chargeSpeed = speed;
            hasChargeMovement = true;
            return true;
        }

        /// <summary>
        /// 돌진 이동 우선권을 해제하고 속도를 정리합니다.
        /// </summary>
        public void EndChargeMovement()
        {
            hasChargeMovement = false;
            chargeDirection = Vector2.zero;
            chargeSpeed = 0f;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 돌진 종료 후 저장한 위치로 복귀하는 이동을 시작합니다.
        /// </summary>
        public void BeginReturnMovement(Vector2 targetPosition)
        {
            EndChargeMovement();
            returnPosition = targetPosition;
            hasReachedReturnPosition = false;
            hasReturnMovement = true;
        }

        /// <summary>
        /// 복귀 우선권을 해제해 일반 추적과 조사를 재개합니다.
        /// </summary>
        public void EndReturnMovement()
        {
            hasReturnMovement = false;
            hasReachedReturnPosition = false;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void FixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            if (hasChargeMovement)
            {
                // 돌진 중에는 추적·조사 상태가 Rigidbody2D 속도를 덮어쓰지 않습니다.
                rb.linearVelocity = chargeDirection * chargeSpeed;
                return;
            }

            if (hasReturnMovement)
            {
                UpdateReturnMovement();
                return;
            }

            if (playerTarget == null || enemyData == null)
            {
                ResetToIdle();
                return;
            }

            Vector2 playerPosition = playerTarget.position;
            float distanceSquared = (playerPosition - rb.position).sqrMagnitude;

            switch (currentState)
            {
                case ChaseState.Idle:
                    UpdateIdle(playerPosition, distanceSquared);
                    break;
                case ChaseState.Chase:
                    UpdateChase(playerPosition, distanceSquared);
                    break;
                case ChaseState.Investigate:
                    UpdateInvestigation(playerPosition, distanceSquared);
                    break;
                default:
                    ResetToIdle();
                    break;
            }
        }

        private void UpdateIdle(Vector2 playerPosition, float distanceSquared)
        {
            StopMovement();
            float detectionRange = Mathf.Max(0f, enemyData.DetectionRange);
            if (distanceSquared > detectionRange * detectionRange)
            {
                return;
            }

            lastKnownPlayerPosition = playerPosition;
            currentState = ChaseState.Chase;
            MoveTowardPlayer(playerPosition, distanceSquared);
        }

        private void UpdateChase(Vector2 playerPosition, float distanceSquared)
        {
            float detectionRange = Mathf.Max(0f, enemyData.DetectionRange);
            float pursuitEndRange = Mathf.Max(detectionRange, enemyData.PursuitEndRange);
            if (distanceSquared > pursuitEndRange * pursuitEndRange)
            {
                currentState = ChaseState.Investigate;
                investigationElapsedTime = 0f;
                hasReachedLastKnownPosition = false;
                MoveTowardLastKnownPosition();
                return;
            }

            lastKnownPlayerPosition = playerPosition;
            MoveTowardPlayer(playerPosition, distanceSquared);
        }

        private void UpdateInvestigation(Vector2 playerPosition, float distanceSquared)
        {
            float detectionRange = Mathf.Max(0f, enemyData.DetectionRange);
            if (distanceSquared <= detectionRange * detectionRange)
            {
                lastKnownPlayerPosition = playerPosition;
                currentState = ChaseState.Chase;
                investigationElapsedTime = 0f;
                hasReachedLastKnownPosition = false;
                MoveTowardPlayer(playerPosition, distanceSquared);
                return;
            }

            if (hasReachedLastKnownPosition)
            {
                UpdateInvestigationWait();
                return;
            }

            MoveTowardLastKnownPosition();
        }

        private void MoveTowardPlayer(Vector2 playerPosition, float distanceSquared)
        {
            float attackRange = Mathf.Max(0f, enemyData.AttackRange);
            if (distanceSquared <= attackRange * attackRange)
            {
                StopMovement();
                return;
            }

            Vector2 direction = playerPosition - rb.position;
            rb.linearVelocity = direction.normalized * enemyData.MoveSpeed;
        }

        private void MoveTowardLastKnownPosition()
        {
            Vector2 direction = lastKnownPlayerPosition - rb.position;
            float distance = direction.magnitude;
            if (distance <= InvestigationArrivalDistance || enemyData.MoveSpeed <= 0f)
            {
                StopMovement();
                hasReachedLastKnownPosition = true;
                investigationElapsedTime = 0f;
                return;
            }

            // 도착 직전에는 남은 거리에 맞춰 속도를 제한해 조사 지점을 지나치지 않게 합니다.
            float stepLimitedSpeed = distance / Time.fixedDeltaTime;
            float moveSpeed = Mathf.Min(enemyData.MoveSpeed, stepLimitedSpeed);
            rb.linearVelocity = direction / distance * moveSpeed;
        }

        private void UpdateReturnMovement()
        {
            if (hasReachedReturnPosition)
            {
                StopMovement();
                return;
            }

            Vector2 direction = returnPosition - rb.position;
            float distance = direction.magnitude;
            if (distance <= InvestigationArrivalDistance)
            {
                StopMovement();
                hasReachedReturnPosition = true;
                return;
            }

            if (enemyData == null || enemyData.MoveSpeed <= 0f)
            {
                StopMovement();
                return;
            }

            direction /= distance;
            if (IsPlayerBlockingReturnPath(direction, distance))
            {
                // 플레이어가 복귀 경로에 있으면 충돌로 밀지 않고 경로가 열릴 때까지 기다립니다.
                StopMovement();
                return;
            }

            float stepLimitedSpeed = distance / Time.fixedDeltaTime;
            float moveSpeed = Mathf.Min(enemyData.MoveSpeed, stepLimitedSpeed);
            rb.linearVelocity = direction * moveSpeed;
        }

        private bool IsPlayerBlockingReturnPath(Vector2 direction, float distance)
        {
            if (movementCollider == null || playerCollider == null || !playerCollider.enabled ||
                !playerCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            int hitCount = movementCollider.Cast(direction, returnContactFilter, returnCastResults, distance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = returnCastResults[i];
                if (hit.collider != playerCollider)
                {
                    continue;
                }

                Vector2 awayFromPlayer = (Vector2)transform.position - (Vector2)playerCollider.bounds.center;
                if (hit.distance <= ReturnCastOriginTolerance && Vector2.Dot(direction, awayFromPlayer) > 0f)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void UpdateInvestigationWait()
        {
            StopMovement();
            investigationElapsedTime += Time.fixedDeltaTime;
            if (investigationElapsedTime < enemyData.InvestigationDuration)
            {
                return;
            }

            currentState = ChaseState.Idle;
            investigationElapsedTime = 0f;
            hasReachedLastKnownPosition = false;
        }

        private void ResetToIdle()
        {
            StopMovement();
            currentState = ChaseState.Idle;
            investigationElapsedTime = 0f;
            hasReachedLastKnownPosition = false;
        }

        private void StopMovement()
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
}
