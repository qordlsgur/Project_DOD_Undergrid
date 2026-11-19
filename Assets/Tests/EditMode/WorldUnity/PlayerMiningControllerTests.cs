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
        private InventoryItemData copperItem;
        private InventoryItemData ironItem;
        private PlayerInventory playerInventory;
        private WorldGrid grid;

        [SetUp]
        public void SetUp()
        {
            playerObject = new GameObject("Mining Test Player");
            playerObject.SetActive(false);
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            CreatePlayerInventory(playerObject, 40, 999);
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
            playerObject.SetActive(true);
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

            if (copperItem != null)
            {
                Object.DestroyImmediate(copperItem);
            }

            if (ironItem != null)
            {
                Object.DestroyImmediate(ironItem);
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

        [Test]
        public void TryMineAtWorldPosition_AwardsOreOnlyOnceWhenTheWallIsDestroyed()
        {
            Assert.IsTrue(grid.TryDamageWall(new WorldGridCoordinate(1, 0), 10, out _, out bool copperRemoved));
            Assert.IsTrue(copperRemoved);
            Assert.IsTrue(grid.TryDamageWall(new WorldGridCoordinate(2, 0), 10, out _, out bool ironRemoved));
            Assert.IsTrue(ironRemoved);

            var copperOre = new WorldOreDefinition(
                WallType.CopperOre, WallType.Stone, WorldResourceType.Copper,
                11, 0.1f, 0.5f, 2, 3);
            var ironOre = new WorldOreDefinition(
                WallType.IronOre, WallType.Stone, WorldResourceType.Iron,
                12, 0.1f, 0.5f, 1, 2);
            Assert.IsTrue(grid.TryPlaceOreWall(new WorldGridCoordinate(1, 0), copperOre));
            Assert.IsTrue(grid.TryPlaceOreWall(new WorldGridCoordinate(2, 0), ironOre));
            SetToolField("miningPower", 1);
            SetToolField("miningRange", 3f);
            miningController.SetEquippedTool(toolData);

            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.AreEqual(0, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));
            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.AreEqual(3, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));
            Assert.IsFalse(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.AreEqual(3, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));

            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(2.5f, 0.5f)));
            Assert.AreEqual(2, miningController.ResourceInventory.GetAmount(WorldResourceType.Iron));
            Assert.IsFalse(miningController.TryMineAtWorldPosition(new Vector2(2.5f, 0.5f)));
            Assert.AreEqual(2, miningController.ResourceInventory.GetAmount(WorldResourceType.Iron));
        }

        [Test]
        public void WorldResourceInventory_RejectsInvalidAmountsAndKeepsResourceCountsIndependent()
        {
            var inventory = new WorldResourceInventory();

            Assert.IsFalse(inventory.TryAdd(WorldResourceType.Copper, 0));
            Assert.IsFalse(inventory.TryAdd(WorldResourceType.Iron, -1));
            Assert.IsFalse(inventory.TryAdd(WorldResourceType.None, 5));
            Assert.IsTrue(inventory.TryAdd(WorldResourceType.Copper, 4));
            Assert.IsTrue(inventory.TryAdd(WorldResourceType.Iron, 2));

            Assert.AreEqual(4, inventory.GetAmount(WorldResourceType.Copper));
            Assert.AreEqual(2, inventory.GetAmount(WorldResourceType.Iron));
        }

        [Test]
        public void TryMineAtWorldPosition_DestroyingNormalWallDoesNotAwardOre()
        {
            SetToolField("miningPower", 10);
            miningController.SetEquippedTool(toolData);

            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.AreEqual(0, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));
            Assert.AreEqual(0, miningController.ResourceInventory.GetAmount(WorldResourceType.Iron));
        }

        [Test]
        public void PlayerInventory_UsesConfiguredIdsAndMaximumStackFromItemData()
        {
            Assert.AreEqual(40, playerInventory.Inventory.Capacity);
            Assert.IsTrue(playerInventory.TryAdd(new ItemId(1001), 1000));
            Assert.IsTrue(playerInventory.TryGetSlot(0, out InventorySlot firstStack));
            Assert.IsTrue(playerInventory.TryGetSlot(1, out InventorySlot secondStack));

            Assert.AreEqual(new ItemId(1001), firstStack.ItemId);
            Assert.AreEqual(999, firstStack.Amount);
            Assert.AreEqual(new ItemId(1001), secondStack.ItemId);
            Assert.AreEqual(1, secondStack.Amount);
            Assert.AreEqual(1000, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));
        }

        [Test]
        public void TryMineAtWorldPosition_FullInventoryRejectsFinalOreHitAndPreservesWall()
        {
            Object.DestroyImmediate(playerObject);
            Object.DestroyImmediate(copperItem);
            Object.DestroyImmediate(ironItem);
            playerObject = new GameObject("Full Mining Inventory Player");
            playerObject.SetActive(false);
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            CreatePlayerInventory(playerObject, 1, 1);
            miningController = playerObject.AddComponent<PlayerMiningController>();
            Assert.IsTrue(miningController.Initialize(grid));
            playerObject.SetActive(true);
            miningController.SetEquippedTool(toolData);

            Assert.IsTrue(playerInventory.TryAdd(new ItemId(1001), 1));
            Assert.IsTrue(grid.TryDamageWall(new WorldGridCoordinate(1, 0), 10, out _, out _));
            var copperOre = new WorldOreDefinition(
                WallType.CopperOre, WallType.Stone, WorldResourceType.Copper,
                11, 0.1f, 0.5f, 10, 1);
            Assert.IsTrue(grid.TryPlaceOreWall(new WorldGridCoordinate(1, 0), copperOre));
            SetToolField("miningPower", 10);
            miningController.SetEquippedTool(toolData);

            Assert.IsFalse(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.IsTrue(grid.TryGetWall(new WorldGridCoordinate(1, 0), out WorldWallData wall));
            Assert.AreEqual(10, wall.CurrentDurability);
            Assert.AreEqual(1, miningController.ResourceInventory.GetAmount(WorldResourceType.Copper));
        }

        private void CreatePlayerInventory(GameObject target, int capacity, int maxStack)
        {
            copperItem = ScriptableObject.CreateInstance<InventoryItemData>();
            ironItem = ScriptableObject.CreateInstance<InventoryItemData>();
            SetItemField(copperItem, "itemId", 1001);
            SetItemField(copperItem, "displayName", "Copper Ore");
            SetItemField(copperItem, "maxStack", maxStack);
            SetItemField(ironItem, "itemId", 1002);
            SetItemField(ironItem, "displayName", "Iron Ore");
            SetItemField(ironItem, "maxStack", 999);

            playerInventory = target.AddComponent<PlayerInventory>();
            SetInventoryField("slotCapacity", capacity);
            SetInventoryField("itemDefinitions", new[] { copperItem, ironItem });
            SetInventoryField("resourceBindings", new[]
            {
                new InventoryResourceBinding(WorldResourceType.Copper, copperItem),
                new InventoryResourceBinding(WorldResourceType.Iron, ironItem)
            });
            Assert.IsTrue(playerInventory.Initialize());
        }

        private void SetInventoryField(string fieldName, object value)
        {
            FieldInfo field = typeof(PlayerInventory).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(playerInventory, value);
        }

        private static void SetItemField(InventoryItemData item, string fieldName, object value)
        {
            FieldInfo field = typeof(InventoryItemData).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(item, value);
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
