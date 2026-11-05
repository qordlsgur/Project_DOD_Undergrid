using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// Rigidbody2D를 통해 플레이어의 이동을 물리 엔진에 적용합니다.
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float sprintMultiplier = 2.0f;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private PlayerInputHandler inputHandler;

        void Awake()
        {
            // Rigidbody2D 컴포넌트를 자동으로 할당
            rb = GetComponent<Rigidbody2D>();

            // 같은 GameObject의 PlayerInputHandler 컴포넌트를 가져옴
            inputHandler = GetComponent<PlayerInputHandler>();
        }

        void FixedUpdate()
        {
            if (rb == null || inputHandler == null) return;

            float effectiveSpeed = moveSpeed;
            if (inputHandler.IsSprinting)
            {
                effectiveSpeed *= sprintMultiplier;
            }

            // 입력 방향 정규화 및 속도 적용
            Vector2 movement = inputHandler.MovementDirection.normalized * effectiveSpeed;

            // 입력이 없으면 MovementDirection이 자동으로 (0, 0)이므로 별도 조건 필요 X
            rb.linearVelocity = movement;
        }
    }
}