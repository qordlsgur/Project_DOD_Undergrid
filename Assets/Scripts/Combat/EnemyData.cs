using UnityEngine;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// 적 종류별로 공유하는 변경되지 않는 설정 데이터를 보관합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Enemies/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string enemyName;
        [SerializeField, Min(1)] private int maxHealth = 1;
        [SerializeField, Min(0f)] private float moveSpeed;
        [SerializeField, Min(0)] private int attackPower;
        [SerializeField, Min(0f)] private float attackRange;
        [SerializeField, Min(0.01f)] private float attackInterval = 0.5f;
        [SerializeField, Min(0f)] private float detectionRange = 5f;
        [SerializeField, Min(0f)] private float pursuitEndRange = 6f;
        [SerializeField, Min(0f)] private float investigationDuration = 1.5f;
        [SerializeField, Min(0f)] private float chargePreparationTime = 0.4f;
        [SerializeField, Min(0.01f)] private float chargeSpeed = 7.5f;
        [SerializeField, Min(0.01f)] private float chargeDuration = 0.35f;

        public string EnemyName => enemyName;
        public int MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public int AttackPower => attackPower;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float DetectionRange => detectionRange;
        public float PursuitEndRange => pursuitEndRange;
        public float InvestigationDuration => investigationDuration;
        public float ChargePreparationTime => chargePreparationTime;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeDuration => chargeDuration;
    }
}
