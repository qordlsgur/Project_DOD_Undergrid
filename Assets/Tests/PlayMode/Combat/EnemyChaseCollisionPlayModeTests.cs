using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class EnemyChaseCollisionPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;
        private GameObject enemyObject;
        private EnemyData enemyData;

        [UnityTest]
        public IEnumerator Chase_StopsOutsideColliderContactAndAppliesConfiguredAttack()
        {
            bootstrapObject = new GameObject("EnemyCollisionTestBootstrap");
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();
            yield return null;

            playerObject = new GameObject("EnemyCollisionTestPlayer");
            playerObject.transform.localScale = Vector3.one * 5f;
            Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 0f;
            playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            CapsuleCollider2D playerCollider = playerObject.AddComponent<CapsuleCollider2D>();
            playerCollider.size = new Vector2(0.19f, 0.16f);
            CombatReceiver playerReceiver = playerObject.AddComponent<CombatReceiver>();
            PlayerCombatInitializer playerInitializer = playerObject.AddComponent<PlayerCombatInitializer>();
            SetField(playerInitializer, "combatBootstrap", bootstrap);
            Assert.That(playerInitializer.Initialize(), Is.True);

            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(enemyData, "maxHealth", 30);
            SetField(enemyData, "moveSpeed", 2f);
            SetField(enemyData, "attackPower", 10);
            // 외접 반경 상한(.621 + .300)과 양 Collider의 contact offset(.020)을 넘어서는 거리입니다.
            SetField(enemyData, "attackRange", 1.5f);
            SetField(enemyData, "attackInterval", 0.5f);

            enemyObject = new GameObject("EnemyCollisionTestEnemy");
            enemyObject.transform.position = new Vector3(3f, 0f, 0f);
            enemyObject.transform.localScale = Vector3.one * 0.6f;
            Rigidbody2D enemyBody = enemyObject.AddComponent<Rigidbody2D>();
            enemyBody.gravityScale = 0f;
            enemyBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D enemyCollider = enemyObject.AddComponent<CircleCollider2D>();
            enemyCollider.radius = 0.5f;
            enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer enemyInitializer = enemyObject.AddComponent<EnemyCombatInitializer>();
            SetField(enemyInitializer, "enemyData", enemyData);
            Assert.That(enemyInitializer.Initialize(bootstrap), Is.True);

            EnemyChaseAI chaseAI = enemyObject.AddComponent<EnemyChaseAI>();
            chaseAI.SetPlayerTarget(playerObject.transform);
            EnemyAttackAI attackAI = enemyObject.AddComponent<EnemyAttackAI>();
            attackAI.SetPlayerTarget(playerReceiver);

            bool touchedDuringChase = false;
            for (int step = 0; step < 100 && enemyBody.position.x > enemyData.AttackRange; step++)
            {
                yield return new WaitForFixedUpdate();
                touchedDuringChase |= playerCollider.IsTouching(enemyCollider);
            }

            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(enemyBody.position.x, Is.GreaterThan(0.94f));
            Assert.That(enemyBody.linearVelocity.sqrMagnitude, Is.LessThan(0.0001f));
            Assert.That(touchedDuringChase, Is.False);
            Assert.That(playerCollider.IsTouching(enemyCollider), Is.False);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(90));
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

            if (bootstrapObject != null)
            {
                Object.Destroy(bootstrapObject);
            }

            if (enemyData != null)
            {
                Object.Destroy(enemyData);
            }

            yield return null;
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
