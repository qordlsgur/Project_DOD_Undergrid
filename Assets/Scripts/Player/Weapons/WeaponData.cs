using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 무기별로 생성하는 ScriptableObject 기반의 기본 데이터입니다.
    /// </summary>
    // CreateAssetMenu를 통해 Project 창의 생성 메뉴에서 무기별 에셋을 만들 수 있습니다.
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Weapons/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] private string weaponName;
        [SerializeField] private Sprite icon;
        [SerializeField, Min(0)] private int attackPower;
        [SerializeField, Min(0.01f)] private float attackInterval = 0.5f;
        [SerializeField] private WeaponType weaponType;
        [SerializeField] private MeleeAttackType meleeAttackType;
        [SerializeField, Min(0.01f)] private float attackRange = 1.5f;
        [SerializeField, Range(0f, 360f)] private float attackAngle = 90f;
        [SerializeField, Min(0.01f)] private float thrustRange = 2f;
        [SerializeField, Min(0.01f)] private float thrustWidth = 0.35f;

        public string WeaponName => weaponName;
        public Sprite Icon => icon;
        public int AttackPower => attackPower;
        public float AttackInterval => attackInterval;
        public WeaponType WeaponType => weaponType;
        public MeleeAttackType MeleeAttackType => meleeAttackType;
        public float AttackRange => attackRange;
        public float AttackAngle => attackAngle;
        public float ThrustRange => thrustRange;
        public float ThrustWidth => thrustWidth;
    }
}
