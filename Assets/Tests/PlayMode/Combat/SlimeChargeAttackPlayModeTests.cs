using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class SlimeChargeAttackPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;
        private Rigidbody2D playerBody;
        private CircleCollider2D playerCollider;
        private CombatBootstrap combatBootstrap;
        private CombatReceiver playerReceiver;
        private EnemyData enemyData;
        private GameObject enemyObject;
        private Rigidbody2D enemyBody;
        private SlimeChargeAttackAI chargeAttackAI;
        private GameObject goblinObject;
        private EnemyData goblinData;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            bootstrapObject = new GameObject("SlimeChargeTestBootstrap");
            combatBootstrap = bootstrapObject.AddComponent<CombatBootstrap>();

            playerObject = new GameObject("SlimeChargeTestPlayer");
            playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.bodyType = RigidbodyType2D.Kinematic;
            playerBody.useFullKinematicContacts = true;
            playerCollider = playerObject.AddComponent<CircleCollider2D>();
            playerCollider.radius = 0.15f;
            playerReceiver = playerObject.AddComponent<CombatReceiver>();
            EntityId playerEntityId = combatBootstrap.EntityManager.Create();
            playerReceiver.Initialize(playerEntityId, combatBootstrap.CombatManager, 100);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Update_PlayerEntersAttackRange_StartsPreparation()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));

            yield return null;

            AssertState("Preparing");
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator Update_PreparationCompletes_StartsCharge()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));

            yield return new WaitForSeconds(enemyData.ChargePreparationTime + 0.05f);
            yield return new WaitForFixedUpdate();

            AssertState("Charging");
            Assert.That(enemyBody.linearVelocity.magnitude,
                Is.EqualTo(enemyData.ChargeSpeed).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Update_ChargeStarts_DirectionRemainsFixedWhenPlayerMoves()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));
            yield return new WaitForSeconds(enemyData.ChargePreparationTime + 0.05f);
            yield return new WaitForFixedUpdate();
            AssertState("Charging");

            playerObject.transform.position = new Vector2(0f, 10f);
            yield return new WaitForFixedUpdate();

            Assert.That(enemyBody.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(Mathf.Abs(enemyBody.linearVelocity.y), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_ChaseAI_DoesNotOverrideChargeSpeed()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));
            yield return new WaitForSeconds(enemyData.ChargePreparationTime + 0.05f);
            yield return new WaitForFixedUpdate();

            Assert.That(enemyBody.linearVelocity.magnitude,
                Is.EqualTo(enemyData.ChargeSpeed).Within(0.01f));
            Assert.That(enemyBody.linearVelocity.magnitude, Is.GreaterThan(enemyData.MoveSpeed));
        }

        [UnityTest]
        public IEnumerator OnCollisionEnter2D_ChargeHitsPlayerOnlyOnce()
        {
            CreateSlime(Vector2.zero, new Vector2(0.9f, 0f));
            yield return new WaitForSeconds(enemyData.ChargePreparationTime + 0.05f);
            yield return new WaitForFixedUpdate();
            AssertState("Charging");

            for (int i = 0; i < 12 && playerReceiver.CurrentHealth == 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - enemyData.AttackPower));
            AssertState("Returning");
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));

            yield return new WaitForFixedUpdate();
            AssertState("Returning");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
            yield return WaitForState("ReturnWait");
            Assert.That(Vector2.Distance(enemyBody.position, GetReturnPosition()),
                Is.LessThanOrEqualTo(0.1f));
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            yield return new WaitForSeconds(0.1f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - enemyData.AttackPower));
        }

        [UnityTest]
        public IEnumerator Update_RepeatedCharges_ReturnToSavedPositionWithoutPassingThroughPlayer()
        {
            Vector2 initialPosition = new Vector2(2f, -1f);
            CreateSlime(initialPosition, initialPosition + new Vector2(0.9f, 0f));
            Vector2 fixedReturnPosition = initialPosition;

            for (int chargeIndex = 1; chargeIndex <= 4; chargeIndex++)
            {
                yield return WaitForState("Charging");
                Assert.That(GetReturnPosition(), Is.EqualTo(fixedReturnPosition));

                int expectedHealth = 100 - enemyData.AttackPower * chargeIndex;
                for (int fixedStep = 0; fixedStep < 30 && playerReceiver.CurrentHealth > expectedHealth; fixedStep++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(expectedHealth));
                AssertState("Returning");
                yield return WaitForState("ReturnWait");

                Assert.That(GetReturnPosition(), Is.EqualTo(fixedReturnPosition));
                Assert.That(Vector2.Distance(enemyBody.position, fixedReturnPosition),
                    Is.LessThanOrEqualTo(0.1f));
                Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            }
        }

        [UnityTest]
        public IEnumerator Update_MissedChargeReturnsToPreparationPositionWithoutDamage()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));
            yield return new WaitForSeconds(enemyData.ChargePreparationTime + 0.05f);
            yield return new WaitForFixedUpdate();
            AssertState("Charging");

            playerObject.transform.position = new Vector2(-10f, 10f);
            yield return WaitForState("ReturnWait");
            Assert.That(Vector2.Distance(enemyBody.position, Vector2.zero), Is.LessThanOrEqualTo(0.1f));
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator Update_DeadPlayer_IsNotAttacked()
        {
            Assert.That(playerReceiver.ReceiveDamage(100), Is.True);
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));

            yield return new WaitForSeconds(enemyData.ChargePreparationTime + enemyData.ChargeDuration + 0.1f);

            Assert.That(playerReceiver.IsDead, Is.True);
            Assert.That(playerReceiver.CurrentHealth, Is.Zero);
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            AssertState("Ready");
        }

        [UnityTest]
        public IEnumerator Update_ChargeEnds_EnemyChaseAIResumesMovement()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));
            yield return WaitForState("Charging");
            AssertState("Charging");

            playerObject.transform.position = new Vector2(5f, 0f);
            yield return new WaitForFixedUpdate();
            yield return WaitForState("Returning");
            yield return new WaitForFixedUpdate();

            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
            Assert.That(enemyBody.linearVelocity.magnitude,
                Is.EqualTo(enemyData.MoveSpeed).Within(0.01f));

            yield return WaitForState("ReturnWait");
            Assert.That(Vector2.Distance(enemyBody.position, Vector2.zero), Is.LessThanOrEqualTo(0.1f));
            yield return new WaitForSeconds(1.05f);
            yield return new WaitForFixedUpdate();

            Assert.That(enemyBody.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(enemyBody.linearVelocity.magnitude,
                Is.EqualTo(enemyData.MoveSpeed).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator FixedUpdate_ReturnMovementWaitsForPlayerToClearPathWithoutPushing()
        {
            CreateSlime(Vector2.zero, new Vector2(1.5f, 0f));
            yield return WaitForState("Charging");

            playerObject.transform.position = new Vector2(0f, 10f);
            yield return WaitForState("Returning");
            playerObject.transform.position = new Vector2(1.2f, 0f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            AssertState("Returning");
            Assert.That(enemyBody.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(playerObject.transform.position, Is.EqualTo(new Vector3(1.2f, 0f, 0f)));
            Assert.That(Physics2D.Distance(enemyObject.GetComponent<Collider2D>(), playerCollider).distance,
                Is.GreaterThan(0f));

            playerObject.transform.position = new Vector2(1.2f, 2f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();

            AssertState("Returning");
            Assert.That(enemyBody.linearVelocity.x, Is.LessThan(0f));
            Assert.That(Mathf.Abs(enemyBody.linearVelocity.y), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Update_ReturnAndOneSecondWaitCompleteBeforeNextCharge()
        {
            CreateSlime(Vector2.zero, new Vector2(0.9f, 0f));
            SetField(enemyData, "attackInterval", 1.5f);
            yield return WaitForState("Charging");

            int damagedHealth = 100 - enemyData.AttackPower;
            for (int i = 0; i < 30 && playerReceiver.CurrentHealth == 100; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(damagedHealth));
            AssertState("Returning");
            yield return new WaitForFixedUpdate();
            AssertState("Returning");

            yield return WaitForState("ReturnWait");
            Assert.That(Vector2.Distance(enemyBody.position, Vector2.zero), Is.LessThanOrEqualTo(0.1f));
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(damagedHealth));

            yield return new WaitForSeconds(0.8f);
            AssertState("ReturnWait");
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(damagedHealth));

            yield return new WaitForSeconds(0.3f);
            Assert.That(GetStateName(), Is.Not.EqualTo("Charging"));
            Assert.That(GetStateName(), Is.Not.EqualTo("Preparing"));
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(damagedHealth));

            yield return WaitForState("Charging");
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(damagedHealth));
        }

        [UnityTest]
        public IEnumerator EnemyAttackAI_GoblinStyleApproachAndRepeatedDamageRemainAvailable()
        {
            CreateEnemyData("Goblin Test", 40, 1.4f, 8, 1.2f, 0.4f);
            enemyObject = new GameObject("GoblinAttackTestEnemy");
            enemyObject.transform.position = new Vector2(3f, 0f);
            enemyBody = enemyObject.AddComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemyObject.AddComponent<CircleCollider2D>().radius = 0.15f;
            enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = enemyObject.AddComponent<EnemyCombatInitializer>();
            SetField(initializer, "enemyData", enemyData);
            EnemyChaseAI chaseAI = enemyObject.AddComponent<EnemyChaseAI>();
            EnemyAttackAI attackAI = enemyObject.AddComponent<EnemyAttackAI>();
            chaseAI.SetPlayerTarget(playerObject.transform);
            attackAI.SetPlayerTarget(playerReceiver);
            Assert.That(initializer.Initialize(combatBootstrap), Is.True);

            yield return new WaitForFixedUpdate();
            Assert.That(enemyBody.linearVelocity.magnitude,
                Is.EqualTo(enemyData.MoveSpeed).Within(0.01f));

            playerObject.transform.position = enemyObject.transform.position;
            yield return null;
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - enemyData.AttackPower));

            yield return new WaitForSeconds(0.1f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - enemyData.AttackPower));
            yield return new WaitForSeconds(0.35f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - enemyData.AttackPower * 2));
        }

        [UnityTest]
        public IEnumerator Update_SlimeAndGoblinAttackTogether_KeepDamageAndCooldownsIndependent()
        {
            CreateSlime(new Vector2(-2f, 0f), Vector2.zero);
            SetField(enemyData, "chargePreparationTime", 0.2f);
            CreateGoblinAttackEnemy(new Vector2(0f, 1f));

            yield return null;

            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - goblinData.AttackPower));
            AssertState("Preparing");
            float firstGoblinAttackTime = Time.time;

            // Slime의 첫 돌진 적중은 Goblin 피해와 별도로 Slime 자신의 공격력을 적용해야 합니다.
            yield return WaitForPlayerHealth(100 - goblinData.AttackPower - enemyData.AttackPower);
            AssertState("Returning");

            float beforeGoblinIntervalEnds = firstGoblinAttackTime + 0.55f - Time.time;
            if (beforeGoblinIntervalEnds > 0f)
            {
                yield return new WaitForSeconds(beforeGoblinIntervalEnds);
            }

            // Goblin의 0.7초 간격이 지나기 전에는 Goblin의 두 번째 피해가 없어야 합니다.
            Assert.That(playerReceiver.CurrentHealth,
                Is.EqualTo(100 - goblinData.AttackPower - enemyData.AttackPower));

            int afterGoblinSecondHit = playerReceiver.CurrentHealth - goblinData.AttackPower;
            yield return WaitForPlayerHealth(afterGoblinSecondHit, 30);
            Assert.That(GetStateName(), Is.EqualTo("Returning").Or.EqualTo("ReturnWait"));

            // Slime 복귀가 길어지는 동안에도 Goblin 공격은 자체 간격에 맞춰 계속 진행됩니다.
            yield return WaitForState("ReturnWait");
            int healthAtReturnWait = playerReceiver.CurrentHealth;
            yield return WaitForPlayerHealth(healthAtReturnWait - goblinData.AttackPower, 50);
            AssertState("ReturnWait");
            int healthAfterGoblinHitDuringWait = playerReceiver.CurrentHealth;
            yield return new WaitForSeconds(0.2f);
            AssertState("ReturnWait");
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(healthAfterGoblinHitDuringWait));

            // Goblin을 멈춘 뒤 Slime의 복귀·1초 대기가 끝나야 Slime 피해가 다시 발생합니다.
            EnemyAttackAI goblinAttackAI = goblinObject.GetComponent<EnemyAttackAI>();
            goblinAttackAI.enabled = false;
            int expectedAfterSlimeSecondHit = playerReceiver.CurrentHealth - enemyData.AttackPower;
            yield return WaitForPlayerHealth(expectedAfterSlimeSecondHit, 220);
            Assert.That(GetStateName(), Is.EqualTo("Returning").Or.EqualTo("ReturnWait"));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (goblinObject != null)
            {
                Object.Destroy(goblinObject);
            }

            if (enemyObject != null)
            {
                Object.Destroy(enemyObject);
            }

            if (playerObject != null)
            {
                Object.Destroy(playerObject);
            }

            if (bootstrapObject != null)
            {
                Object.Destroy(bootstrapObject);
            }

            if (enemyData != null)
            {
                Object.Destroy(enemyData);
            }

            if (goblinData != null)
            {
                Object.Destroy(goblinData);
            }

            yield return null;
        }

        private void CreateSlime(Vector2 enemyPosition, Vector2 targetPosition)
        {
            CreateEnemyData("Slime Test", 20, 1.5f, 7, 2f, 0.5f);
            playerObject.transform.position = targetPosition;

            enemyObject = new GameObject("SlimeChargeTestEnemy");
            enemyObject.SetActive(false);
            enemyObject.transform.position = enemyPosition;
            enemyBody = enemyObject.AddComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyObject.AddComponent<CircleCollider2D>().radius = 0.15f;
            enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = enemyObject.AddComponent<EnemyCombatInitializer>();
            SetField(initializer, "enemyData", enemyData);
            EnemyChaseAI chaseAI = enemyObject.AddComponent<EnemyChaseAI>();
            chargeAttackAI = enemyObject.AddComponent<SlimeChargeAttackAI>();

            chaseAI.SetPlayerTarget(playerObject.transform);
            chargeAttackAI.SetPlayerTarget(playerReceiver);
            Assert.That(initializer.Initialize(combatBootstrap), Is.True);
            enemyObject.SetActive(true);
        }

        private void CreateEnemyData(
            string enemyName,
            int maxHealth,
            float moveSpeed,
            int attackPower,
            float attackRange,
            float attackInterval)
        {
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(enemyData, "enemyName", enemyName);
            SetField(enemyData, "maxHealth", maxHealth);
            SetField(enemyData, "moveSpeed", moveSpeed);
            SetField(enemyData, "attackPower", attackPower);
            SetField(enemyData, "attackRange", attackRange);
            SetField(enemyData, "attackInterval", attackInterval);
            SetField(enemyData, "detectionRange", 5f);
            SetField(enemyData, "pursuitEndRange", 6f);
            SetField(enemyData, "chargePreparationTime", 0.4f);
            SetField(enemyData, "chargeSpeed", 7.5f);
            SetField(enemyData, "chargeDuration", 0.35f);
        }

        private void CreateGoblinAttackEnemy(Vector2 position)
        {
            goblinData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(goblinData, "enemyName", "Goblin Concurrent Test");
            SetField(goblinData, "maxHealth", 40);
            SetField(goblinData, "moveSpeed", 1.4f);
            SetField(goblinData, "attackPower", 11);
            SetField(goblinData, "attackRange", 1.5f);
            SetField(goblinData, "attackInterval", 0.7f);

            goblinObject = new GameObject("GoblinConcurrentAttackTestEnemy");
            goblinObject.SetActive(false);
            goblinObject.transform.position = position;
            goblinObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = goblinObject.AddComponent<EnemyCombatInitializer>();
            SetField(initializer, "enemyData", goblinData);
            EnemyAttackAI attackAI = goblinObject.AddComponent<EnemyAttackAI>();
            attackAI.SetPlayerTarget(playerReceiver);
            Assert.That(initializer.Initialize(combatBootstrap), Is.True);
            goblinObject.SetActive(true);
        }

        private IEnumerator WaitForPlayerHealth(int expectedHealth, int maximumFixedSteps = 200)
        {
            for (int step = 0; step < maximumFixedSteps; step++)
            {
                if (playerReceiver.CurrentHealth == expectedHealth)
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(expectedHealth));
        }

        private void AssertState(string expectedState)
        {
            Assert.That(GetStateName(), Is.EqualTo(expectedState));
        }

        private IEnumerator WaitForState(string expectedState, int maximumFixedSteps = 300)
        {
            for (int step = 0; step < maximumFixedSteps; step++)
            {
                if (GetStateName() == expectedState)
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.Fail($"{expectedState} 상태에 도달하지 못했습니다.");
        }

        private string GetStateName()
        {
            FieldInfo stateField = typeof(SlimeChargeAttackAI).GetField(
                "currentState",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(stateField, Is.Not.Null);
            return stateField.GetValue(chargeAttackAI).ToString();
        }

        private Vector2 GetReturnPosition()
        {
            FieldInfo positionField = typeof(SlimeChargeAttackAI).GetField(
                "returnPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(positionField, Is.Not.Null);
            return (Vector2)positionField.GetValue(chargeAttackAI);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드를 찾지 못했습니다.");
            field.SetValue(target, value);
        }
    }
}
