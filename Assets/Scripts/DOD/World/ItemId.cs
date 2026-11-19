using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 아이템 정의와 슬롯에서 공통으로 사용하는 명시적 정수 식별자입니다.
    /// 값 0은 비어 있는 슬롯을 나타내기 위해 유효한 아이템 ID로 사용하지 않습니다.
    /// </summary>
    public readonly struct ItemId : IEquatable<ItemId>
    {
        /// <summary>에셋과 저장 데이터에서 유지할 명시적 정수 아이템 ID입니다.</summary>
        public int Value { get; }
        /// <summary>0보다 큰 ID가 지정되었으면 true입니다.</summary>
        public bool IsValid => Value > 0;

        /// <summary>정수 ID로 아이템 식별 값을 생성합니다. 0 이하 값은 유효하지 않습니다.</summary>
        public ItemId(int value)
        {
            Value = value;
        }

        /// <summary>두 아이템 ID의 정수 값이 같은지 비교합니다.</summary>
        public bool Equals(ItemId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ItemId other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);
        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
    }
}
