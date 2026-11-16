using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class EnemyChaseAIPlayModeTests
    {
        private const float InvestigationArrivalTolerance = 0.1f;

        private GameObject enemyObject;
        private GameObject playerObject;
        private EnemyData enemyData;
        private Rigidbody2D enemyBody;
        private EnemyChaseAI chaseAI;

        [UnityTest]
        public IEnumerator FixedUpdate_ChasesInsideDetectionStopsAtDistanceAndUsesDataSpeed()
        {
            CreateScenario(new Vector2(8f, 0f), Vector2.zero, 2f, 1f, 5f, 6f, 0.06f);

            yield return new WaitForFixedUpdate();
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            AssertState("Idle");

            enemyBody.position = new Vector2(4f, 0f);
            yield return new WaitForFixedUpdate();
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
            Assert.That(enemyBody.linearVelocity.magnitude, Is.EqualTo(enemyData.MoveSpeed).Within(0.01f));
            AssertState("Chase");

            playerObject.transform.position = new Vector2(9.5f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Chase");
            Assert.That(enemyBody.linearVelocity.x, Is.GreaterThan(0f));

            playerObject.transform.position = Vector2.zero;
            enemyBody.position = new Vector2(1f, 0f);
            enemyBody.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            enemyBody.position = new Vector2(7f, 0f);
            enemyBody.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_ReachesLastKnownPositionWaitsThenReturnsToIdle()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.06f);

            yield return new WaitForFixedUpdate();
            AssertState("Chase");

            playerObject.transform.position = new Vector2(5f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            playerObject.transform.position = new Vector2(10f, 0f);
            enemyBody.position = new Vector2(1.5f, 0f);
            enemyBody.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            yield return new WaitForSeconds(0.12f);
            AssertState("Idle");
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_ReacquiresPlayerUsingPhysicalMovementScenarioAndResumesChase()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.06f);

            yield return new WaitForFixedUpdate();
            playerObject.transform.position = new Vector2(5f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            playerObject.transform.position = new Vector2(-1f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Chase");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_MovesTowardLastKnownPositionWithoutPositionOverrides()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.5f);
            Vector2 lastKnownPosition = playerObject.transform.position;

            yield return new WaitForFixedUpdate();
            AssertState("Chase");

            playerObject.transform.position = new Vector2(4f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            float distanceBeforeMovement = Vector2.Distance(enemyBody.position, lastKnownPosition);
            yield return new WaitForFixedUpdate();

            Assert.That(Vector2.Distance(enemyBody.position, lastKnownPosition),
                Is.LessThan(distanceBeforeMovement));
            Assert.That(enemyBody.linearVelocity.x, Is.GreaterThan(0f));
            AssertState("Investigate");
        }

        [UnityTest]
        public IEnumerator FixedUpdate_ReacquiresPlayerDuringInvestigationAndResumesChase()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.5f);

            yield return new WaitForFixedUpdate();
            playerObject.transform.position = new Vector2(4f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            // 플레이어만 감지 거리 안으로 이동시켜 AI의 실제 재감지 전이를 자극합니다.
            playerObject.transform.position = enemyBody.position + Vector2.left * 1.5f;
            yield return new WaitForFixedUpdate();

            AssertState("Chase");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_StopsAtLastKnownPositionAfterPhysicalMovement()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.5f);
            Vector2 lastKnownPosition = playerObject.transform.position;

            yield return new WaitForFixedUpdate();
            playerObject.transform.position = new Vector2(4f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            int fixedStepCount = 0;
            while (Vector2.Distance(enemyBody.position, lastKnownPosition) > InvestigationArrivalTolerance &&
                   fixedStepCount < 100)
            {
                yield return new WaitForFixedUpdate();
                fixedStepCount++;
            }

            Assert.That(fixedStepCount, Is.LessThan(100));
            yield return new WaitForFixedUpdate();

            Assert.That(Vector2.Distance(enemyBody.position, lastKnownPosition),
                Is.LessThanOrEqualTo(InvestigationArrivalTolerance));
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_TracksPlayerAfterReacquiringDuringInvestigation()
        {
            CreateScenario(Vector2.zero, new Vector2(1.5f, 0f), 2f, 0.3f, 2f, 3f, 0.5f);

            yield return new WaitForFixedUpdate();
            playerObject.transform.position = new Vector2(4f, 0f);
            yield return new WaitForFixedUpdate();
            AssertState("Investigate");

            playerObject.transform.position = enemyBody.position + Vector2.left * 1.5f;
            yield return new WaitForFixedUpdate();
            AssertState("Chase");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));

            playerObject.transform.position = enemyBody.position + Vector2.right * 1.5f;
            yield return new WaitForFixedUpdate();

            AssertState("Chase");
            Assert.That(enemyBody.linearVelocity.x, Is.GreaterThan(0f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemyObject != null)
            {
                Object.Destroy(enemyObject);
            }

            if (playerObject != null)
            {
                Object.Destroy(playerObject);
            }

            if (enemyData != null)
            {
                Object.Destroy(enemyData);
            }

            yield return null;
        }

        private void CreateScenario(
            Vector2 enemyPosition,
            Vector2 playerPosition,
            float moveSpeed,
            float attackRange,
            float detectionRange,
            float pursuitEndRange,
            float investigationDuration)
        {
            playerObject = new GameObject("EnemyChaseTestPlayer");
            playerObject.transform.position = playerPosition;

            enemyObject = new GameObject("EnemyChaseTestEnemy");
            enemyObject.transform.position = enemyPosition;
            enemyBody = enemyObject.AddComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = enemyObject.AddComponent<EnemyCombatInitializer>();

            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(enemyData, "moveSpeed", moveSpeed);
            SetField(enemyData, "attackRange", attackRange);
            SetField(enemyData, "detectionRange", detectionRange);
            SetField(enemyData, "pursuitEndRange", pursuitEndRange);
            SetField(enemyData, "investigationDuration", investigationDuration);
            SetField(initializer, "enemyData", enemyData);

            chaseAI = enemyObject.AddComponent<EnemyChaseAI>();
            chaseAI.SetPlayerTarget(playerObject.transform);
        }

        private void AssertState(string expectedState)
        {
            FieldInfo stateField = typeof(EnemyChaseAI).GetField(
                "currentState",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(stateField, Is.Not.Null);
            Assert.That(stateField.GetValue(chaseAI).ToString(), Is.EqualTo(expectedState));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
