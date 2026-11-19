using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>청크별 결정적 지형과 광맥 생성에 필요한 불변 설정입니다.</summary>
    public sealed class WorldChunkGenerationSettings
    {
        private readonly WorldCellCoordinate[] additionalSafeCenters;
        private readonly WorldOreDefinition[] oreDefinitions;

        public int Seed { get; }
        public float WallNoiseFrequency { get; }
        public float WallDensityThreshold { get; }
        public float StoneWallRatio { get; }
        public int WallMaxDurability { get; }
        public float CaveNoiseFrequency { get; }
        public float CaveCarveThreshold { get; }
        public int MinimumMajorCaveSize { get; }
        public int CorridorWidth { get; }
        public WorldCellCoordinate PlayerStart { get; }
        public float SafeRadius { get; }
        internal WorldCellCoordinate[] AdditionalSafeCenters => additionalSafeCenters;
        internal WorldOreDefinition[] OreDefinitions => oreDefinitions;

        public WorldChunkGenerationSettings(
            int seed,
            float wallNoiseFrequency,
            float wallDensityThreshold,
            float stoneWallRatio,
            int wallMaxDurability,
            float caveNoiseFrequency,
            float caveCarveThreshold,
            int minimumMajorCaveSize,
            int corridorWidth,
            WorldCellCoordinate playerStart,
            float safeRadius,
            WorldCellCoordinate[] additionalSafeCenters = null,
            WorldOreDefinition[] oreDefinitions = null)
        {
            if (!IsFinitePositive(wallNoiseFrequency))
            {
                throw new ArgumentOutOfRangeException(nameof(wallNoiseFrequency));
            }

            if (!IsFinite(wallDensityThreshold) || !IsFinite(stoneWallRatio) || wallMaxDurability <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wallDensityThreshold));
            }

            if (!IsFinitePositive(caveNoiseFrequency) || !IsFinite(caveNoiseFrequency * 2.7f) ||
                !IsFinite(caveCarveThreshold))
            {
                throw new ArgumentOutOfRangeException(nameof(caveNoiseFrequency));
            }

            if (minimumMajorCaveSize < 1 || corridorWidth < 1 || !IsFinite(safeRadius) || safeRadius < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumMajorCaveSize));
            }

            Seed = seed;
            WallNoiseFrequency = wallNoiseFrequency;
            WallDensityThreshold = wallDensityThreshold;
            StoneWallRatio = stoneWallRatio;
            WallMaxDurability = wallMaxDurability;
            CaveNoiseFrequency = caveNoiseFrequency;
            CaveCarveThreshold = caveCarveThreshold;
            MinimumMajorCaveSize = minimumMajorCaveSize;
            CorridorWidth = corridorWidth;
            PlayerStart = playerStart;
            SafeRadius = safeRadius;
            this.additionalSafeCenters = additionalSafeCenters == null
                ? Array.Empty<WorldCellCoordinate>()
                : (WorldCellCoordinate[])additionalSafeCenters.Clone();
            this.oreDefinitions = oreDefinitions == null
                ? Array.Empty<WorldOreDefinition>()
                : (WorldOreDefinition[])oreDefinitions.Clone();
            ValidateOreDefinitions(this.oreDefinitions);
        }

        private static void ValidateOreDefinitions(WorldOreDefinition[] definitions)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (!definitions[i].IsValid)
                {
                    throw new ArgumentOutOfRangeException(nameof(definitions), "광물 정의가 유효하지 않습니다.");
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (definitions[previous].WallType == definitions[i].WallType ||
                        definitions[previous].NoiseChannel == definitions[i].NoiseChannel)
                    {
                        throw new ArgumentException("광물 벽 종류와 노이즈 채널은 각각 고유해야 합니다.", nameof(definitions));
                    }
                }
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

    /// <summary>
    /// 대기 청크의 바닥, 벽·광맥·동굴·연결 통로와 저장된 플레이어 변경을 준비한 뒤 Ready로 전환합니다.
    /// 생성 중에는 데이터 계층에 직접 기록하므로 지형 생성 결과가 플레이어 변경 기록에 섞이지 않습니다.
    /// </summary>
    public sealed class WorldChunkGenerationPipeline
    {
        private readonly WorldChunkStore chunkStore;
        private readonly WorldChunkGenerationSettings settings;

        public WorldChunkGenerationPipeline(
            WorldChunkStore chunkStore,
            WorldChunkGenerationSettings settings)
        {
            this.chunkStore = chunkStore ?? throw new ArgumentNullException(nameof(chunkStore));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (!chunkStore.WorldBounds.Contains(settings.PlayerStart.X, settings.PlayerStart.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "플레이어 시작 셀은 월드 범위 안에 있어야 합니다.");
            }

            if (settings.CorridorWidth > chunkStore.ChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(settings), "통로 너비는 청크 크기보다 클 수 없습니다.");
            }
        }

        /// <summary>대기 상태의 단일 청크를 생성하고 복원한 뒤 조회 가능한 상태로 전환합니다.</summary>
        public bool TryGenerateQueuedChunk(WorldChunkCoordinate chunk)
        {
            if (chunkStore.GetDataState(chunk) != WorldChunkDataState.Queued ||
                !chunkStore.TryBeginGeneration(chunk, out WorldGrid grid, out WorldCellCoordinate gridWorldOrigin))
            {
                return false;
            }

            try
            {
                if (!PlaceGroundFloor(grid) ||
                    !GenerateWallsAndCaves(grid, gridWorldOrigin, chunk) ||
                    !WorldTerrainGenerator.ConnectChunkEntrances(
                        grid,
                        gridWorldOrigin,
                        chunkStore.WorldBounds,
                        chunk,
                        chunkStore.ChunkSize,
                        settings.Seed,
                        settings.MinimumMajorCaveSize,
                        settings.CorridorWidth,
                        out _) ||
                    !chunkStore.TryApplyRecordedWallMutations(chunk, grid, gridWorldOrigin) ||
                    !chunkStore.TryMarkReady(chunk))
                {
                    chunkStore.TryMarkFailed(chunk);
                    return false;
                }

                return true;
            }
            catch
            {
                chunkStore.TryMarkFailed(chunk);
                throw;
            }
        }

        private bool PlaceGroundFloor(WorldGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (!grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool GenerateWallsAndCaves(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk)
        {
            WorldTerrainGenerator.GenerateChunkWalls(
                grid,
                gridWorldOrigin,
                chunk,
                chunkStore.ChunkSize,
                settings.Seed,
                settings.WallNoiseFrequency,
                settings.WallDensityThreshold,
                settings.StoneWallRatio,
                settings.WallMaxDurability,
                settings.PlayerStart,
                settings.SafeRadius,
                settings.AdditionalSafeCenters,
                settings.OreDefinitions);

            WorldTerrainGenerator.CarveChunkCaves(
                grid,
                gridWorldOrigin,
                chunk,
                chunkStore.ChunkSize,
                settings.Seed,
                settings.CaveNoiseFrequency,
                settings.CaveCarveThreshold);
            return true;
        }
    }
}
