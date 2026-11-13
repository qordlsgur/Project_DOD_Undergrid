using UnityEngine;
using System.Collections;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어의 일반 이동과 대시를 처리하고 Rigidbody2D에 속도를 적용합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("이동 파라미터")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float sprintMultiplier = 2.0f;

        [Header("대시 파라미터")]
        [SerializeField] private float dashSpeed = 15.0f;
        [SerializeField] private float dashDuration = 0.2f;
        [SerializeField] private float dashCooldown = 1.0f;

        private Rigidbody2D rb;
        private PlayerInputHandler inputHandler;

        private bool canDash = true;
        private bool isDashing;

        private Vector2 lastMoveDirection = Vector2.zero;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        private void FixedUpdate()
        {
            if (rb == null || inputHandler == null || isDashing)
            {
                return;
            }

            Vector2 inputDirection = inputHandler.MovementDirection;

            // 입력이 있을 때만 마지막 이동 방향을 갱신합니다.
            if (inputDirection != Vector2.zero)
            {
                lastMoveDirection = inputDirection.normalized;
            }

            float effectiveSpeed = inputHandler.IsSprinting
                ? moveSpeed * sprintMultiplier
                : moveSpeed;

            // 대각선 이동 시에도 일정한 속도를 유지합니다.
            rb.linearVelocity = inputDirection.normalized * effectiveSpeed;
        }

        /// <summary>
        /// 현재 입력 방향 또는 마지막 이동 방향으로 대시를 시작합니다.
        /// </summary>
        public void OnDashRequested(Vector2 inputDirection)
        {
            if (rb == null || !canDash || isDashing)
            {
                return;
            }

            Vector2 dashDirection = inputDirection != Vector2.zero
                ? inputDirection.normalized
                : lastMoveDirection;

            // 이동 기록이 없으면 대시하지 않습니다.
            if (dashDirection == Vector2.zero)
            {
                return;
            }

            StartCoroutine(Dash(dashDirection));
        }

        private void OnDisable()
        {
            // 오브젝트가 비활성화되면 실행 중이던 코루틴이 중단되므로 상태 잠김을 방지하기 위해 초기화합니다.
            isDashing = false;
            canDash = true;

            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 일정 시간 동안 대시한 뒤 쿨다운을 적용합니다.
        /// </summary>
        private IEnumerator Dash(Vector2 direction)
        {
            canDash = false;
            isDashing = true;

            // 대시 속도를 적용하여 순간적인 돌진 가속을 부여합니다.
            rb.linearVelocity = direction.normalized * dashSpeed;

            // 대시 지속 시간 동안 대기합니다.
            yield return new WaitForSeconds(dashDuration);

            isDashing = false;

            // 대시 종료 시 이동 입력이 유지되고 있다면 속도를 해당 이동/스프린트 속도로 복귀하고, 입력이 없으면 정지합니다.
            if (rb != null)
            {
                if (inputHandler != null && inputHandler.MovementDirection != Vector2.zero)
                {
                    float resumeSpeed = inputHandler.IsSprinting
                        ? moveSpeed * sprintMultiplier
                        : moveSpeed;

                    rb.linearVelocity = inputHandler.MovementDirection.normalized * resumeSpeed;
                }
                else
                {
                    rb.linearVelocity = Vector2.zero;
                }
            }

            // 대시가 끝난 시점부터 쿨다운을 계산합니다.
            yield return new WaitForSeconds(dashCooldown);

            canDash = true;
        }
    }
}
