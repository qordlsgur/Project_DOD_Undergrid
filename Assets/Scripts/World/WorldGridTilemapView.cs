using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Tilemaps;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// DOD 월드 셀을 Floor·Wall Tilemap으로 표현하고 변경 이벤트를 점진적으로 반영합니다.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class WorldGridTilemapView : MonoBehaviour
    {
        private static readonly ProfilerMarker chunkTilemapWriteMarker = new ProfilerMarker("WorldChunk.TilemapWrite");
        private static readonly ProfilerMarker chunkTilemapClearMarker = new ProfilerMarker("WorldChunk.TilemapClear");
        private static readonly ProfilerMarker wallColliderRefreshMarker = new ProfilerMarker("WorldChunk.ColliderRefresh");

        [Header("Tilemap 연결")]
        [SerializeField] private Grid worldGrid;
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;

        [Header("바닥 임시 타일")]
        [SerializeField] private TileBase groundFloorTile;
        [SerializeField] private TileBase soilFloorTile;
        [SerializeField] private TileBase stoneFloorTile;

        [Header("벽 임시 타일")]
        [SerializeField] private TileBase soilWallTile;
        [SerializeField] private TileBase stoneWallTile;
        [Tooltip("선택적으로 광물 Tile을 지정할 수 있습니다. Sprite와 Collider 설정을 복제하고 광물 색상을 적용합니다.")]
        [SerializeField] private TileBase copperOreWallTile;
        [SerializeField] private TileBase ironOreWallTile;

        private TileBase generatedCopperOreWallTile;
        private TileBase generatedIronOreWallTile;

        private WorldGrid gridData;
        private WorldChunkStore chunkStore;
        private bool needsFullSynchronization;
        private bool needsChunkSynchronization;

        private static readonly Color32 CopperOreColor = new Color32(0xE8, 0x75, 0x32, 0xFF);
        private static readonly Color32 IronOreColor = new Color32(0xC0, 0xC0, 0xC0, 0xFF);

        /// <summary>
        /// 입력이 유효하면 현재 Tilemap과 구독을 새 데이터에 맞춰 교체합니다.
        /// 실패하면 기존 데이터 참조, 타일 표시, 이벤트 구독을 유지합니다.
        /// </summary>
        public bool Initialize(WorldGrid source)
        {
            // 유효성 검사는 기존 표시와 구독을 바꾸기 전에 끝내므로 입력 오류는 현재 상태를 보존합니다.
            if (source == null || chunkStore != null || !ValidateTilemaps())
            {
                return false;
            }

            TileBase[] newFloorBlock = new TileBase[source.CellCount];
            TileBase[] newWallBlock = new TileBase[source.CellCount];
            PopulateBlocks(source, newFloorBlock, newWallBlock);

            WorldGrid previousGrid = gridData;
            TileBase[] previousFloorBlock = null;
            TileBase[] previousWallBlock = null;
            if (previousGrid != null)
            {
                previousFloorBlock = new TileBase[previousGrid.CellCount];
                previousWallBlock = new TileBase[previousGrid.CellCount];
                PopulateBlocks(previousGrid, previousFloorBlock, previousWallBlock);
            }

            Unsubscribe();
            try
            {
                RenderBlocks(source, newFloorBlock, newWallBlock);
            }
            catch (System.Exception exception)
            {
                RestoreAfterFailedInitialization(
                    previousGrid,
                    previousFloorBlock,
                    previousWallBlock,
                    exception);
                return false;
            }

            gridData = source;
            needsFullSynchronization = !isActiveAndEnabled;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
            return true;
        }

        /// <summary>청크 저장소에 준비된 데이터 중 표시 상태인 청크를 Tilemap에 동기화합니다.</summary>
        public bool InitializeChunks(WorldChunkStore source)
        {
            if (source == null || gridData != null || (chunkStore != null && chunkStore != source) ||
                !ValidateTilemaps())
            {
                return false;
            }

            chunkStore = source;
            needsFullSynchronization = false;
            if (!isActiveAndEnabled)
            {
                needsChunkSynchronization = true;
                return true;
            }

            if (!SynchronizeDisplayedChunks())
            {
                needsChunkSynchronization = true;
                return false;
            }

            needsChunkSynchronization = false;
            SubscribeToChunks();
            return true;
        }

        /// <summary>Ready 상태 청크 하나를 절대 월드 위치에 표시하고 Store의 표시 상태를 갱신합니다.</summary>
        public bool TryDisplayChunk(WorldChunkCoordinate chunk)
        {
            if (chunkStore == null || chunkStore.GetDataState(chunk) != WorldChunkDataState.Ready ||
                !chunkStore.TryGetReadyGrid(chunk, out WorldGrid chunkGrid) ||
                !TryGetChunkTileBounds(chunkGrid, out BoundsInt tileBounds))
            {
                return false;
            }

            if (chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) &&
                state == WorldChunkPresentationState.Displayed)
            {
                return true;
            }

            try
            {
                RenderChunk(chunkGrid, tileBounds);
                ProcessWallColliderChanges();
                if (!chunkStore.TrySetDisplayed(chunk, true))
                {
                    ClearChunkTiles(chunkGrid, tileBounds);
                    ProcessWallColliderChanges();
                    return false;
                }

                return true;
            }
            catch (System.Exception exception)
            {
                RestoreChunkAfterFailedDisplay(chunk, chunkGrid, tileBounds, exception);
                return false;
            }
        }

        /// <summary>지정 청크의 Tilemap 타일과 벽 충돌 형상만 제거하고 Store 표시 상태를 해제합니다.</summary>
        public bool TryRemoveChunkDisplay(WorldChunkCoordinate chunk)
        {
            if (chunkStore == null)
            {
                return false;
            }

            if (!chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) ||
                state == WorldChunkPresentationState.NotDisplayed)
            {
                return true;
            }

            if (!chunkStore.TryGetReadyGrid(chunk, out WorldGrid chunkGrid) ||
                !TryGetChunkTileBounds(chunkGrid, out BoundsInt tileBounds))
            {
                return false;
            }

            try
            {
                ClearChunkTiles(chunkGrid, tileBounds);
                ProcessWallColliderChanges();
                if (!chunkStore.TrySetDisplayed(chunk, false))
                {
                    RenderChunk(chunkGrid, tileBounds);
                    ProcessWallColliderChanges();
                    return false;
                }

                return true;
            }
            catch (System.Exception exception)
            {
                RestoreChunkAfterFailedRemoval(chunk, chunkGrid, tileBounds, exception);
                return false;
            }
        }

        private void RestoreAfterFailedInitialization(
            WorldGrid previousGrid,
            TileBase[] previousFloorBlock,
            TileBase[] previousWallBlock,
            System.Exception renderException)
        {
            if (previousGrid == null)
            {
                gridData = null;
                ClearTilemapsAfterFailure(renderException);
                return;
            }

            try
            {
                RenderBlocks(previousGrid, previousFloorBlock, previousWallBlock);
                gridData = previousGrid;
                needsFullSynchronization = !isActiveAndEnabled;
                if (isActiveAndEnabled)
                {
                    Subscribe();
                }

                Debug.LogError($"새 WorldGrid 표시를 적용하지 못해 기존 표시를 복원했습니다: {renderException.Message}", this);
            }
            catch (System.Exception restoreException)
            {
                gridData = null;
                ClearTilemapsAfterFailure(restoreException);
                Debug.LogError(
                    $"새 WorldGrid 표시와 기존 표시 복원에 모두 실패했습니다. 데이터 구독을 해제했습니다: {renderException.Message}; {restoreException.Message}",
                    this);
            }
        }

        private void ClearTilemapsAfterFailure(System.Exception exception)
        {
            try
            {
                floorTilemap.ClearAllTiles();
                wallTilemap.ClearAllTiles();
            }
            catch (System.Exception clearException)
            {
                Debug.LogError(
                    $"초기화 실패 후 Tilemap을 비우지 못했습니다: {exception.Message}; {clearException.Message}",
                    this);
                return;
            }

            Debug.LogError($"WorldGrid Tilemap 초기화에 실패해 표시를 비웠습니다: {exception.Message}", this);
        }

        private void OnEnable()
        {
            if (chunkStore != null)
            {
                if (needsChunkSynchronization)
                {
                    needsChunkSynchronization = !SynchronizeDisplayedChunks();
                }

                SubscribeToChunks();
                return;
            }

            if (gridData == null)
            {
                return;
            }

            if (needsFullSynchronization)
            {
                SynchronizeAllCells();
                needsFullSynchronization = false;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnsubscribeFromChunks();
            needsFullSynchronization = gridData != null;
            needsChunkSynchronization = chunkStore != null;
        }

        private void OnDestroy()
        {
            DestroyGeneratedTile(ref generatedCopperOreWallTile);
            DestroyGeneratedTile(ref generatedIronOreWallTile);
        }

        private bool ValidateTilemaps()
        {
            if (worldGrid == null || floorTilemap == null || wallTilemap == null)
            {
                Debug.LogError("Grid, Floor Tilemap, Wall Tilemap 참조를 모두 연결해야 합니다.", this);
                return false;
            }

            if (floorTilemap.GetComponent<TilemapCollider2D>() != null ||
                wallTilemap.GetComponent<TilemapCollider2D>() == null)
            {
                Debug.LogError("TilemapCollider2D는 Wall Tilemap에만 연결해야 합니다.", this);
                return false;
            }

            if (floorTilemap.GetComponentInParent<Grid>() != worldGrid ||
                wallTilemap.GetComponentInParent<Grid>() != worldGrid)
            {
                Debug.LogError("Floor와 Wall Tilemap은 지정된 Grid의 자식이어야 합니다.", this);
                return false;
            }

            if (!HasIdentityLocalTransform(floorTilemap.transform) ||
                !HasIdentityLocalTransform(wallTilemap.transform))
            {
                Debug.LogError("Floor와 Wall Tilemap의 로컬 위치·회전·스케일은 Grid 셀과 일치해야 합니다.", this);
                return false;
            }

            if (groundFloorTile == null || soilFloorTile == null || stoneFloorTile == null ||
                soilWallTile == null || stoneWallTile == null)
            {
                Debug.LogError("바닥 및 벽 종류에 대응하는 임시 Tile 에셋을 모두 연결해야 합니다.", this);
                return false;
            }

            return true;
        }

        private bool SynchronizeDisplayedChunks()
        {
            WorldChunkCoordinate[] displayedChunks = chunkStore.GetDisplayedChunkCoordinates();
            var chunkGrids = new WorldGrid[displayedChunks.Length];
            var tileBounds = new BoundsInt[displayedChunks.Length];
            for (int i = 0; i < displayedChunks.Length; i++)
            {
                if (!chunkStore.TryGetReadyGrid(displayedChunks[i], out chunkGrids[i]) ||
                    !TryGetChunkTileBounds(chunkGrids[i], out tileBounds[i]))
                {
                    return false;
                }
            }

            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();

            for (int i = 0; i < displayedChunks.Length; i++)
            {
                try
                {
                    RenderChunk(chunkGrids[i], tileBounds[i]);
                }
                catch
                {
                    floorTilemap.ClearAllTiles();
                    wallTilemap.ClearAllTiles();
                    for (int chunkIndex = 0; chunkIndex < displayedChunks.Length; chunkIndex++)
                    {
                        chunkStore.TrySetDisplayed(displayedChunks[chunkIndex], false);
                    }

                    ProcessWallColliderChanges();
                    return false;
                }
            }

            ProcessWallColliderChanges();
            return true;
        }

        private bool TryGetChunkTileBounds(WorldGrid source, out BoundsInt tileBounds)
        {
            tileBounds = default;
            if (source == null || !ValidateTilemaps())
            {
                return false;
            }

            Vector3 cellSize = worldGrid.cellSize;
            Vector3 gridScale = worldGrid.transform.lossyScale;
            bool cellSizeMatches = Mathf.Approximately(cellSize.x * gridScale.x, source.CellWidth) &&
                                   Mathf.Approximately(cellSize.y * gridScale.y, source.CellHeight) &&
                                   Mathf.Approximately(worldGrid.cellGap.x, 0f) &&
                                   Mathf.Approximately(worldGrid.cellGap.y, 0f);
            bool gridLayoutMatches = worldGrid.cellLayout == GridLayout.CellLayout.Rectangle &&
                                     worldGrid.cellSwizzle == GridLayout.CellSwizzle.XYZ &&
                                     Quaternion.Angle(worldGrid.transform.rotation, Quaternion.identity) < 0.001f;
            if (!cellSizeMatches || !gridLayoutMatches)
            {
                return false;
            }

            Vector3 worldOrigin = new Vector3(source.OriginX, source.OriginY, worldGrid.transform.position.z);
            Vector3Int tileOrigin = worldGrid.WorldToCell(worldOrigin);
            Vector3 snappedOrigin = worldGrid.CellToWorld(tileOrigin);
            if (!Mathf.Approximately(snappedOrigin.x, worldOrigin.x) ||
                !Mathf.Approximately(snappedOrigin.y, worldOrigin.y))
            {
                return false;
            }

            tileBounds = new BoundsInt(tileOrigin.x, tileOrigin.y, tileOrigin.z, source.Width, source.Height, 1);
            return true;
        }

        private void RenderChunk(WorldGrid source, BoundsInt tileBounds)
        {
            using (chunkTilemapWriteMarker.Auto())
            {
                TileBase[] floorBlock = new TileBase[source.CellCount];
                TileBase[] wallBlock = new TileBase[source.CellCount];
                PopulateBlocks(source, floorBlock, wallBlock);
                floorTilemap.SetTilesBlock(tileBounds, floorBlock);
                wallTilemap.SetTilesBlock(tileBounds, wallBlock);
            }
        }

        private void ClearChunkTiles(WorldGrid source, BoundsInt tileBounds)
        {
            using (chunkTilemapClearMarker.Auto())
            {
                TileBase[] emptyBlock = new TileBase[source.CellCount];
                floorTilemap.SetTilesBlock(tileBounds, emptyBlock);
                wallTilemap.SetTilesBlock(tileBounds, emptyBlock);
            }
        }

        private void RestoreChunkAfterFailedDisplay(
            WorldChunkCoordinate chunk,
            WorldGrid chunkGrid,
            BoundsInt tileBounds,
            System.Exception renderException)
        {
            try
            {
                ClearChunkTiles(chunkGrid, tileBounds);
                chunkStore?.TrySetDisplayed(chunk, false);
            }
            catch (System.Exception rollbackException)
            {
                chunkStore?.TrySetDisplayed(chunk, true);
                Debug.LogError(
                    $"청크 표시 실패 후 타일을 정리하지 못했습니다: {renderException.Message}; {rollbackException.Message}",
                    this);
                return;
            }

            TryProcessColliderAfterRollback(renderException);
        }

        private void RestoreChunkAfterFailedRemoval(
            WorldChunkCoordinate chunk,
            WorldGrid chunkGrid,
            BoundsInt tileBounds,
            System.Exception removalException)
        {
            try
            {
                RenderChunk(chunkGrid, tileBounds);
            }
            catch (System.Exception rollbackException)
            {
                try
                {
                    ClearChunkTiles(chunkGrid, tileBounds);
                    chunkStore?.TrySetDisplayed(chunk, false);
                }
                catch
                {
                    // 제거와 복구가 모두 실패하면 표시 상태를 유지해 데이터 해제를 막습니다.
                }

                Debug.LogError(
                    $"청크 표시 제거 실패 후 타일을 복원하지 못했습니다: {removalException.Message}; {rollbackException.Message}",
                    this);
                TryProcessColliderAfterRollback(removalException);
                return;
            }

            chunkStore?.TrySetDisplayed(chunk, true);
            TryProcessColliderAfterRollback(removalException);
        }

        private void TryProcessColliderAfterRollback(System.Exception operationException)
        {
            try
            {
                ProcessWallColliderChanges();
            }
            catch (System.Exception colliderException)
            {
                Debug.LogError(
                    $"Tilemap 상태는 복원했지만 벽 Collider 갱신에 실패했습니다: {operationException.Message}; {colliderException.Message}",
                    this);
            }
        }

        private void ProcessWallColliderChanges()
        {
            TilemapCollider2D wallCollider = wallTilemap.GetComponent<TilemapCollider2D>();
            if (wallCollider != null && wallCollider.enabled && wallCollider.hasTilemapChanges)
            {
                using (wallColliderRefreshMarker.Auto())
                {
                    wallCollider.ProcessTilemapChanges();
                }
            }
        }

        private static bool HasIdentityLocalTransform(Transform tilemapTransform)
        {
            return tilemapTransform.localPosition == Vector3.zero &&
                   Quaternion.Angle(tilemapTransform.localRotation, Quaternion.identity) < 0.001f &&
                   tilemapTransform.localScale == Vector3.one;
        }

        private void PopulateBlocks(
            WorldGrid source,
            TileBase[] floorBlock,
            TileBase[] wallBlock)
        {
            int width = source.Width;
            int height = source.Height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    WorldGridCoordinate coordinate = new WorldGridCoordinate(x, y);

                    if (source.TryGetFloor(coordinate, out WorldFloorData floor))
                    {
                        floorBlock[index] = GetFloorTile(floor.Type);
                    }

                    if (source.TryGetWall(coordinate, out WorldWallData wall))
                    {
                        wallBlock[index] = GetWallTile(wall.Type);
                    }
                }
            }
        }

        private void RenderBlocks(WorldGrid source, TileBase[] floorBlock, TileBase[] wallBlock)
        {
            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
            BoundsInt bounds = new BoundsInt(0, 0, 0, source.Width, source.Height, 1);
            floorTilemap.SetTilesBlock(bounds, floorBlock);
            wallTilemap.SetTilesBlock(bounds, wallBlock);
        }

        private void SynchronizeAllCells()
        {
            TileBase[] floorBlock = new TileBase[gridData.CellCount];
            TileBase[] wallBlock = new TileBase[gridData.CellCount];
            PopulateBlocks(gridData, floorBlock, wallBlock);
            RenderBlocks(gridData, floorBlock, wallBlock);
        }

        private void Subscribe()
        {
            if (gridData != null)
            {
                gridData.CellChanged -= HandleCellChanged;
                gridData.CellChanged += HandleCellChanged;
            }
        }

        private void Unsubscribe()
        {
            if (gridData != null)
            {
                gridData.CellChanged -= HandleCellChanged;
            }
        }

        private void SubscribeToChunks()
        {
            if (chunkStore != null)
            {
                chunkStore.CellChanged -= HandleChunkCellChanged;
                chunkStore.CellChanged += HandleChunkCellChanged;
            }
        }

        private void UnsubscribeFromChunks()
        {
            if (chunkStore != null)
            {
                chunkStore.CellChanged -= HandleChunkCellChanged;
            }
        }

        private void HandleCellChanged(WorldGridChange change)
        {
            if (!isActiveAndEnabled)
            {
                // 비활성 상태에서는 Tilemap을 건드리지 않고, 다시 켤 때 최신 데이터로 전체 동기화합니다.
                needsFullSynchronization = true;
                return;
            }

            if (gridData == null)
            {
                return;
            }

            Vector3Int tileCoordinate = new Vector3Int(change.Coordinate.X, change.Coordinate.Y, 0);
            if (change.Layer == WorldGridLayer.Floor)
            {
                TileBase tile = gridData.TryGetFloor(change.Coordinate, out WorldFloorData floor)
                    ? GetFloorTile(floor.Type)
                    : null;
                SetTileIfChanged(floorTilemap, tileCoordinate, tile);
                return;
            }

            TileBase wallTile = gridData.TryGetWall(change.Coordinate, out WorldWallData wall)
                ? GetWallTile(wall.Type)
                : null;

            // 내구도만 바뀌고 같은 Tile이 유지되는 경우 Tilemap과 Collider를 다시 갱신하지 않습니다.
            SetTileIfChanged(wallTilemap, tileCoordinate, wallTile);
        }

        private void HandleChunkCellChanged(WorldCellChange change)
        {
            if (!isActiveAndEnabled)
            {
                needsChunkSynchronization = true;
                return;
            }

            if (chunkStore == null ||
                !chunkStore.TryGetChunkAddress(change.Coordinate, out WorldChunkCoordinate chunk, out _) ||
                !chunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState presentationState) ||
                presentationState != WorldChunkPresentationState.Displayed ||
                !chunkStore.TryGetReadyGrid(chunk, out WorldGrid source) ||
                !chunkStore.TryGetReadyGridWorldOrigin(chunk, out WorldCellCoordinate gridWorldOrigin) ||
                !TryGetChunkCellTileCoordinate(source, gridWorldOrigin, change.Coordinate, out Vector3Int tileCoordinate))
            {
                return;
            }

            if (change.Layer == WorldGridLayer.Floor)
            {
                TileBase tile = source.TryGetFloor(
                    GetLocalCoordinate(change.Coordinate, gridWorldOrigin),
                    out WorldFloorData floor)
                    ? GetFloorTile(floor.Type)
                    : null;
                SetTileIfChanged(floorTilemap, tileCoordinate, tile);
                return;
            }

            TileBase wallTile = source.TryGetWall(
                GetLocalCoordinate(change.Coordinate, gridWorldOrigin),
                out WorldWallData wall)
                ? GetWallTile(wall.Type)
                : null;

            // 표시 Tile이 실제로 달라진 경우에만 Collider 재구성을 요청합니다.
            if (SetTileIfChanged(wallTilemap, tileCoordinate, wallTile))
            {
                ProcessWallColliderChanges();
            }
        }

        private bool TryGetChunkCellTileCoordinate(
            WorldGrid source,
            WorldCellCoordinate gridWorldOrigin,
            WorldCellCoordinate worldCell,
            out Vector3Int tileCoordinate)
        {
            tileCoordinate = default;
            WorldGridCoordinate localCoordinate = GetLocalCoordinate(worldCell, gridWorldOrigin);
            if (!source.IsInBounds(localCoordinate) ||
                !source.TryGetCellCenterWorldPosition(localCoordinate, out float centerX, out float centerY))
            {
                return false;
            }

            tileCoordinate = worldGrid.WorldToCell(new Vector3(centerX, centerY, worldGrid.transform.position.z));
            return true;
        }

        private static WorldGridCoordinate GetLocalCoordinate(
            WorldCellCoordinate worldCell,
            WorldCellCoordinate gridWorldOrigin)
        {
            return new WorldGridCoordinate(
                (int)((long)worldCell.X - gridWorldOrigin.X),
                (int)((long)worldCell.Y - gridWorldOrigin.Y));
        }

        private static bool SetTileIfChanged(Tilemap tilemap, Vector3Int coordinate, TileBase tile)
        {
            if (tilemap.GetTile(coordinate) != tile)
            {
                tilemap.SetTile(coordinate, tile);
                return true;
            }

            return false;
        }

        private TileBase GetFloorTile(FloorType type)
        {
            switch (type)
            {
                case FloorType.Ground: return groundFloorTile;
                case FloorType.Soil: return soilFloorTile;
                case FloorType.Stone: return stoneFloorTile;
                default: return null;
            }
        }

        private TileBase GetWallTile(WallType type)
        {
            switch (type)
            {
                case WallType.Soil: return soilWallTile;
                case WallType.Stone: return stoneWallTile;
                case WallType.CopperOre:
                    return GetColoredOreTile(
                        copperOreWallTile != null ? copperOreWallTile : stoneWallTile,
                        ref generatedCopperOreWallTile,
                        CopperOreColor,
                        "CopperOre");
                case WallType.IronOre:
                    return GetColoredOreTile(
                        ironOreWallTile != null ? ironOreWallTile : stoneWallTile,
                        ref generatedIronOreWallTile,
                        IronOreColor,
                        "IronOre");
                default: return (int)type > (int)WallType.Stone ? stoneWallTile : null;
            }
        }

        private TileBase GetColoredOreTile(
            TileBase source,
            ref TileBase cachedTile,
            Color32 color,
            string oreName)
        {
            if (source == null || cachedTile != null)
            {
                return cachedTile != null ? cachedTile : source;
            }

            // Tile 복제본은 Sprite와 ColliderType을 그대로 유지하면서 색만 바꾸므로
            // 공유 돌벽 에셋과 일반 돌벽의 외형·충돌 설정은 영향을 받지 않습니다.
            Tile sourceTile = source as Tile;
            if (sourceTile == null)
            {
                Debug.LogError($"{oreName} 표시용 원본은 Unity Tile 에셋이어야 합니다.", this);
                return source;
            }

            Tile coloredTile = Instantiate(sourceTile);
            coloredTile.name = $"{sourceTile.name}_{oreName}_Runtime";
            coloredTile.color = color;
            coloredTile.hideFlags = HideFlags.HideAndDontSave;
            cachedTile = coloredTile;
            return cachedTile;
        }

        private void DestroyGeneratedTile(ref TileBase tile)
        {
            if (tile == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(tile);
            }
            else
            {
                DestroyImmediate(tile);
            }

            tile = null;
        }
    }
}
