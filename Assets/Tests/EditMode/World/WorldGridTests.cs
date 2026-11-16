using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;

namespace Prohect_DOD_Undergird.DOD.World.Tests
{
    /// <summary>
    /// 월드 그리드의 좌표, 바닥·벽 생명주기, 변경 통지 및 대량 배열 접근을 검증합니다.
    /// </summary>
    [TestFixture]
    public class WorldGridTests
    {
        private WorldGrid grid;

        [SetUp]
        public void SetUp()
        {
            grid = new WorldGrid(8, 6, 1f, 1f, -2f, -3f);
        }

        [Test]
        public void PlaceFloor_StoresTypeAndMakesEmptyCellWalkable()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(2, 3);
            int changeCount = 0;
            grid.CellChanged += _ => changeCount++;

            Assert.IsTrue(grid.TryPlaceFloor(coordinate, FloorType.Ground));
            Assert.IsTrue(grid.TryGetFloor(coordinate, out WorldFloorData floor));
            Assert.AreEqual(FloorType.Ground, floor.Type);
            Assert.AreEqual(FloorState.Present, floor.State);
            Assert.AreEqual(1, grid.FloorCount);
            Assert.IsTrue(grid.IsWalkable(coordinate));
            Assert.AreEqual(1, changeCount);
        }

        [Test]
        public void PlaceWall_RequiresFloorAndBlocksMovement()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(1, 1);

            Assert.IsFalse(grid.TryPlaceWall(coordinate, WallType.Stone, 25));
            Assert.IsTrue(grid.TryPlaceFloor(coordinate, FloorType.Stone));
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 25));
            Assert.IsTrue(grid.TryGetWall(coordinate, out WorldWallData wall));
            Assert.AreEqual(WallType.Stone, wall.Type);
            Assert.AreEqual(25, wall.MaxDurability);
            Assert.AreEqual(25, wall.CurrentDurability);
            Assert.AreEqual(1, grid.WallCount);
            Assert.IsFalse(grid.IsWalkable(coordinate));
        }

        [Test]
        public void DamageWall_ReducesDurabilityThenRemovesWallAndPreservesFloor()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(4, 2);
            grid.TryPlaceFloor(coordinate, FloorType.Soil);
            grid.TryPlaceWall(coordinate, WallType.Soil, 20);
            int changeCount = 0;
            WorldGridChange lastChange = default;
            grid.CellChanged += change =>
            {
                changeCount++;
                lastChange = change;
            };

            Assert.IsTrue(grid.TryDamageWall(coordinate, 7, out int remaining, out bool wasDestroyed));
            Assert.AreEqual(13, remaining);
            Assert.IsFalse(wasDestroyed);
            Assert.IsTrue(grid.TryGetWall(coordinate, out WorldWallData damagedWall));
            Assert.AreEqual(20, damagedWall.MaxDurability);
            Assert.AreEqual(13, damagedWall.CurrentDurability);
            Assert.IsFalse(grid.IsWalkable(coordinate));
            Assert.AreEqual(WorldGridChangeKind.Damaged, lastChange.Kind);

            Assert.IsTrue(grid.TryDamageWall(coordinate, 13, out remaining, out wasDestroyed));
            Assert.AreEqual(0, remaining);
            Assert.IsTrue(wasDestroyed);
            Assert.IsFalse(grid.TryGetWall(coordinate, out _));
            Assert.AreEqual(0, grid.WallCount);
            Assert.IsTrue(grid.TryGetFloor(coordinate, out WorldFloorData floor));
            Assert.AreEqual(FloorType.Soil, floor.Type);
            Assert.IsTrue(grid.IsWalkable(coordinate));
            Assert.AreEqual(2, changeCount);
            Assert.AreEqual(WorldGridLayer.Wall, lastChange.Layer);
            Assert.AreEqual(WorldGridChangeKind.Removed, lastChange.Kind);
        }

        [Test]
        public void DamageWall_NonPositiveDamage_DoesNotChangeDurabilityOrRaiseEvent()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(2, 2);
            Assert.IsTrue(grid.TryPlaceFloor(coordinate, FloorType.Ground));
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 30));
            int changeCount = 0;
            grid.CellChanged += _ => changeCount++;

            Assert.IsFalse(grid.TryDamageWall(coordinate, 0, out int zeroRemaining, out bool zeroDestroyed));
            Assert.IsFalse(grid.TryDamageWall(coordinate, -5, out int negativeRemaining, out bool negativeDestroyed));

            Assert.AreEqual(0, zeroRemaining);
            Assert.IsFalse(zeroDestroyed);
            Assert.AreEqual(0, negativeRemaining);
            Assert.IsFalse(negativeDestroyed);
            Assert.IsTrue(grid.TryGetWall(coordinate, out WorldWallData wall));
            Assert.AreEqual(30, wall.MaxDurability);
            Assert.AreEqual(30, wall.CurrentDurability);
            Assert.AreEqual(1, grid.WallCount);
            Assert.AreEqual(0, changeCount);
        }

        [Test]
        public void CellChanged_ReportsCoordinateLayerAndKindForEachMutation()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(3, 4);
            WorldGridChange[] changes = new WorldGridChange[4];
            int changeCount = 0;
            grid.CellChanged += change => changes[changeCount++] = change;

            Assert.IsTrue(grid.TryPlaceFloor(coordinate, FloorType.Soil));
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 10));
            Assert.IsTrue(grid.TryDamageWall(coordinate, 3, out _, out bool destroyed));
            Assert.IsFalse(destroyed);
            Assert.IsTrue(grid.TryDamageWall(coordinate, 7, out _, out destroyed));
            Assert.IsTrue(destroyed);

            Assert.AreEqual(4, changeCount);
            AssertChange(changes[0], coordinate, WorldGridLayer.Floor, WorldGridChangeKind.Placed);
            AssertChange(changes[1], coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Placed);
            AssertChange(changes[2], coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Damaged);
            AssertChange(changes[3], coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Removed);
        }

        [Test]
        public void DestroyedWall_CanBeReplacedButOccupiedCellRejectsDuplicate()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(3, 2);
            grid.TryPlaceFloor(coordinate, FloorType.Ground);
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 1));
            Assert.IsFalse(grid.TryPlaceWall(coordinate, WallType.Soil, 10));
            Assert.IsTrue(grid.TryDamageWall(coordinate, 1, out _, out bool wasDestroyed));
            Assert.IsTrue(wasDestroyed);
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Soil, 10));
            Assert.IsTrue(grid.TryGetWall(coordinate, out WorldWallData wall));
            Assert.AreEqual(WallType.Soil, wall.Type);
            Assert.AreEqual(10, wall.CurrentDurability);
        }

        [Test]
        public void InvalidCoordinatesAndDamage_DoNotMutateOrNotify()
        {
            int changeCount = 0;
            grid.CellChanged += _ => changeCount++;
            WorldGridCoordinate outside = new WorldGridCoordinate(-1, 0);

            Assert.IsFalse(grid.IsInBounds(outside));
            Assert.IsFalse(grid.TryPlaceFloor(outside, FloorType.Ground));
            Assert.IsFalse(grid.TryGetFloor(outside, out _));
            Assert.IsFalse(grid.TryPlaceWall(outside, WallType.Stone, 10));
            Assert.IsFalse(grid.TryDamageWall(outside, 1, out _, out _));
            Assert.IsFalse(grid.TryPlaceFloor(new WorldGridCoordinate(0, 0), FloorType.None));
            Assert.IsFalse(grid.TryPlaceFloor(new WorldGridCoordinate(0, 0), (FloorType)99));
            Assert.IsFalse(grid.TryPlaceWall(new WorldGridCoordinate(0, 0), (WallType)99, 10));
            Assert.IsFalse(grid.TryPlaceWall(new WorldGridCoordinate(0, 0), WallType.Stone, 0));
            Assert.AreEqual(0, changeCount);
            Assert.AreEqual(0, grid.FloorCount);
            Assert.AreEqual(0, grid.WallCount);
        }

        [Test]
        public void Floor_IsNotMineableAndDuplicatePlacementDoesNotChangeData()
        {
            WorldGridCoordinate coordinate = new WorldGridCoordinate(0, 0);
            grid.TryPlaceFloor(coordinate, FloorType.Ground);
            int changeCount = 0;
            grid.CellChanged += _ => changeCount++;

            Assert.IsFalse(grid.TryPlaceFloor(coordinate, FloorType.Stone));
            Assert.IsFalse(grid.TryDamageWall(coordinate, 10, out _, out _));
            Assert.IsTrue(grid.TryGetFloor(coordinate, out WorldFloorData floor));
            Assert.AreEqual(FloorType.Ground, floor.Type);
            Assert.AreEqual(0, changeCount);
        }

        [Test]
        public void CoordinateConversion_UsesOriginCellSizeAndExclusiveMaximumBounds()
        {
            var offsetGrid = new WorldGrid(2, 3, 2f, 4f, -1f, 5f);

            Assert.IsTrue(offsetGrid.TryWorldToCell(-1f, 5f, out WorldGridCoordinate first));
            Assert.AreEqual(new WorldGridCoordinate(0, 0), first);
            Assert.IsTrue(offsetGrid.TryGetCellCenterWorldPosition(first, out float centerX, out float centerY));
            Assert.AreEqual(0f, centerX, 0.0001f);
            Assert.AreEqual(7f, centerY, 0.0001f);
            Assert.IsTrue(offsetGrid.TryWorldToCell(2.999f, 16.999f, out WorldGridCoordinate last));
            Assert.AreEqual(new WorldGridCoordinate(1, 2), last);
            Assert.IsFalse(offsetGrid.TryWorldToCell(3f, 10f, out _));
            Assert.IsFalse(offsetGrid.TryWorldToCell(float.NaN, 5f, out _));
            Assert.IsFalse(offsetGrid.TryGetCellCenterWorldPosition(new WorldGridCoordinate(2, 0), out _, out _));
        }

        [Test]
        public void WorldToCell_IncludesMinimumAndExcludesMaximumBounds()
        {
            var offsetGrid = new WorldGrid(250, 200, 1f, 1f, -125f, -100f);

            Assert.IsTrue(offsetGrid.TryWorldToCell(-125f, -100f, out WorldGridCoordinate minimum));
            Assert.AreEqual(new WorldGridCoordinate(0, 0), minimum);
            Assert.IsFalse(offsetGrid.TryWorldToCell(-125.001f, -50f, out _));
            Assert.IsFalse(offsetGrid.TryWorldToCell(0f, -100.001f, out _));

            Assert.IsTrue(offsetGrid.TryWorldToCell(124.999f, 99.999f, out WorldGridCoordinate last));
            Assert.AreEqual(new WorldGridCoordinate(249, 199), last);
            Assert.IsFalse(offsetGrid.TryWorldToCell(125f, 0f, out _));
            Assert.IsFalse(offsetGrid.TryWorldToCell(0f, 100f, out _));
        }

        [Test]
        public void CellCenterToWorldToCell_RoundTripsEveryCoordinate()
        {
            var offsetGrid = new WorldGrid(13, 9, 0.5f, 1.25f, -4.25f, -7.5f);

            for (int y = 0; y < offsetGrid.Height; y++)
            {
                for (int x = 0; x < offsetGrid.Width; x++)
                {
                    var expected = new WorldGridCoordinate(x, y);
                    Assert.IsTrue(offsetGrid.TryGetCellCenterWorldPosition(expected, out float worldX, out float worldY));
                    Assert.IsTrue(offsetGrid.TryWorldToCell(worldX, worldY, out WorldGridCoordinate actual));
                    Assert.AreEqual(expected, actual);
                }
            }

            var extremeOriginGrid = new WorldGrid(2, 1, float.MaxValue, 1f, float.MinValue, 0f);
            var extremeCoordinate = new WorldGridCoordinate(1, 0);
            Assert.IsTrue(extremeOriginGrid.TryGetCellCenterWorldPosition(
                extremeCoordinate, out float extremeWorldX, out float extremeWorldY));
            Assert.IsFalse(float.IsNaN(extremeWorldX) || float.IsInfinity(extremeWorldX));
            Assert.IsTrue(extremeOriginGrid.TryWorldToCell(
                extremeWorldX, extremeWorldY, out WorldGridCoordinate extremeRoundTrip));
            Assert.AreEqual(extremeCoordinate, extremeRoundTrip);
        }

        [Test]
        public void Constructor_RejectsInvalidDimensionsAndCellSizes()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(0, 1, 1f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(1, -1, 1f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(1, 1, 0f, 1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(1, 1, 1f, float.PositiveInfinity));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(1, 1, 1f, 1f, float.NaN, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new WorldGrid(1, 1, 1f, 1f, 0f, float.NegativeInfinity));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new WorldGrid(1, 1, float.MaxValue, 1f, float.MaxValue, 0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new WorldGrid(int.MaxValue, 2, 1f, 1f));
        }

        [Test]
        public void LargeTwoDimensionalWorld_PlacesQueriesAndDestroysAllCellsAccurately()
        {
            const int width = 250;
            const int height = 200;
            const int cellCount = width * height;
            var largeGrid = new WorldGrid(width, height, 1f, 1f, -125f, -100f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    WorldGridCoordinate coordinate = new WorldGridCoordinate(x, y);
                    FloorType floorType = (x + y) % 2 == 0 ? FloorType.Ground : FloorType.Soil;
                    WallType wallType = (x + y) % 2 == 0 ? WallType.Stone : WallType.Soil;
                    int durability = y * width + x + 1;

                    Assert.IsTrue(largeGrid.TryPlaceFloor(coordinate, floorType));
                    Assert.IsTrue(largeGrid.TryPlaceWall(coordinate, wallType, durability));
                }
            }

            Assert.AreEqual(cellCount, largeGrid.CellCount);
            Assert.AreEqual(cellCount, largeGrid.FloorCount);
            Assert.AreEqual(cellCount, largeGrid.WallCount);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    WorldGridCoordinate coordinate = new WorldGridCoordinate(x, y);
                    FloorType expectedFloor = (x + y) % 2 == 0 ? FloorType.Ground : FloorType.Soil;
                    WallType expectedWall = (x + y) % 2 == 0 ? WallType.Stone : WallType.Soil;
                    int durability = y * width + x + 1;

                    Assert.IsTrue(largeGrid.TryGetFloor(coordinate, out WorldFloorData floor));
                    Assert.AreEqual(expectedFloor, floor.Type);
                    Assert.IsTrue(largeGrid.TryGetWall(coordinate, out WorldWallData wall));
                    Assert.AreEqual(expectedWall, wall.Type);
                    Assert.AreEqual(durability, wall.MaxDurability);
                    Assert.AreEqual(durability, wall.CurrentDurability);
                    Assert.IsFalse(largeGrid.IsWalkable(coordinate));
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    WorldGridCoordinate coordinate = new WorldGridCoordinate(x, y);
                    int durability = y * width + x + 1;
                    Assert.IsTrue(largeGrid.TryDamageWall(
                        coordinate, durability, out int remainingDurability, out bool wasDestroyed));
                    Assert.AreEqual(0, remainingDurability);
                    Assert.IsTrue(wasDestroyed);
                }
            }

            Assert.AreEqual(cellCount, largeGrid.FloorCount);
            Assert.AreEqual(0, largeGrid.WallCount);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    WorldGridCoordinate coordinate = new WorldGridCoordinate(x, y);
                    Assert.IsTrue(largeGrid.TryGetFloor(coordinate, out _));
                    Assert.IsFalse(largeGrid.TryGetWall(coordinate, out _));
                    Assert.IsTrue(largeGrid.IsWalkable(coordinate));
                }
            }
        }

        private static void AssertChange(
            WorldGridChange change,
            WorldGridCoordinate coordinate,
            WorldGridLayer layer,
            WorldGridChangeKind kind)
        {
            Assert.AreEqual(coordinate, change.Coordinate);
            Assert.AreEqual(layer, change.Layer);
            Assert.AreEqual(kind, change.Kind);
        }

        [TestCase(1000)]
        [TestCase(10000)]
        [TestCase(50000)]
        public void LargeWorld_CreateAndLookupWalls_PreservesEveryCell(int count)
        {
            var largeGrid = new WorldGrid(count, 1, 1f, 1f);

            for (int x = 0; x < count; x++)
            {
                var coordinate = new WorldGridCoordinate(x, 0);
                Assert.IsTrue(largeGrid.TryPlaceFloor(coordinate, FloorType.Ground));
                Assert.IsTrue(largeGrid.TryPlaceWall(coordinate, WallType.Stone, x + 1));
            }

            Assert.AreEqual(count, largeGrid.CellCount);
            Assert.AreEqual(count, largeGrid.FloorCount);
            Assert.AreEqual(count, largeGrid.WallCount);

            for (int x = 0; x < count; x++)
            {
                var coordinate = new WorldGridCoordinate(x, 0);
                Assert.IsTrue(largeGrid.TryGetFloor(coordinate, out WorldFloorData floor));
                Assert.AreEqual(FloorType.Ground, floor.Type);
                Assert.IsTrue(largeGrid.TryGetWall(coordinate, out WorldWallData wall));
                Assert.AreEqual(WallType.Stone, wall.Type);
                Assert.AreEqual(x + 1, wall.MaxDurability);
                Assert.AreEqual(x + 1, wall.CurrentDurability);
                Assert.IsFalse(largeGrid.IsWalkable(coordinate));
            }
        }
    }
}
