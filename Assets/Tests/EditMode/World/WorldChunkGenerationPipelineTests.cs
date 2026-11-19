using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;

namespace Prohect_DOD_Undergird.DOD.World.Tests
{
    /// <summary>청크 지형 생성, 플레이어 변경 복원 및 좌표 경계 처리를 검증합니다.</summary>
    [TestFixture]
    public sealed class WorldChunkGenerationPipelineTests
    {
        private const int ChunkSize = 32;
        private const int Seed = 1847;
        private static readonly WorldCellBounds StandardBounds = new WorldCellBounds(-32, -32, 32, 32);

        [Test]
        public void GenerateChunk_SameSeedAndCoordinate_RecreatesIdenticalTerrainWithoutMutationRecords()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));

            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.AreEqual(0, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid firstGrid));
            WorldWallData[] firstWalls = CopyWalls(firstGrid);

            Assert.IsTrue(store.TryUnloadChunk(chunk));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid regeneratedGrid));
            AssertWallDataEqual(firstWalls, regeneratedGrid);
            Assert.AreEqual(0, store.GetRecordedWallMutationCount(chunk));
        }

        [Test]
        public void GenerateChunkWalls_OrdinaryCoordinates_MatchesExistingWholeGridNoise()
        {
            WorldGrid wholeGrid = CreateGroundGrid(ChunkSize, ChunkSize);
            WorldGrid chunkGrid = CreateGroundGrid(ChunkSize, ChunkSize);
            var worldOrigin = new WorldCellCoordinate(0, 0);

            WorldTerrainGenerator.GenerateWalls(
                wholeGrid,
                Seed,
                0.12f,
                0.66f,
                0.35f,
                10,
                new WorldGridCoordinate(0, 0),
                0f);
            WorldTerrainGenerator.GenerateChunkWalls(
                chunkGrid,
                worldOrigin,
                new WorldChunkCoordinate(0, 0),
                ChunkSize,
                Seed,
                0.12f,
                0.66f,
                0.35f,
                10,
                worldOrigin,
                0f);

            AssertWallDataEqual(CopyWalls(wholeGrid), chunkGrid);
        }

        [Test]
        public void GenerateChunks_InDifferentOrders_ProducesIdenticalConnectedResults()
        {
            WorldChunkCoordinate[] forwardOrder =
            {
                new WorldChunkCoordinate(-1, -1),
                new WorldChunkCoordinate(0, -1),
                new WorldChunkCoordinate(-1, 0),
                new WorldChunkCoordinate(0, 0)
            };
            WorldChunkCoordinate[] reverseOrder =
            {
                new WorldChunkCoordinate(0, 0),
                new WorldChunkCoordinate(-1, 0),
                new WorldChunkCoordinate(0, -1),
                new WorldChunkCoordinate(-1, -1)
            };
            WorldChunkStore forwardStore = CreateStore(StandardBounds);
            WorldChunkStore reverseStore = CreateStore(StandardBounds);
            WorldOreDefinition[] ores = CreateOreDefinitions(0.5f);
            WorldChunkGenerationPipeline forwardPipeline = CreatePipeline(
                forwardStore,
                new WorldCellCoordinate(0, 0),
                ores);
            WorldChunkGenerationPipeline reversePipeline = CreatePipeline(
                reverseStore,
                new WorldCellCoordinate(0, 0),
                ores);

            for (int i = 0; i < forwardOrder.Length; i++)
            {
                Assert.IsTrue(Generate(forwardStore, forwardPipeline, forwardOrder[i]));
                Assert.IsTrue(Generate(reverseStore, reversePipeline, reverseOrder[i]));
            }

            for (int i = 0; i < forwardOrder.Length; i++)
            {
                WorldChunkCoordinate chunk = forwardOrder[i];
                Assert.IsTrue(forwardStore.TryGetReadyGrid(chunk, out WorldGrid forwardGrid));
                Assert.IsTrue(reverseStore.TryGetReadyGrid(chunk, out WorldGrid reverseGrid));
                AssertWallDataEqual(CopyWalls(forwardGrid), reverseGrid);
            }
        }

        [Test]
        public void AdjacentChunks_KeepSharedEntranceAndInternalMajorAreasConnected()
        {
            WorldChunkCoordinate westChunk = new WorldChunkCoordinate(-1, 0);
            WorldChunkCoordinate eastChunk = new WorldChunkCoordinate(0, 0);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(Generate(store, pipeline, westChunk));
            Assert.IsTrue(Generate(store, pipeline, eastChunk));
            Assert.IsTrue(store.TryGetReadyGrid(westChunk, out WorldGrid westGrid));
            Assert.IsTrue(store.TryGetReadyGrid(eastChunk, out WorldGrid eastGrid));
            Assert.IsTrue(store.TryGetReadyGridWorldOrigin(westChunk, out WorldCellCoordinate westOrigin));
            Assert.IsTrue(store.TryGetReadyGridWorldOrigin(eastChunk, out WorldCellCoordinate eastOrigin));

            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                westChunk, ChunkSize, Seed, WorldChunkSide.East, 2, out WorldCellCoordinate westDoor));
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                eastChunk, ChunkSize, Seed, WorldChunkSide.West, 2, out WorldCellCoordinate eastDoor));
            Assert.AreEqual(westDoor.Y, eastDoor.Y);
            Assert.AreEqual(westDoor.X + 1, eastDoor.X);
            Assert.IsTrue(westGrid.IsWalkable(ToLocal(westDoor, westOrigin)));
            Assert.IsTrue(eastGrid.IsWalkable(ToLocal(eastDoor, eastOrigin)));

            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                westChunk, ChunkSize, Seed, WorldChunkSide.South, 2, out WorldCellCoordinate westSouthDoor));
            Assert.IsTrue(WorldTerrainGenerator.TryGetChunkBoundaryEntranceStart(
                eastChunk, ChunkSize, Seed, WorldChunkSide.South, 2, out WorldCellCoordinate eastSouthDoor));
            AssertWalkablePath(westGrid, westOrigin, westDoor, westSouthDoor);
            AssertWalkablePath(eastGrid, eastOrigin, eastDoor, eastSouthDoor);
        }

        [Test]
        public void DamageAndUnload_RestoresCurrentWallDurabilityOnRegeneration()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid grid));
            WorldCellCoordinate wallCell = FindWall(grid, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(world.TryGetWall(wallCell, out WorldWallData initialWall, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);

            Assert.IsTrue(world.TryDamageWall(wallCell, 3, out int remaining, out bool destroyed, out status));
            Assert.IsFalse(destroyed);
            Assert.AreEqual(initialWall.CurrentDurability - 3, remaining);
            Assert.AreEqual(1, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryUnloadChunk(chunk));

            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(world.TryGetWall(wallCell, out WorldWallData restored, out status));
            Assert.AreEqual(remaining, restored.CurrentDurability);
            Assert.AreEqual(initialWall.MaxDurability, restored.MaxDurability);
        }

        [Test]
        public void DestroyAndUnload_KeepsWallRemovedAndPreservesFloor()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid grid));
            WorldCellCoordinate wallCell = FindWall(grid, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(world.TryGetWall(wallCell, out WorldWallData wall, out _));

            Assert.IsTrue(world.TryDamageWall(wallCell, wall.CurrentDurability, out _, out bool destroyed, out _));
            Assert.IsTrue(destroyed);
            Assert.AreEqual(1, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryUnloadChunk(chunk));

            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsFalse(world.TryGetWall(wallCell, out _, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);
            Assert.AreEqual(WorldCellWalkability.Walkable, world.GetWalkability(wallCell));
            Assert.IsTrue(world.TryGetFloor(wallCell, out WorldFloorData floor, out _));
            Assert.AreEqual(FloorType.Ground, floor.Type);
        }

        [Test]
        public void GeneratedOre_DurabilityAndMinedStateSurviveChunkUnloadAndRegeneration()
        {
            WorldOreDefinition[] ores = CreateOreDefinitions(spawnThreshold: 0f);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(
                store,
                new WorldCellCoordinate(0, 0),
                ores);
            WorldChunkCoordinate[] chunks =
            {
                new WorldChunkCoordinate(-1, -1),
                new WorldChunkCoordinate(0, -1),
                new WorldChunkCoordinate(-1, 0),
                new WorldChunkCoordinate(0, 0)
            };
            WorldCellCoordinate oreCell = default;
            WorldChunkCoordinate oreChunk = default;
            bool foundOre = false;
            for (int i = 0; i < chunks.Length; i++)
            {
                Assert.IsTrue(Generate(store, pipeline, chunks[i]));
                if (!foundOre && TryFindOre(store, chunks[i], WallType.CopperOre, out oreCell))
                {
                    oreChunk = chunks[i];
                    foundOre = true;
                }
            }

            Assert.IsTrue(foundOre, "고정된 Seed에서 생성된 청크 중 구리 광맥을 찾아야 합니다.");
            Assert.IsTrue(world.TryGetWall(oreCell, out WorldWallData initialOre, out _));
            Assert.AreEqual(WorldResourceType.Copper, initialOre.ResourceType);
            Assert.IsTrue(world.TryDamageWall(oreCell, 1, out int remaining, out bool destroyed, out _));
            Assert.IsFalse(destroyed);
            Assert.IsTrue(store.TryUnloadChunk(oreChunk));

            Assert.IsTrue(Generate(store, pipeline, oreChunk));
            Assert.IsTrue(world.TryGetWall(oreCell, out WorldWallData restoredOre, out _));
            Assert.AreEqual(WallType.CopperOre, restoredOre.Type);
            Assert.AreEqual(WorldResourceType.Copper, restoredOre.ResourceType);
            Assert.AreEqual(initialOre.ResourceAmount, restoredOre.ResourceAmount);
            Assert.AreEqual(remaining, restoredOre.CurrentDurability);

            Assert.IsTrue(world.TryDamageWall(
                oreCell,
                restoredOre.CurrentDurability,
                out _,
                out destroyed,
                out _));
            Assert.IsTrue(destroyed);
            Assert.IsTrue(store.TryUnloadChunk(oreChunk));
            Assert.IsTrue(Generate(store, pipeline, oreChunk));
            Assert.IsFalse(world.TryGetWall(oreCell, out _, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);
            Assert.IsTrue(world.TryGetFloor(oreCell, out WorldFloorData floor, out _));
            Assert.AreEqual(FloorType.Ground, floor.Type);
        }

        [Test]
        public void PlaceWallAndUnload_RestoresAddedWallAndDropsDeltaWhenReturnedToBaseline()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid grid));
            WorldCellCoordinate emptyCell = FindEmptyCell(grid, new WorldCellCoordinate(-32, -32));

            Assert.IsTrue(world.TryPlaceWall(emptyCell, WallType.Stone, 14, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);
            Assert.AreEqual(1, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryUnloadChunk(chunk));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(world.TryGetWall(emptyCell, out WorldWallData restored, out status));
            Assert.AreEqual(WallType.Stone, restored.Type);
            Assert.AreEqual(14, restored.CurrentDurability);

            Assert.IsTrue(world.TryDamageWall(emptyCell, 14, out _, out bool destroyed, out _));
            Assert.IsTrue(destroyed);
            Assert.AreEqual(0, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryUnloadChunk(chunk));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsFalse(world.TryGetWall(emptyCell, out _, out status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);
            Assert.IsTrue(world.TryGetFloor(emptyCell, out WorldFloorData floor, out _));
            Assert.AreEqual(FloorType.Ground, floor.Type);
        }

        [Test]
        public void PlayerWallChange_StoresOnlyChangedCellAndLeavesGeneratedCellsUntouched()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid grid));
            WorldWallData[] baseline = CopyWalls(grid);
            WorldCellCoordinate changedCell = FindWall(grid, new WorldCellCoordinate(-32, -32));
            int changedIndex = ToIndex(changedCell, new WorldCellCoordinate(-32, -32), grid.Width);

            Assert.IsTrue(world.TryDamageWall(
                changedCell,
                1,
                out _,
                out _,
                out _));
            Assert.AreEqual(1, store.GetRecordedWallMutationCount(chunk));
            Assert.IsTrue(store.TryUnloadChunk(chunk));
            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid restoredGrid));

            for (int i = 0; i < baseline.Length; i++)
            {
                if (i == changedIndex)
                {
                    continue;
                }

                AssertWallEqual(baseline[i], GetWall(restoredGrid, i));
            }
        }

        [Test]
        public void GenerationMutations_AreNotRecordedAsPlayerChangesAndReadyIsFinalState()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            WorldChunkStore store = CreateStore(StandardBounds);
            WorldGridWorld world = new WorldGridWorld(store);
            WorldChunkGenerationPipeline pipeline = CreatePipeline(store, new WorldCellCoordinate(0, 0));
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.AreEqual(WorldCellAccessStatus.Queued, world.GetAccessStatus(new WorldCellCoordinate(1, 1)));

            Assert.IsTrue(pipeline.TryGenerateQueuedChunk(chunk));
            Assert.AreEqual(WorldChunkDataState.Ready, store.GetDataState(chunk));
            Assert.AreEqual(WorldCellAccessStatus.Ready, world.GetAccessStatus(new WorldCellCoordinate(1, 1)));
            Assert.AreEqual(0, store.GetRecordedWallMutationCount(chunk));
        }

        [TestCase(int.MinValue, int.MinValue)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void GeneratePartialExtremeChunk_UsesLongIntermediatesWithoutOverflow(int cellX, int cellY)
        {
            var bounds = new WorldCellBounds(cellX, cellY, (long)cellX + 1L, (long)cellY + 1L);
            var store = new WorldChunkStore(bounds, 3, 1f, 1f);
            var settings = CreateSettings(new WorldCellCoordinate(cellX, cellY), safeRadius: 0f);
            var pipeline = new WorldChunkGenerationPipeline(store, settings);
            WorldChunkCoordinate chunk = WorldChunkCoordinate.FromWorldCell(new WorldCellCoordinate(cellX, cellY), 3);

            Assert.IsTrue(Generate(store, pipeline, chunk));
            Assert.IsTrue(store.TryGetReadyGrid(chunk, out WorldGrid grid));
            Assert.AreEqual(1, grid.Width);
            Assert.AreEqual(1, grid.Height);
            Assert.IsTrue(WorldAccessIsReady(store, cellX, cellY));
            Assert.IsTrue(grid.TryGetFloor(new WorldGridCoordinate(0, 0), out WorldFloorData floor));
            Assert.AreEqual(FloorType.Ground, floor.Type);
        }

        private static bool WorldAccessIsReady(WorldChunkStore store, int x, int y)
        {
            return new WorldGridWorld(store).GetAccessStatus(new WorldCellCoordinate(x, y)) == WorldCellAccessStatus.Ready;
        }

        private static WorldChunkStore CreateStore(WorldCellBounds bounds)
        {
            return new WorldChunkStore(bounds, ChunkSize, 1f, 1f);
        }

        private static WorldGrid CreateGroundGrid(int width, int height)
        {
            var grid = new WorldGrid(width, height, 1f, 1f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            return grid;
        }

        private static WorldChunkGenerationPipeline CreatePipeline(
            WorldChunkStore store,
            WorldCellCoordinate playerStart,
            WorldOreDefinition[] oreDefinitions = null)
        {
            return new WorldChunkGenerationPipeline(store, CreateSettings(playerStart, 5f, oreDefinitions));
        }

        private static WorldChunkGenerationSettings CreateSettings(
            WorldCellCoordinate playerStart,
            float safeRadius = 5f,
            WorldOreDefinition[] oreDefinitions = null)
        {
            return new WorldChunkGenerationSettings(
                Seed,
                0.12f,
                0.66f,
                0.35f,
                10,
                0.1f,
                0.58f,
                16,
                2,
                playerStart,
                safeRadius,
                null,
                oreDefinitions);
        }

        private static WorldOreDefinition[] CreateOreDefinitions(float spawnThreshold)
        {
            return new[]
            {
                new WorldOreDefinition(
                    WallType.CopperOre, WallType.Soil, WorldResourceType.Copper,
                    0x217A613D, 0.09f, spawnThreshold, 14, 2),
                new WorldOreDefinition(
                    WallType.IronOre, WallType.Stone, WorldResourceType.Iron,
                    0x4D8B9271, 0.13f, spawnThreshold, 18, 1)
            };
        }

        private static bool TryFindOre(
            WorldChunkStore store,
            WorldChunkCoordinate chunk,
            WallType wallType,
            out WorldCellCoordinate oreCell)
        {
            oreCell = default;
            if (!store.TryGetReadyGrid(chunk, out WorldGrid grid) ||
                !store.TryGetReadyGridWorldOrigin(chunk, out WorldCellCoordinate origin))
            {
                return false;
            }

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var local = new WorldGridCoordinate(x, y);
                    if (grid.TryGetWall(local, out WorldWallData wall) && wall.Type == wallType)
                    {
                        oreCell = new WorldCellCoordinate(origin.X + x, origin.Y + y);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Generate(
            WorldChunkStore store,
            WorldChunkGenerationPipeline pipeline,
            WorldChunkCoordinate chunk)
        {
            WorldChunkDataState state = store.GetDataState(chunk);
            if (state == WorldChunkDataState.Unloaded || state == WorldChunkDataState.Failed)
            {
                if (!store.TryQueueChunk(chunk))
                {
                    return false;
                }
            }

            return pipeline.TryGenerateQueuedChunk(chunk);
        }

        private static WorldCellCoordinate FindWall(WorldGrid grid, WorldCellCoordinate origin)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    WorldGridCoordinate local = new WorldGridCoordinate(x, y);
                    if (grid.TryGetWall(local, out _))
                    {
                        return new WorldCellCoordinate(origin.X + x, origin.Y + y);
                    }
                }
            }

            Assert.Fail("생성된 테스트 청크에 벽이 없습니다.");
            return default;
        }

        private static WorldCellCoordinate FindEmptyCell(WorldGrid grid, WorldCellCoordinate origin)
        {
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    WorldGridCoordinate local = new WorldGridCoordinate(x, y);
                    if (!grid.TryGetWall(local, out _))
                    {
                        return new WorldCellCoordinate(origin.X + x, origin.Y + y);
                    }
                }
            }

            Assert.Fail("생성된 테스트 청크에 빈 셀이 없습니다.");
            return default;
        }

        private static WorldGridCoordinate ToLocal(WorldCellCoordinate worldCell, WorldCellCoordinate origin)
        {
            return new WorldGridCoordinate(worldCell.X - origin.X, worldCell.Y - origin.Y);
        }

        private static int ToIndex(WorldCellCoordinate worldCell, WorldCellCoordinate origin, int width)
        {
            WorldGridCoordinate local = ToLocal(worldCell, origin);
            return local.Y * width + local.X;
        }

        private static WorldWallData[] CopyWalls(WorldGrid grid)
        {
            WorldWallData[] result = new WorldWallData[grid.CellCount];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = GetWall(grid, index);
            }

            return result;
        }

        private static WorldWallData GetWall(WorldGrid grid, int index)
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(index % grid.Width, index / grid.Width);
            return grid.TryGetWall(coordinate, out WorldWallData wall) ? wall : default;
        }

        private static void AssertWallDataEqual(WorldWallData[] expected, WorldGrid actual)
        {
            Assert.AreEqual(expected.Length, actual.CellCount);
            for (int i = 0; i < expected.Length; i++)
            {
                AssertWallEqual(expected[i], GetWall(actual, i));
            }
        }

        private static void AssertWallEqual(WorldWallData expected, WorldWallData actual)
        {
            Assert.AreEqual(expected.HasWall, actual.HasWall);
            if (!expected.HasWall)
            {
                return;
            }

            Assert.AreEqual(expected.Type, actual.Type);
            Assert.AreEqual(expected.MaxDurability, actual.MaxDurability);
            Assert.AreEqual(expected.CurrentDurability, actual.CurrentDurability);
            Assert.AreEqual(expected.ResourceType, actual.ResourceType);
            Assert.AreEqual(expected.ResourceAmount, actual.ResourceAmount);
        }

        private static void AssertWalkablePath(
            WorldGrid grid,
            WorldCellCoordinate gridOrigin,
            WorldCellCoordinate start,
            WorldCellCoordinate end)
        {
            bool[] visited = new bool[grid.CellCount];
            int[] queue = new int[grid.CellCount];
            WorldGridCoordinate localStart = ToLocal(start, gridOrigin);
            int startIndex = localStart.Y * grid.Width + localStart.X;
            queue[0] = startIndex;
            visited[startIndex] = true;
            int head = 0;
            int tail = 1;
            while (head < tail)
            {
                int index = queue[head++];
                int x = index % grid.Width;
                int y = index / grid.Width;
                if (x == end.X - gridOrigin.X && y == end.Y - gridOrigin.Y)
                {
                    return;
                }

                EnqueueWalkable(grid, x - 1, y, visited, queue, ref tail);
                EnqueueWalkable(grid, x + 1, y, visited, queue, ref tail);
                EnqueueWalkable(grid, x, y - 1, visited, queue, ref tail);
                EnqueueWalkable(grid, x, y + 1, visited, queue, ref tail);
            }

            Assert.Fail("청크 내부의 활성 입구가 같은 이동 가능 영역에 있지 않습니다.");
        }

        private static void EnqueueWalkable(
            WorldGrid grid,
            int x,
            int y,
            bool[] visited,
            int[] queue,
            ref int tail)
        {
            if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height)
            {
                return;
            }

            int index = y * grid.Width + x;
            if (!visited[index] && grid.IsWalkable(new WorldGridCoordinate(x, y)))
            {
                visited[index] = true;
                queue[tail++] = index;
            }
        }
    }
}
