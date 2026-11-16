using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.DOD.Entity;
using Prohect_DOD_Undergird.Combat;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class EnemyCombatInitializerPlayModeTests
    {
        private GameObject bootstrapObject;
        private GameObject enemyObject;
        private EnemyData enemyData;

        [UnityTest]
        public IEnumerator Initialize_RegistersEntityAndDeathRemovesObjectAndReleasesEntity()
        {
            bootstrapObject = new GameObject("EnemyCombatTestBootstrap");
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();
            yield return null;

            enemyObject = new GameObject("EnemyCombatTestTarget");
            CombatReceiver receiver = enemyObject.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = enemyObject.AddComponent<EnemyCombatInitializer>();
            enemyData = ScriptableObject.CreateInstance<EnemyData>();
            SetField(enemyData, "enemyName", "Test Enemy");
            SetField(enemyData, "maxHealth", 30);
            SetField(enemyData, "moveSpeed", 2.5f);
            SetField(enemyData, "attackPower", 7);
            SetField(enemyData, "attackRange", 1.25f);
            SetField(enemyData, "attackInterval", 0.75f);
            SetField(initializer, "enemyData", enemyData);

            Assert.That(initializer.Initialize(bootstrap), Is.True);
            Assert.That(initializer.Data, Is.SameAs(enemyData));
            Assert.That(
                initializer.EntityId,
                Is.Not.EqualTo(Prohect_DOD_Undergird.DOD.Entity.EntityId.Invalid));
            Assert.That(bootstrap.EntityManager.IsAlive(initializer.EntityId), Is.True);
            Assert.That(receiver.EntityId, Is.EqualTo(initializer.EntityId));
            Assert.That(enemyData.EnemyName, Is.EqualTo("Test Enemy"));
            Assert.That(enemyData.MaxHealth, Is.EqualTo(30));
            Assert.That(enemyData.MoveSpeed, Is.EqualTo(2.5f));
            Assert.That(enemyData.AttackPower, Is.EqualTo(7));
            Assert.That(enemyData.AttackRange, Is.EqualTo(1.25f));
            Assert.That(enemyData.AttackInterval, Is.EqualTo(0.75f));
            Assert.That(receiver.CurrentHealth, Is.EqualTo(30));
            Assert.That(receiver.MaxHealth, Is.EqualTo(30));
            Assert.That(receiver.ReceiveDamage(7), Is.True);
            Assert.That(receiver.CurrentHealth, Is.EqualTo(23));
            Assert.That(receiver.MaxHealth, Is.EqualTo(30));
            Assert.That(receiver.ReceiveDamage(30), Is.True);
            Assert.That(receiver.CurrentHealth, Is.EqualTo(0));
            Assert.That(receiver.MaxHealth, Is.EqualTo(30));

            Prohect_DOD_Undergird.DOD.Entity.EntityId deadEntityId = initializer.EntityId;
            Assert.That(bootstrap.EntityManager.GetState(deadEntityId), Is.EqualTo(EntityState.Dead));

            yield return null;

            Assert.That(enemyObject == null, Is.True);
            Assert.That(bootstrap.EntityManager.GetState(deadEntityId), Is.EqualTo(EntityState.Released));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (enemyObject != null)
            {
                Object.Destroy(enemyObject);
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
