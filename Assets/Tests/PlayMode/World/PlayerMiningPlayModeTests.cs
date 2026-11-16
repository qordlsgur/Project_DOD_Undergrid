using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;
using Prohect_DOD_Undergird.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Prohect_DOD_Undergird.World.Tests
{
    /// <summary>
    /// 채굴 피해가 WorldGrid 이벤트를 거쳐 Tilemap과 벽 Collider에 반영되는지 검증합니다.
    /// </summary>
    public sealed class PlayerMiningPlayModeTests
    {
        private GameObject gridObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private Tile groundTile;
        private Tile wallTile;
        private Sprite tileSprite;
        private Texture2D tileTexture;
        private WorldMiningToolData toolData;
        private WorldMiningToolData fasterToolData;
        private float previousTimeScale;

        [UnityTest]
        public IEnumerator MiningWall_RemovesWallTileAndColliderAndPreservesFloor()
        {
            BeginTimeTracking();
            CreateWorld();
            WorldGrid gridData = CreateGrid();
            Assert.IsTrue(gridData.TryPlaceWall(new WorldGridCoordinate(1, 0), WallType.Stone, 10));
            WorldGridTilemapView view = gridObject.GetComponent<WorldGridTilemapView>();
            Assert.IsTrue(view.Initialize(gridData));

            PlayerMiningController miningController = CreatePlayer();
            Assert.IsTrue(miningController.Initialize(gridData));
            toolData = ScriptableObject.CreateInstance<WorldMiningToolData>();
            SetToolField("miningPower", 10);
            SetToolField("miningInterval", 0.5f);
            SetToolField("miningRange", 1.5f);
            miningController.SetEquippedTool(toolData);

            Vector3Int wallCell = new Vector3Int(1, 0, 0);
            Assert.AreSame(wallTile, wallTilemap.GetTile(wallCell));
            Assert.IsTrue(miningController.TryMineAtWorldPosition(new Vector2(1.5f, 0.5f)));
            Assert.IsNull(wallTilemap.GetTile(wallCell));
            Assert.AreSame(groundTile, floorTilemap.GetTile(wallCell));
            Assert.IsTrue(gridData.IsWalkable(new WorldGridCoordinate(1, 0)));

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.IsNull(
                Physics2D.OverlapPoint(new Vector2(1.5f, 0.5f)),
                "채굴된 벽 셀에는 충돌 Collider가 남아 있으면 안 됩니다.");
        }

        [UnityTest]
        public IEnumerator HandlePrimaryAction_UsesEquippedToolMiningInterval()
        {
            BeginTimeTracking();
            CreateWorld();
            WorldGrid gridData = CreateGrid();
            Assert.IsTrue(gridData.TryPlaceWall(new WorldGridCoordinate(1, 0), WallType.Stone, 10));
            Assert.IsTrue(gridObject.GetComponent<WorldGridTilemapView>().Initialize(gridData));
            PlayerMiningController miningController = CreatePlayer();
            Assert.IsTrue(miningController.Initialize(gridData));
            toolData = CreateTool(1, 0.35f, 1.5f);
            miningController.SetEquippedTool(toolData);
            Camera camera = CreateCamera();
            SetField(miningController, "miningCamera", camera);
            Vector3 mouseScreenPosition = camera.WorldToScreenPoint(new Vector3(1.5f, 0.5f, 0f));

            Assert.IsTrue(miningController.HandlePrimaryAction(true, mouseScreenPosition));
            Assert.IsTrue(miningController.HandlePrimaryAction(true, mouseScreenPosition));
            Assert.AreEqual(9, GetWall(gridData).CurrentDurability, "쿨다운 중에는 추가 피해가 없어야 합니다.");

            yield return new WaitForSeconds(0.4f);
            Assert.IsTrue(miningController.HandlePrimaryAction(true, mouseScreenPosition));
            Assert.AreEqual(8, GetWall(gridData).CurrentDurability);

            fasterToolData = ScriptableObject.CreateInstance<WorldMiningToolData>();
            SetToolField(fasterToolData, "miningPower", 1);
            SetToolField(fasterToolData, "miningInterval", 0.05f);
            SetToolField(fasterToolData, "miningRange", 1.5f);
            miningController.SetEquippedTool(fasterToolData);
            Assert.IsTrue(miningController.HandlePrimaryAction(true, mouseScreenPosition));
            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(miningController.HandlePrimaryAction(true, mouseScreenPosition));
            Assert.AreEqual(6, GetWall(gridData).CurrentDurability);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (playerObject != null) Object.Destroy(playerObject);
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (gridObject != null) Object.Destroy(gridObject);
            if (toolData != null) Object.Destroy(toolData);
            if (fasterToolData != null) Object.Destroy(fasterToolData);
            if (wallTile != null) Object.Destroy(wallTile);
            if (groundTile != null) Object.Destroy(groundTile);
            if (tileSprite != null) Object.Destroy(tileSprite);
            if (tileTexture != null) Object.Destroy(tileTexture);
            yield return null;
        }

        private void BeginTimeTracking()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        private void CreateWorld()
        {
            gridObject = new GameObject("Mining Test Grid", typeof(Grid));
            gridObject.GetComponent<Grid>().cellSize = Vector3.one;
            floorTilemap = CreateTilemap("Floor", gridObject.transform, false);
            wallTilemap = CreateTilemap("Wall", gridObject.transform, true);
            WorldGridTilemapView view = gridObject.AddComponent<WorldGridTilemapView>();
            CreateTiles();

            SetField(view, "worldGrid", gridObject.GetComponent<Grid>());
            SetField(view, "floorTilemap", floorTilemap);
            SetField(view, "wallTilemap", wallTilemap);
            SetField(view, "groundFloorTile", groundTile);
            SetField(view, "soilFloorTile", groundTile);
            SetField(view, "stoneFloorTile", groundTile);
            SetField(view, "soilWallTile", wallTile);
            SetField(view, "stoneWallTile", wallTile);
        }

        private PlayerMiningController CreatePlayer()
        {
            playerObject = new GameObject("Mining Test Player");
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            return playerObject.AddComponent<PlayerMiningController>();
        }

        private WorldGrid CreateGrid()
        {
            var gridData = new WorldGrid(3, 1, 1f, 1f);
            for (int x = 0; x < gridData.Width; x++)
            {
                Assert.IsTrue(gridData.TryPlaceFloor(new WorldGridCoordinate(x, 0), FloorType.Ground));
            }

            return gridData;
        }

        private WorldMiningToolData CreateTool(int power, float interval, float range)
        {
            toolData = ScriptableObject.CreateInstance<WorldMiningToolData>();
            SetToolField("miningPower", power);
            SetToolField("miningInterval", interval);
            SetToolField("miningRange", range);
            return toolData;
        }

        private void CreateTiles()
        {
            tileTexture = new Texture2D(1, 1);
            tileTexture.SetPixel(0, 0, Color.white);
            tileTexture.Apply();
            tileSprite = Sprite.Create(tileTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

            groundTile = ScriptableObject.CreateInstance<Tile>();
            groundTile.sprite = tileSprite;
            groundTile.colliderType = Tile.ColliderType.None;
            wallTile = ScriptableObject.CreateInstance<Tile>();
            wallTile.sprite = tileSprite;
            wallTile.colliderType = Tile.ColliderType.Grid;
        }

        private Camera CreateCamera()
        {
            cameraObject = new GameObject("Mining Test Camera");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            return camera;
        }

        private static WorldWallData GetWall(WorldGrid gridData)
        {
            Assert.IsTrue(gridData.TryGetWall(new WorldGridCoordinate(1, 0), out WorldWallData wall));
            return wall;
        }

        private void SetToolField(string fieldName, object value)
        {
            SetField(toolData, fieldName, value);
        }

        private static void SetToolField(WorldMiningToolData target, string fieldName, object value)
        {
            SetField(target, fieldName, value);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }

        private static Tilemap CreateTilemap(string objectName, Transform parent, bool addCollider)
        {
            var tilemapObject = new GameObject(objectName, typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(parent, false);
            if (addCollider)
            {
                tilemapObject.AddComponent<TilemapCollider2D>();
            }

            return tilemapObject.GetComponent<Tilemap>();
        }
    }
}
