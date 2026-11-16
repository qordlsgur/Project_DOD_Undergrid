using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class EnemyAttackAIPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject playerObject;
        private GameObject enemyObject;
        private EnemyData enemyData;

        [UnityTest]
        public IEnumerator Update_AttacksInRangeOnIntervalAndStopsOutsideRangeOrAfterDeath()
        {
            bootstrapObject = new GameObject("EnemyAttackTestBootstrap");
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();
            yield return null;

            playerObject = new GameObject("EnemyAttackTestPlayer");
            CombatReceiver playerReceiver = playerObject.AddComponent<CombatReceiver>();
            PlayerCombatInitializer playerInitializer = playerObject.AddComponent<PlayerCombatInitializer>();
            SetField(playerInitializer, "combatBootstrap", bootstrap);
            Assert.That(playerInitializer.Initialize(), Is.True);

            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(enemyData, "maxHealth", 30);
            SetField(enemyData, "attackPower", 7);
            SetField(enemyData, "attackRange", 1.5f);
            SetField(enemyData, "attackInterval", 0.1f);

            enemyObject = new GameObject("EnemyAttackTestEnemy");
            enemyObject.transform.position = new Vector3(2f, 0f, 0f);
            enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer enemyInitializer = enemyObject.AddComponent<EnemyCombatInitializer>();
            SetField(enemyInitializer, "enemyData", enemyData);
            Assert.That(enemyInitializer.Initialize(bootstrap), Is.True);
            EnemyAttackAI attackAI = enemyObject.AddComponent<EnemyAttackAI>();
            attackAI.SetPlayerTarget(playerReceiver);

            yield return null;
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100));

            enemyObject.transform.position = new Vector3(1.5f, 0f, 0f);
            yield return null;
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(93));

            yield return new WaitForSeconds(0.12f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(86));

            enemyObject.transform.position = new Vector3(2f, 0f, 0f);
            yield return new WaitForSeconds(0.12f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(86));

            Assert.That(playerReceiver.ReceiveDamage(100), Is.True);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(0));
            yield return new WaitForSeconds(0.12f);
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(0));
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
