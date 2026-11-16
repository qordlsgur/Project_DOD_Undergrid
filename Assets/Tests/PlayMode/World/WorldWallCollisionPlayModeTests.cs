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

            Tilemap floorTilemap = CreateTilemap("Floor", gridObject.transform, false);
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
