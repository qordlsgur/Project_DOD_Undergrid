using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>준비 상태를 포함해 절대 월드 셀 조회 결과를 나타냅니다.</summary>
    public enum WorldCellWalkability : byte
    {
        OutOfWorldBounds,
        Unloaded,
        Queued,
        Generating,
        Failed,
        Blocked,
        Walkable
    }

    /// <summary>
    /// 절대 월드 셀 좌표를 청크 데이터로 중계합니다.
    /// 셀 규칙은 WorldGrid에 위임하고, 미준비 상태를 빈 셀과 구분합니다.
    /// </summary>
    public sealed class WorldGridWorld
    {
        private readonly WorldChunkStore chunkStore;

        public WorldCellBounds WorldBounds => chunkStore.WorldBounds;
        public int ChunkSize => chunkStore.ChunkSize;
        public int LoadedChunkCount => chunkStore.LoadedChunkCount;
        public float CellWidth => chunkStore.CellWidth;
        public float CellHeight => chunkStore.CellHeight;

        /// <summary>준비된 청크에서 발생한 변경을 절대 월드 셀 좌표로 전달합니다.</summary>
        public event Action<WorldCellChange> CellChanged
        {
            add => chunkStore.CellChanged += value;
            remove => chunkStore.CellChanged -= value;
        }

        /// <summary>청크 저장소를 이용해 절대 좌표 조회와 데이터 변경을 중계합니다.</summary>
        public WorldGridWorld(WorldChunkStore chunkStore)
        {
            this.chunkStore = chunkStore ?? throw new ArgumentNullException(nameof(chunkStore));
        }

        /// <summary>Unity 월드 위치를 절대 셀 좌표로 변환하며 유한 월드 밖은 거부합니다.</summary>
        public bool TryWorldPositionToCell(float worldX, float worldY, out WorldCellCoordinate worldCell)
        {
            worldCell = default;
            if (!IsFinite(worldX) || !IsFinite(worldY))
            {
                return false;
            }

            double cellX = Math.Floor(((double)worldX - chunkStore.WorldOriginX) / chunkStore.CellWidth);
            double cellY = Math.Floor(((double)worldY - chunkStore.WorldOriginY) / chunkStore.CellHeight);
            if (cellX < int.MinValue || cellX > int.MaxValue ||
                cellY < int.MinValue || cellY > int.MaxValue ||
                !chunkStore.WorldBounds.Contains((long)cellX, (long)cellY))
            {
                return false;
            }

            worldCell = new WorldCellCoordinate((int)cellX, (int)cellY);
            return true;
        }

        /// <summary>유효 월드 셀의 물리적 중심 위치를 반환합니다. 청크 로딩 상태에는 의존하지 않습니다.</summary>
        public bool TryGetCellCenterWorldPosition(
            WorldCellCoordinate worldCell,
            out float centerX,
            out float centerY)
        {
            centerX = 0f;
            centerY = 0f;
            if (!chunkStore.WorldBounds.Contains(worldCell.X, worldCell.Y))
            {
                return false;
            }

            centerX = (float)(chunkStore.WorldOriginX + ((double)worldCell.X + 0.5d) * chunkStore.CellWidth);
            centerY = (float)(chunkStore.WorldOriginY + ((double)worldCell.Y + 0.5d) * chunkStore.CellHeight);
            return IsFinite(centerX) && IsFinite(centerY);
        }

        /// <summary>셀의 청크 데이터 준비 상태를 반환합니다.</summary>
        public WorldCellAccessStatus GetAccessStatus(WorldCellCoordinate worldCell)
        {
            if (!chunkStore.WorldBounds.Contains(worldCell.X, worldCell.Y))
            {
                return WorldCellAccessStatus.OutOfWorldBounds;
            }

            WorldChunkCoordinate chunk = WorldChunkCoordinate.FromWorldCell(worldCell, chunkStore.ChunkSize);
            switch (chunkStore.GetDataState(chunk))
            {
                case WorldChunkDataState.Ready: return WorldCellAccessStatus.Ready;
                case WorldChunkDataState.Queued: return WorldCellAccessStatus.Queued;
                case WorldChunkDataState.Generating: return WorldCellAccessStatus.Generating;
                case WorldChunkDataState.Failed: return WorldCellAccessStatus.Failed;
                case WorldChunkDataState.OutOfWorldBounds: return WorldCellAccessStatus.OutOfWorldBounds;
                default: return WorldCellAccessStatus.Unloaded;
            }
        }

        /// <summary>준비된 셀의 바닥 데이터를 읽습니다. 미준비 여부는 status로 구분합니다.</summary>
        public bool TryGetFloor(
            WorldCellCoordinate worldCell,
            out WorldFloorData floor,
            out WorldCellAccessStatus status)
        {
            floor = default;
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            return grid.TryGetFloor(localCell, out floor);
        }

        /// <summary>준비된 셀의 벽 데이터를 읽습니다. 미준비 여부는 status로 구분합니다.</summary>
        public bool TryGetWall(
            WorldCellCoordinate worldCell,
            out WorldWallData wall,
            out WorldCellAccessStatus status)
        {
            wall = default;
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            return grid.TryGetWall(localCell, out wall);
        }

        /// <summary>준비된 셀에 바닥을 배치하고 나머지 데이터 규칙은 WorldGrid에 위임합니다.</summary>
        public bool TryPlaceFloor(
            WorldCellCoordinate worldCell,
            FloorType type,
            out WorldCellAccessStatus status)
        {
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            return grid.TryPlaceFloor(localCell, type);
        }

        /// <summary>준비된 셀에 벽을 배치하고 나머지 데이터 규칙은 WorldGrid에 위임합니다.</summary>
        public bool TryPlaceWall(
            WorldCellCoordinate worldCell,
            WallType type,
            int maxDurability,
            out WorldCellAccessStatus status)
        {
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            bool hadWallBefore = grid.TryGetWall(localCell, out WorldWallData wallBefore);
            if (!grid.TryPlaceWall(localCell, type, maxDurability))
            {
                return false;
            }

            bool hasWallAfter = grid.TryGetWall(localCell, out WorldWallData wallAfter);
            chunkStore.RecordPlayerWallMutation(
                worldCell,
                hadWallBefore,
                wallBefore,
                hasWallAfter,
                wallAfter);
            return true;
        }

        /// <summary>광물 벽을 배치하고 청크 해제 후에도 유지되도록 플레이어 변경분에 기록합니다.</summary>
        public bool TryPlaceOreWall(
            WorldCellCoordinate worldCell,
            WorldOreDefinition definition,
            out WorldCellAccessStatus status)
        {
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            bool hadWallBefore = grid.TryGetWall(localCell, out WorldWallData wallBefore);
            if (!grid.TryPlaceOreWall(localCell, definition))
            {
                return false;
            }

            bool hasWallAfter = grid.TryGetWall(localCell, out WorldWallData wallAfter);
            chunkStore.RecordPlayerWallMutation(
                worldCell,
                hadWallBefore,
                wallBefore,
                hasWallAfter,
                wallAfter);
            return true;
        }

        /// <summary>준비된 셀의 벽에 피해를 전달하며 미준비 상태는 성공으로 오인되지 않습니다.</summary>
        public bool TryDamageWall(
            WorldCellCoordinate worldCell,
            int damage,
            out int remainingDurability,
            out bool wasDestroyed,
            out WorldCellAccessStatus status)
        {
            remainingDurability = 0;
            wasDestroyed = false;
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out status))
            {
                return false;
            }

            bool hadWallBefore = grid.TryGetWall(localCell, out WorldWallData wallBefore);
            if (!grid.TryDamageWall(localCell, damage, out remainingDurability, out wasDestroyed))
            {
                return false;
            }

            bool hasWallAfter = grid.TryGetWall(localCell, out WorldWallData wallAfter);
            chunkStore.RecordPlayerWallMutation(
                worldCell,
                hadWallBefore,
                wallBefore,
                hasWallAfter,
                wallAfter);
            return true;
        }

        /// <summary>준비 상태와 벽 유무를 함께 고려해 이동 가능 결과를 반환합니다.</summary>
        public WorldCellWalkability GetWalkability(WorldCellCoordinate worldCell)
        {
            if (!TryResolveReadyCell(worldCell, out WorldGrid grid, out WorldGridCoordinate localCell, out WorldCellAccessStatus status))
            {
                return ToWalkability(status);
            }

            return grid.IsWalkable(localCell)
                ? WorldCellWalkability.Walkable
                : WorldCellWalkability.Blocked;
        }

        private bool TryResolveReadyCell(
            WorldCellCoordinate worldCell,
            out WorldGrid grid,
            out WorldGridCoordinate localCell,
            out WorldCellAccessStatus status)
        {
            return chunkStore.TryGetReadyLocalCoordinate(worldCell, out grid, out localCell, out status);
        }

        private static WorldCellWalkability ToWalkability(WorldCellAccessStatus status)
        {
            switch (status)
            {
                case WorldCellAccessStatus.Queued: return WorldCellWalkability.Queued;
                case WorldCellAccessStatus.Generating: return WorldCellWalkability.Generating;
                case WorldCellAccessStatus.Failed: return WorldCellWalkability.Failed;
                case WorldCellAccessStatus.OutOfWorldBounds: return WorldCellWalkability.OutOfWorldBounds;
                default: return WorldCellWalkability.Unloaded;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
