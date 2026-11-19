using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>청크의 네 변을 식별합니다.</summary>
    public enum WorldChunkSide : byte
    {
        West,
        East,
        South,
        North
    }

    /// <summary>
    /// 절대 월드 셀 좌표를 기준으로 한 청크 주소입니다. 음수 좌표도 바닥 나눗셈으로 처리합니다.
    /// </summary>
    public readonly struct WorldChunkCoordinate : IEquatable<WorldChunkCoordinate>
    {
        public int X { get; }
        public int Y { get; }

        public WorldChunkCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static WorldChunkCoordinate FromWorldCell(WorldCellCoordinate worldCell, int chunkSize)
        {
            ValidateChunkSize(chunkSize);
            return new WorldChunkCoordinate(
                FloorDivide(worldCell.X, chunkSize),
                FloorDivide(worldCell.Y, chunkSize));
        }

        public WorldGridCoordinate ToLocalCell(WorldCellCoordinate worldCell, int chunkSize)
        {
            ValidateChunkSize(chunkSize);
            WorldChunkCoordinate actualChunk = FromWorldCell(worldCell, chunkSize);
            if (actualChunk != this)
            {
                throw new ArgumentOutOfRangeException(nameof(worldCell), "월드 셀이 이 청크에 속하지 않습니다.");
            }

            long localX = (long)worldCell.X - (long)X * chunkSize;
            long localY = (long)worldCell.Y - (long)Y * chunkSize;
            if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize ||
                localX > int.MaxValue || localY > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(worldCell), "월드 셀을 청크 로컬 좌표로 표현할 수 없습니다.");
            }

            return new WorldGridCoordinate((int)localX, (int)localY);
        }

        public WorldCellCoordinate ToWorldCell(WorldGridCoordinate localCell, int chunkSize)
        {
            ValidateChunkSize(chunkSize);
            if (localCell.X < 0 || localCell.X >= chunkSize || localCell.Y < 0 || localCell.Y >= chunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(localCell), "청크 로컬 좌표가 청크 경계를 벗어났습니다.");
            }

            long worldX = (long)X * chunkSize + localCell.X;
            long worldY = (long)Y * chunkSize + localCell.Y;
            if (worldX < int.MinValue || worldX > int.MaxValue ||
                worldY < int.MinValue || worldY > int.MaxValue)
            {
                throw new OverflowException("청크 로컬 좌표가 WorldCellCoordinate의 표현 범위를 벗어났습니다.");
            }

            return new WorldCellCoordinate((int)worldX, (int)worldY);
        }

        public bool Equals(WorldChunkCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldChunkCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public static bool operator ==(WorldChunkCoordinate left, WorldChunkCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WorldChunkCoordinate left, WorldChunkCoordinate right)
        {
            return !left.Equals(right);
        }

        private static int FloorDivide(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static void ValidateChunkSize(int chunkSize)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "청크 크기는 1 이상이어야 합니다.");
            }
        }
    }
}
