using System;
using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 장착 무기의 즉시 공격과 누르고 있을 때의 반복 주기를 관리합니다.
    /// </summary>
    [RequireComponent(typeof(WeaponManager))]
    public class WeaponAttack : MonoBehaviour
    {
        private WeaponManager weaponManager;
        private bool attackInputHeld;
        private bool requiresButtonRelease;
        private float nextAttackTime;
        private bool isSubscribed;

        /// <summary>
        /// 공격 실행 지점이 연결할 이벤트입니다. 공격 판정이나 피해 처리는 여기서 구현하지 않습니다.
        /// </summary>
        public event Action<WeaponData> OnAttackRequested;

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
            if (weaponManager != null && isSubscribed)
            {
                weaponManager.OnWeaponChanged -= HandleWeaponChanged;
                isSubscribed = false;
            }

            nextAttackTime = 0f;
            requiresButtonRelease = attackInputHeld;
        }

        private void Update()
        {
            // Time.time은 timeScale이 반영되는 경과 시간이므로 게임 일시정지 중 공격 주기도 멈춥니다.
            UpdateAttackCycle(Time.time);
        }

        /// <summary>
        /// 입력 핸들러가 전달한 버튼 상태를 반영하고, 새로 누른 경우 첫 공격을 바로 요청합니다.
        /// </summary>
        public void SetAttackInput(bool isHeld)
        {
            EnsureInitialized();

            if (!isActiveAndEnabled)
            {
                return;
            }

            if (!isHeld)
            {
                attackInputHeld = false;
                requiresButtonRelease = false;
                nextAttackTime = 0f;
                return;
            }

            if (attackInputHeld)
            {
                return;
            }

            attackInputHeld = true;
            if (requiresButtonRelease)
            {
                return;
            }

            RequestAttack(Time.time);
        }

        private void UpdateAttackCycle(float currentTime)
        {
            EnsureInitialized();

            if (!attackInputHeld || requiresButtonRelease || weaponManager == null)
            {
                return;
            }

            WeaponData currentWeapon = weaponManager.CurrentWeapon;
            if (currentWeapon == null || currentTime < nextAttackTime)
            {
                return;
            }

            RequestAttack(currentTime);
        }

        private void RequestAttack(float currentTime)
        {
            WeaponData currentWeapon = weaponManager != null ? weaponManager.CurrentWeapon : null;
            if (currentWeapon == null)
            {
                nextAttackTime = 0f;
                return;
            }

            OnAttackRequested?.Invoke(currentWeapon);
            // Inspector 제한과 별개로 런타임 데이터도 최소 간격을 지켜 0초 반복을 방지합니다.
            nextAttackTime = currentTime + Mathf.Max(currentWeapon.AttackInterval, 0.01f);
        }

        private void HandleWeaponChanged(WeaponData previousWeapon, WeaponData newWeapon)
        {
            nextAttackTime = 0f;
            if (attackInputHeld)
            {
                requiresButtonRelease = true;
            }
        }

        private void EnsureInitialized()
        {
            if (weaponManager == null)
            {
                weaponManager = GetComponent<WeaponManager>();
            }

            if (weaponManager == null || !isActiveAndEnabled || isSubscribed)
            {
                return;
            }

            weaponManager.OnWeaponChanged += HandleWeaponChanged;
            isSubscribed = true;
        }
    }
}
