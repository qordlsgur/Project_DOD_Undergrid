using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 원점 기준의 절대 월드 셀 좌표입니다. WorldGridCoordinate의 배열 로컬 좌표와 구분합니다.
    /// </summary>
    public readonly struct WorldCellCoordinate : IEquatable<WorldCellCoordinate>
    {
        public int X { get; }
        public int Y { get; }

        public WorldCellCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(WorldCellCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldCellCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(WorldCellCoordinate left, WorldCellCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldCellCoordinate left, WorldCellCoordinate right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 월드에서 유효한 셀 좌표의 직사각 범위입니다. 최대값은 배타 경계로 보관해 int 최댓값 셀도 표현합니다.
    /// </summary>
    public readonly struct WorldCellBounds
    {
        public long MinimumX { get; }
        public long MinimumY { get; }
        public long MaximumXExclusive { get; }
        public long MaximumYExclusive { get; }

        public WorldCellBounds(
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive)
        {
            if (maximumXExclusive <= minimumX)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumXExclusive), "월드 셀 범위의 X 최대 경계는 시작점보다 커야 합니다.");
            }

            if (maximumYExclusive <= minimumY)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumYExclusive), "월드 셀 범위의 Y 최대 경계는 시작점보다 커야 합니다.");
            }

            MinimumX = minimumX;
            MinimumY = minimumY;
            MaximumXExclusive = maximumXExclusive;
            MaximumYExclusive = maximumYExclusive;
        }

        /// <summary>절대 셀 좌표가 월드의 최소 포함·최대 배타 경계 안에 있는지 확인합니다.</summary>
        public bool Contains(long x, long y)
        {
            return x >= MinimumX && x < MaximumXExclusive &&
                   y >= MinimumY && y < MaximumYExclusive;
        }
    }
}
