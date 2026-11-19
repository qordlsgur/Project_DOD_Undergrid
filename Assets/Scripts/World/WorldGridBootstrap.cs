using System;
using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// Main 씬의 월드 설정으로 전체 WorldGrid 또는 청크 데이터 저장소를 초기화합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldGridBootstrap : MonoBehaviour
    {
        [Serializable]
        private struct OreGenerationEntry
        {
            public WallType wallType;
            public WallType hostWallType;
            public WorldResourceType resourceType;
            public int noiseChannel;
            public float noiseFrequency;
            [Range(0f, 1f)] public float spawnThreshold;
            [Min(1)] public int maxDurability;
            [Min(1)] public int dropAmount;

            public WorldOreDefinition ToDefinition()
            {
                return new WorldOreDefinition(
                    wallType,
                    hostWallType,
                    resourceType,
                    noiseChannel,
                    noiseFrequency,
                    spawnThreshold,
                    maxDurability,
                    dropAmount);
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

        [Header("절차적 지형 생성")]
        [SerializeField] private Transform playerStartTransform;
        [SerializeField] private int terrainSeed = 1847;
        [SerializeField, Min(0.001f)] private float wallNoiseFrequency = 0.12f;
        [SerializeField, Range(0f, 1f)] private float wallDensityThreshold = 0.66f;
        [SerializeField, Range(0f, 1f)] private float stoneWallRatio = 0.35f;
        [SerializeField, Min(1)] private int wallMaxDurability = 10;
        [SerializeField, Min(0.001f)] private float caveNoiseFrequency = 0.1f;
        [SerializeField, Range(0f, 1f)] private float caveCarveThreshold = 0.58f;
        [SerializeField, Min(1)] private int minimumMajorCaveSize = 16;
        [SerializeField, Min(1)] private int corridorWidth = 2;
        [SerializeField, Min(0f)] private float safeRadius = 5f;
        [Tooltip("테스트 적 등 추가 시작 위치입니다. 플레이어와 같은 안전 반경이 적용됩니다.")]
        [SerializeField] private Vector2[] additionalSafeWorldPositions;

        [Header("광물 생성")]
        [Tooltip("각 항목은 바탕 벽 종류와 별도 Seed 노이즈 채널을 사용해 광맥을 배치합니다.")]
        [SerializeField] private OreGenerationEntry[] oreGeneration =
        {
            new OreGenerationEntry
            {
                wallType = WallType.CopperOre,
                hostWallType = WallType.Stone,
                resourceType = WorldResourceType.Copper,
                noiseChannel = 0x217A613D,
                noiseFrequency = 0.09f,
                spawnThreshold = 0.78f,
                maxDurability = 14,
                dropAmount = 2
            },
            new OreGenerationEntry
            {
                wallType = WallType.IronOre,
                hostWallType = WallType.Stone,
                resourceType = WorldResourceType.Iron,
                noiseChannel = 0x4D8B9271,
                noiseFrequency = 0.13f,
                spawnThreshold = 0.86f,
                maxDurability = 18,
                dropAmount = 1
            }
        };

        private WorldOreDefinition[] oreDefinitions;

        [Header("전체 월드 지형 생성")]
        [SerializeField] private bool useChunkGeneration = true;
        [SerializeField, Min(1)] private int chunkSize = 32;

        [Header("청크 스트리밍")]
        [SerializeField] private bool useChunkStreaming;

        /// <summary>이 컴포넌트가 생성하고 소유하는 DOD 월드 데이터입니다.</summary>
        public WorldGrid GridData { get; private set; }
        public WorldChunkStore ChunkStore { get; private set; }
        public WorldGridWorld ChunkWorld { get; private set; }
        public WorldChunkGenerationPipeline ChunkGenerationPipeline { get; private set; }
        public bool IsChunkStreamingEnabled => useChunkStreaming;
        public float WorldMinimumX => origin.x;
        public float WorldMinimumY => origin.y;
        public float WorldMaximumX => origin.x + width * cellWidth;
        public float WorldMaximumY => origin.y + height * cellHeight;

        private void Awake()
        {
            InitializeWorld();
        }

        /// <summary>
        /// 월드 좌표 설정과 일치하는지 확인한 뒤 초기 데이터를 만들고 Tilemap 표현을 시작합니다.
        /// </summary>
        public bool InitializeWorld()
        {
            if (GridData != null || ChunkStore != null)
            {
                return true;
            }

            if (!ValidateReferencesAndAlignment())
            {
                return false;
            }

            if (playerStartTransform == null)
            {
                Debug.LogError("절차적 지형의 안전 영역을 계산할 Player Transform을 연결해야 합니다.", this);
                return false;
            }

            if (!GridDataCoordinateForWorldPosition(playerStartTransform.position, out WorldGridCoordinate playerStart))
            {
                Debug.LogError("Player 시작 위치가 WorldGrid 경계 안에 있어야 합니다.", this);
                return false;
            }

            try
            {
                oreDefinitions = BuildOreDefinitions();
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"광물 생성 설정이 유효하지 않습니다: {exception.Message}", this);
                return false;
            }

            if (useChunkStreaming)
            {
                return InitializeStreamingWorld(playerStart);
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
            if (useChunkGeneration)
            {
                if (!TryGenerateTerrainByChunks(playerStart))
                {
                    GridData = null;
                    return false;
                }
            }
            else
            {
                // 비교용 기존 전체 월드 생성 경로를 유지합니다.
                PlaceProceduralWalls(playerStart);
                WorldTerrainGenerator.CarveCaves(
                    GridData,
                    terrainSeed,
                    caveNoiseFrequency,
                    caveCarveThreshold);
                if (!WorldTerrainGenerator.ConnectMajorCaves(
                    GridData,
                    playerStart,
                    minimumMajorCaveSize,
                    corridorWidth,
                    out _))
                {
                    Debug.LogError("전체 월드 생성 경로에서 주요 동굴을 Player 시작 영역과 연결하지 못했습니다.", this);
                    GridData = null;
                    return false;
                }
            }

            if (!tilemapView.Initialize(GridData))
            {
                GridData = null;
                return false;
            }

            return true;
        }

        private bool InitializeStreamingWorld(WorldGridCoordinate localPlayerStart)
        {
            if (chunkSize <= 0 || !TryGetGridWorldCellOrigin(out WorldCellCoordinate worldCellOrigin))
            {
                Debug.LogError("청크 스트리밍은 양수 청크 크기와 절대 셀 격자에 정렬된 월드 원점이 필요합니다.", this);
                return false;
            }

            long maximumWorldCellX = (long)worldCellOrigin.X + width;
            long maximumWorldCellY = (long)worldCellOrigin.Y + height;
            if (maximumWorldCellX > (long)int.MaxValue + 1L ||
                maximumWorldCellY > (long)int.MaxValue + 1L)
            {
                Debug.LogError("월드 경계가 절대 셀 좌표 범위를 초과합니다.", this);
                return false;
            }

            var worldBounds = new WorldCellBounds(
                worldCellOrigin.X,
                worldCellOrigin.Y,
                maximumWorldCellX,
                maximumWorldCellY);
            WorldCellCoordinate playerStart = new WorldCellCoordinate(
                checked(worldCellOrigin.X + localPlayerStart.X),
                checked(worldCellOrigin.Y + localPlayerStart.Y));

            try
            {
                ChunkStore = new WorldChunkStore(worldBounds, chunkSize, cellWidth, cellHeight);
                ChunkWorld = new WorldGridWorld(ChunkStore);
                var settings = new WorldChunkGenerationSettings(
                    terrainSeed,
                    wallNoiseFrequency,
                    wallDensityThreshold,
                    stoneWallRatio,
                    wallMaxDurability,
                    caveNoiseFrequency,
                    caveCarveThreshold,
                    minimumMajorCaveSize,
                    corridorWidth,
                    playerStart,
                    safeRadius,
                    GetAdditionalSafeWorldCells(),
                    oreDefinitions);
                ChunkGenerationPipeline = new WorldChunkGenerationPipeline(ChunkStore, settings);
            }
            catch (ArgumentException exception)
            {
                ChunkStore = null;
                ChunkWorld = null;
                ChunkGenerationPipeline = null;
                Debug.LogError($"청크 월드 설정이 유효하지 않습니다: {exception.Message}", this);
                return false;
            }

            if (!tilemapView.InitializeChunks(ChunkStore))
            {
                ChunkStore = null;
                ChunkWorld = null;
                ChunkGenerationPipeline = null;
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

        private bool GridDataCoordinateForWorldPosition(Vector3 worldPosition, out WorldGridCoordinate coordinate)
        {
            coordinate = default;
            double cellX = Math.Floor(((double)worldPosition.x - origin.x) / cellWidth);
            double cellY = Math.Floor(((double)worldPosition.y - origin.y) / cellHeight);
            if (cellX < 0 || cellX >= width || cellY < 0 || cellY >= height)
            {
                return false;
            }

            coordinate = new WorldGridCoordinate((int)cellX, (int)cellY);
            return true;
        }

        private void PlaceProceduralWalls(WorldGridCoordinate playerStart)
        {
            WorldGridCoordinate[] additionalSafeCenters = null;
            if (additionalSafeWorldPositions != null && additionalSafeWorldPositions.Length > 0)
            {
                additionalSafeCenters = new WorldGridCoordinate[additionalSafeWorldPositions.Length];
                for (int i = 0; i < additionalSafeWorldPositions.Length; i++)
                {
                    Vector2 position = additionalSafeWorldPositions[i];
                    if (!GridDataCoordinateForWorldPosition(position, out additionalSafeCenters[i]))
                    {
                        Debug.LogWarning($"추가 안전 위치가 WorldGrid 경계 밖이라 무시됩니다: {position}", this);
                        additionalSafeCenters[i] = new WorldGridCoordinate(int.MinValue, int.MinValue);
                    }
                }
            }

            WorldTerrainGenerator.GenerateWalls(
                GridData,
                terrainSeed,
                wallNoiseFrequency,
                wallDensityThreshold,
                stoneWallRatio,
                wallMaxDurability,
                playerStart,
                safeRadius,
                additionalSafeCenters,
                oreDefinitions);
        }

        private bool TryGenerateTerrainByChunks(WorldGridCoordinate playerStart)
        {
            if (chunkSize <= 0 || !TryGetGridWorldCellOrigin(out WorldCellCoordinate gridWorldOrigin))
            {
                Debug.LogError("청크 생성은 양수 청크 크기와 셀 격자에 정렬된 월드 원점이 필요합니다.", this);
                return false;
            }

            WorldCellCoordinate playerWorldCell = new WorldCellCoordinate(
                checked(gridWorldOrigin.X + playerStart.X),
                checked(gridWorldOrigin.Y + playerStart.Y));
            WorldCellCoordinate[] additionalSafeCenters = GetAdditionalSafeWorldCells();

            long maximumWorldCellX = (long)gridWorldOrigin.X + width - 1;
            long maximumWorldCellY = (long)gridWorldOrigin.Y + height - 1;
            if (maximumWorldCellX > int.MaxValue || maximumWorldCellY > int.MaxValue)
            {
                Debug.LogError("월드 범위가 절대 셀 좌표에서 지원하는 범위를 초과합니다.", this);
                return false;
            }

            var worldBounds = new WorldCellBounds(
                gridWorldOrigin.X,
                gridWorldOrigin.Y,
                maximumWorldCellX + 1L,
                maximumWorldCellY + 1L);

            WorldChunkCoordinate minimumChunk = WorldChunkCoordinate.FromWorldCell(
                gridWorldOrigin,
                chunkSize);
            WorldChunkCoordinate maximumChunk = WorldChunkCoordinate.FromWorldCell(
                new WorldCellCoordinate((int)maximumWorldCellX, (int)maximumWorldCellY),
                chunkSize);

            for (int chunkY = minimumChunk.Y; chunkY <= maximumChunk.Y; chunkY++)
            {
                for (int chunkX = minimumChunk.X; chunkX <= maximumChunk.X; chunkX++)
                {
                    var chunk = new WorldChunkCoordinate(chunkX, chunkY);
                    WorldTerrainGenerator.GenerateChunkWalls(
                        GridData,
                        gridWorldOrigin,
                        chunk,
                        chunkSize,
                        terrainSeed,
                        wallNoiseFrequency,
                        wallDensityThreshold,
                        stoneWallRatio,
                        wallMaxDurability,
                        playerWorldCell,
                        safeRadius,
                        additionalSafeCenters,
                        oreDefinitions);
                }
            }

            for (int chunkY = minimumChunk.Y; chunkY <= maximumChunk.Y; chunkY++)
            {
                for (int chunkX = minimumChunk.X; chunkX <= maximumChunk.X; chunkX++)
                {
                    WorldTerrainGenerator.CarveChunkCaves(
                        GridData,
                        gridWorldOrigin,
                        new WorldChunkCoordinate(chunkX, chunkY),
                        chunkSize,
                        terrainSeed,
                        caveNoiseFrequency,
                        caveCarveThreshold);
                }
            }

            for (int chunkY = minimumChunk.Y; chunkY <= maximumChunk.Y; chunkY++)
            {
                for (int chunkX = minimumChunk.X; chunkX <= maximumChunk.X; chunkX++)
                {
                    var chunk = new WorldChunkCoordinate(chunkX, chunkY);
                    if (!WorldTerrainGenerator.ConnectChunkEntrances(
                        GridData,
                        gridWorldOrigin,
                        worldBounds,
                        chunk,
                        chunkSize,
                        terrainSeed,
                        minimumMajorCaveSize,
                        corridorWidth,
                        out _))
                    {
                        Debug.LogError($"청크 내부 입구와 주요 동굴을 연결하지 못했습니다: ({chunkX}, {chunkY})", this);
                        return false;
                    }
                }
            }

            return true;
        }

        private bool TryGetGridWorldCellOrigin(out WorldCellCoordinate worldCellOrigin)
        {
            worldCellOrigin = default;
            double cellX = (double)origin.x / cellWidth;
            double cellY = (double)origin.y / cellHeight;
            double roundedX = Math.Round(cellX);
            double roundedY = Math.Round(cellY);
            if (double.IsNaN(cellX) || double.IsInfinity(cellX) ||
                double.IsNaN(cellY) || double.IsInfinity(cellY) ||
                Math.Abs(cellX - roundedX) > 0.00001d ||
                Math.Abs(cellY - roundedY) > 0.00001d ||
                roundedX < int.MinValue || roundedX > int.MaxValue ||
                roundedY < int.MinValue || roundedY > int.MaxValue)
            {
                return false;
            }

            worldCellOrigin = new WorldCellCoordinate((int)roundedX, (int)roundedY);
            return true;
        }

        private WorldCellCoordinate[] GetAdditionalSafeWorldCells()
        {
            if (additionalSafeWorldPositions == null || additionalSafeWorldPositions.Length == 0)
            {
                return null;
            }

            var safeCells = new WorldCellCoordinate[additionalSafeWorldPositions.Length];
            for (int i = 0; i < additionalSafeWorldPositions.Length; i++)
            {
                Vector2 position = additionalSafeWorldPositions[i];
                safeCells[i] = new WorldCellCoordinate(
                    (int)Math.Floor((double)position.x / cellWidth),
                    (int)Math.Floor((double)position.y / cellHeight));
            }

            return safeCells;
        }

        private WorldOreDefinition[] BuildOreDefinitions()
        {
            if (oreGeneration == null || oreGeneration.Length == 0)
            {
                return Array.Empty<WorldOreDefinition>();
            }

            var definitions = new WorldOreDefinition[oreGeneration.Length];
            for (int i = 0; i < oreGeneration.Length; i++)
            {
                definitions[i] = oreGeneration[i].ToDefinition();
                for (int previous = 0; previous < i; previous++)
                {
                    if (definitions[previous].WallType == definitions[i].WallType ||
                        definitions[previous].NoiseChannel == definitions[i].NoiseChannel)
                    {
                        throw new ArgumentException("광물 벽 종류와 노이즈 채널은 각각 고유해야 합니다.");
                    }
                }
            }

            return definitions;
        }
    }
}
