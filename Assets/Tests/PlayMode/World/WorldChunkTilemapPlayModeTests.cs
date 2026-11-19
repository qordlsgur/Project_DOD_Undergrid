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
    /// <summary>청크별 Tilemap 표시와 벽 물리 형상 수명을 검증합니다.</summary>
    public sealed class WorldChunkTilemapPlayModeTests
    {
        private GameObject gridObject;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private WorldGridTilemapView view;
        private Tile groundTile;
        private Tile soilWallTile;
        private Tile stoneWallTile;
        private Texture2D tileTexture;
        private Sprite tileSprite;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            gridObject = new GameObject("Chunk Test Grid", typeof(Grid));
            Grid grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.transform.position = new Vector3(-2f, -2f, 0f);

            floorTilemap = CreateTilemap("Floor", gridObject.transform, false);
            wallTilemap = CreateTilemap("Wall", gridObject.transform, true);
            view = gridObject.AddComponent<WorldGridTilemapView>();
            CreateTiles();

            SetField("worldGrid", grid);
            SetField("floorTilemap", floorTilemap);
            SetField("wallTilemap", wallTilemap);
            SetField("groundFloorTile", groundTile);
            SetField("soilFloorTile", groundTile);
            SetField("stoneFloorTile", groundTile);
            SetField("soilWallTile", soilWallTile);
            SetField("stoneWallTile", stoneWallTile);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (gridObject != null)
            {
                Object.Destroy(gridObject);
            }

            if (groundTile != null)
            {
                Object.Destroy(groundTile);
            }

            if (soilWallTile != null)
            {
                Object.Destroy(soilWallTile);
            }

            if (stoneWallTile != null)
            {
                Object.Destroy(stoneWallTile);
            }

            if (tileSprite != null)
            {
                Object.Destroy(tileSprite);
            }

            if (tileTexture != null)
            {
                Object.Destroy(tileTexture);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisplayAndRemoveChunk_UpdatesOnlyItsTilesAndColliderShapes()
        {
            WorldChunkStore store = CreateStore(new WorldCellBounds(-2, -2, 2, 2), 2);
            WorldChunkCoordinate leftChunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkCoordinate rightChunk = new WorldChunkCoordinate(0, -1);
            CreateReadyChunk(store, leftChunk, new WorldGridCoordinate(0, 0), WallType.Stone);
            CreateReadyChunk(store, rightChunk, new WorldGridCoordinate(0, 0), WallType.Soil);
            Assert.IsTrue(view.InitializeChunks(store));

            Assert.IsTrue(view.TryDisplayChunk(leftChunk));
            Assert.IsTrue(view.TryDisplayChunk(rightChunk));
            TilemapCollider2D wallCollider = wallTilemap.GetComponent<TilemapCollider2D>();
            Assert.IsTrue(wallCollider.OverlapPoint(new Vector2(-1.5f, -1.5f)));
            Assert.IsTrue(wallCollider.OverlapPoint(new Vector2(0.5f, -1.5f)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(soilWallTile, wallTilemap.GetTile(new Vector3Int(2, 0, 0)));
            Assert.IsFalse(store.TryUnloadChunk(leftChunk));

            Assert.IsTrue(view.TryRemoveChunkDisplay(leftChunk));
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(wallCollider.OverlapPoint(new Vector2(-1.5f, -1.5f)));
            Assert.IsTrue(wallCollider.OverlapPoint(new Vector2(0.5f, -1.5f)));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(soilWallTile, wallTilemap.GetTile(new Vector3Int(2, 0, 0)));
            Assert.IsTrue(view.TryDisplayChunk(leftChunk));
            Assert.IsTrue(wallCollider.OverlapPoint(new Vector2(-1.5f, -1.5f)));
            Assert.IsTrue(view.TryRemoveChunkDisplay(leftChunk));
            Assert.IsTrue(store.TryUnloadChunk(leftChunk));
            Assert.AreEqual(WorldChunkDataState.Unloaded, store.GetDataState(leftChunk));
        }

        [UnityTest]
        public IEnumerator MiningDisplayedChunk_UpdatesTileAndPhysicalShapeButKeepsSameTileDuringDamage()
        {
            WorldChunkStore store = CreateStore(new WorldCellBounds(-2, -2, 2, 2), 2);
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            CreateReadyChunk(store, chunk, new WorldGridCoordinate(0, 0), WallType.Stone, 2);
            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsTrue(view.TryDisplayChunk(chunk));
            var world = new WorldGridWorld(store);
            var wallCell = new WorldCellCoordinate(-2, -2);
            Vector3Int tileCoordinate = new Vector3Int(0, 0, 0);
            Vector2 wallCenter = new Vector2(-1.5f, -1.5f);
            TilemapCollider2D wallCollider = wallTilemap.GetComponent<TilemapCollider2D>();

            Assert.IsTrue(wallCollider.OverlapPoint(wallCenter));
            Assert.IsTrue(world.TryDamageWall(wallCell, 1, out int remaining, out bool destroyed, out _));
            Assert.AreEqual(1, remaining);
            Assert.IsFalse(destroyed);
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(tileCoordinate));
            Assert.IsTrue(wallCollider.OverlapPoint(wallCenter));

            Assert.IsTrue(world.TryDamageWall(wallCell, 1, out _, out destroyed, out _));
            Assert.IsTrue(destroyed);
            Assert.IsNull(wallTilemap.GetTile(tileCoordinate));
            Assert.IsTrue(floorTilemap.GetTile(tileCoordinate) != null);
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(wallCollider.OverlapPoint(wallCenter));
            Assert.IsTrue(world.GetWalkability(wallCell) == WorldCellWalkability.Walkable);
        }

        [UnityTest]
        public IEnumerator HiddenChunkChanges_AppearOnlyAfterThatChunkIsDisplayed()
        {
            WorldChunkStore store = CreateStore(new WorldCellBounds(-2, -2, 2, 2), 2);
            WorldChunkCoordinate displayedChunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkCoordinate hiddenChunk = new WorldChunkCoordinate(0, -1);
            CreateReadyChunk(store, displayedChunk, null, WallType.None);
            CreateReadyChunk(store, hiddenChunk, null, WallType.None);
            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsTrue(view.TryDisplayChunk(displayedChunk));

            var world = new WorldGridWorld(store);
            var hiddenCell = new WorldCellCoordinate(0, -2);
            Vector3Int hiddenTileCoordinate = new Vector3Int(2, 0, 0);
            Assert.IsTrue(world.TryPlaceWall(hiddenCell, WallType.Soil, 5, out _));
            Assert.IsNull(wallTilemap.GetTile(hiddenTileCoordinate));

            Assert.IsTrue(view.TryDisplayChunk(hiddenChunk));
            Assert.AreSame(soilWallTile, wallTilemap.GetTile(hiddenTileCoordinate));
            TilemapCollider2D wallCollider = wallTilemap.GetComponent<TilemapCollider2D>();
            Assert.IsTrue(wallCollider.OverlapPoint(new Vector2(0.5f, -1.5f)));
            yield return null;
        }

        private static WorldChunkStore CreateStore(WorldCellBounds bounds, int chunkSize)
        {
            return new WorldChunkStore(bounds, chunkSize, 1f, 1f);
        }

        private static void CreateReadyChunk(
            WorldChunkStore store,
            WorldChunkCoordinate chunk,
            WorldGridCoordinate? wallCoordinate,
            WallType wallType,
            int durability = 10)
        {
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.IsTrue(store.TryBeginGeneration(chunk, out WorldGrid chunkGrid));
            for (int y = 0; y < chunkGrid.Height; y++)
            {
                for (int x = 0; x < chunkGrid.Width; x++)
                {
                    Assert.IsTrue(chunkGrid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            if (wallCoordinate.HasValue)
            {
                Assert.IsTrue(chunkGrid.TryPlaceWall(wallCoordinate.Value, wallType, durability));
            }

            Assert.IsTrue(store.TryMarkReady(chunk));
        }

        private void CreateTiles()
        {
            tileTexture = new Texture2D(1, 1);
            tileTexture.SetPixel(0, 0, Color.white);
            tileTexture.Apply();
            tileSprite = Sprite.Create(
                tileTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            groundTile = CreateTile(Tile.ColliderType.None);
            soilWallTile = CreateTile(Tile.ColliderType.Grid);
            stoneWallTile = CreateTile(Tile.ColliderType.Grid);
        }

        private Tile CreateTile(Tile.ColliderType colliderType)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = tileSprite;
            tile.colliderType = colliderType;
            return tile;
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

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(WorldGridTilemapView).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(view, value);
        }
    }
}
