using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;

namespace Prohect_DOD_Undergird.DOD.World.Tests
{
    /// <summary>청크 데이터 상태와 절대 월드 셀 중계 API의 경계 처리를 검증합니다.</summary>
    [TestFixture]
    public class WorldChunkStoreTests
    {
        private WorldCellBounds bounds;
        private WorldChunkStore store;
        private WorldGridWorld world;

        [SetUp]
        public void SetUp()
        {
            bounds = new WorldCellBounds(-2, -1, 35, 34);
            store = new WorldChunkStore(bounds, 32, 1f, 1f, -10f, 4f);
            world = new WorldGridWorld(store);
        }

        [Test]
        public void TryGetChunkAddress_MapsPositiveNegativeAndBoundaryCells()
        {
            AssertAddress(new WorldCellCoordinate(-2, -1), new WorldChunkCoordinate(-1, -1), new WorldGridCoordinate(30, 31));
            AssertAddress(new WorldCellCoordinate(-1, 0), new WorldChunkCoordinate(-1, 0), new WorldGridCoordinate(31, 0));
            AssertAddress(new WorldCellCoordinate(0, 0), new WorldChunkCoordinate(0, 0), new WorldGridCoordinate(0, 0));
            AssertAddress(new WorldCellCoordinate(31, 31), new WorldChunkCoordinate(0, 0), new WorldGridCoordinate(31, 31));
            AssertAddress(new WorldCellCoordinate(32, 32), new WorldChunkCoordinate(1, 1), new WorldGridCoordinate(0, 0));
            Assert.IsFalse(store.TryGetChunkAddress(new WorldCellCoordinate(-3, 0), out _, out _));
            Assert.IsFalse(store.TryGetChunkAddress(new WorldCellCoordinate(35, 0), out _, out _));
        }

        [Test]
        public void TryGetChunkAddress_HandlesIntegerCoordinateLimitsWithoutOverflow()
        {
            var minimumStore = new WorldChunkStore(
                new WorldCellBounds(int.MinValue, 0, (long)int.MinValue + 1L, 1),
                3,
                1f,
                1f);
            Assert.IsTrue(minimumStore.TryGetChunkAddress(
                new WorldCellCoordinate(int.MinValue, 0),
                out WorldChunkCoordinate minimumChunk,
                out WorldGridCoordinate minimumLocal));
            Assert.AreEqual(-715827883, minimumChunk.X);
            Assert.AreEqual(1, minimumLocal.X);

            var maximumStore = new WorldChunkStore(
                new WorldCellBounds(int.MaxValue, 0, (long)int.MaxValue + 1L, 1),
                3,
                1f,
                1f);
            Assert.IsTrue(maximumStore.TryGetChunkAddress(
                new WorldCellCoordinate(int.MaxValue, 0),
                out WorldChunkCoordinate maximumChunk,
                out WorldGridCoordinate maximumLocal));
            Assert.AreEqual(715827882, maximumChunk.X);
            Assert.AreEqual(1, maximumLocal.X);
        }

        [Test]
        public void ChunkCoordinateConversions_HandlePartialChunksAtIntegerLimits()
        {
            const int chunkSize = 3;
            WorldCellCoordinate minimumCell = new WorldCellCoordinate(int.MinValue, int.MinValue);
            WorldChunkCoordinate minimumChunk = WorldChunkCoordinate.FromWorldCell(minimumCell, chunkSize);
            WorldGridCoordinate minimumLocal = minimumChunk.ToLocalCell(minimumCell, chunkSize);
            Assert.AreEqual(new WorldGridCoordinate(1, 1), minimumLocal);
            Assert.AreEqual(minimumCell, minimumChunk.ToWorldCell(minimumLocal, chunkSize));

            WorldCellCoordinate maximumCell = new WorldCellCoordinate(int.MaxValue, int.MaxValue);
            WorldChunkCoordinate maximumChunk = WorldChunkCoordinate.FromWorldCell(maximumCell, chunkSize);
            WorldGridCoordinate maximumLocal = maximumChunk.ToLocalCell(maximumCell, chunkSize);
            Assert.AreEqual(new WorldGridCoordinate(1, 1), maximumLocal);
            Assert.AreEqual(maximumCell, maximumChunk.ToWorldCell(maximumLocal, chunkSize));

            Assert.Throws<System.OverflowException>(() =>
                new WorldChunkCoordinate(maximumChunk.X + 1, maximumChunk.Y).ToWorldCell(
                    new WorldGridCoordinate(0, 0), chunkSize));
        }

        [Test]
        public void CellQueries_DistinguishUnloadedQueuedGeneratingFailedAndOutOfBounds()
        {
            WorldCellCoordinate cell = new WorldCellCoordinate(3, 4);
            Assert.AreEqual(WorldCellAccessStatus.Unloaded, world.GetAccessStatus(cell));
            Assert.AreEqual(WorldCellWalkability.Unloaded, world.GetWalkability(cell));
            Assert.IsFalse(world.TryGetWall(cell, out _, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Unloaded, status);

            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            Assert.IsFalse(store.TryQueueChunk(new WorldChunkCoordinate(2, 0)));
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.AreEqual(WorldChunkDataState.Queued, store.GetDataState(chunk));
            Assert.AreEqual(WorldCellAccessStatus.Queued, world.GetAccessStatus(cell));
            Assert.AreEqual(WorldCellWalkability.Queued, world.GetWalkability(cell));

            Assert.IsTrue(store.TryBeginGeneration(chunk, out _));
            Assert.AreEqual(WorldCellAccessStatus.Generating, world.GetAccessStatus(cell));
            Assert.AreEqual(WorldCellWalkability.Generating, world.GetWalkability(cell));
            Assert.IsTrue(store.TryMarkFailed(chunk));
            Assert.AreEqual(WorldCellAccessStatus.Failed, world.GetAccessStatus(cell));
            Assert.AreEqual(WorldCellWalkability.Failed, world.GetWalkability(cell));
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.IsTrue(store.TryBeginGeneration(chunk, out _));
            Assert.IsTrue(store.TryMarkReady(chunk));
            Assert.AreEqual(WorldCellAccessStatus.Ready, world.GetAccessStatus(cell));
            Assert.AreEqual(WorldCellAccessStatus.OutOfWorldBounds, world.GetAccessStatus(new WorldCellCoordinate(35, 0)));
            Assert.AreEqual(WorldCellWalkability.OutOfWorldBounds, world.GetWalkability(new WorldCellCoordinate(35, 0)));
        }

        [Test]
        public void WorldPositionConversion_HandlesNegativeCellsAndWorldEdges()
        {
            Assert.IsTrue(world.TryWorldPositionToCell(-12f, 3f, out WorldCellCoordinate minimumCell));
            Assert.AreEqual(new WorldCellCoordinate(-2, -1), minimumCell);
            Assert.IsTrue(world.TryWorldPositionToCell(-10.01f, 3.99f, out WorldCellCoordinate negativeCell));
            Assert.AreEqual(new WorldCellCoordinate(-1, -1), negativeCell);
            Assert.IsTrue(world.TryWorldPositionToCell(24.99f, 36.99f, out WorldCellCoordinate maximumCell));
            Assert.AreEqual(new WorldCellCoordinate(34, 32), maximumCell);
            Assert.IsFalse(world.TryWorldPositionToCell(25f, 36f, out _));
            Assert.IsFalse(world.TryWorldPositionToCell(float.NaN, 4f, out _));
        }

        [Test]
        public void CellCenterConversion_RoundTripsWithoutLoadingChunkData()
        {
            WorldCellCoordinate cell = new WorldCellCoordinate(-2, -1);
            Assert.IsTrue(world.TryGetCellCenterWorldPosition(cell, out float centerX, out float centerY));
            Assert.AreEqual(-11.5f, centerX);
            Assert.AreEqual(3.5f, centerY);
            Assert.IsTrue(world.TryWorldPositionToCell(centerX, centerY, out WorldCellCoordinate roundTrip));
            Assert.AreEqual(cell, roundTrip);
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(new WorldChunkCoordinate(-1, -1)));
        }

        [Test]
        public void PartialEdgeChunk_ContainsOnlyCellsInsideWorldBounds()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(chunk));
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.IsTrue(store.TryBeginGeneration(
                chunk,
                out WorldGrid grid,
                out WorldCellCoordinate gridWorldOrigin));
            Assert.AreEqual(2, grid.Width);
            Assert.AreEqual(1, grid.Height);
            Assert.AreEqual(new WorldCellCoordinate(-2, -1), gridWorldOrigin);
            Assert.AreEqual(-12f, grid.OriginX);
            Assert.AreEqual(3f, grid.OriginY);
            Assert.IsTrue(store.TryMarkReady(chunk));
            Assert.IsTrue(store.TryGetReadyGridWorldOrigin(chunk, out WorldCellCoordinate readyOrigin));
            Assert.AreEqual(gridWorldOrigin, readyOrigin);

            Assert.IsTrue(world.TryPlaceFloor(new WorldCellCoordinate(-2, -1), FloorType.Ground, out WorldCellAccessStatus status));
            Assert.AreEqual(WorldCellAccessStatus.Ready, status);
            Assert.IsTrue(world.TryGetFloor(new WorldCellCoordinate(-2, -1), out WorldFloorData floor, out status));
            Assert.AreEqual(FloorType.Ground, floor.Type);
            Assert.IsFalse(world.TryGetFloor(new WorldCellCoordinate(-3, -1), out _, out status));
            Assert.AreEqual(WorldCellAccessStatus.OutOfWorldBounds, status);
        }

        [Test]
        public void WorldApi_UsesIndependentChunkGridsAndForwardsAbsoluteChanges()
        {
            WorldChunkCoordinate firstChunk = new WorldChunkCoordinate(0, 0);
            WorldChunkCoordinate secondChunk = new WorldChunkCoordinate(1, 0);
            WorldGrid firstGrid = CreateReadyChunk(firstChunk);
            WorldGrid secondGrid = CreateReadyChunk(secondChunk);
            WorldCellCoordinate firstCell = new WorldCellCoordinate(2, 3);
            WorldCellCoordinate secondCell = new WorldCellCoordinate(34, 3);
            WorldCellChange observed = default;
            int eventCount = 0;
            world.CellChanged += change =>
            {
                observed = change;
                eventCount++;
            };

            Assert.IsTrue(world.TryPlaceFloor(firstCell, FloorType.Ground, out _));
            Assert.IsTrue(world.TryPlaceWall(firstCell, WallType.Stone, 20, out _));
            Assert.IsTrue(world.TryPlaceFloor(secondCell, FloorType.Ground, out _));
            Assert.IsTrue(world.TryPlaceWall(secondCell, WallType.Soil, 12, out _));
            Assert.AreEqual(firstGrid.WallCount, 1);
            Assert.AreEqual(secondGrid.WallCount, 1);
            Assert.AreEqual(new WorldCellCoordinate(34, 3), observed.Coordinate);
            Assert.AreEqual(WorldGridLayer.Wall, observed.Layer);
            Assert.AreEqual(WorldGridChangeKind.Placed, observed.Kind);
            Assert.AreEqual(4, eventCount);

            Assert.IsTrue(world.TryDamageWall(firstCell, 7, out int remaining, out bool destroyed, out _));
            Assert.AreEqual(13, remaining);
            Assert.IsFalse(destroyed);
            Assert.IsTrue(world.TryGetWall(secondCell, out WorldWallData otherWall, out _));
            Assert.AreEqual(12, otherWall.CurrentDurability);
            Assert.AreEqual(WorldCellWalkability.Blocked, world.GetWalkability(firstCell));
            Assert.IsTrue(world.TryDamageWall(firstCell, 13, out _, out destroyed, out _));
            Assert.IsTrue(destroyed);
            Assert.AreEqual(WorldCellWalkability.Walkable, world.GetWalkability(firstCell));
            Assert.IsTrue(world.TryGetFloor(firstCell, out WorldFloorData floor, out _));
            Assert.AreEqual(FloorType.Ground, floor.Type);
        }

        [Test]
        public void ChunkStateTransitions_KeepPresentationSeparateAndProtectDisplayedData()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(chunk));
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.AreEqual(WorldChunkDataState.Queued, store.GetDataState(chunk));
            Assert.AreEqual(0, store.LoadedChunkCount);
            Assert.IsTrue(store.TryBeginGeneration(chunk, out _));
            Assert.AreEqual(WorldChunkDataState.Generating, store.GetDataState(chunk));
            Assert.AreEqual(1, store.LoadedChunkCount);
            Assert.IsTrue(store.TryMarkReady(chunk));
            Assert.AreEqual(WorldChunkDataState.Ready, store.GetDataState(chunk));
            Assert.IsTrue(store.TryGetPresentationState(chunk, out WorldChunkPresentationState presentation));
            Assert.AreEqual(WorldChunkPresentationState.NotDisplayed, presentation);
            Assert.IsTrue(store.TrySetDisplayed(chunk, true));
            Assert.AreEqual(WorldChunkDataState.Ready, store.GetDataState(chunk));
            Assert.AreEqual(WorldChunkPresentationState.Displayed, GetPresentation(chunk));
            Assert.IsFalse(store.TryUnloadChunk(chunk));
            Assert.IsTrue(store.TrySetDisplayed(chunk, false));
            Assert.IsTrue(store.TryUnloadChunk(chunk));
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(chunk));
            Assert.AreEqual(0, store.LoadedChunkCount);
        }

        [Test]
        public void KnownChunkSnapshot_TracksRequestsAndQueuedRequestsCanBeCancelled()
        {
            WorldChunkCoordinate queued = new WorldChunkCoordinate(0, 0);
            WorldChunkCoordinate failed = new WorldChunkCoordinate(1, 0);
            Assert.IsTrue(store.TryQueueChunk(queued));
            Assert.IsTrue(store.TryQueueChunk(failed));
            Assert.IsTrue(store.TryMarkFailed(failed));

            WorldChunkCoordinate[] known = store.GetKnownChunkCoordinates();
            CollectionAssert.AreEquivalent(new[] { queued, failed }, known);
            Assert.IsTrue(store.TryCancelQueuedChunk(queued));
            Assert.IsFalse(store.TryCancelQueuedChunk(queued));
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(queued));
            Assert.IsTrue(store.TryForgetFailedChunk(failed));
            Assert.IsFalse(store.TryForgetFailedChunk(failed));
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(failed));
        }

        [Test]
        public void ExistingWorldGridBehavior_RemainsAvailableInsideReadyChunk()
        {
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(0, 0);
            CreateReadyChunk(chunk);
            WorldCellCoordinate cell = new WorldCellCoordinate(5, 6);

            Assert.IsTrue(world.TryPlaceFloor(cell, FloorType.Stone, out _));
            Assert.IsTrue(world.TryPlaceWall(cell, WallType.Stone, 8, out _));
            Assert.AreEqual(WorldCellWalkability.Blocked, world.GetWalkability(cell));
            Assert.IsTrue(world.TryDamageWall(cell, 8, out int remaining, out bool destroyed, out _));
            Assert.AreEqual(0, remaining);
            Assert.IsTrue(destroyed);
            Assert.AreEqual(WorldCellWalkability.Walkable, world.GetWalkability(cell));
            Assert.IsTrue(world.TryGetFloor(cell, out WorldFloorData floor, out _));
            Assert.AreEqual(FloorType.Stone, floor.Type);
        }

        private WorldGrid CreateReadyChunk(WorldChunkCoordinate chunk)
        {
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.IsTrue(store.TryBeginGeneration(chunk, out WorldGrid grid));
            Assert.IsTrue(store.TryMarkReady(chunk));
            return grid;
        }

        private WorldChunkPresentationState GetPresentation(WorldChunkCoordinate chunk)
        {
            Assert.IsTrue(store.TryGetPresentationState(chunk, out WorldChunkPresentationState state));
            return state;
        }

        private void AssertAddress(
            WorldCellCoordinate worldCell,
            WorldChunkCoordinate expectedChunk,
            WorldGridCoordinate expectedLocal)
        {
            Assert.IsTrue(store.TryGetChunkAddress(worldCell, out WorldChunkCoordinate chunk, out WorldGridCoordinate local));
            Assert.AreEqual(expectedChunk, chunk);
            Assert.AreEqual(expectedLocal, local);
        }

    }
}
