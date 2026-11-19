using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;

namespace Prohect_DOD_Undergird.DOD.World.Tests
{
    /// <summary>
    /// 절차적 벽 생성의 재현성, 안전 영역, 종류 분포와 기존 채굴 데이터 흐름을 검증합니다.
    /// </summary>
    public sealed class WorldTerrainGeneratorTests
    {
        private const int Width = 64;
        private const int Height = 64;
        private const float Frequency = 0.12f;
        private const float DensityThreshold = 0.66f;
        private const float StoneRatio = 0.35f;
        private static readonly WorldGridCoordinate PlayerStart = new WorldGridCoordinate(32, 32);

        [Test]
        public void GenerateWalls_SameSeedAndSettings_ProducesSameTerrain()
        {
            WorldGrid first = CreateGrid();
            WorldGrid second = CreateGrid();

            int firstCount = Generate(first, 1847);
            int secondCount = Generate(second, 1847);

            Assert.AreEqual(firstCount, secondCount);
            Assert.AreEqual(first.WallCount, second.WallCount);
            AssertSameWallData(first, second);
        }

        [Test]
        public void GenerateWalls_DifferentSeed_ChangesTerrain()
        {
            WorldGrid first = CreateGrid();
            WorldGrid second = CreateGrid();
            Generate(first, 1847);
            Generate(second, 2917);

            Assert.Greater(CountDifferences(first, second), 0, "다른 Seed는 벽 배치 또는 종류가 달라야 합니다.");
        }

        [Test]
        public void GenerateWalls_WithOreDefinitions_IsDeterministicAndCreatesConfiguredMinerals()
        {
            WorldGrid first = CreateGrid();
            WorldGrid second = CreateGrid();
            WorldOreDefinition[] ores =
            {
                new WorldOreDefinition(
                    WallType.CopperOre, WallType.Soil, WorldResourceType.Copper,
                    0x217A613D, 0.09f, 0.5f, 14, 2),
                new WorldOreDefinition(
                    WallType.IronOre, WallType.Stone, WorldResourceType.Iron,
                    0x4D8B9271, 0.13f, 0.5f, 18, 1)
            };

            GenerateWithOres(first, 1847, ores);
            GenerateWithOres(second, 1847, ores);

            AssertSameWallData(first, second);
            Assert.Greater(CountWallType(first, WallType.CopperOre), 0);
            Assert.Greater(CountWallType(first, WallType.IronOre), 0);
            Assert.IsTrue(first.TryGetWall(FindWall(first, WallType.CopperOre), out WorldWallData copper));
            Assert.AreEqual(WorldResourceType.Copper, copper.ResourceType);
            Assert.AreEqual(2, copper.ResourceAmount);
            Assert.IsTrue(first.TryGetWall(FindWall(first, WallType.IronOre), out WorldWallData iron));
            Assert.AreEqual(WorldResourceType.Iron, iron.ResourceType);
            Assert.AreEqual(1, iron.ResourceAmount);
        }

        [Test]
        public void ChunkGeneration_SameSeedAndChunks_ProducesSameTerrainRegardlessOfOrder()
        {
            WorldGrid forward = CreateGrid();
            WorldGrid reverse = CreateGrid();

            GenerateChunkedTerrain(forward, reverseOrder: false);
            GenerateChunkedTerrain(reverse, reverseOrder: true);

            Assert.AreEqual(forward.WallCount, reverse.WallCount);
            AssertSameWallData(forward, reverse);
        }

        [Test]
        public void ConnectChunkEntrances_AllActiveEntrancesAndMajorCaveShareOneComponent()
        {
            WorldGrid grid = CreateGrid();
            FillWithWalls(grid);
            OpenRectangle(grid, 12, 12, 19, 19);
            int floorCountBeforeConnection = grid.FloorCount;
            var origin = new WorldCellCoordinate(-32, -32);
            var worldBounds = new WorldCellBounds(-32, -32, 32, 32);
            var chunk = new WorldChunkCoordinate(-1, -1);

            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, worldBounds, chunk, 32, 1847, 20, 3, out int entranceCount));

            Assert.AreEqual(2, entranceCount, "2×2 월드에서 대상 청크는 내부 경계 입구 두 개를 가져야 합니다.");
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                chunk, 32, 1847, WorldChunkSide.East, 3, out WorldCellCoordinate eastEntrance));
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                chunk, 32, 1847, WorldChunkSide.North, 3, out WorldCellCoordinate northEntrance));
            AssertChunkConnected(grid, origin, chunk, 32, eastEntrance, northEntrance);
            AssertChunkConnected(grid, origin, chunk, 32, eastEntrance, new WorldCellCoordinate(-16, -16));
            Assert.AreEqual(floorCountBeforeConnection, grid.FloorCount, "통로 생성은 바닥 데이터를 바꾸면 안 됩니다.");
        }

        [Test]
        public void ConnectChunkEntrances_AdjacentNegativeChunksSharePassableBoundaryAndConnect()
        {
            var grid = CreateFilledGrid(64, 32, -32, 0);
            FillWithWalls(grid);
            OpenRectangle(grid, 8, 10, 14, 16);
            OpenRectangle(grid, 42, 10, 48, 16);
            var origin = new WorldCellCoordinate(-32, 0);
            var worldBounds = new WorldCellBounds(-32, 0, 32, 32);
            var westChunk = new WorldChunkCoordinate(-1, 0);
            var eastChunk = new WorldChunkCoordinate(0, 0);

            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, worldBounds, westChunk, 32, 991, 16, 3, out int westEntranceCount));
            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, worldBounds, eastChunk, 32, 991, 16, 3, out int eastEntranceCount));

            Assert.AreEqual(1, westEntranceCount);
            Assert.AreEqual(1, eastEntranceCount);
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                westChunk, 32, 991, WorldChunkSide.East, 3, out WorldCellCoordinate westDoor));
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                eastChunk, 32, 991, WorldChunkSide.West, 3, out WorldCellCoordinate eastDoor));
            Assert.AreEqual(westDoor.Y, eastDoor.Y, "공유 경계의 입구 높이는 양쪽 청크에서 같아야 합니다.");
            Assert.AreEqual(westDoor.X + 1, eastDoor.X, "양쪽 입구 셀은 경계에서 서로 이웃해야 합니다.");
            for (int offset = 0; offset < 3; offset++)
            {
                Assert.IsTrue(grid.IsWalkable(new WorldGridCoordinate(westDoor.X - origin.X, westDoor.Y + offset - origin.Y)));
                Assert.IsTrue(grid.IsWalkable(new WorldGridCoordinate(eastDoor.X - origin.X, eastDoor.Y + offset - origin.Y)));
            }

            AssertWorldConnected(
                grid,
                origin,
                westDoor,
                eastDoor);
        }

        [Test]
        public void ConnectChunkEntrances_UncreatedNeighborStillActivatesDeterministicEntrance()
        {
            var origin = new WorldCellCoordinate(-32, 0);
            var bounds = new WorldCellBounds(-32, 0, 32, 32);
            var chunk = new WorldChunkCoordinate(-1, 0);
            WorldGrid first = CreateFilledGrid(32, 32, -32, 0);
            WorldGrid second = CreateFilledGrid(32, 32, -32, 0);
            FillWithWalls(first);
            FillWithWalls(second);
            OpenRectangle(first, 8, 10, 15, 17);
            OpenRectangle(second, 8, 10, 15, 17);

            Assert.IsFalse(first.TryWorldToCell(0.5f, 12.5f, out _),
                "인접 청크에 해당하는 월드 좌표는 현재 그리드에 저장되어 있지 않아야 합니다.");
            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                first, origin, bounds, chunk, 32, 4201, 16, 3, out int firstEntranceCount));
            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                second, origin, bounds, chunk, 32, 4201, 16, 3, out int secondEntranceCount));

            Assert.AreEqual(1, firstEntranceCount);
            Assert.AreEqual(firstEntranceCount, secondEntranceCount);
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                chunk, 32, 4201, WorldChunkSide.East, 3, out WorldCellCoordinate entrance));
            AssertChunkConnected(first, origin, chunk, 32, entrance, new WorldCellCoordinate(-24, 12));
            AssertSameWallData(first, second);
        }

        [Test]
        public void ConnectChunkEntrances_RepeatedCallDoesNotChangeWallsOrRaiseCellChanged()
        {
            WorldGrid grid = CreateGrid();
            WorldCellCoordinate origin = new WorldCellCoordinate(-32, -32);
            var bounds = new WorldCellBounds(-32, -32, 32, 32);
            var chunk = new WorldChunkCoordinate(-1, -1);
            WorldTerrainGenerator.GenerateChunkWalls(
                grid, origin, chunk, 32, 1847, Frequency, DensityThreshold, StoneRatio, 10,
                new WorldCellCoordinate(0, 0), 5f);
            WorldTerrainGenerator.CarveChunkCaves(grid, origin, chunk, 32, 1847, 0.1f, 0.58f);

            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, bounds, chunk, 32, 1847, 16, 2, out _));
            WorldWallData[] firstResult = CopyWallData(grid);
            int secondRunEventCount = 0;
            System.Action<WorldGridChange> countChanges = _ => secondRunEventCount++;
            grid.CellChanged += countChanges;

            try
            {
                Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                    grid, origin, bounds, chunk, 32, 1847, 16, 2, out _));
            }
            finally
            {
                grid.CellChanged -= countChanges;
            }

            AssertWallSnapshot(grid, firstResult);
            Assert.AreEqual(0, secondRunEventCount, "연결이 완료된 청크를 다시 처리할 때 데이터 변경 이벤트가 없어야 합니다.");
        }

        [Test]
        public void ConnectChunkEntrances_DoesNotCreateDoorwayAtWorldBoundaryOrModifyOutsideGrid()
        {
            var grid = CreateFilledGrid(32, 32, -32, 0);
            FillWithWalls(grid);
            int initialWallCount = grid.WallCount;
            var origin = new WorldCellCoordinate(-32, 0);
            var worldBounds = new WorldCellBounds(-32, 0, 0, 32);

            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, worldBounds, new WorldChunkCoordinate(-1, 0), 32, 1847, 4, 2, out int outerEntranceCount));
            Assert.AreEqual(0, outerEntranceCount, "이웃 청크가 월드 범위 밖이면 입구를 만들면 안 됩니다.");
            Assert.AreEqual(initialWallCount, grid.WallCount);

            Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                grid, origin, worldBounds, new WorldChunkCoordinate(1, 0), 32, 1847, 4, 2, out int outsideEntranceCount));
            Assert.AreEqual(0, outsideEntranceCount);
            Assert.AreEqual(0, WorldTerrainGenerator.GenerateChunkWalls(
                grid,
                origin,
                new WorldChunkCoordinate(1, 0),
                32,
                1847,
                Frequency,
                DensityThreshold,
                StoneRatio,
                10,
                new WorldCellCoordinate(-16, 16),
                5f));
            Assert.AreEqual(0, WorldTerrainGenerator.CarveChunkCaves(
                grid,
                origin,
                new WorldChunkCoordinate(1, 0),
                32,
                1847,
                0.1f,
                0.58f));
            Assert.AreEqual(initialWallCount, grid.WallCount);
        }

        [Test]
        public void ChunkGeneration_OnlyWritesCellsInsideRequestedNegativeChunk()
        {
            WorldGrid grid = CreateGrid();
            WorldCellCoordinate gridWorldOrigin = new WorldCellCoordinate(-32, -32);
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);

            int placedCount = WorldTerrainGenerator.GenerateChunkWalls(
                grid,
                gridWorldOrigin,
                chunk,
                32,
                1847,
                Frequency,
                DensityThreshold,
                StoneRatio,
                10,
                new WorldCellCoordinate(0, 0),
                5f);

            Assert.Greater(placedCount, 0);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    bool hasWall = grid.TryGetWall(new WorldGridCoordinate(x, y), out _);
                    if (hasWall)
                    {
                        Assert.IsTrue(x < 32 && y < 32, "요청한 청크 밖에는 벽이 배치되면 안 됩니다.");
                        Assert.IsTrue(IsOutsidePlayerSafeArea(x, y), "플레이어 안전 반경에는 벽이 배치되면 안 됩니다.");
                    }
                }
            }
        }

        [Test]
        public void WorldChunkCoordinate_MapsNegativeWorldCellsWithFloorDivisionAndRoundTrips()
        {
            var negativeCell = new WorldCellCoordinate(-1, -33);
            WorldChunkCoordinate chunk = WorldChunkCoordinate.FromWorldCell(negativeCell, 32);

            Assert.AreEqual(new WorldChunkCoordinate(-1, -2), chunk);
            WorldGridCoordinate localCell = chunk.ToLocalCell(negativeCell, 32);
            Assert.AreEqual(new WorldGridCoordinate(31, 31), localCell);
            Assert.AreEqual(negativeCell, chunk.ToWorldCell(localCell, 32));

            var boundaryCell = new WorldCellCoordinate(-32, -64);
            WorldChunkCoordinate boundaryChunk = WorldChunkCoordinate.FromWorldCell(boundaryCell, 32);
            Assert.AreEqual(new WorldChunkCoordinate(-1, -2), boundaryChunk);
            Assert.AreEqual(new WorldGridCoordinate(0, 0), boundaryChunk.ToLocalCell(boundaryCell, 32));
        }

        [Test]
        public void GenerateWalls_LeavesPlayerAndConfiguredSpawnAreasWalkable()
        {
            WorldGrid grid = CreateGrid();
            WorldGridCoordinate[] extraSafeCenters =
            {
                new WorldGridCoordinate(34, 32),
                new WorldGridCoordinate(36, 32)
            };

            Generate(grid, 1847, extraSafeCenters);

            AssertSafeArea(grid, PlayerStart, 5);
            AssertSafeArea(grid, extraSafeCenters[0], 5);
            AssertSafeArea(grid, extraSafeCenters[1], 5);
        }

        [Test]
        public void GenerateWalls_CreatesBothWallTypesAndWallsRemainMineable()
        {
            WorldGrid grid = CreateGrid();
            Generate(grid, 1847);
            int soilCount = 0;
            int stoneCount = 0;
            WorldGridCoordinate mineableCoordinate = default;
            WorldWallData mineableWall = default;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    if (!grid.TryGetWall(coordinate, out WorldWallData wall))
                    {
                        continue;
                    }

                    if (wall.Type == WallType.Soil) soilCount++;
                    if (wall.Type == WallType.Stone) stoneCount++;
                    if (!mineableWall.HasWall)
                    {
                        mineableCoordinate = coordinate;
                        mineableWall = wall;
                    }
                }
            }

            Assert.Greater(soilCount, 0);
            Assert.Greater(stoneCount, 0);
            Assert.Greater(grid.WallCount, 0);
            Assert.IsTrue(grid.TryDamageWall(
                mineableCoordinate,
                mineableWall.CurrentDurability,
                out int remainingDurability,
                out bool wasDestroyed));

            Assert.IsTrue(wasDestroyed);
            Assert.AreEqual(0, remainingDurability);
            Assert.IsFalse(grid.TryGetWall(mineableCoordinate, out _));
            Assert.IsTrue(grid.TryGetFloor(mineableCoordinate, out WorldFloorData floor));
            Assert.AreEqual(FloorType.Ground, floor.Type);
            Assert.IsTrue(grid.IsWalkable(mineableCoordinate));
        }

        [Test]
        public void CarveCaves_RemovesOnlyWallsAndPreservesEveryFloorCell()
        {
            WorldGrid grid = CreateGrid();
            WorldTerrainGenerator.GenerateWalls(
                grid, 1847, Frequency, DensityThreshold, StoneRatio, 10, PlayerStart, 5f);
            int floorCountBefore = grid.FloorCount;
            int wallCountBefore = grid.WallCount;

            int removedWallCount = WorldTerrainGenerator.CarveCaves(grid, 1847, 0.1f, 0.58f);

            Assert.Greater(removedWallCount, 0);
            Assert.AreEqual(floorCountBefore, grid.FloorCount);
            Assert.AreEqual(wallCountBefore - removedWallCount, grid.WallCount);
            AssertSafeArea(grid, PlayerStart, 5);
        }

        [Test]
        public void ConnectMajorCaves_OnNonSquareNon64Grid_ConnectsLargeCaveAndLeavesTinyPocketIsolated()
        {
            const int width = 23;
            const int height = 17;
            var grid = new WorldGrid(width, height, 1f, 1f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    Assert.IsTrue(grid.TryPlaceFloor(coordinate, FloorType.Ground));
                    Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 10));
                }
            }

            OpenRectangle(grid, 1, 1, 3, 3);
            OpenRectangle(grid, 12, 8, 15, 11);
            OpenRectangle(grid, 21, 15, 22, 16);
            OpenCell(grid, new WorldGridCoordinate(0, 16));
            int floorCountBefore = grid.FloorCount;
            int wallCountBefore = grid.WallCount;
            WorldGridCoordinate start = new WorldGridCoordinate(2, 2);
            WorldGridCoordinate otherMajorCave = new WorldGridCoordinate(13, 9);
            WorldGridCoordinate majorCave = new WorldGridCoordinate(22, 16);
            WorldGridCoordinate tinyPocket = new WorldGridCoordinate(0, 16);

            Assert.IsTrue(grid.IsWalkable(tinyPocket));
            Assert.IsFalse(WorldTerrainGenerator.AreConnected(grid, start, tinyPocket));

            Assert.IsFalse(WorldTerrainGenerator.AreConnected(
                grid,
                start,
                new WorldGridCoordinate(width, height - 1)));

            bool connected = WorldTerrainGenerator.ConnectMajorCaves(
                grid,
                start,
                4,
                2,
                out int majorCaveCount);

            Assert.IsTrue(connected);
            Assert.AreEqual(2, majorCaveCount);
            Assert.IsTrue(WorldTerrainGenerator.AreConnected(grid, start, otherMajorCave));
            Assert.IsTrue(WorldTerrainGenerator.AreConnected(grid, start, majorCave));
            Assert.IsFalse(WorldTerrainGenerator.AreConnected(grid, start, tinyPocket));
            Assert.AreEqual(floorCountBefore, grid.FloorCount);
            Assert.Less(grid.WallCount, wallCountBefore, "주요 동굴을 잇는 통로에서 벽 데이터가 제거되어야 합니다.");
            Assert.IsTrue(grid.TryGetFloor(new WorldGridCoordinate(22, 16), out _));
            Assert.IsFalse(grid.TryGetWall(new WorldGridCoordinate(22, 16), out _));
        }

        private static WorldGrid CreateGrid()
        {
            var grid = new WorldGrid(Width, Height, 1f, 1f, -32f, -32f);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            return grid;
        }

        private static WorldGrid CreateFilledGrid(int width, int height, float originX, float originY)
        {
            var grid = new WorldGrid(width, height, 1f, 1f, originX, originY);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            return grid;
        }

        private static void FillWithWalls(WorldGrid grid)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceWall(new WorldGridCoordinate(x, y), WallType.Stone, 10));
                }
            }
        }

        private static WorldWallData[] CopyWallData(WorldGrid grid)
        {
            var snapshot = new WorldWallData[grid.CellCount];
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    grid.TryGetWall(new WorldGridCoordinate(x, y), out snapshot[y * grid.Width + x]);
                }
            }

            return snapshot;
        }

        private static void AssertWallSnapshot(WorldGrid grid, WorldWallData[] expected)
        {
            Assert.AreEqual(grid.CellCount, expected.Length);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    grid.TryGetWall(new WorldGridCoordinate(x, y), out WorldWallData actual);
                    WorldWallData expectedWall = expected[y * grid.Width + x];
                    Assert.AreEqual(expectedWall.HasWall, actual.HasWall);
                    Assert.AreEqual(expectedWall.Type, actual.Type);
                    Assert.AreEqual(expectedWall.MaxDurability, actual.MaxDurability);
                    Assert.AreEqual(expectedWall.CurrentDurability, actual.CurrentDurability);
                }
            }
        }

        private static void GenerateChunkedTerrain(WorldGrid grid, bool reverseOrder)
        {
            WorldCellCoordinate gridWorldOrigin = new WorldCellCoordinate(-32, -32);
            var worldBounds = new WorldCellBounds(-32, -32, 32, 32);
            WorldChunkCoordinate[] chunks =
            {
                new WorldChunkCoordinate(-1, -1),
                new WorldChunkCoordinate(0, -1),
                new WorldChunkCoordinate(-1, 0),
                new WorldChunkCoordinate(0, 0)
            };
            WorldCellCoordinate[] extraSafeCenters =
            {
                new WorldCellCoordinate(2, 0),
                new WorldCellCoordinate(4, 0)
            };

            for (int step = 0; step < chunks.Length; step++)
            {
                int index = reverseOrder ? chunks.Length - step - 1 : step;
                WorldTerrainGenerator.GenerateChunkWalls(
                    grid,
                    gridWorldOrigin,
                    chunks[index],
                    32,
                    1847,
                    Frequency,
                    DensityThreshold,
                    StoneRatio,
                    10,
                    new WorldCellCoordinate(0, 0),
                    5f,
                    extraSafeCenters);
                WorldTerrainGenerator.CarveChunkCaves(
                    grid,
                    gridWorldOrigin,
                    chunks[index],
                    32,
                    1847,
                    0.1f,
                    0.58f);
                Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                    grid,
                    gridWorldOrigin,
                    worldBounds,
                    chunks[index],
                    32,
                    1847,
                    16,
                    2,
                    out _));
            }
        }

        private static void AssertChunkConnected(
            WorldGrid grid,
            WorldCellCoordinate gridOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            WorldCellCoordinate start,
            WorldCellCoordinate target)
        {
            Assert.IsTrue(grid.IsWalkable(new WorldGridCoordinate(start.X - gridOrigin.X, start.Y - gridOrigin.Y)));
            Assert.IsTrue(grid.IsWalkable(new WorldGridCoordinate(target.X - gridOrigin.X, target.Y - gridOrigin.Y)));
            Assert.IsTrue(IsReachable(grid, gridOrigin, chunk, chunkSize, start, target));
        }

        private static void AssertWorldConnected(
            WorldGrid grid,
            WorldCellCoordinate gridOrigin,
            WorldCellCoordinate start,
            WorldCellCoordinate target)
        {
            Assert.IsTrue(IsReachable(grid, gridOrigin, null, 0, start, target));
        }

        private static bool IsReachable(
            WorldGrid grid,
            WorldCellCoordinate gridOrigin,
            WorldChunkCoordinate? chunk,
            int chunkSize,
            WorldCellCoordinate start,
            WorldCellCoordinate target)
        {
            int startX = start.X - gridOrigin.X;
            int startY = start.Y - gridOrigin.Y;
            int targetX = target.X - gridOrigin.X;
            int targetY = target.Y - gridOrigin.Y;
            if (!grid.IsWalkable(new WorldGridCoordinate(startX, startY)) ||
                !grid.IsWalkable(new WorldGridCoordinate(targetX, targetY)))
            {
                return false;
            }

            var visited = new bool[grid.CellCount];
            var queue = new System.Collections.Generic.Queue<WorldGridCoordinate>();
            var first = new WorldGridCoordinate(startX, startY);
            queue.Enqueue(first);
            visited[startY * grid.Width + startX] = true;
            while (queue.Count > 0)
            {
                WorldGridCoordinate current = queue.Dequeue();
                if (current.X == targetX && current.Y == targetY)
                {
                    return true;
                }

                Visit(current.X - 1, current.Y);
                Visit(current.X + 1, current.Y);
                Visit(current.X, current.Y - 1);
                Visit(current.X, current.Y + 1);
            }

            return false;

            void Visit(int x, int y)
            {
                var coordinate = new WorldGridCoordinate(x, y);
                if (!grid.IsInBounds(coordinate))
                {
                    return;
                }

                if (chunk.HasValue)
                {
                    WorldCellCoordinate worldCell = new WorldCellCoordinate(x + (int)gridOrigin.X, y + (int)gridOrigin.Y);
                    if (WorldChunkCoordinate.FromWorldCell(worldCell, chunkSize) != chunk.Value)
                    {
                        return;
                    }
                }

                int index = y * grid.Width + x;
                if (!visited[index] && grid.IsWalkable(coordinate))
                {
                    visited[index] = true;
                    queue.Enqueue(coordinate);
                }
            }
        }

        private static bool IsOutsidePlayerSafeArea(int gridX, int gridY)
        {
            int worldX = gridX - 32;
            int worldY = gridY - 32;
            return worldX * worldX + worldY * worldY > 25;
        }

        private static int Generate(WorldGrid grid, int seed, WorldGridCoordinate[] extraSafeCenters = null)
        {
            WorldTerrainGenerator.GenerateWalls(
                grid,
                seed,
                Frequency,
                DensityThreshold,
                StoneRatio,
                10,
                PlayerStart,
                5f,
                extraSafeCenters);
            WorldTerrainGenerator.CarveCaves(grid, seed, 0.1f, 0.58f);
            Assert.IsTrue(WorldTerrainGenerator.ConnectMajorCaves(
                grid,
                PlayerStart,
                16,
                2,
                out _));
            return grid.WallCount;
        }

        private static void OpenRectangle(WorldGrid grid, int minX, int minY, int maxX, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    OpenCell(grid, new WorldGridCoordinate(x, y));
                }
            }
        }

        private static void OpenCell(WorldGrid grid, WorldGridCoordinate coordinate)
        {
            Assert.IsTrue(grid.TryDamageWall(coordinate, 10, out _, out bool wasDestroyed));
            Assert.IsTrue(wasDestroyed);
        }

        private static void AssertSafeArea(WorldGrid grid, WorldGridCoordinate center, int radius)
        {
            int radiusSquared = radius * radius;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    int deltaX = x - center.X;
                    int deltaY = y - center.Y;
                    if (deltaX * deltaX + deltaY * deltaY <= radiusSquared)
                    {
                        Assert.IsTrue(grid.IsWalkable(new WorldGridCoordinate(x, y)));
                    }
                }
            }
        }

        private static void AssertSameWallData(WorldGrid first, WorldGrid second)
        {
            Assert.AreEqual(first.Width, second.Width);
            Assert.AreEqual(first.Height, second.Height);
            for (int y = 0; y < first.Height; y++)
            {
                for (int x = 0; x < first.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    bool firstHasWall = first.TryGetWall(coordinate, out WorldWallData firstWall);
                    bool secondHasWall = second.TryGetWall(coordinate, out WorldWallData secondWall);
                    Assert.AreEqual(firstHasWall, secondHasWall);
                    if (firstHasWall)
                    {
                        Assert.AreEqual(firstWall.Type, secondWall.Type);
                        Assert.AreEqual(firstWall.MaxDurability, secondWall.MaxDurability);
                        Assert.AreEqual(firstWall.CurrentDurability, secondWall.CurrentDurability);
                        Assert.AreEqual(firstWall.ResourceType, secondWall.ResourceType);
                        Assert.AreEqual(firstWall.ResourceAmount, secondWall.ResourceAmount);
                    }
                }
            }
        }

        private static void GenerateWithOres(WorldGrid grid, int seed, WorldOreDefinition[] ores)
        {
            WorldTerrainGenerator.GenerateWalls(
                grid,
                seed,
                Frequency,
                DensityThreshold,
                StoneRatio,
                10,
                PlayerStart,
                5f,
                null,
                ores);
        }

        private static int CountWallType(WorldGrid grid, WallType type)
        {
            int count = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.TryGetWall(new WorldGridCoordinate(x, y), out WorldWallData wall) && wall.Type == type)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static WorldGridCoordinate FindWall(WorldGrid grid, WallType type)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    if (grid.TryGetWall(coordinate, out WorldWallData wall) && wall.Type == type)
                    {
                        return coordinate;
                    }
                }
            }

            Assert.Fail($"{type} 벽을 생성하지 못했습니다.");
            return default;
        }

        private static int CountDifferences(WorldGrid first, WorldGrid second)
        {
            int differences = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    bool firstHasWall = first.TryGetWall(coordinate, out WorldWallData firstWall);
                    bool secondHasWall = second.TryGetWall(coordinate, out WorldWallData secondWall);
                    if (firstHasWall != secondHasWall ||
                        (firstHasWall && firstWall.Type != secondWall.Type))
                    {
                        differences++;
                    }
                }
            }

            return differences;
        }
    }
}
