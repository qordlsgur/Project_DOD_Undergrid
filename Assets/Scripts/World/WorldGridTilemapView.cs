using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;
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

        private WorldGrid gridData;
        private bool needsFullSynchronization;

        /// <summary>
        /// 입력이 유효하면 현재 Tilemap과 구독을 새 데이터에 맞춰 교체합니다.
        /// 실패하면 기존 데이터 참조, 타일 표시, 이벤트 구독을 유지합니다.
        /// </summary>
        public bool Initialize(WorldGrid source)
        {
            // 유효성 검사는 기존 표시와 구독을 바꾸기 전에 끝내므로 입력 오류는 현재 상태를 보존합니다.
            if (source == null || !ValidateTilemaps())
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
            needsFullSynchronization = gridData != null;
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

        private static void SetTileIfChanged(Tilemap tilemap, Vector3Int coordinate, TileBase tile)
        {
            if (tilemap.GetTile(coordinate) != tile)
            {
                tilemap.SetTile(coordinate, tile);
            }
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
                default: return null;
            }
        }
    }
}
