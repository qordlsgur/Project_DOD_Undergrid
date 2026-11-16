using System;
using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// Main 씬의 월드 설정으로 순수 DOD 그리드를 만들고 초기 셀 데이터를 준비합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldGridBootstrap : MonoBehaviour
    {
        [Serializable]
        private sealed class InitialWallPlacement
        {
            [SerializeField] private Vector2Int coordinate;
            [SerializeField] private WallType type = WallType.Stone;
            [SerializeField, Min(1)] private int maxDurability = 10;

            public Vector2Int Coordinate => coordinate;
            public WallType Type => type;
            public int MaxDurability => maxDurability;

            public InitialWallPlacement() { }

            public InitialWallPlacement(int x, int y, WallType wallType, int durability)
            {
                coordinate = new Vector2Int(x, y);
                type = wallType;
                maxDurability = durability;
            }
        }

        [Header("월드 크기 및 좌표")]
        [SerializeField, Min(1)] private int width = 64;
        [SerializeField, Min(1)] private int height = 64;
        [SerializeField, Min(0.01f)] private float cellWidth = 1f;
        [SerializeField, Min(0.01f)] private float cellHeight = 1f;
        [SerializeField] private Vector2 origin = new Vector2(-32f, -32f);

        [Header("Unity 표현 연결")]
        [SerializeField] private Grid worldGrid;
        [SerializeField] private WorldGridTilemapView tilemapView;

        [Header("초기 테스트 벽")]
        [SerializeField] private InitialWallPlacement[] initialWalls =
        {
            new InitialWallPlacement(30, 38, WallType.Stone, 10),
            new InitialWallPlacement(31, 38, WallType.Stone, 10),
            new InitialWallPlacement(32, 38, WallType.Stone, 10),
            new InitialWallPlacement(33, 38, WallType.Stone, 10),
            new InitialWallPlacement(34, 38, WallType.Stone, 10)
        };

        /// <summary>이 컴포넌트가 생성하고 소유하는 DOD 월드 데이터입니다.</summary>
        public WorldGrid GridData { get; private set; }

        private void Awake()
        {
            InitializeWorld();
        }

        /// <summary>
        /// 월드 좌표 설정과 일치하는지 확인한 뒤 초기 데이터를 만들고 Tilemap 표현을 시작합니다.
        /// </summary>
        public bool InitializeWorld()
        {
            if (GridData != null)
            {
                return true;
            }

            if (!ValidateReferencesAndAlignment())
            {
                return false;
            }

            try
            {
                GridData = new WorldGrid(width, height, cellWidth, cellHeight, origin.x, origin.y);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                Debug.LogError($"WorldGrid 설정이 유효하지 않습니다: {exception.Message}", this);
                return false;
            }

            PlaceInitialFloors();
            PlaceInitialWalls();
            if (!tilemapView.Initialize(GridData))
            {
                GridData = null;
                return false;
            }

            return true;
        }

        private bool ValidateReferencesAndAlignment()
        {
            if (worldGrid == null || tilemapView == null)
            {
                Debug.LogError("Grid와 WorldGridTilemapView 참조를 연결해야 합니다.", this);
                return false;
            }

            Vector3 cellSize = worldGrid.cellSize;
            Vector3 cellOrigin = worldGrid.CellToWorld(Vector3Int.zero);
            Vector3 gridScale = worldGrid.transform.lossyScale;
            bool cellSizeMatches = Mathf.Approximately(cellSize.x, cellWidth) &&
                                   Mathf.Approximately(cellSize.y, cellHeight) &&
                                   Mathf.Approximately(worldGrid.cellGap.x, 0f) &&
                                   Mathf.Approximately(worldGrid.cellGap.y, 0f);
            bool originMatches = Mathf.Approximately(cellOrigin.x, origin.x) &&
                                 Mathf.Approximately(cellOrigin.y, origin.y);
            bool scaleMatches = Mathf.Approximately(gridScale.x, 1f) &&
                                Mathf.Approximately(gridScale.y, 1f);
            bool rotationMatches = Quaternion.Angle(worldGrid.transform.rotation, Quaternion.identity) < 0.001f;

            if (!cellSizeMatches || !originMatches || !scaleMatches || !rotationMatches ||
                worldGrid.cellLayout != GridLayout.CellLayout.Rectangle ||
                worldGrid.cellSwizzle != GridLayout.CellSwizzle.XYZ)
            {
                Debug.LogError("Grid의 사각 셀 크기, 원점, 간격 또는 Transform이 WorldGrid 설정과 일치하지 않습니다.", this);
                return false;
            }

            return true;
        }

        private void PlaceInitialFloors()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridData.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground);
                }
            }
        }

        private void PlaceInitialWalls()
        {
            if (initialWalls == null)
            {
                return;
            }

            for (int i = 0; i < initialWalls.Length; i++)
            {
                InitialWallPlacement placement = initialWalls[i];
                if (placement == null)
                {
                    continue;
                }

                WorldGridCoordinate coordinate = new WorldGridCoordinate(
                    placement.Coordinate.x,
                    placement.Coordinate.y);

                if (!GridData.TryPlaceWall(coordinate, placement.Type, placement.MaxDurability))
                {
                    Debug.LogWarning($"초기 테스트 벽을 배치하지 못했습니다. 좌표: {coordinate}", this);
                }
            }
        }
    }
}
