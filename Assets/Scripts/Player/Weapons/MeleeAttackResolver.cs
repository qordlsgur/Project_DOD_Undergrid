using System.Collections.Generic;
using UnityEngine;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 공격 요청을 2D 공간 판정으로 변환하고 적의 CombatReceiver에 피해를 전달합니다.
    /// </summary>
    [RequireComponent(typeof(WeaponAttack))]
    public class MeleeAttackResolver : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyLayerMask;

        private readonly List<Collider2D> overlapResults = new List<Collider2D>(16);
        private readonly HashSet<CombatReceiver> hitReceivers = new HashSet<CombatReceiver>();
        private WeaponAttack weaponAttack;
        private WeaponView weaponView;
        private bool isSubscribed;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            if (weaponAttack != null && isSubscribed)
            {
                weaponAttack.OnAttackRequested -= HandleAttackRequested;
                isSubscribed = false;
            }
        }

        /// <summary>
        /// WeaponAttack이 보낸 공격 요청을 현재 조준 방향으로 판정합니다.
        /// </summary>
        public void ResolveAttack(WeaponData weaponData)
        {
            EnsureInitialized();

            if (weaponData == null || weaponData.WeaponType != WeaponType.Melee || weaponView == null)
            {
                return;
            }

            // LateUpdate 전에 공격 입력이 들어온 경우에도 현재 프레임 커서 방향을 먼저 반영합니다.
            weaponView.RefreshAim();
            Vector2 aimDirection = weaponView.AimDirection;
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 attackOrigin = transform.position;
            hitReceivers.Clear();

            switch (weaponData.MeleeAttackType)
            {
                case MeleeAttackType.Arc:
                    ResolveArc(weaponData, attackOrigin, aimDirection);
                    break;
                case MeleeAttackType.Thrust:
                    ResolveThrust(weaponData, attackOrigin, aimDirection);
                    break;
            }
        }

        private void HandleAttackRequested(WeaponData weaponData)
        {
            ResolveAttack(weaponData);
        }

        private void ResolveArc(WeaponData weaponData, Vector2 origin, Vector2 aimDirection)
        {
            overlapResults.Clear();
            ContactFilter2D contactFilter = CreateEnemyContactFilter();
            float range = Mathf.Max(weaponData.AttackRange, 0.01f);
            // ContactFilter2D와 재사용 List 오버로드로 Enemy Layer 결과 전체를 받아 고정 버퍼 누락을 피합니다.
            Physics2D.OverlapCircle(origin, range, contactFilter, overlapResults);

            float halfAngle = Mathf.Clamp(weaponData.AttackAngle, 0f, 360f) * 0.5f;
            for (int i = 0; i < overlapResults.Count; i++)
            {
                Collider2D targetCollider = overlapResults[i];
                if (targetCollider == null || !IsInsideArc(targetCollider, origin, aimDirection, halfAngle))
                {
                    continue;
                }

                ApplyDamageOnce(targetCollider, weaponData.AttackPower);
            }
        }

        private void ResolveThrust(WeaponData weaponData, Vector2 origin, Vector2 aimDirection)
        {
            float range = Mathf.Max(weaponData.ThrustRange, 0.01f);
            float width = Mathf.Max(weaponData.ThrustWidth, 0.01f);
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            Vector2 boxCenter = origin + aimDirection * (range * 0.5f);

            overlapResults.Clear();
            ContactFilter2D contactFilter = CreateEnemyContactFilter();
            Physics2D.OverlapBox(
                boxCenter,
                new Vector2(range, width),
                angle,
                contactFilter,
                overlapResults);

            for (int i = 0; i < overlapResults.Count; i++)
            {
                Collider2D targetCollider = overlapResults[i];
                if (targetCollider != null)
                {
                    ApplyDamageOnce(targetCollider, weaponData.AttackPower);
                }
            }
        }

        private bool IsInsideArc(
            Collider2D targetCollider,
            Vector2 origin,
            Vector2 aimDirection,
            float halfAngle)
        {
            // ClosestPoint는 Collider 표면 중 원점에 가장 가까운 점을 반환해 경계에 걸친 적도 판정합니다.
            Vector2 closestPoint = targetCollider.ClosestPoint(origin);
            Vector2 targetDirection = closestPoint - origin;

            // 공격 원점 안에 겹친 대상은 방향이 정의되지 않으므로 부채꼴 안으로 처리합니다.
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector2.Angle(aimDirection, targetDirection) <= halfAngle;
        }

        private void ApplyDamageOnce(Collider2D targetCollider, int damage)
        {
            // 자식 Collider도 부모의 공통 CombatReceiver로 모아 한 개체 단위로 필터링합니다.
            CombatReceiver receiver = targetCollider.GetComponentInParent<CombatReceiver>();
            if (receiver == null || receiver.IsDead || !hitReceivers.Add(receiver))
            {
                return;
            }

            receiver.ReceiveDamage(damage);
        }

        private ContactFilter2D CreateEnemyContactFilter()
        {
            ContactFilter2D contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(enemyLayerMask);
            contactFilter.useTriggers = true;
            return contactFilter;
        }

        private void EnsureInitialized()
        {
            if (weaponAttack == null)
            {
                weaponAttack = GetComponent<WeaponAttack>();
            }

            if (weaponView == null)
            {
                weaponView = GetComponentInChildren<WeaponView>();
            }

            if (weaponAttack == null || !isActiveAndEnabled || isSubscribed)
            {
                return;
            }

            weaponAttack.OnAttackRequested += HandleAttackRequested;
            isSubscribed = true;
        }
    }
}
