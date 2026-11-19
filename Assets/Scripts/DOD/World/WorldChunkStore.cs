using System;
using System.Collections.Generic;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>유한 월드 범위와 청크 데이터 준비 단계를 구분합니다.</summary>
    public enum WorldChunkDataState : byte
    {
        OutOfWorldBounds,
        Unloaded,
        Queued,
        Generating,
        Ready,
        Failed
    }

    /// <summary>데이터 준비와 독립적으로 관리하는 청크 화면 표시 상태입니다.</summary>
    public enum WorldChunkPresentationState : byte
    {
        NotDisplayed,
        Displayed
    }

    /// <summary>준비된 청크에서 셀 데이터에 접근할 수 있는지 나타냅니다.</summary>
    public enum WorldCellAccessStatus : byte
    {
        Ready,
        OutOfWorldBounds,
        Unloaded,
        Queued,
        Generating,
        Failed
    }

    /// <summary>절대 월드 셀 좌표에서 발생한 청크 데이터 변경 정보입니다.</summary>
    public readonly struct WorldCellChange
    {
        public WorldCellCoordinate Coordinate { get; }
        public WorldGridLayer Layer { get; }
        public WorldGridChangeKind Kind { get; }

        internal WorldCellChange(
            WorldCellCoordinate coordinate,
            WorldGridLayer layer,
            WorldGridChangeKind kind)
        {
            Coordinate = coordinate;
            Layer = layer;
            Kind = kind;
        }
    }

    /// <summary>
    /// 월드 bounds 안의 청크별 WorldGrid를 보유하고 데이터 생명주기를 관리합니다.
    /// 화면 표시와 지형 규칙은 이 저장소의 책임에 포함하지 않습니다.
    /// </summary>
    public sealed class WorldChunkStore
    {
        private readonly struct WallStateSnapshot
        {
            public readonly bool HasWall;
            public readonly WallType Type;
            public readonly int MaxDurability;
            public readonly int CurrentDurability;
            public readonly WorldResourceType ResourceType;
            public readonly int ResourceAmount;

            public WallStateSnapshot(bool hasWall, WorldWallData wall)
            {
                HasWall = hasWall;
                Type = hasWall ? wall.Type : WallType.None;
                MaxDurability = hasWall ? wall.MaxDurability : 0;
                CurrentDurability = hasWall ? wall.CurrentDurability : 0;
                ResourceType = hasWall ? wall.ResourceType : WorldResourceType.None;
                ResourceAmount = hasWall ? wall.ResourceAmount : 0;
            }

            public bool Matches(WallStateSnapshot other)
            {
                return HasWall == other.HasWall && Type == other.Type &&
                       MaxDurability == other.MaxDurability && CurrentDurability == other.CurrentDurability &&
                       ResourceType == other.ResourceType && ResourceAmount == other.ResourceAmount;
            }
        }

        private readonly struct WallMutation
        {
            public readonly WallStateSnapshot Baseline;
            public readonly WallStateSnapshot Current;

            public WallMutation(WallStateSnapshot baseline, WallStateSnapshot current)
            {
                Baseline = baseline;
                Current = current;
            }
        }

        private sealed class ChunkRecord
        {
            public WorldChunkDataState DataState;
            public WorldChunkPresentationState PresentationState;
            public WorldGrid Grid;
            public WorldCellCoordinate GridWorldOrigin;
            public Action<WorldGridChange> ChangeHandler;
        }

        private readonly Dictionary<WorldChunkCoordinate, ChunkRecord> chunks =
            new Dictionary<WorldChunkCoordinate, ChunkRecord>();
        private readonly Dictionary<WorldChunkCoordinate, Dictionary<WorldCellCoordinate, WallMutation>> wallMutations =
            new Dictionary<WorldChunkCoordinate, Dictionary<WorldCellCoordinate, WallMutation>>();
        private readonly WorldCellBounds worldBounds;
        private readonly int chunkSize;
        private readonly float cellWidth;
        private readonly float cellHeight;
        private readonly float worldOriginX;
        private readonly float worldOriginY;
        private int loadedChunkCount;

        public WorldCellBounds WorldBounds => worldBounds;
        public int ChunkSize => chunkSize;
        public int LoadedChunkCount => loadedChunkCount;
        public float CellWidth => cellWidth;
        public float CellHeight => cellHeight;
        public float WorldOriginX => worldOriginX;
        public float WorldOriginY => worldOriginY;

        /// <summary>청크 내부 WorldGrid에서 변경된 셀을 절대 월드 좌표로 전달합니다.</summary>
        public event Action<WorldCellChange> CellChanged;

        /// <summary>청크에 저장된 플레이어 벽 변경 셀 수를 반환합니다.</summary>
        public int GetRecordedWallMutationCount(WorldChunkCoordinate chunk)
        {
            return wallMutations.TryGetValue(chunk, out Dictionary<WorldCellCoordinate, WallMutation> mutations)
                ? mutations.Count
                : 0;
        }

        /// <summary>
        /// 셀 범위, 청크 크기 및 절대 셀 좌표 0의 월드 위치를 설정합니다.
        /// 월드 bounds는 청크가 로드되지 않은 동안에도 유효 범위를 정의합니다.
        /// </summary>
        public WorldChunkStore(
            WorldCellBounds worldBounds,
            int chunkSize,
            float cellWidth,
            float cellHeight,
            float worldOriginX = 0f,
            float worldOriginY = 0f)
        {
            if (chunkSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "청크 크기는 1 이상이어야 합니다.");
            }

            if (!IsFinitePositive(cellWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(cellWidth), "셀 너비는 유한한 양수여야 합니다.");
            }

            if (!IsFinitePositive(cellHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(cellHeight), "셀 높이는 유한한 양수여야 합니다.");
            }

            if (!IsFinite(worldOriginX))
            {
                throw new ArgumentOutOfRangeException(nameof(worldOriginX), "월드 X 원점은 유한한 좌표여야 합니다.");
            }

            if (!IsFinite(worldOriginY))
            {
                throw new ArgumentOutOfRangeException(nameof(worldOriginY), "월드 Y 원점은 유한한 좌표여야 합니다.");
            }

            if (worldBounds.MinimumX < int.MinValue || worldBounds.MinimumY < int.MinValue ||
                worldBounds.MaximumXExclusive > (long)int.MaxValue + 1L ||
                worldBounds.MaximumYExclusive > (long)int.MaxValue + 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(worldBounds), "월드 셀 범위는 WorldCellCoordinate가 표현할 수 있는 영역 안에 있어야 합니다.");
            }

            this.worldBounds = worldBounds;
            this.chunkSize = chunkSize;
            this.cellWidth = cellWidth;
            this.cellHeight = cellHeight;
            this.worldOriginX = worldOriginX;
            this.worldOriginY = worldOriginY;
        }

        /// <summary>절대 셀 좌표가 월드 안에 있을 때 청크 주소와 명목 로컬 좌표를 반환합니다.</summary>
        public bool TryGetChunkAddress(
            WorldCellCoordinate worldCell,
            out WorldChunkCoordinate chunk,
            out WorldGridCoordinate chunkLocalCell)
        {
            chunk = default;
            chunkLocalCell = default;
            if (!worldBounds.Contains(worldCell.X, worldCell.Y))
            {
                return false;
            }

            chunk = WorldChunkCoordinate.FromWorldCell(worldCell, chunkSize);
            long localX = (long)worldCell.X - (long)chunk.X * chunkSize;
            long localY = (long)worldCell.Y - (long)chunk.Y * chunkSize;
            chunkLocalCell = new WorldGridCoordinate((int)localX, (int)localY);
            return true;
        }

        /// <summary>청크가 월드와 겹치는지 확인하고 현재 데이터 상태를 반환합니다.</summary>
        public WorldChunkDataState GetDataState(WorldChunkCoordinate chunk)
        {
            if (!TryGetChunkCellBounds(chunk, out _, out _, out _, out _))
            {
                return WorldChunkDataState.OutOfWorldBounds;
            }

            return chunks.TryGetValue(chunk, out ChunkRecord record)
                ? record.DataState
                : WorldChunkDataState.Unloaded;
        }

        /// <summary>등록된 청크의 표시 상태를 반환합니다. 아직 등록되지 않은 청크는 false입니다.</summary>
        public bool TryGetPresentationState(
            WorldChunkCoordinate chunk,
            out WorldChunkPresentationState state)
        {
            state = WorldChunkPresentationState.NotDisplayed;
            if (!chunks.TryGetValue(chunk, out ChunkRecord record))
            {
                return false;
            }

            state = record.PresentationState;
            return true;
        }

        /// <summary>현재 표시 상태인 청크 좌표의 스냅샷을 반환합니다.</summary>
        public WorldChunkCoordinate[] GetDisplayedChunkCoordinates()
        {
            var displayedChunks = new List<WorldChunkCoordinate>();
            foreach (KeyValuePair<WorldChunkCoordinate, ChunkRecord> entry in chunks)
            {
                if (entry.Value.DataState == WorldChunkDataState.Ready &&
                    entry.Value.PresentationState == WorldChunkPresentationState.Displayed)
                {
                    displayedChunks.Add(entry.Key);
                }
            }

            return displayedChunks.ToArray();
        }

        /// <summary>현재 저장소가 추적하는 청크 주소의 스냅샷을 반환합니다.</summary>
        public WorldChunkCoordinate[] GetKnownChunkCoordinates()
        {
            var knownChunks = new WorldChunkCoordinate[chunks.Count];
            int index = 0;
            foreach (WorldChunkCoordinate chunk in chunks.Keys)
            {
                knownChunks[index++] = chunk;
            }

            return knownChunks;
        }

        /// <summary>아직 생성되지 않은 대기 요청만 취소해 Unloaded 상태로 되돌립니다.</summary>
        public bool TryCancelQueuedChunk(WorldChunkCoordinate chunk)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Queued)
            {
                return false;
            }

            chunks.Remove(chunk);
            return true;
        }

        /// <summary>Grid 데이터가 없는 실패 기록만 제거해 이후 요청이 Unloaded에서 시작되게 합니다.</summary>
        public bool TryForgetFailedChunk(WorldChunkCoordinate chunk)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Failed)
            {
                return false;
            }

            chunks.Remove(chunk);
            return true;
        }

        /// <summary>유효한 미로드 청크 또는 실패한 청크를 생성 대기 상태로 전환합니다.</summary>
        public bool TryQueueChunk(WorldChunkCoordinate chunk)
        {
            if (!TryGetChunkCellBounds(chunk, out _, out _, out _, out _))
            {
                return false;
            }

            if (!chunks.TryGetValue(chunk, out ChunkRecord record))
            {
                chunks.Add(chunk, new ChunkRecord
                {
                    DataState = WorldChunkDataState.Queued,
                    PresentationState = WorldChunkPresentationState.NotDisplayed
                });
                return true;
            }

            if (record.DataState != WorldChunkDataState.Failed)
            {
                return false;
            }

            record.DataState = WorldChunkDataState.Queued;
            return true;
        }

        /// <summary>
        /// 대기 청크를 생성 중으로 전환하고 bounds에 맞는 WorldGrid를 만듭니다.
        /// 부분 청크는 월드 안에 포함된 셀만 배열에 보유합니다.
        /// </summary>
        public bool TryBeginGeneration(WorldChunkCoordinate chunk, out WorldGrid grid)
        {
            return TryBeginGeneration(chunk, out grid, out _);
        }

        /// <summary>생성할 배열과 로컬 (0, 0)에 대응하는 절대 셀 좌표를 함께 반환합니다.</summary>
        public bool TryBeginGeneration(
            WorldChunkCoordinate chunk,
            out WorldGrid grid,
            out WorldCellCoordinate gridWorldOrigin)
        {
            grid = null;
            gridWorldOrigin = default;
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Queued ||
                !TryGetChunkCellBounds(chunk, out long minimumX, out long minimumY, out long maximumX, out long maximumY))
            {
                return false;
            }

            int width = checked((int)(maximumX - minimumX));
            int height = checked((int)(maximumY - minimumY));
            float originX = (float)((double)worldOriginX + minimumX * cellWidth);
            float originY = (float)((double)worldOriginY + minimumY * cellHeight);
            WorldGrid newGrid = new WorldGrid(width, height, cellWidth, cellHeight, originX, originY);

            record.Grid = newGrid;
            record.GridWorldOrigin = new WorldCellCoordinate((int)minimumX, (int)minimumY);
            record.DataState = WorldChunkDataState.Generating;
            loadedChunkCount++;
            grid = newGrid;
            gridWorldOrigin = record.GridWorldOrigin;
            return true;
        }

        /// <summary>생성이 완료된 청크를 조회 가능한 상태로 전환합니다.</summary>
        public bool TryMarkReady(WorldChunkCoordinate chunk)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Generating || record.Grid == null)
            {
                return false;
            }

            record.DataState = WorldChunkDataState.Ready;
            SubscribeToGridChanges(record);
            return true;
        }

        /// <summary>대기 또는 생성 중인 청크를 실패 상태로 만들고 중간 데이터를 폐기합니다.</summary>
        public bool TryMarkFailed(WorldChunkCoordinate chunk)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                (record.DataState != WorldChunkDataState.Queued && record.DataState != WorldChunkDataState.Generating))
            {
                return false;
            }

            if (record.DataState == WorldChunkDataState.Generating)
            {
                loadedChunkCount--;
            }

            record.Grid = null;
            record.ChangeHandler = null;
            record.DataState = WorldChunkDataState.Failed;
            record.PresentationState = WorldChunkPresentationState.NotDisplayed;
            return true;
        }

        /// <summary>준비된 청크의 배열 데이터를 얻습니다. 생성 중 데이터는 외부에 노출하지 않습니다.</summary>
        public bool TryGetReadyGrid(WorldChunkCoordinate chunk, out WorldGrid grid)
        {
            grid = null;
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Ready)
            {
                return false;
            }

            grid = record.Grid;
            return true;
        }

        /// <summary>준비된 청크에서 WorldGrid 로컬 원점에 대응하는 절대 셀 좌표를 반환합니다.</summary>
        public bool TryGetReadyGridWorldOrigin(
            WorldChunkCoordinate chunk,
            out WorldCellCoordinate worldOrigin)
        {
            worldOrigin = default;
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Ready)
            {
                return false;
            }

            worldOrigin = record.GridWorldOrigin;
            return true;
        }

        /// <summary>표시 상태를 갱신합니다. 청크 데이터가 준비된 경우에만 표시 상태가 될 수 있습니다.</summary>
        public bool TrySetDisplayed(WorldChunkCoordinate chunk, bool displayed)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                (displayed && record.DataState != WorldChunkDataState.Ready))
            {
                return false;
            }

            record.PresentationState = displayed
                ? WorldChunkPresentationState.Displayed
                : WorldChunkPresentationState.NotDisplayed;
            return true;
        }

        /// <summary>표시되지 않는 준비 청크의 데이터를 해제해 상태를 Unloaded로 되돌립니다.</summary>
        public bool TryUnloadChunk(WorldChunkCoordinate chunk)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Ready ||
                record.PresentationState == WorldChunkPresentationState.Displayed)
            {
                return false;
            }

            if (record.ChangeHandler != null)
            {
                record.Grid.CellChanged -= record.ChangeHandler;
            }

            chunks.Remove(chunk);
            loadedChunkCount--;
            return true;
        }

        /// <summary>
        /// 생성 중인 청크에 플레이어 변경 스냅샷을 적용합니다.
        /// 이 과정은 Ready 전이 및 변경 기록 구독보다 먼저 실행되어 생성 결과를 새 변경으로 기록하지 않습니다.
        /// </summary>
        internal bool TryApplyRecordedWallMutations(
            WorldChunkCoordinate chunk,
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin)
        {
            if (!chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Generating || record.Grid != grid ||
                record.GridWorldOrigin != gridWorldOrigin)
            {
                return false;
            }

            if (!wallMutations.TryGetValue(chunk, out Dictionary<WorldCellCoordinate, WallMutation> mutations))
            {
                return true;
            }

            foreach (KeyValuePair<WorldCellCoordinate, WallMutation> entry in mutations)
            {
                long localX = (long)entry.Key.X - gridWorldOrigin.X;
                long localY = (long)entry.Key.Y - gridWorldOrigin.Y;
                if (localX < 0 || localX >= grid.Width || localY < 0 || localY >= grid.Height ||
                    !ApplyWallState(grid, new WorldGridCoordinate((int)localX, (int)localY), entry.Value.Current))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>절대 셀 좌표에서 청크 내부 WorldGrid 배열 좌표를 계산합니다.</summary>
        public bool TryGetReadyLocalCoordinate(
            WorldCellCoordinate worldCell,
            out WorldGrid grid,
            out WorldGridCoordinate localCell,
            out WorldCellAccessStatus status)
        {
            grid = null;
            localCell = default;
            if (!TryGetChunkAddress(worldCell, out WorldChunkCoordinate chunk, out _))
            {
                status = WorldCellAccessStatus.OutOfWorldBounds;
                return false;
            }

            WorldChunkDataState state = GetDataState(chunk);
            status = ToAccessStatus(state);
            if (state != WorldChunkDataState.Ready ||
                !chunks.TryGetValue(chunk, out ChunkRecord record))
            {
                return false;
            }

            long localX = (long)worldCell.X - record.GridWorldOrigin.X;
            long localY = (long)worldCell.Y - record.GridWorldOrigin.Y;
            if (localX < 0 || localX >= record.Grid.Width || localY < 0 || localY >= record.Grid.Height)
            {
                status = WorldCellAccessStatus.OutOfWorldBounds;
                return false;
            }

            grid = record.Grid;
            localCell = new WorldGridCoordinate((int)localX, (int)localY);
            return true;
        }

        private void SubscribeToGridChanges(ChunkRecord record)
        {
            WorldCellCoordinate gridOrigin = record.GridWorldOrigin;
            record.ChangeHandler = change => CellChanged?.Invoke(new WorldCellChange(
                new WorldCellCoordinate(
                    checked(gridOrigin.X + change.Coordinate.X),
                    checked(gridOrigin.Y + change.Coordinate.Y)),
                change.Layer,
                change.Kind));
            record.Grid.CellChanged += record.ChangeHandler;
        }

        internal void RecordPlayerWallMutation(
            WorldCellCoordinate worldCell,
            bool hadWallBefore,
            WorldWallData wallBefore,
            bool hasWallAfter,
            WorldWallData wallAfter)
        {
            if (!TryGetChunkAddress(worldCell, out WorldChunkCoordinate chunk, out _) ||
                !chunks.TryGetValue(chunk, out ChunkRecord record) ||
                record.DataState != WorldChunkDataState.Ready)
            {
                throw new InvalidOperationException("준비된 청크의 플레이어 벽 변경만 기록할 수 있습니다.");
            }

            var baselineCandidate = new WallStateSnapshot(hadWallBefore, wallBefore);
            var current = new WallStateSnapshot(hasWallAfter, wallAfter);
            if (!wallMutations.TryGetValue(chunk, out Dictionary<WorldCellCoordinate, WallMutation> mutations))
            {
                mutations = new Dictionary<WorldCellCoordinate, WallMutation>();
                wallMutations.Add(chunk, mutations);
            }

            WallStateSnapshot baseline = mutations.TryGetValue(worldCell, out WallMutation existing)
                ? existing.Baseline
                : baselineCandidate;
            if (baseline.Matches(current))
            {
                mutations.Remove(worldCell);
                if (mutations.Count == 0)
                {
                    wallMutations.Remove(chunk);
                }

                return;
            }

            mutations[worldCell] = new WallMutation(baseline, current);
        }

        private static bool ApplyWallState(
            WorldGrid grid,
            WorldGridCoordinate coordinate,
            WallStateSnapshot desired)
        {
            bool hasCurrentWall = grid.TryGetWall(coordinate, out WorldWallData current);
            if (!desired.HasWall)
            {
                if (!hasCurrentWall)
                {
                    return true;
                }

                return grid.TryDamageWall(coordinate, current.CurrentDurability, out _, out bool wasDestroyed) &&
                       wasDestroyed;
            }

            if (desired.MaxDurability <= 0 || desired.CurrentDurability <= 0 ||
                desired.CurrentDurability > desired.MaxDurability)
            {
                return false;
            }

            if (hasCurrentWall && current.Type == desired.Type &&
                current.MaxDurability == desired.MaxDurability &&
                current.CurrentDurability == desired.CurrentDurability &&
                current.ResourceType == desired.ResourceType &&
                current.ResourceAmount == desired.ResourceAmount)
            {
                return true;
            }

            if (hasCurrentWall)
            {
                if (!grid.TryDamageWall(
                        coordinate,
                        current.CurrentDurability,
                        out _,
                        out bool wasCurrentDestroyed) || !wasCurrentDestroyed)
                {
                    return false;
                }
            }

            if (!grid.TryPlaceWallState(
                    coordinate,
                    desired.Type,
                    desired.MaxDurability,
                    desired.ResourceType,
                    desired.ResourceAmount))
            {
                return false;
            }

            int damage = desired.MaxDurability - desired.CurrentDurability;
            return damage == 0 || grid.TryDamageWall(
                coordinate,
                damage,
                out _,
                out bool wasDesiredDestroyed) && !wasDesiredDestroyed;
        }

        private bool TryGetChunkCellBounds(
            WorldChunkCoordinate chunk,
            out long minimumX,
            out long minimumY,
            out long maximumXExclusive,
            out long maximumYExclusive)
        {
            long chunkMinimumX = (long)chunk.X * chunkSize;
            long chunkMinimumY = (long)chunk.Y * chunkSize;
            long chunkMaximumX = chunkMinimumX + chunkSize;
            long chunkMaximumY = chunkMinimumY + chunkSize;

            minimumX = Math.Max(chunkMinimumX, worldBounds.MinimumX);
            minimumY = Math.Max(chunkMinimumY, worldBounds.MinimumY);
            maximumXExclusive = Math.Min(chunkMaximumX, worldBounds.MaximumXExclusive);
            maximumYExclusive = Math.Min(chunkMaximumY, worldBounds.MaximumYExclusive);
            return minimumX < maximumXExclusive && minimumY < maximumYExclusive;
        }

        private static WorldCellAccessStatus ToAccessStatus(WorldChunkDataState state)
        {
            switch (state)
            {
                case WorldChunkDataState.Ready: return WorldCellAccessStatus.Ready;
                case WorldChunkDataState.Queued: return WorldCellAccessStatus.Queued;
                case WorldChunkDataState.Generating: return WorldCellAccessStatus.Generating;
                case WorldChunkDataState.Failed: return WorldCellAccessStatus.Failed;
                case WorldChunkDataState.OutOfWorldBounds: return WorldCellAccessStatus.OutOfWorldBounds;
                default: return WorldCellAccessStatus.Unloaded;
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
