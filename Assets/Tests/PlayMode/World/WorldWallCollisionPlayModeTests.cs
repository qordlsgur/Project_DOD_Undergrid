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
    /// 벽 TilemapCollider2D가 이동을 막고 벽 제거 후 통로가 열리는지 검증합니다.
    /// </summary>
    public sealed class WorldWallCollisionPlayModeTests
    {
        private GameObject gridObject;
        private GameObject playerObject;
        private Texture2D tileTexture;
        private Sprite tileSprite;
        private Tile groundTile;
        private Tile wallTile;
        private Tilemap floorTilemap;
        private Tilemap wallTilemap;
        private WorldGrid gridData;
        private float previousTimeScale;
        private float previousFixedDeltaTime;
        private SimulationMode2D previousSimulationMode;
        private bool physicsSettingsCaptured;

        [UnityTest]
        public IEnumerator WallTile_BlocksPlayerAndRemovingWallOpensPath()
        {
            CaptureAndSetPhysicsSettings();
            CreateWorldTilemaps();
            CreatePlayer();
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            float initialPositionX = body.position.x;
            body.linearVelocity = Vector2.right * 5f;

            bool movedBeforeCollision = false;
            int consecutiveStoppedSteps = 0;
            float collisionTimeout = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < collisionTimeout && consecutiveStoppedSteps < 3)
            {
                yield return new WaitForFixedUpdate();

                if (body.position.x > initialPositionX + 0.05f)
                {
                    movedBeforeCollision = true;
                }

                if (body.position.x > initialPositionX + 0.05f &&
                    Mathf.Abs(body.linearVelocity.x) < 0.02f)
                {
                    consecutiveStoppedSteps++;
                }
                else
                {
                    consecutiveStoppedSteps = 0;
                }
            }

            Assert.IsTrue(movedBeforeCollision, "충돌 전에 Rigidbody2D가 실제로 이동해야 합니다.");
            Assert.GreaterOrEqual(consecutiveStoppedSteps, 3, "벽 접촉 후 이동 속도가 안정적으로 멈춰야 합니다.");
            Assert.Greater(body.position.x, 0.65f, "Player가 시작점에서 벽 앞까지 이동해야 합니다.");
            Assert.Less(body.position.x, 0.9f, "벽 충돌로 Player가 벽 셀을 통과하지 않아야 합니다.");

            var wallCoordinate = new WorldGridCoordinate(1, 0);
            Assert.IsTrue(gridData.TryDamageWall(wallCoordinate, 10, out _, out bool wasDestroyed));
            Assert.IsTrue(wasDestroyed);
            Assert.IsNull(wallTilemap.GetTile(new Vector3Int(1, 0, 0)), "파괴 후 Wall Tile이 즉시 제거되어야 합니다.");
            Assert.IsTrue(gridData.IsWalkable(wallCoordinate));

            Physics2D.SyncTransforms();
            Vector2 formerWallCenter = new Vector2(1.5f, 0.5f);
            float colliderTimeout = Time.realtimeSinceStartup + 3f;
            while (Physics2D.OverlapPoint(formerWallCenter) != null &&
                   Time.realtimeSinceStartup < colliderTimeout)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsNull(
                Physics2D.OverlapPoint(formerWallCenter),
                "물리 갱신 후 이전 벽 셀 위치에 Collider가 남아 있으면 안 됩니다.");

            body.linearVelocity = Vector2.right * 2f;
            float passageTimeout = Time.realtimeSinceStartup + 3f;
            while (body.position.x <= 1.2f && Time.realtimeSinceStartup < passageTimeout)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(body.position.x, 1.2f, "벽 제거 후 Player가 이전 벽 위치를 통과해야 합니다.");
        }

        [UnityTest]
        public IEnumerator GeneratedWall_IsRenderedAndItsColliderIsRemovedAfterMiningDataChange()
        {
            CaptureAndSetPhysicsSettings();
            CreateWorldTilemaps();
            gridData = new WorldGrid(64, 64, 1f, 1f);
            for (int y = 0; y < gridData.Height; y++)
            {
                for (int x = 0; x < gridData.Width; x++)
                {
                    Assert.IsTrue(gridData.TryPlaceFloor(new WorldGridCoordinate(x, y), FloorType.Ground));
                }
            }

            int generatedCount = GenerateChunkedTerrain(gridData);
            Assert.Greater(generatedCount, 0);
            Assert.IsTrue(gridObject.GetComponent<WorldGridTilemapView>().Initialize(gridData));

            WorldGridCoordinate target = default;
            WorldWallData targetWall = default;
            bool foundTarget = false;
            for (int y = 0; y < gridData.Height && !foundTarget; y++)
            {
                for (int x = 0; x < gridData.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    if (gridData.TryGetWall(coordinate, out targetWall))
                    {
                        target = coordinate;
                        foundTarget = true;
                        break;
                    }
                }
            }

            Assert.IsTrue(foundTarget);
            Vector3Int tileCoordinate = new Vector3Int(target.X, target.Y, 0);
            TilemapCollider2D wallCollider = wallTilemap.GetComponent<TilemapCollider2D>();
            Assert.IsNotNull(wallCollider, "Wall Tilemap에는 Collider 컴포넌트가 있어야 합니다.");
            Assert.AreSame(wallTile, wallTilemap.GetTile(tileCoordinate));
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Vector2 formerWallCenter = new Vector2(target.X + 0.5f, target.Y + 0.5f);
            Assert.IsTrue(
                wallCollider.OverlapPoint(formerWallCenter),
                "생성된 벽 타일에는 Wall Tilemap Collider가 있어야 합니다.");

            int wallChangeCount = 0;
            WorldGridChange observedChange = default;
            gridData.CellChanged += change =>
            {
                if (change.Layer == WorldGridLayer.Wall)
                {
                    wallChangeCount++;
                    observedChange = change;
                }
            };

            Assert.IsTrue(gridData.TryDamageWall(
                target,
                targetWall.CurrentDurability,
                out _,
                out bool wasDestroyed));
            Assert.IsTrue(wasDestroyed);
            Assert.AreEqual(1, wallChangeCount, "벽 파괴 시 CellChanged 이벤트가 한 번 발생해야 합니다.");
            Assert.AreEqual(target, observedChange.Coordinate);
            Assert.AreEqual(WorldGridLayer.Wall, observedChange.Layer);
            Assert.AreEqual(WorldGridChangeKind.Removed, observedChange.Kind);
            Assert.IsNull(wallTilemap.GetTile(tileCoordinate));
            Assert.AreSame(groundTile, floorTilemap.GetTile(tileCoordinate));

            // TilemapCollider2D는 변경을 LateUpdate에서 묶어 처리하므로 물리 쿼리 전에 보류된 형상을 반영합니다.
            if (wallCollider.hasTilemapChanges)
            {
                wallCollider.ProcessTilemapChanges();
            }

            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            Assert.IsFalse(
                wallCollider.OverlapPoint(formerWallCenter),
                "TilemapCollider2D 컴포넌트는 유지되지만 파괴된 벽 셀의 충돌 형상은 제거되어야 합니다.");
            Assert.IsTrue(gridData.IsWalkable(target));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (physicsSettingsCaptured)
            {
                Physics2D.simulationMode = previousSimulationMode;
                Time.fixedDeltaTime = previousFixedDeltaTime;
                Time.timeScale = previousTimeScale;
            }

            if (playerObject != null)
            {
                Object.Destroy(playerObject);
            }

            if (gridObject != null)
            {
                Object.Destroy(gridObject);
            }

            if (groundTile != null)
            {
                Object.Destroy(groundTile);
            }

            if (wallTile != null)
            {
                Object.Destroy(wallTile);
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

        private void CaptureAndSetPhysicsSettings()
        {
            previousTimeScale = Time.timeScale;
            previousFixedDeltaTime = Time.fixedDeltaTime;
            previousSimulationMode = Physics2D.simulationMode;
            physicsSettingsCaptured = true;

            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        }

        private void CreateWorldTilemaps()
        {
            gridObject = new GameObject("Test Grid", typeof(Grid));
            Grid unityGrid = gridObject.GetComponent<Grid>();
            unityGrid.cellSize = Vector3.one;

            floorTilemap = CreateTilemap("Floor", gridObject.transform, false);
            wallTilemap = CreateTilemap("Wall", gridObject.transform, true);
            WorldGridTilemapView view = gridObject.AddComponent<WorldGridTilemapView>();
            CreateTiles();

            SetField(view, "worldGrid", unityGrid);
            SetField(view, "floorTilemap", floorTilemap);
            SetField(view, "wallTilemap", wallTilemap);
            SetField(view, "groundFloorTile", groundTile);
            SetField(view, "soilFloorTile", groundTile);
            SetField(view, "stoneFloorTile", groundTile);
            SetField(view, "soilWallTile", wallTile);
            SetField(view, "stoneWallTile", wallTile);

            gridData = new WorldGrid(3, 1, 1f, 1f);
            Assert.IsTrue(gridData.TryPlaceFloor(new WorldGridCoordinate(0, 0), FloorType.Ground));
            Assert.IsTrue(gridData.TryPlaceFloor(new WorldGridCoordinate(1, 0), FloorType.Ground));
            Assert.IsTrue(gridData.TryPlaceFloor(new WorldGridCoordinate(2, 0), FloorType.Ground));
            Assert.IsTrue(gridData.TryPlaceWall(new WorldGridCoordinate(1, 0), WallType.Stone, 10));
            Assert.IsTrue(view.Initialize(gridData));
        }

        private static int GenerateChunkedTerrain(WorldGrid grid)
        {
            var gridWorldOrigin = new WorldCellCoordinate(0, 0);
            var worldBounds = new WorldCellBounds(0, 0, 64, 64);
            var playerStartWorldCell = new WorldCellCoordinate(32, 32);
            var playerStartGridCell = new WorldGridCoordinate(32, 32);
            WorldChunkCoordinate[] chunks =
            {
                new WorldChunkCoordinate(0, 0),
                new WorldChunkCoordinate(1, 0),
                new WorldChunkCoordinate(0, 1),
                new WorldChunkCoordinate(1, 1)
            };
            int generatedCount = 0;

            for (int i = 0; i < chunks.Length; i++)
            {
                generatedCount += WorldTerrainGenerator.GenerateChunkWalls(
                    grid,
                    gridWorldOrigin,
                    chunks[i],
                    32,
                    1847,
                    0.12f,
                    0.66f,
                    0.35f,
                    10,
                    playerStartWorldCell,
                    5f);
            }

            for (int i = 0; i < chunks.Length; i++)
            {
                WorldTerrainGenerator.CarveChunkCaves(
                    grid,
                    gridWorldOrigin,
                    chunks[i],
                    32,
                    1847,
                    0.1f,
                    0.58f);
                Assert.IsTrue(WorldTerrainGenerator.ConnectChunkEntrances(
                    grid,
                    gridWorldOrigin,
                    worldBounds,
                    chunks[i],
                    32,
                    1847,
                    16,
                    2,
                    out _));
            }

            Assert.IsTrue(grid.IsWalkable(playerStartGridCell));
            return generatedCount;
        }

        private void CreatePlayer()
        {
            playerObject = new GameObject("Test Player", typeof(Rigidbody2D), typeof(BoxCollider2D));
            playerObject.transform.position = new Vector3(0.5f, 0.5f, 0f);
            Rigidbody2D body = playerObject.GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerObject.GetComponent<BoxCollider2D>().size = new Vector2(0.4f, 0.4f);
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

        private static void SetField(WorldGridTilemapView view, string fieldName, object value)
        {
            FieldInfo field = typeof(WorldGridTilemapView).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{fieldName} 필드를 찾을 수 없습니다.");
            field.SetValue(view, value);
        }
    }
}
