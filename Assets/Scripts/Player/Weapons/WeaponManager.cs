using System;
using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 플레이어의 열 개 무기 슬롯과 현재 장착 상태를 관리합니다.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        public const int SlotCount = 10;

        [SerializeField] private WeaponSlot[] slots = new WeaponSlot[SlotCount];

        private int selectedSlotIndex;
        private WeaponData currentWeapon;

        public WeaponData CurrentWeapon => currentWeapon;
        public int SelectedSlotNumber => selectedSlotIndex + 1;

        /// <summary>
        /// 실제 장착 무기가 바뀐 경우에만 이전 무기와 새 무기를 전달합니다.
        /// </summary>
        public event Action<WeaponData, WeaponData> OnWeaponChanged;

        private void Awake()
        {
            EnsureSlots();
            currentWeapon = slots[selectedSlotIndex].WeaponData;
        }

        private void OnValidate()
        {
            EnsureSlots();
        }

        /// <summary>
        /// 지정한 1~10번 슬롯을 선택합니다. 빈 슬롯은 비무장 상태로 전환합니다.
        /// </summary>
        public bool SelectSlot(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > SlotCount)
            {
                return false;
            }

            int newSlotIndex = slotNumber - 1;
            WeaponData newWeapon = slots[newSlotIndex].WeaponData;
            selectedSlotIndex = newSlotIndex;
            SetCurrentWeapon(newWeapon);
            return true;
        }

        /// <summary>
        /// 현재 슬롯을 기준으로 다음 등록 무기 슬롯을 선택합니다.
        /// </summary>
        public bool SelectNextWeapon()
        {
            return SelectRegisteredWeapon(1);
        }

        /// <summary>
        /// 현재 슬롯을 기준으로 이전 등록 무기 슬롯을 선택합니다.
        /// </summary>
        public bool SelectPreviousWeapon()
        {
            return SelectRegisteredWeapon(-1);
        }

        /// <summary>
        /// 지정 슬롯에 무기를 설정합니다. 현재 선택 슬롯의 무기가 바뀌면 변경 이벤트를 알립니다.
        /// </summary>
        public bool SetSlotWeapon(int slotNumber, WeaponData weaponData)
        {
            if (slotNumber < 1 || slotNumber > SlotCount)
            {
                return false;
            }

            int slotIndex = slotNumber - 1;
            slots[slotIndex].SetWeapon(weaponData);

            if (slotIndex == selectedSlotIndex)
            {
                SetCurrentWeapon(weaponData);
            }

            return true;
        }

        private bool SelectRegisteredWeapon(int direction)
        {
            for (int step = 1; step <= SlotCount; step++)
            {
                int candidateIndex = (selectedSlotIndex + direction * step) % SlotCount;
                if (candidateIndex < 0)
                {
                    candidateIndex += SlotCount;
                }

                if (slots[candidateIndex].WeaponData == null)
                {
                    continue;
                }

                selectedSlotIndex = candidateIndex;
                SetCurrentWeapon(slots[candidateIndex].WeaponData);
                return true;
            }

            return false;
        }

        private void SetCurrentWeapon(WeaponData newWeapon)
        {
            if (currentWeapon == newWeapon)
            {
                return;
            }

            WeaponData previousWeapon = currentWeapon;
            currentWeapon = newWeapon;
            OnWeaponChanged?.Invoke(previousWeapon, currentWeapon);
        }

        private void EnsureSlots()
        {
            if (slots == null || slots.Length != SlotCount)
            {
                WeaponSlot[] resizedSlots = new WeaponSlot[SlotCount];
                if (slots != null)
                {
                    Array.Copy(slots, resizedSlots, Mathf.Min(slots.Length, SlotCount));
                }

                slots = resizedSlots;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new WeaponSlot();
                }
            }
        }
    }
}
