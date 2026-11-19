using System;
using System.Collections.Generic;
using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;
using Unity.Profiling;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>플레이어 주변 청크 데이터를 생성하고 카메라에 필요한 청크만 표시합니다.</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class WorldChunkStreamer : MonoBehaviour
    {
        private static readonly ProfilerMarker generationMarker = new ProfilerMarker("WorldChunk.GenerationAndRestore");
        private readonly struct ChunkRange : IEquatable<ChunkRange>
        {
            public readonly int MinimumX;
            public readonly int MinimumY;
            public readonly int MaximumX;
            public readonly int MaximumY;

            public ChunkRange(int minimumX, int minimumY, int maximumX, int maximumY)
            {
                MinimumX = minimumX;
                MinimumY = minimumY;
                MaximumX = maximumX;
                MaximumY = maximumY;
            }

            public bool Equals(ChunkRange other)
            {
                return MinimumX == other.MinimumX && MinimumY == other.MinimumY &&
                       MaximumX == other.MaximumX && MaximumY == other.MaximumY;
            }
        }

        private readonly struct ChunkRequest
        {
            public readonly WorldChunkCoordinate Coordinate;
            public readonly int Priority;
            public readonly long DistanceSquared;

            public ChunkRequest(WorldChunkCoordinate coordinate, int priority, long distanceSquared)
            {
                Coordinate = coordinate;
                Priority = priority;
                DistanceSquared = distanceSquared;
            }
        }

        [Header("스트리밍 연결")]
        [SerializeField] private WorldGridBootstrap worldBootstrap;
        [SerializeField] private WorldGridTilemapView tilemapView;
        [SerializeField] private Transform player;
        [SerializeField] private Collider2D playerCollider;
        [SerializeField] private Camera targetCamera;

        [Header("플레이어 중심 청크 범위")]
        [SerializeField, Min(1)] private int retainedRadius = 2;
        [SerializeField, Min(1)] private int priorityRadius = 1;

        [Header("프레임별 처리량")]
        [SerializeField, Min(1)] private int maxChunksGeneratedPerFrame = 1;
        [SerializeField, Min(1)] private int maxChunksDisplayedPerFrame = 2;
        [SerializeField, Min(1)] private int maxChunkDisplaysRemovedPerFrame = 2;
        [SerializeField, Min(1)] private int maxChunksUnloadedPerFrame = 2;

        private readonly HashSet<WorldChunkCoordinate> desiredDataChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> displayChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> priorityChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> visibleChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> movementDemandChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> movementDemandScratch = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> dashPathSet = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> reservedDashChunks = new HashSet<WorldChunkCoordinate>();
        private readonly HashSet<WorldChunkCoordinate> pinnedDashChunks = new HashSet<WorldChunkCoordinate>();
        private readonly List<WorldChunkCoordinate> dashPathChunks = new List<WorldChunkCoordinate>();
        private readonly List<WorldChunkCoordinate> releaseCandidates = new List<WorldChunkCoordinate>();
        private readonly List<WorldChunkCoordinate> displayRequests = new List<WorldChunkCoordinate>();
        private readonly List<ChunkRequest> generationRequests = new List<ChunkRequest>();

        private WorldChunkStore chunkStore;
        private WorldGridWorld chunkWorld;
        private WorldChunkGenerationPipeline generationPipeline;
        private WorldChunkCoordinate playerChunk;
        private ChunkRange cameraRange;
        private bool hasCoverage;
        private bool initialMovementUnlocked;

        /// <summary>초기 화면과 플레이어 주변의 필수 청크가 모두 준비되면 true입니다.</summary>
        public bool IsInitialRegionReady => initialMovementUnlocked;

        private void Start()
        {
            if (worldBootstrap == null || tilemapView == null || player == null ||
                playerCollider == null || targetCamera == null ||
                !worldBootstrap.IsChunkStreamingEnabled || !worldBootstrap.InitializeWorld())
            {
                Debug.LogError("청크 스트리밍 연결과 Bootstrap 스트리밍 모드를 확인해야 합니다.", this);
                enabled = false;
                return;
            }

            chunkStore = worldBootstrap.ChunkStore;
            chunkWorld = worldBootstrap.ChunkWorld;
            generationPipeline = worldBootstrap.ChunkGenerationPipeline;
            if (chunkStore == null || chunkWorld == null || generationPipeline == null)
            {
                Debug.LogError("Bootstrap에서 청크 월드 데이터를 초기화하지 못했습니다.", this);
                enabled = false;
                return;
            }

            RefreshCoverage(true);
        }

        private void Update()
        {
            if (chunkStore == null)
            {
                return;
            }

            RefreshCoverage(false);
            ProcessGeneration();
            ProcessDisplays();
            ProcessReleases();
            UpdateInitialMovementGate();
        }

        /// <summary>물리 이동의 현재 Collider 범위가 Ready 및 표시된 청크 안에 머무는지 확인합니다.</summary>
        public bool CanMovePlayer(Vector2 position, Vector2 velocity, float fixedDeltaTime)
        {
            if (chunkWorld == null || playerCollider == null)
            {
                return false;
            }

            Bounds colliderBounds = playerCollider.bounds;
            Vector2 centerOffset = (Vector2)colliderBounds.center - position;
            Vector2 extents = colliderBounds.extents;
            float distance = velocity.magnitude * fixedDeltaTime;
            float cellStep = Mathf.Max(0.01f, Mathf.Min(chunkWorld.CellWidth, chunkWorld.CellHeight) * 0.5f);
            int sampleCount = Mathf.Clamp(Mathf.CeilToInt(distance / cellStep), 1, 128);
            movementDemandScratch.Clear();
            bool insideWorld = true;

            for (int sample = 0; sample <= sampleCount; sample++)
            {
                float fraction = sample / (float)sampleCount;
                Vector2 sampleCenter = position + velocity * (fixedDeltaTime * fraction) + centerOffset;
                insideWorld &= CollectFootprintDemand(sampleCenter, extents);
            }

            bool demandChanged = !movementDemandChunks.SetEquals(movementDemandScratch);
            if (demandChanged)
            {
                movementDemandChunks.Clear();
                foreach (WorldChunkCoordinate chunk in movementDemandScratch)
                {
                    movementDemandChunks.Add(chunk);
                }

                if (hasCoverage)
                {
                    RebuildCoverage();
                }
            }

            return insideWorld && initialMovementUnlocked && movementDemandChunks.Count == 0;
        }

        /// <summary>대시 경로의 모든 청크 데이터와 벽 충돌 표시를 준비하고 준비되면 해제 방지 상태로 고정합니다.</summary>
        public bool TryPrepareDashPath(
            Vector2 start,
            Vector2 direction,
            float speed,
            float duration,
            out bool pathInsideWorld)
        {
            pathInsideWorld = true;
            if (chunkWorld == null || playerCollider == null || direction == Vector2.zero || speed <= 0f || duration <= 0f)
            {
                return false;
            }

            pathInsideWorld = BuildDashPath(start, direction.normalized * speed * duration);
            if (!pathInsideWorld)
            {
                dashPathChunks.Clear();
                dashPathSet.Clear();
                if (UpdateDashReservation() && hasCoverage)
                {
                    RebuildCoverage();
                }

                return false;
            }

            bool reservationChanged = UpdateDashReservation();
            if (reservationChanged && hasCoverage)
            {
                RebuildCoverage();
            }

            bool ready = true;
            for (int i = 0; i < dashPathChunks.Count; i++)
            {
                WorldChunkCoordinate chunk = dashPathChunks[i];
                if (chunkStore.GetDataState(chunk) != WorldChunkDataState.Ready ||
                    !chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) ||
                    state != WorldChunkPresentationState.Displayed)
                {
                    ready = false;
                }
            }

            ready &= initialMovementUnlocked;

            if (!ready)
            {
                return false;
            }

            pinnedDashChunks.Clear();
            for (int i = 0; i < dashPathChunks.Count; i++)
            {
                pinnedDashChunks.Add(dashPathChunks[i]);
            }

            return true;
        }

        /// <summary>대시가 끝나면 해당 경로의 해제 보호를 풀고 현재 플레이어 범위를 다시 계산합니다.</summary>
        public void ReleaseDashPath()
        {
            if (pinnedDashChunks.Count == 0 && reservedDashChunks.Count == 0)
            {
                return;
            }

            pinnedDashChunks.Clear();
            reservedDashChunks.Clear();
            dashPathChunks.Clear();
            dashPathSet.Clear();
            if (chunkStore != null && hasCoverage)
            {
                RebuildCoverage();
            }
        }

        private void RefreshCoverage(bool force)
        {
            if (!chunkWorld.TryWorldPositionToCell(player.position.x, player.position.y, out WorldCellCoordinate playerCell))
            {
                return;
            }

            WorldChunkCoordinate nextPlayerChunk = WorldChunkCoordinate.FromWorldCell(playerCell, chunkStore.ChunkSize);
            if (!TryGetCameraChunkRange(out ChunkRange nextCameraRange))
            {
                return;
            }

            if (!force && hasCoverage && nextPlayerChunk == playerChunk && nextCameraRange.Equals(cameraRange))
            {
                return;
            }

            playerChunk = nextPlayerChunk;
            cameraRange = nextCameraRange;
            hasCoverage = true;
            RebuildCoverage();
        }

        private void RebuildCoverage()
        {
            desiredDataChunks.Clear();
            displayChunks.Clear();
            priorityChunks.Clear();
            visibleChunks.Clear();
            displayRequests.Clear();
            generationRequests.Clear();
            releaseCandidates.Clear();

            for (int y = -retainedRadius; y <= retainedRadius; y++)
            {
                for (int x = -retainedRadius; x <= retainedRadius; x++)
                {
                    long chunkX = (long)playerChunk.X + x;
                    long chunkY = (long)playerChunk.Y + y;
                    if (chunkX < int.MinValue || chunkX > int.MaxValue ||
                        chunkY < int.MinValue || chunkY > int.MaxValue)
                    {
                        continue;
                    }

                    var chunk = new WorldChunkCoordinate((int)chunkX, (int)chunkY);
                    if (chunkStore.GetDataState(chunk) == WorldChunkDataState.OutOfWorldBounds)
                    {
                        continue;
                    }

                    desiredDataChunks.Add(chunk);
                    if (Math.Abs(x) <= priorityRadius && Math.Abs(y) <= priorityRadius)
                    {
                        priorityChunks.Add(chunk);
                        displayChunks.Add(chunk);
                    }
                }
            }

            AddVisibleCameraChunks();
            foreach (WorldChunkCoordinate chunk in reservedDashChunks)
            {
                if (chunkStore.GetDataState(chunk) != WorldChunkDataState.OutOfWorldBounds)
                {
                    desiredDataChunks.Add(chunk);
                    displayChunks.Add(chunk);
                    priorityChunks.Add(chunk);
                }
            }

            foreach (WorldChunkCoordinate chunk in movementDemandChunks)
            {
                if (chunkStore.GetDataState(chunk) != WorldChunkDataState.OutOfWorldBounds)
                {
                    desiredDataChunks.Add(chunk);
                    displayChunks.Add(chunk);
                    priorityChunks.Add(chunk);
                }
            }

            QueueDesiredChunks();
            BuildDisplayRequests();
            BuildReleaseCandidates();
            SortGenerationRequests();
        }

        private void BuildDisplayRequests()
        {
            foreach (WorldChunkCoordinate chunk in displayChunks)
            {
                displayRequests.Add(chunk);
            }

            displayRequests.Sort(CompareDisplayPriority);
        }

        private void AddVisibleCameraChunks()
        {
            for (long y = cameraRange.MinimumY; y <= cameraRange.MaximumY; y++)
            {
                for (long x = cameraRange.MinimumX; x <= cameraRange.MaximumX; x++)
                {
                    var chunk = new WorldChunkCoordinate((int)x, (int)y);
                    if (chunkStore.GetDataState(chunk) == WorldChunkDataState.OutOfWorldBounds)
                    {
                        continue;
                    }

                    visibleChunks.Add(chunk);
                    displayChunks.Add(chunk);
                    desiredDataChunks.Add(chunk);
                }
            }
        }

        private void QueueDesiredChunks()
        {
            foreach (WorldChunkCoordinate chunk in desiredDataChunks)
            {
                WorldChunkDataState state = chunkStore.GetDataState(chunk);
                if (state == WorldChunkDataState.Unloaded || state == WorldChunkDataState.Failed)
                {
                    chunkStore.TryQueueChunk(chunk);
                }

                if (chunkStore.GetDataState(chunk) == WorldChunkDataState.Queued)
                {
                    int priority = GetRequestPriority(chunk);
                    generationRequests.Add(new ChunkRequest(chunk, priority, GetDistanceSquared(chunk)));
                }
            }
        }

        private void BuildReleaseCandidates()
        {
            WorldChunkCoordinate[] knownChunks = chunkStore.GetKnownChunkCoordinates();
            for (int i = 0; i < knownChunks.Length; i++)
            {
                WorldChunkCoordinate chunk = knownChunks[i];
                if (desiredDataChunks.Contains(chunk) || pinnedDashChunks.Contains(chunk))
                {
                    continue;
                }

                WorldChunkDataState state = chunkStore.GetDataState(chunk);
                if (state == WorldChunkDataState.Queued)
                {
                    chunkStore.TryCancelQueuedChunk(chunk);
                }
                else if (state == WorldChunkDataState.Ready)
                {
                    releaseCandidates.Add(chunk);
                }
                else if (state == WorldChunkDataState.Failed)
                {
                    chunkStore.TryForgetFailedChunk(chunk);
                }
            }
        }

        private void SortGenerationRequests()
        {
            generationRequests.Sort((left, right) =>
            {
                int priorityOrder = left.Priority.CompareTo(right.Priority);
                if (priorityOrder != 0)
                {
                    return priorityOrder;
                }

                int distanceOrder = left.DistanceSquared.CompareTo(right.DistanceSquared);
                if (distanceOrder != 0)
                {
                    return distanceOrder;
                }

                int xOrder = left.Coordinate.X.CompareTo(right.Coordinate.X);
                return xOrder != 0 ? xOrder : left.Coordinate.Y.CompareTo(right.Coordinate.Y);
            });
        }

        private void ProcessGeneration()
        {
            int generated = 0;
            for (int i = 0; i < generationRequests.Count && generated < maxChunksGeneratedPerFrame; i++)
            {
                WorldChunkCoordinate chunk = generationRequests[i].Coordinate;
                if (chunkStore.GetDataState(chunk) != WorldChunkDataState.Queued)
                {
                    continue;
                }

                try
                {
                    bool succeeded;
                    using (generationMarker.Auto())
                    {
                        succeeded = generationPipeline.TryGenerateQueuedChunk(chunk);
                    }

                    if (!succeeded)
                    {
                        Debug.LogWarning($"청크 생성을 완료하지 못했습니다: ({chunk.X}, {chunk.Y})", this);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"청크 생성 중 예외가 발생했습니다: ({chunk.X}, {chunk.Y}), {exception.Message}", this);
                }

                generated++;
            }
        }

        private void ProcessDisplays()
        {
            int displayed = 0;
            for (int i = 0; i < displayRequests.Count && displayed < maxChunksDisplayedPerFrame; i++)
            {
                WorldChunkCoordinate chunk = displayRequests[i];
                if (chunkStore.GetDataState(chunk) != WorldChunkDataState.Ready ||
                    !chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) ||
                    state == WorldChunkPresentationState.Displayed)
                {
                    continue;
                }

                if (tilemapView.TryDisplayChunk(chunk))
                {
                    displayed++;
                }
            }
        }

        private int CompareDisplayPriority(WorldChunkCoordinate left, WorldChunkCoordinate right)
        {
            int leftPriority = GetRequestPriority(left);
            int rightPriority = GetRequestPriority(right);
            int priorityOrder = leftPriority.CompareTo(rightPriority);
            if (priorityOrder != 0)
            {
                return priorityOrder;
            }

            return GetDistanceSquared(left).CompareTo(GetDistanceSquared(right));
        }

        private void ProcessReleases()
        {
            int removed = 0;
            int unloaded = 0;
            for (int i = releaseCandidates.Count - 1; i >= 0; i--)
            {
                WorldChunkCoordinate chunk = releaseCandidates[i];
                if (desiredDataChunks.Contains(chunk) || pinnedDashChunks.Contains(chunk))
                {
                    releaseCandidates.RemoveAt(i);
                    continue;
                }

                if (chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) &&
                    state == WorldChunkPresentationState.Displayed)
                {
                    if (removed >= maxChunkDisplaysRemovedPerFrame)
                    {
                        continue;
                    }

                    if (!tilemapView.TryRemoveChunkDisplay(chunk))
                    {
                        continue;
                    }

                    removed++;
                }

                if (unloaded < maxChunksUnloadedPerFrame && chunkStore.TryUnloadChunk(chunk))
                {
                    unloaded++;
                    releaseCandidates.RemoveAt(i);
                }
            }
        }

        private void UpdateInitialMovementGate()
        {
            if (initialMovementUnlocked)
            {
                return;
            }

            foreach (WorldChunkCoordinate chunk in priorityChunks)
            {
                if (!IsReadyAndDisplayed(chunk))
                {
                    return;
                }
            }

            foreach (WorldChunkCoordinate chunk in visibleChunks)
            {
                if (!IsReadyAndDisplayed(chunk))
                {
                    return;
                }
            }

            initialMovementUnlocked = true;
        }

        private bool IsReadyAndDisplayed(WorldChunkCoordinate chunk)
        {
            return chunkStore.GetDataState(chunk) == WorldChunkDataState.Ready &&
                   chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) &&
                   state == WorldChunkPresentationState.Displayed;
        }

        private bool CollectFootprintDemand(Vector2 center, Vector2 extents)
        {
            return CollectPositionDemand(center.x - extents.x, center.y - extents.y) &
                   CollectPositionDemand(center.x - extents.x, center.y + extents.y) &
                   CollectPositionDemand(center.x + extents.x, center.y - extents.y) &
                   CollectPositionDemand(center.x + extents.x, center.y + extents.y) &
                   CollectPositionDemand(center.x, center.y);
        }

        private bool CollectPositionDemand(float worldX, float worldY)
        {
            if (!chunkWorld.TryWorldPositionToCell(worldX, worldY, out WorldCellCoordinate cell) ||
                !chunkStore.TryGetChunkAddress(cell, out WorldChunkCoordinate chunk, out _))
            {
                return false;
            }

            if (!IsReadyAndDisplayed(chunk))
            {
                movementDemandScratch.Add(chunk);
                return false;
            }

            return true;
        }

        private bool BuildDashPath(Vector2 start, Vector2 displacement)
        {
            dashPathChunks.Clear();
            dashPathSet.Clear();
            Bounds bounds = playerCollider.bounds;
            Vector2 centerOffset = (Vector2)bounds.center - start;
            Vector2 extents = bounds.extents;
            float cellStep = Mathf.Max(0.01f, Mathf.Min(chunkWorld.CellWidth, chunkWorld.CellHeight) * 0.5f);
            int sampleCount = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / cellStep), 1, 512);
            bool insideWorld = true;

            for (int sample = 0; sample <= sampleCount; sample++)
            {
                Vector2 center = start + displacement * (sample / (float)sampleCount) + centerOffset;
                insideWorld &= AddDashPosition(center.x - extents.x, center.y - extents.y);
                insideWorld &= AddDashPosition(center.x - extents.x, center.y + extents.y);
                insideWorld &= AddDashPosition(center.x + extents.x, center.y - extents.y);
                insideWorld &= AddDashPosition(center.x + extents.x, center.y + extents.y);
                insideWorld &= AddDashPosition(center.x, center.y);
            }

            return insideWorld;
        }

        private bool AddDashPosition(float worldX, float worldY)
        {
            if (!chunkWorld.TryWorldPositionToCell(worldX, worldY, out WorldCellCoordinate cell) ||
                !chunkStore.TryGetChunkAddress(cell, out WorldChunkCoordinate chunk, out _))
            {
                return false;
            }

            if (dashPathSet.Add(chunk))
            {
                dashPathChunks.Add(chunk);
            }

            return true;
        }

        private bool UpdateDashReservation()
        {
            dashPathSet.Clear();
            for (int i = 0; i < dashPathChunks.Count; i++)
            {
                dashPathSet.Add(dashPathChunks[i]);
            }

            if (reservedDashChunks.SetEquals(dashPathSet))
            {
                return false;
            }

            reservedDashChunks.Clear();
            foreach (WorldChunkCoordinate chunk in dashPathSet)
            {
                reservedDashChunks.Add(chunk);
            }

            return true;
        }

        private bool TryGetCameraChunkRange(out ChunkRange range)
        {
            range = default;
            float cameraDepth = Vector3.Dot(
                player.position - targetCamera.transform.position,
                targetCamera.transform.forward);
            Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, cameraDepth));
            Vector3 topLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 1f, cameraDepth));
            Vector3 bottomRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 0f, cameraDepth));
            Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, cameraDepth));
            float minimumX = Mathf.Min(Mathf.Min(bottomLeft.x, topLeft.x), Mathf.Min(bottomRight.x, topRight.x));
            float maximumX = Mathf.Max(Mathf.Max(bottomLeft.x, topLeft.x), Mathf.Max(bottomRight.x, topRight.x));
            float minimumY = Mathf.Min(Mathf.Min(bottomLeft.y, topLeft.y), Mathf.Min(bottomRight.y, topRight.y));
            float maximumY = Mathf.Max(Mathf.Max(bottomLeft.y, topLeft.y), Mathf.Max(bottomRight.y, topRight.y));

            return TryWorldRectToChunkRange(minimumX, minimumY, maximumX, maximumY, out range);
        }

        private bool TryWorldRectToChunkRange(
            float minimumX,
            float minimumY,
            float maximumX,
            float maximumY,
            out ChunkRange range)
        {
            range = default;
            minimumX = Mathf.Clamp(minimumX, worldBootstrap.WorldMinimumX, worldBootstrap.WorldMaximumX);
            minimumY = Mathf.Clamp(minimumY, worldBootstrap.WorldMinimumY, worldBootstrap.WorldMaximumY);
            maximumX = Mathf.Clamp(maximumX, worldBootstrap.WorldMinimumX, worldBootstrap.WorldMaximumX);
            maximumY = Mathf.Clamp(maximumY, worldBootstrap.WorldMinimumY, worldBootstrap.WorldMaximumY);
            if (maximumX <= minimumX || maximumY <= minimumY)
            {
                return false;
            }

            double minCellX = Math.Floor((minimumX - chunkStore.WorldOriginX) / chunkStore.CellWidth);
            double minCellY = Math.Floor((minimumY - chunkStore.WorldOriginY) / chunkStore.CellHeight);
            double maxCellX = Math.Ceiling((maximumX - chunkStore.WorldOriginX) / chunkStore.CellWidth) - 1d;
            double maxCellY = Math.Ceiling((maximumY - chunkStore.WorldOriginY) / chunkStore.CellHeight) - 1d;
            minCellX = Math.Max(minCellX, chunkStore.WorldBounds.MinimumX);
            minCellY = Math.Max(minCellY, chunkStore.WorldBounds.MinimumY);
            maxCellX = Math.Min(maxCellX, chunkStore.WorldBounds.MaximumXExclusive - 1L);
            maxCellY = Math.Min(maxCellY, chunkStore.WorldBounds.MaximumYExclusive - 1L);
            if (maxCellX < minCellX || maxCellY < minCellY ||
                minCellX < int.MinValue || minCellY < int.MinValue ||
                maxCellX > int.MaxValue || maxCellY > int.MaxValue)
            {
                return false;
            }

            WorldChunkCoordinate minimumChunk = WorldChunkCoordinate.FromWorldCell(
                new WorldCellCoordinate((int)minCellX, (int)minCellY),
                chunkStore.ChunkSize);
            WorldChunkCoordinate maximumChunk = WorldChunkCoordinate.FromWorldCell(
                new WorldCellCoordinate((int)maxCellX, (int)maxCellY),
                chunkStore.ChunkSize);
            range = new ChunkRange(minimumChunk.X, minimumChunk.Y, maximumChunk.X, maximumChunk.Y);
            return true;
        }

        private int GetRequestPriority(WorldChunkCoordinate chunk)
        {
            if (reservedDashChunks.Contains(chunk) || priorityChunks.Contains(chunk))
            {
                return 0;
            }

            return visibleChunks.Contains(chunk) ? 1 : 2;
        }

        private long GetDistanceSquared(WorldChunkCoordinate chunk)
        {
            long deltaX = (long)chunk.X - playerChunk.X;
            long deltaY = (long)chunk.Y - playerChunk.Y;
            double distanceSquared = (double)deltaX * deltaX + (double)deltaY * deltaY;
            return distanceSquared >= long.MaxValue ? long.MaxValue : (long)distanceSquared;
        }
    }
}
