using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어의 키 입력을 처리하고 이동 방향과 스프린트 상태를 전달합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerInputHandler : MonoBehaviour
    {
        private PlayerMovement movement;

        public Vector2 MovementDirection { get; private set; }
        public bool IsSprinting { get; private set; }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
        }

        private void Update()
        {
            if (movement == null) return;

            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            MovementDirection = new Vector2(moveX, moveY);

            IsSprinting =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);

            // Space 입력 시 현재 이동 방향으로 대시를 요청합니다.
            if (Input.GetKeyDown(KeyCode.Space))
            {
                movement.OnDashRequested(MovementDirection);
            }
        }
    }
}
