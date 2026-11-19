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
    /// <summary>플레이어 중심 생성 범위, 화면 표시 범위와 대시 경로 준비를 통합 검증합니다.</summary>
    public sealed class WorldChunkStreamingPlayModeTests
    {
        private GameObject worldObject;
        private GameObject playerObject;
        private GameObject cameraObject;
        private Grid grid;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private Tile groundTile;
        private Tile soilWallTile;
        private Texture2D tileTexture;
        private Sprite tileSprite;
        private WorldGridBootstrap bootstrap;
        private WorldGridTilemapView tilemapView;
        private WorldChunkStreamer streamer;
        private Collider2D playerCollider;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            worldObject = new GameObject("Streaming Test World", typeof(Grid));
            worldObject.SetActive(false);
            grid = worldObject.GetComponent<Grid>();
            grid.cellSize = Vector3.one;
            worldObject.transform.position = new Vector3(-128f, -128f, 0f);
            floorTilemap = CreateTilemap("Floor", worldObject.transform, false);
            wallTilemap = CreateTilemap("Wall", worldObject.transform, true);

            tilemapView = worldObject.AddComponent<WorldGridTilemapView>();
            bootstrap = worldObject.AddComponent<WorldGridBootstrap>();
            streamer = worldObject.AddComponent<WorldChunkStreamer>();
            CreateTiles();
            SetField(tilemapView, "worldGrid", grid);
            SetField(tilemapView, "floorTilemap", floorTilemap);
            SetField(tilemapView, "wallTilemap", wallTilemap);
            SetField(tilemapView, "groundFloorTile", groundTile);
            SetField(tilemapView, "soilFloorTile", groundTile);
            SetField(tilemapView, "stoneFloorTile", groundTile);
            SetField(tilemapView, "soilWallTile", soilWallTile);
            SetField(tilemapView, "stoneWallTile", soilWallTile);

            playerObject = new GameObject("Streaming Test Player", typeof(Rigidbody2D), typeof(BoxCollider2D));
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            playerCollider = playerObject.GetComponent<Collider2D>();

            cameraObject = new GameObject("Streaming Test Camera", typeof(Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;
            camera.aspect = 16f / 9f;

            ConfigureBootstrap();
            ConfigureStreamer(camera);
            worldObject.SetActive(true);
            Physics2D.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (worldObject != null) Object.Destroy(worldObject);
            if (playerObject != null) Object.Destroy(playerObject);
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (groundTile != null) Object.Destroy(groundTile);
            if (soilWallTile != null) Object.Destroy(soilWallTile);
            if (tileSprite != null) Object.Destroy(tileSprite);
            if (tileTexture != null) Object.Destroy(tileTexture);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Movement_RemainsLockedUntilInitialCoreAndVisibleChunksAreReady()
        {
            Assert.IsTrue(streamer.enabled);
            Assert.IsNotNull(bootstrap.ChunkStore);
            Assert.AreEqual(0, bootstrap.ChunkStore.LoadedChunkCount);
            Assert.AreEqual(WorldChunkDataState.Queued,
                bootstrap.ChunkStore.GetDataState(new WorldChunkCoordinate(0, 0)));
            Assert.IsFalse(streamer.IsInitialRegionReady);
            Assert.IsFalse(streamer.CanMovePlayer(
                new Vector2(0.5f, 0.5f),
                Vector2.right,
                0.02f));

            yield return WaitForInitialRegion();

            Assert.IsTrue(streamer.CanMovePlayer(
                new Vector2(0.5f, 0.5f),
                Vector2.right,
                0.02f));
        }

        [UnityTest]
        public IEnumerator Start_GeneratesPlayerCenteredFiveByFiveAndDisplaysPriorityThreeByThree()
        {
            yield return WaitForInitialRegion();

            WorldChunkStore store = bootstrap.ChunkStore;
            yield return WaitUntil(() => store.LoadedChunkCount == 25, 120);
            Assert.AreEqual(25, store.LoadedChunkCount);
            Assert.AreEqual(25, store.GetKnownChunkCoordinates().Length);
            Assert.AreEqual(WorldChunkDataState.Ready, store.GetDataState(new WorldChunkCoordinate(-2, -2)));
            Assert.AreEqual(WorldChunkPresentationState.NotDisplayed,
                GetPresentationState(store, new WorldChunkCoordinate(-2, -2)));

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    var chunk = new WorldChunkCoordinate(x, y);
                    Assert.AreEqual(WorldChunkDataState.Ready, store.GetDataState(chunk));
                    Assert.AreEqual(WorldChunkPresentationState.Displayed, GetPresentationState(store, chunk));
                }
            }
        }

        [UnityTest]
        public IEnumerator WorldEdge_ClipsTheRetainedRangeWithoutCreatingOutOfBoundsChunks()
        {
            yield return WaitForInitialRegion();
            MovePlayerTo(new Vector2(126.5f, 0.5f));

            WorldChunkCoordinate edgeChunk = new WorldChunkCoordinate(3, 0);
            yield return WaitUntil(() => IsDisplayed(edgeChunk), 120);

            Assert.AreEqual(WorldChunkDataState.OutOfWorldBounds,
                bootstrap.ChunkStore.GetDataState(new WorldChunkCoordinate(4, 0)));
            Assert.LessOrEqual(bootstrap.ChunkStore.LoadedChunkCount, 25);
        }

        [UnityTest]
        public IEnumerator CameraAspectAndViewSize_DisplayVisibleChunksOutsidePriorityThreeByThree()
        {
            yield return WaitForInitialRegion();
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographicSize = 20f;
            camera.aspect = 3f;

            WorldChunkCoordinate visibleOuterChunk = new WorldChunkCoordinate(-2, 0);
            yield return WaitUntil(() => IsDisplayed(visibleOuterChunk), 120);

            Assert.AreEqual(WorldChunkDataState.Ready, bootstrap.ChunkStore.GetDataState(visibleOuterChunk));
            Assert.AreEqual(WorldChunkPresentationState.Displayed,
                GetPresentationState(bootstrap.ChunkStore, visibleOuterChunk));
            Assert.IsNotNull(wallTilemap.GetComponent<TilemapCollider2D>());
        }

        [UnityTest]
        public IEnumerator LeavingFiveByFive_UnloadsOldChunkAndRestoresMinedDurabilityOnReturn()
        {
            yield return WaitForInitialRegion();
            WorldGridWorld world = bootstrap.ChunkWorld;
            WorldCellCoordinate wallCell = FindWallCell(world);
            Assert.IsTrue(world.TryGetWall(wallCell, out _, out WorldCellAccessStatus access));
            Assert.AreEqual(WorldCellAccessStatus.Ready, access);
            Assert.IsTrue(world.TryDamageWall(wallCell, 3, out int remaining, out bool destroyed, out _));
            Assert.IsFalse(destroyed);
            Assert.AreEqual(7, remaining);

            MovePlayerTo(new Vector2(96.5f, 0.5f));
            yield return WaitUntil(
                () => bootstrap.ChunkStore.GetDataState(new WorldChunkCoordinate(0, 0)) == WorldChunkDataState.Unloaded,
                160);

            MovePlayerTo(new Vector2(0.5f, 0.5f));
            yield return WaitUntil(
                () => bootstrap.ChunkStore.GetDataState(new WorldChunkCoordinate(0, 0)) == WorldChunkDataState.Ready &&
                      bootstrap.ChunkStore.TryGetPresentationState(
                          new WorldChunkCoordinate(0, 0), out WorldChunkPresentationState state) &&
                      state == WorldChunkPresentationState.Displayed,
                160);

            Assert.IsTrue(world.TryGetWall(wallCell, out WorldWallData restoredWall, out access));
            Assert.AreEqual(7, restoredWall.CurrentDurability);
            Assert.AreEqual(WorldCellAccessStatus.Ready, access);
            Assert.IsTrue(bootstrap.ChunkStore.GetRecordedWallMutationCount(new WorldChunkCoordinate(0, 0)) > 0);
        }

        [UnityTest]
        public IEnumerator UnreadyDashPath_IsQueuedBeforeStartAndPinnedUntilReleased()
        {
            yield return WaitForInitialRegion();
            WorldChunkCoordinate dashChunk = new WorldChunkCoordinate(3, 0);
            Assert.AreEqual(WorldChunkDataState.Unloaded, bootstrap.ChunkStore.GetDataState(dashChunk));

            bool canStart = streamer.TryPrepareDashPath(
                new Vector2(0.5f, 0.5f),
                Vector2.right,
                120f,
                1f,
                out bool pathInsideWorld);
            Assert.IsFalse(canStart);
            Assert.IsTrue(pathInsideWorld);
            Assert.AreEqual(WorldChunkDataState.Queued, bootstrap.ChunkStore.GetDataState(dashChunk));

            yield return WaitUntil(
                () => bootstrap.ChunkStore.GetDataState(dashChunk) == WorldChunkDataState.Ready &&
                      IsDisplayed(dashChunk),
                120);

            Assert.IsTrue(streamer.TryPrepareDashPath(
                new Vector2(0.5f, 0.5f),
                Vector2.right,
                120f,
                1f,
                out pathInsideWorld));
            Assert.IsTrue(pathInsideWorld);
            Assert.IsFalse(bootstrap.ChunkStore.TryUnloadChunk(dashChunk));

            MovePlayerTo(new Vector2(0.5f, 96.5f));
            yield return null;
            Assert.AreEqual(WorldChunkDataState.Ready, bootstrap.ChunkStore.GetDataState(dashChunk));
            Assert.AreEqual(WorldChunkPresentationState.Displayed,
                GetPresentationState(bootstrap.ChunkStore, dashChunk));

            streamer.ReleaseDashPath();
            yield return WaitUntil(
                () => bootstrap.ChunkStore.GetDataState(dashChunk) == WorldChunkDataState.Unloaded,
                80);
        }

        [UnityTest]
        public IEnumerator MovementGate_RejectsSweepIntoUnpreparedChunks()
        {
            yield return WaitForInitialRegion();

            Assert.IsTrue(streamer.CanMovePlayer(new Vector2(0.5f, 0.5f), Vector2.right, 0.02f));
            Assert.IsFalse(streamer.CanMovePlayer(new Vector2(0.5f, 0.5f), Vector2.right * 120f, 1f));
            Assert.AreEqual(WorldChunkDataState.Queued,
                bootstrap.ChunkStore.GetDataState(new WorldChunkCoordinate(3, 0)));

            yield return WaitUntil(
                () => IsDisplayed(new WorldChunkCoordinate(3, 0)),
                120);

            Assert.IsTrue(streamer.CanMovePlayer(
                new Vector2(0.5f, 0.5f),
                Vector2.right * 120f,
                1f));
            yield return null;
        }

        private static WorldCellCoordinate FindWallCell(WorldGridWorld world)
        {
            for (int y = 8; y < 32; y++)
            {
                for (int x = 8; x < 32; x++)
                {
                    var cell = new WorldCellCoordinate(x, y);
                    if (world.TryGetWall(cell, out _, out WorldCellAccessStatus status) &&
                        status == WorldCellAccessStatus.Ready)
                    {
                        return cell;
                    }
                }
            }

            Assert.Fail("중앙 청크에서 변경 데이터를 검증할 벽을 찾지 못했습니다.");
            return default;
        }

        private void ConfigureBootstrap()
        {
            SetField(bootstrap, "width", 256);
            SetField(bootstrap, "height", 256);
            SetField(bootstrap, "cellWidth", 1f);
            SetField(bootstrap, "cellHeight", 1f);
            SetField(bootstrap, "origin", new Vector2(-128f, -128f));
            SetField(bootstrap, "worldGrid", grid);
            SetField(bootstrap, "tilemapView", tilemapView);
            SetField(bootstrap, "playerStartTransform", playerObject.transform);
            SetField(bootstrap, "terrainSeed", 1847);
            SetField(bootstrap, "wallNoiseFrequency", 0.12f);
            SetField(bootstrap, "wallDensityThreshold", 0f);
            SetField(bootstrap, "stoneWallRatio", 0.5f);
            SetField(bootstrap, "wallMaxDurability", 10);
            SetField(bootstrap, "caveNoiseFrequency", 0.1f);
            SetField(bootstrap, "caveCarveThreshold", 1f);
            SetField(bootstrap, "minimumMajorCaveSize", 16);
            SetField(bootstrap, "corridorWidth", 2);
            SetField(bootstrap, "safeRadius", 5f);
            SetField(bootstrap, "additionalSafeWorldPositions", new Vector2[0]);
            SetField(bootstrap, "useChunkGeneration", true);
            SetField(bootstrap, "chunkSize", 32);
            SetField(bootstrap, "useChunkStreaming", true);
        }

        private void ConfigureStreamer(Camera camera)
        {
            SetField(streamer, "worldBootstrap", bootstrap);
            SetField(streamer, "tilemapView", tilemapView);
            SetField(streamer, "player", playerObject.transform);
            SetField(streamer, "playerCollider", playerCollider);
            SetField(streamer, "targetCamera", camera);
            SetField(streamer, "retainedRadius", 2);
            SetField(streamer, "priorityRadius", 1);
            // 초기 잠금 검증이 테스트 시작 전에 완료되지 않도록 생성 대역폭을 멈춰 둡니다.
            // 초기 영역 준비를 기다리는 시점에 WaitForInitialRegion이 정상 처리량을 복구합니다.
            SetField(streamer, "maxChunksGeneratedPerFrame", 0);
            SetField(streamer, "maxChunksDisplayedPerFrame", 8);
            SetField(streamer, "maxChunkDisplaysRemovedPerFrame", 8);
            SetField(streamer, "maxChunksUnloadedPerFrame", 8);
        }

        private IEnumerator WaitForInitialRegion()
        {
            SetField(streamer, "maxChunksGeneratedPerFrame", 4);
            yield return WaitUntil(() => streamer.IsInitialRegionReady, 180);
            Assert.IsTrue(streamer.IsInitialRegionReady);
        }

        private static IEnumerator WaitUntil(System.Func<bool> predicate, int maximumFrames)
        {
            int waitedFrames = 0;
            while (!predicate() && waitedFrames < maximumFrames)
            {
                waitedFrames++;
                yield return null;
            }

            Assert.IsTrue(predicate(), $"조건이 {maximumFrames} 프레임 안에 충족되지 않았습니다.");
        }

        private void MovePlayerTo(Vector2 position)
        {
            playerObject.transform.position = position;
            playerObject.GetComponent<Rigidbody2D>().position = position;
            cameraObject.transform.position = new Vector3(position.x - 0.5f, position.y - 0.5f, -10f);
            Physics2D.SyncTransforms();
        }

        private bool IsDisplayed(WorldChunkCoordinate chunk)
        {
            return bootstrap.ChunkStore.GetDataState(chunk) == WorldChunkDataState.Ready &&
                   bootstrap.ChunkStore.TryGetPresentationState(chunk, out WorldChunkPresentationState state) &&
                   state == WorldChunkPresentationState.Displayed;
        }

        private static WorldChunkPresentationState GetPresentationState(
            WorldChunkStore store,
            WorldChunkCoordinate chunk)
        {
            Assert.IsTrue(store.TryGetPresentationState(chunk, out WorldChunkPresentationState state));
            return state;
        }

        private void CreateTiles()
        {
            tileTexture = new Texture2D(1, 1);
            tileTexture.SetPixel(0, 0, Color.white);
            tileTexture.Apply();
            tileSprite = Sprite.Create(tileTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);

            groundTile = CreateTile(Tile.ColliderType.None);
            soilWallTile = CreateTile(Tile.ColliderType.Grid);
        }

        private Tile CreateTile(Tile.ColliderType colliderType)
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = tileSprite;
            tile.colliderType = colliderType;
            return tile;
        }

        private static Tilemap CreateTilemap(string objectName, Transform parent, bool withCollider)
        {
            var tilemapObject = new GameObject(objectName, typeof(Tilemap), typeof(TilemapRenderer));
            tilemapObject.transform.SetParent(parent, false);
            if (withCollider)
            {
                tilemapObject.AddComponent<TilemapCollider2D>();
            }

            return tilemapObject.GetComponent<Tilemap>();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(target, value);
        }
    }
}
