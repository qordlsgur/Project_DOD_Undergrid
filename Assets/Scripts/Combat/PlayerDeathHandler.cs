using UnityEngine;
using Prohect_DOD_Undergird.DOD.Entity;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// CombatReceiver의 사망 통지를 Unity 플레이어 입력 및 이동 정지로 연결합니다.
    /// </summary>
    [RequireComponent(typeof(CombatReceiver))]
    public class PlayerDeathHandler : MonoBehaviour
    {
        [Header("플레이어 정지 대상")]
        // Combat 어셈블리에서 Player 입력·이동 어셈블리를 직접 참조하지 않도록 Inspector로 주입합니다.
        [SerializeField] private Behaviour playerInputHandler;
        [SerializeField] private Behaviour playerMovement;
        [Header("무기 중단 대상")]
        [SerializeField] private Behaviour weaponView;
        [SerializeField] private Behaviour weaponAttack;
        [SerializeField] private Behaviour meleeAttackResolver;

        private CombatReceiver combatReceiver;
        // 활성화 반복 시 같은 이벤트에 중복 등록하지 않기 위한 구독 상태입니다.
        private bool isSubscribed;
        // 사망 이벤트가 여러 경로에서 전달되어도 정지 처리를 한 번만 수행합니다.
        private bool isDeathHandled;

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
        }

        private void SubscribeToDeath()
        {
            if (isSubscribed || combatReceiver == null)
            {
                return;
            }

            combatReceiver.OnDied += HandleDeath;
            isSubscribed = true;

            // 비활성화되었다가 사망 후 다시 켜지는 경우에도 상태를 놓치지 않습니다.
            if (combatReceiver.EntityId != EntityId.Invalid && combatReceiver.IsDead)
            {
                HandleDeath();
            }
        }

        private void UnsubscribeFromDeath()
        {
            if (!isSubscribed || combatReceiver == null)
            {
                return;
            }

            combatReceiver.OnDied -= HandleDeath;
            isSubscribed = false;
        }

        private void HandleDeath()
        {
            if (isDeathHandled)
            {
                return;
            }

            isDeathHandled = true;

            if (playerInputHandler != null)
            {
                playerInputHandler.enabled = false;
            }

            if (playerMovement != null)
            {
                playerMovement.enabled = false;
            }

            // 조준 갱신과 반복 공격 실행기를 함께 중단해 사망 이후 무기 동작을 막습니다.
            if (weaponView != null)
            {
                weaponView.enabled = false;
            }

            if (weaponAttack != null)
            {
                weaponAttack.enabled = false;
            }

            if (meleeAttackResolver != null)
            {
                meleeAttackResolver.enabled = false;
            }
        }
    }
}
