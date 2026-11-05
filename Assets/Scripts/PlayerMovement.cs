using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어 WASD 이동 기능을 처리하는 컴포넌트입니다.
    /// </summary>
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private Rigidbody2D rb;

        // 입력 값을 저장하기 위한 변수
        private Vector2 _movement;

        void Update()
        {
            // WASD 입력 받기 (Update에서 처리)
            float moveX = Input.GetAxis("Horizontal");
            float moveY = Input.GetAxis("Vertical");

            // 수정: movement 계산은 Update()에서만 수행, 클래스 내부 필드에 저장
            _movement = new Vector2(moveX, moveY).normalized * moveSpeed;
        }

        void FixedUpdate()
        {
            // 이동 방향 계산 및 Rigidbody에 속도 적용 (FixedUpdate에서 처리)
            if (rb != null)
            {
                // 수정: rb.linearVelocity에 _movement 값 적용 (기존 rb.velocity 사용 금지)
                rb.linearVelocity = _movement;
            }
        }
    }
}
