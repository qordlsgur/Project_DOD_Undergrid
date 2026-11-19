using System.Reflection;
using NUnit.Framework;
using Prohect_DOD_Undergird.DOD.World;
using Prohect_DOD_Undergird.World;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Prohect_DOD_Undergird.World.Tests
{
    /// <summary>
    /// DOD 셀과 Tilemap 사이의 초기 동기화 및 변경 셀 갱신을 검증합니다.
    /// </summary>
    public sealed class WorldGridTilemapViewTests
    {
        private GameObject gridObject;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private WorldGridTilemapView view;
        private TileBase groundTile;
        private TileBase soilFloorTile;
        private TileBase stoneFloorTile;
        private TileBase soilWallTile;
        private TileBase stoneWallTile;

        [SetUp]
        public void SetUp()
        {
            gridObject = new GameObject("Test Grid", typeof(Grid));
            Grid grid = gridObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;
            grid.transform.position = new Vector3(-2f, -2f, 0f);

            floorTilemap = CreateTilemap("Floor", gridObject.transform, false);
            wallTilemap = CreateTilemap("Wall", gridObject.transform, true);
            view = gridObject.AddComponent<WorldGridTilemapView>();

            groundTile = CreateTile(Tile.ColliderType.None);
            soilFloorTile = CreateTile(Tile.ColliderType.None);
            stoneFloorTile = CreateTile(Tile.ColliderType.None);
            soilWallTile = CreateTile(Tile.ColliderType.Grid);
            stoneWallTile = CreateTile(Tile.ColliderType.Grid);

            SetField("worldGrid", grid);
            SetField("floorTilemap", floorTilemap);
            SetField("wallTilemap", wallTilemap);
            SetField("groundFloorTile", groundTile);
            SetField("soilFloorTile", soilFloorTile);
            SetField("stoneFloorTile", stoneFloorTile);
            SetField("soilWallTile", soilWallTile);
            SetField("stoneWallTile", stoneWallTile);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gridObject);
            Object.DestroyImmediate(groundTile);
            Object.DestroyImmediate(soilFloorTile);
            Object.DestroyImmediate(stoneFloorTile);
            Object.DestroyImmediate(soilWallTile);
            Object.DestroyImmediate(stoneWallTile);
        }

        [Test]
        public void Initialize_RendersInitialBlocksAndAppliesOnlyChangedWallTiles()
        {
            var grid = new WorldGrid(4, 4, 1f, 1f, -2f, -2f);
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            var wallCoordinate = new WorldGridCoordinate(3, 3);
            Assert.IsTrue(grid.TryPlaceWall(wallCoordinate, WallType.Stone, 10));

            Assert.IsTrue(view.Initialize(grid));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(3, 3, 0)));
            Assert.IsNotNull(wallTilemap.GetComponent<TilemapCollider2D>());
            Assert.IsNull(floorTilemap.GetComponent<TilemapCollider2D>());

            Assert.IsTrue(grid.TryDamageWall(wallCoordinate, 1, out _, out bool wasDestroyed));
            Assert.IsFalse(wasDestroyed);
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(3, 3, 0)));

            Assert.IsTrue(grid.TryDamageWall(wallCoordinate, 9, out _, out wasDestroyed));
            Assert.IsTrue(wasDestroyed);
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(3, 3, 0)));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(3, 3, 0)));
            Assert.IsTrue(grid.IsWalkable(wallCoordinate));
        }

        [Test]
        public void CellChanged_AppliesWallPlacementAfterInitialSynchronization()
        {
            WorldGrid grid = CreateGroundGrid(4, 4);

            Assert.IsTrue(view.Initialize(grid));
            var coordinate = new WorldGridCoordinate(2, 1);
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Soil, 5));
            Assert.AreSame(soilWallTile, wallTilemap.GetTile(new Vector3Int(2, 1, 0)));
        }

        [Test]
        public void Reactivate_AppliesChangesMadeWhileViewWasDisabled()
        {
            WorldGrid grid = CreateGroundGrid(4, 4);
            Assert.IsTrue(view.Initialize(grid));

            view.enabled = false;
            var coordinate = new WorldGridCoordinate(2, 2);
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 8));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(2, 2, 0)));

            view.enabled = true;

            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(2, 2, 0)));
        }

        [Test]
        public void Reinitialize_WithSmallerWorld_ClearsTilesOutsideNewBounds()
        {
            WorldGrid largerGrid = CreateGroundGrid(4, 4);
            var oldWallCoordinate = new WorldGridCoordinate(3, 3);
            Assert.IsTrue(largerGrid.TryPlaceWall(oldWallCoordinate, WallType.Stone, 8));
            Assert.IsTrue(view.Initialize(largerGrid));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(3, 3, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(3, 3, 0)));

            WorldGrid smallerGrid = CreateGroundGrid(2, 2);
            Assert.IsTrue(view.Initialize(smallerGrid));

            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(1, 1, 0)));
            Assert.IsNull(floorTilemap.GetTile(new Vector3Int(3, 3, 0)));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(3, 3, 0)));

            Assert.IsTrue(largerGrid.TryPlaceWall(new WorldGridCoordinate(2, 2), WallType.Soil, 3));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(2, 2, 0)));
        }

        [Test]
        public void Initialize_InvalidSource_PreservesCurrentViewAndSubscription()
        {
            WorldGrid grid = CreateGroundGrid(4, 4);
            Assert.IsTrue(view.Initialize(grid));

            Assert.IsFalse(view.Initialize(null));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(0, 0, 0)));

            var coordinate = new WorldGridCoordinate(1, 1);
            Assert.IsTrue(grid.TryPlaceWall(coordinate, WallType.Stone, 5));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(1, 1, 0)));
        }

        [Test]
        public void InitializeChunks_RendersNegativeAndPartialChunksAtGridWorldPositions()
        {
            var bounds = new WorldCellBounds(-3, -3, 2, 2);
            var store = new WorldChunkStore(bounds, 4, 1f, 1f);
            WorldChunkCoordinate negativeChunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkCoordinate positiveChunk = new WorldChunkCoordinate(0, 0);
            CreateReadyChunk(store, negativeChunk, new WorldGridCoordinate(1, 1));
            CreateReadyChunk(store, positiveChunk, new WorldGridCoordinate(1, 1));

            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsTrue(view.TryDisplayChunk(negativeChunk));
            Assert.IsTrue(view.TryDisplayChunk(positiveChunk));

            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(-1, -1, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(2, 2, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(3, 3, 0)));
        }

        [Test]
        public void RemovingChunkDisplay_PreservesNeighborAndAllowsRedisplayBeforeUnload()
        {
            var store = new WorldChunkStore(new WorldCellBounds(-2, -2, 2, 2), 2, 1f, 1f);
            WorldChunkCoordinate leftChunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkCoordinate rightChunk = new WorldChunkCoordinate(0, -1);
            CreateReadyChunk(store, leftChunk, new WorldGridCoordinate(0, 0));
            CreateReadyChunk(store, rightChunk, new WorldGridCoordinate(0, 0));
            Assert.IsTrue(view.InitializeChunks(store));

            Assert.IsTrue(view.TryDisplayChunk(leftChunk));
            Assert.IsTrue(view.TryDisplayChunk(rightChunk));
            Assert.IsFalse(store.TryUnloadChunk(leftChunk));
            Assert.IsTrue(view.TryRemoveChunkDisplay(leftChunk));

            Assert.IsNull(floorTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(2, 0, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(2, 0, 0)));
            Assert.IsTrue(view.TryDisplayChunk(leftChunk));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.IsTrue(view.TryRemoveChunkDisplay(leftChunk));
            Assert.IsTrue(store.TryUnloadChunk(leftChunk));
        }

        [Test]
        public void ChunkChanges_UpdateDisplayedOnlyAndHiddenChunkUsesLatestDataOnDisplay()
        {
            var store = new WorldChunkStore(new WorldCellBounds(-2, -2, 2, 2), 2, 1f, 1f);
            WorldChunkCoordinate displayedChunk = new WorldChunkCoordinate(-1, -1);
            WorldChunkCoordinate hiddenChunk = new WorldChunkCoordinate(0, -1);
            CreateReadyChunk(store, displayedChunk, new WorldGridCoordinate(0, 0));
            CreateReadyChunk(store, hiddenChunk, null);
            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsTrue(view.TryDisplayChunk(displayedChunk));
            var world = new WorldGridWorld(store);

            Assert.IsTrue(world.TryDamageWall(
                new WorldCellCoordinate(-2, -2),
                10,
                out _,
                out bool wasDestroyed,
                out _));
            Assert.IsTrue(wasDestroyed);
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(0, 0, 0)));

            Assert.IsTrue(world.TryPlaceWall(new WorldCellCoordinate(0, -2), WallType.Soil, 6, out _));
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(2, 0, 0)));
            Assert.IsTrue(view.TryDisplayChunk(hiddenChunk));
            Assert.AreSame(soilWallTile, wallTilemap.GetTile(new Vector3Int(2, 0, 0)));
        }

        [Test]
        public void TryDisplayChunk_RejectsQueuedDataAndIsIdempotentWhenReady()
        {
            var store = new WorldChunkStore(new WorldCellBounds(-2, -2, 2, 2), 2, 1f, 1f);
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            Assert.IsTrue(store.TryQueueChunk(chunk));
            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsFalse(view.TryDisplayChunk(chunk));

            CreateReadyChunk(store, chunk, new WorldGridCoordinate(0, 0));
            Assert.IsTrue(view.TryDisplayChunk(chunk));
            Assert.IsTrue(view.TryDisplayChunk(chunk));
            Assert.AreEqual(WorldChunkPresentationState.Displayed, GetPresentationState(store, chunk));
            Assert.IsTrue(view.TryRemoveChunkDisplay(chunk));
            Assert.IsTrue(view.TryRemoveChunkDisplay(chunk));
            Assert.AreEqual(WorldChunkPresentationState.NotDisplayed, GetPresentationState(store, chunk));
        }

        [Test]
        public void InitializeChunks_WhenDisabled_DefersSynchronizationUntilEnabled()
        {
            var store = new WorldChunkStore(new WorldCellBounds(-2, -2, 2, 2), 2, 1f, 1f);
            WorldChunkCoordinate chunk = new WorldChunkCoordinate(-1, -1);
            CreateReadyChunk(store, chunk, new WorldGridCoordinate(0, 0));
            Assert.IsTrue(store.TrySetDisplayed(chunk, true));

            view.enabled = false;
            Assert.IsTrue(view.InitializeChunks(store));
            Assert.IsNull(floorTilemap.GetTile(new Vector3Int(0, 0, 0)));

            view.enabled = true;

            Assert.AreSame(groundTile, floorTilemap.GetTile(new Vector3Int(0, 0, 0)));
            Assert.AreSame(stoneWallTile, wallTilemap.GetTile(new Vector3Int(0, 0, 0)));
        }

        private static WorldGrid CreateGroundGrid(int width, int height)
        {
            var grid = new WorldGrid(width, height, 1f, 1f, -2f, -2f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Assert.IsTrue(grid.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            return grid;
        }

        private WorldGrid CreateReadyChunk(
            WorldChunkStore store,
            WorldChunkCoordinate chunk,
            WorldGridCoordinate? wallCoordinate)
        {
            WorldChunkDataState dataState = store.GetDataState(chunk);
            if (dataState == WorldChunkDataState.Unloaded || dataState == WorldChunkDataState.Failed)
            {
                Assert.IsTrue(store.TryQueueChunk(chunk));
            }
            else
            {
                Assert.AreEqual(WorldChunkDataState.Queued, dataState);
            }

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
                Assert.IsTrue(chunkGrid.TryPlaceWall(wallCoordinate.Value, WallType.Stone, 10));
            }

            Assert.IsTrue(store.TryMarkReady(chunk));
            return chunkGrid;
        }

        private static WorldChunkPresentationState GetPresentationState(
            WorldChunkStore store,
            WorldChunkCoordinate chunk)
        {
            Assert.IsTrue(store.TryGetPresentationState(chunk, out WorldChunkPresentationState state));
            return state;
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

        private static Tile CreateTile(Tile.ColliderType colliderType)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.colliderType = colliderType;
            return tile;
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
