using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// 곡괭이 등 채굴 장비가 제공하는 고정 능력치를 보관합니다.
    /// 런타임의 월드와 채굴 입력 상태는 보유하지 않습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldMiningToolData", menuName = "Game/World/Mining Tool Data")]
    public sealed class WorldMiningToolData : ScriptableObject
    {
        [SerializeField] private string toolName;
        [SerializeField, Min(1)] private int miningPower = 2;
        [SerializeField, Min(0.01f)] private float miningInterval = 0.5f;
        [SerializeField, Min(0f)] private float miningRange = 1.5f;

        public string ToolName => toolName;
        public int MiningPower => miningPower;
        public float MiningInterval => miningInterval;
        public float MiningRange => miningRange;
    }
}
