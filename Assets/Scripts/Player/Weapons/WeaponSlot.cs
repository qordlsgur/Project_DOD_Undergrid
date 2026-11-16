using System;
using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 인스펙터에서 설정하는 단축키 무기 슬롯 하나입니다.
    /// </summary>
    // Serializable은 일반 C# 데이터를 Unity 인스펙터와 씬 직렬화에 포함합니다.
    [Serializable]
    public sealed class WeaponSlot
    {
        [SerializeField] private WeaponData weaponData;

        public WeaponData WeaponData => weaponData;

        public void SetWeapon(WeaponData weaponData)
        {
            this.weaponData = weaponData;
        }
    }
}
