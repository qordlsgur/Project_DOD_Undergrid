using System.Reflection;
using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;
using Prohect_DOD_Undergird.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Prohect_DOD_Undergird.World.Tests
{
    /// <summary>
    /// 장착 채굴 도구의 거리, 커서 대상 셀 선택 및 채굴력 적용을 검증합니다.
    /// </summary>
    public sealed class PlayerMiningControllerTests
    {
        private GameObject playerObject;
        private PlayerMiningController miningController;
        private WorldMiningToolData toolData;
        private WorldMiningToolData strongerTool;
        private WorldGrid grid;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("Mining Test Player");
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            miningController = playerObject.AddComponent<PlayerMiningController>();
            toolData = ScriptableObject.CreateInstance<WorldMiningToolData>();
            SetToolField("miningPower", 2);
            SetToolField("miningInterval", 0.5f);
            SetToolField("miningRange", 1.5f);
            miningController.SetEquippedTool(toolData);

            grid = CreateGridWithFloors(4, 1);
            Assert.IsTrue(grid.TryPlaceWall(new WorldGridCoordinate(1, 0), WallType.Stone, 10));
            Assert.IsTrue(grid.TryPlaceWall(new WorldGridCoordinate(2, 0), WallType.Soil, 10));
            Assert.IsTrue(miningController.Initialize(grid));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(toolData);
            if (strongerTool != null)
            {
                Object.DestroyImmediate(strongerTool);
            }
        }

        [Test]
        public void TryMineAtWorldPosition_UsesCursorCellAndDamagesOnlyThatWall()
        {
            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.2f, 0.4f)));

            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(1, 0), out WorldWallData selectedWall));
            Assert.AreEqual(8, selectedWall.CurrentDurability);
            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(2, 0), out WorldWallData otherWall));
            Assert.AreEqual(10, otherWall.CurrentDurability);
        }

        [Test]
        public void TryMineAtWorldPosition_RejectsWallOutsideEquippedToolRange()
        {
            Assert.IsFalse(miningController.TryMineAtWorldPosition(new Vector2(2.5f, 0.5f)));

            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(2, 0), out WorldWallData wall));
            Assert.AreEqual(10, wall.CurrentDurability);
        }

        [Test]
        public void SetEquippedTool_ChangesMiningPowerAndRange()
        {
            strongerTool = ScriptableObject.CreateInstance<WorldMiningToolData>();
            SetToolField(strongerTool, "miningPower", 5);
            SetToolField(strongerTool, "miningInterval", 0.2f);
            SetToolField(strongerTool, "miningRange", 1.1f);
            miningController.SetEquippedTool(strongerTool);

            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.2f, 0.4f)));
            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(1, 0), out WorldWallData wall));
            Assert.AreEqual(5, wall.CurrentDurability);

            Assert.IsFalse(miningController.TryMineAtWorldPosition(new Vector2(2.5f, 0.5f)));
            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(2, 0), out WorldWallData outOfRangeWall));
            Assert.AreEqual(10, outOfRangeWall.CurrentDurability);

        }

        private void SetToolField(string fieldName, object value)
        {
            SetToolField(toolData, fieldName, value);
        }

        private static void SetToolField(WorldMiningToolData target, string fieldName, object value)
        {
            FieldInfo field = typeof(WorldMiningToolData).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        private static WorldGrid CreateGridWithFloors(int width, int height)
        {
            var result = new WorldGrid(width, height, 1f, 1f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.IsTrue(result.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            return result;
        }
    }
}
