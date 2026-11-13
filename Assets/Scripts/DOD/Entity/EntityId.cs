using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Prohect_DOD_Undergird.DOD.Entity.Tests")]

namespace Prohect_DOD_Undergird.DOD.Entity
{
    /// <summary>
    /// DOD 엔티티를 고유하게 식별하는 읽기 전용 구조체입니다.
    /// 슬롯의 배열 인덱스와 세대(Generation) 정보를 포함하여 슬롯 재사용 시 발생하는 유령 참조(Dangling Reference) 문제를 방지합니다.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        /// <summary>
        /// 무효한 엔티티 식별자 기본값입니다. Generation이 0입니다.
        /// </summary>
        public static readonly EntityId Invalid = default;

        /// <summary>
        /// 엔티티 슬롯의 배열 위치 인덱스입니다.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 엔티티 슬롯의 재사용 세대 번호입니다. 1부터 시작하며 0은 무효합니다.
        /// </summary>
        public int Generation { get; }

        /// <summary>
        /// 외부에서 임의로 유효한 ID를 생성하지 못하도록 내부 어셈블리(EntityManager)에서만 호출 가능한 생성자입니다.
        /// </summary>
        /// <param name="index">슬롯의 배열 인덱스</param>
        /// <param name="generation">슬롯의 세대 번호</param>
        internal EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        /// <summary>
        /// 두 EntityId의 값이 일치하는지 비교합니다.
        /// </summary>
        /// <param name="other">비교할 다른 EntityId</param>
        /// <returns>Index와 Generation이 모두 같으면 true, 다르면 false</returns>
        public bool Equals(EntityId other)
        {
            return Index == other.Index && Generation == other.Generation;
        }

        /// <summary>
        /// 객체가 EntityId이며 현재 인스턴스와 일치하는지 비교합니다.
        /// </summary>
        /// <param name="obj">비교할 객체</param>
        /// <returns>동일한 EntityId이면 true, 아니면 false</returns>
        public override bool Equals(object obj)
        {
            return obj is EntityId other && Equals(other);
        }

        /// <summary>
        /// EntityId의 해시 코드를 반환합니다.
        /// </summary>
        /// <returns>계산된 정수형 해시 코드</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Index * 397) ^ Generation;
            }
        }

        /// <summary>
        /// 엔티티 식별자 정보를 문자열로 표현합니다.
        /// </summary>
        /// <returns>인덱스와 세대가 포함된 문자열</returns>
        public override string ToString()
        {
            return $"EntityId(Index={Index}, Generation={Generation})";
        }

        /// <summary>
        /// 두 EntityId가 같은지 비교하는 동등 연산자입니다.
        /// </summary>
        public static bool operator ==(EntityId left, EntityId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 두 EntityId가 다른지 비교하는 부등 연산자입니다.
        /// </summary>
        public static bool operator !=(EntityId left, EntityId right)
        {
            return !left.Equals(right);
        }
    }
}
