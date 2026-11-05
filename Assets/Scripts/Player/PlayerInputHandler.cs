using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어의 입력(키 입력, Shift 등)을 처리하고 외부로 이동 방향 및 스프린트 상태를 전달합니다.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Vector2 movementDirection;
        [SerializeField] private bool isSprinting;

        // 외부 접근용 프로퍼티 제공
        public Vector2 MovementDirection => movementDirection;
        public bool IsSprinting => isSprinting;

        void Update()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            // 이동 방향만 원본 그대로 전달 (정규화 및 속도 처리는 Movement에서 수행)
            movementDirection = new Vector2(moveX, moveY);

            isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
    }
}