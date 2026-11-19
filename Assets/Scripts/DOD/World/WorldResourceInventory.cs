using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 플레이어가 획득한 월드 자원 수량을 Unity 객체와 분리해 보관합니다.
    /// 배열 인덱스는 자원 enum 값과 같아 자원 조회가 일정한 비용으로 처리됩니다.
    /// </summary>
    public sealed class WorldResourceInventory
    {
        private static readonly int ResourceTypeCapacity = GetResourceTypeCapacity();
        private readonly int[] amounts = new int[ResourceTypeCapacity];

        /// <summary>지정한 자원의 현재 보유 수량을 반환합니다.</summary>
        public int GetAmount(WorldResourceType resourceType)
        {
            int index = (int)resourceType;
            return IsValidResourceIndex(resourceType, index) ? amounts[index] : 0;
        }

        /// <summary>양수 수량을 추가하며 잘못된 종류나 int 범위 초과는 거부합니다.</summary>
        public bool TryAdd(WorldResourceType resourceType, int amount)
        {
            int index = (int)resourceType;
            if (!IsValidResourceIndex(resourceType, index) || amount <= 0 ||
                amounts[index] > int.MaxValue - amount)
            {
                return false;
            }

            amounts[index] += amount;
            return true;
        }

        private static int GetResourceTypeCapacity()
        {
            Array resourceTypes = Enum.GetValues(typeof(WorldResourceType));
            int maximumIndex = 0;
            foreach (WorldResourceType resourceType in resourceTypes)
            {
                maximumIndex = Math.Max(maximumIndex, (int)resourceType);
            }

            return maximumIndex + 1;
        }

        private static bool IsValidResourceIndex(WorldResourceType resourceType, int index)
        {
            return index > 0 && index < ResourceTypeCapacity &&
                   Enum.IsDefined(typeof(WorldResourceType), resourceType);
        }
    }
}
