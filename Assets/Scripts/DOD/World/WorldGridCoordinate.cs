using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 정수 좌표계에서 월드 셀 하나를 식별하는 변경 불가 값입니다.
    /// </summary>
    public readonly struct WorldGridCoordinate : IEquatable<WorldGridCoordinate>
    {
        /// <summary>좌우 방향의 셀 인덱스입니다.</summary>
        public int X { get; }

        /// <summary>상하 방향의 셀 인덱스입니다.</summary>
        public int Y { get; }

        /// <summary>
        /// 두 정수 인덱스로 셀 좌표를 생성합니다. 월드 경계 유효성은 WorldGrid가 검사합니다.
        /// </summary>
        public WorldGridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>두 좌표의 X와 Y가 모두 같은지 비교합니다.</summary>
        public bool Equals(WorldGridCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <summary>객체가 같은 월드 셀 좌표인지 비교합니다.</summary>
        public override bool Equals(object obj)
        {
            return obj is WorldGridCoordinate other && Equals(other);
        }

        /// <summary>좌표를 해시 기반 자료구조에서 사용할 정수 해시값으로 변환합니다.</summary>
        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        /// <summary>두 좌표가 같은 셀을 가리키는지 비교합니다.</summary>
        public static bool operator ==(WorldGridCoordinate left, WorldGridCoordinate right)
        {
            return left.Equals(right);
        }

        /// <summary>두 좌표가 서로 다른 셀을 가리키는지 비교합니다.</summary>
        public static bool operator !=(WorldGridCoordinate left, WorldGridCoordinate right)
        {
            return !left.Equals(right);
        }
    }
}
