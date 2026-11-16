using UnityEngine;
using Prohect_DOD_Undergird.World;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어의 키 입력을 처리하고 이동 방향과 스프린트 상태를 전달합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(WeaponManager))]
    [RequireComponent(typeof(WeaponAttack))]
    [RequireComponent(typeof(PlayerMiningController))]
    public class PlayerInputHandler : MonoBehaviour
    {
        // 키 배열은 프레임마다 생성하지 않고 숫자 입력을 슬롯 순서에 맞춰 검사합니다.
        private static readonly KeyCode[] topRowWeaponKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        private static readonly KeyCode[] keypadWeaponKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0
        };

        private PlayerMovement movement;
        private WeaponManager weaponManager;
        private WeaponAttack weaponAttack;
        private PlayerMiningController miningController;

        public Vector2 MovementDirection { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAttackHeld { get; private set; }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            weaponManager = GetComponent<WeaponManager>();
            weaponAttack = GetComponent<WeaponAttack>();
            miningController = GetComponent<PlayerMiningController>();
        }

        private void Update()
        {
            if (movement != null)
            {
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

            HandleWeaponInput();

            // 좌클릭은 가까운 벽 채굴이 먼저 소비하고, 대상이 없을 때만 기존 무기 공격으로 전달합니다.
            bool primaryActionHeld = Input.GetMouseButton(0);
            bool isMiningAction = miningController != null &&
                                  miningController.HandlePrimaryAction(primaryActionHeld, Input.mousePosition);
            IsAttackHeld = primaryActionHeld && !isMiningAction;
            if (weaponAttack != null)
            {
                weaponAttack.SetAttackInput(IsAttackHeld);
            }
        }

        private void HandleWeaponInput()
        {
            if (weaponManager == null)
            {
                return;
            }

            for (int i = 0; i < topRowWeaponKeys.Length; i++)
            {
                // GetKeyDown은 키를 누른 프레임에만 true를 반환하므로 슬롯 변경을 한 번 요청합니다.
                if (Input.GetKeyDown(topRowWeaponKeys[i]) || Input.GetKeyDown(keypadWeaponKeys[i]))
                {
                    int slotNumber = i == 9 ? 10 : i + 1;
                    weaponManager.SelectSlot(slotNumber);
                    break;
                }
            }

            // 마우스 휠의 y축 부호만 사용해 한 번에 한 슬롯씩 이동합니다.
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0f)
            {
                weaponManager.SelectNextWeapon();
            }
            else if (scroll < 0f)
            {
                weaponManager.SelectPreviousWeapon();
            }
        }
    }
}
