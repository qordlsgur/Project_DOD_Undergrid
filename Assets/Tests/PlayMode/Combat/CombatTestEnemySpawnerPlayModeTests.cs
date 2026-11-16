using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Prohect_DOD_Undergird.DOD.Entity;
using EntityId = Prohect_DOD_Undergird.DOD.Entity.EntityId;

namespace Prohect_DOD_Undergird.Combat.Tests
{
    public class CombatTestEnemySpawnerPlayModeTests
    {
        private readonly List<GameObject> objectsToDestroy = new List<GameObject>();
        private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
        private readonly List<GameObject> templateObjects = new List<GameObject>();
        private readonly List<EnemyData> dataAssets = new List<EnemyData>();
        private GameObject bootstrapObject;
        private GameObject playerObject;
        private GameObject spawnerObject;

        [UnityTest]
        public IEnumerator Start_SpawnsDifferentEnemyTypesWithIndependentCombatState()
        {
            bootstrapObject = new GameObject("MultiEnemyTestBootstrap");
            objectsToDestroy.Add(bootstrapObject);
            CombatBootstrap bootstrap = bootstrapObject.AddComponent<CombatBootstrap>();
            yield return null;

            playerObject = new GameObject("MultiEnemyTestPlayer");
            objectsToDestroy.Add(playerObject);
            CombatReceiver playerReceiver = playerObject.AddComponent<CombatReceiver>();
            EntityId playerEntityId = bootstrap.EntityManager.Create();
            playerReceiver.Initialize(playerEntityId, bootstrap.CombatManager, 100);

            EnemyData slimeData = CreateEnemyData("Slime", 20, 1.5f, 6, 1.1f, 1f, 4.5f, 5.5f, 1.25f);
            EnemyData goblinData = CreateEnemyData("Goblin", 45, 2.6f, 12, 1.4f, 0.7f, 6f, 7.5f, 2f);
            GameObject slimeTemplate = CreateEnemyTemplate("SlimeTemplate", slimeData);
            GameObject goblinTemplate = CreateEnemyTemplate("GoblinTemplate", goblinData);

            spawnerObject = new GameObject("MultiEnemyTestSpawner");
            objectsToDestroy.Add(spawnerObject);
            spawnerObject.transform.position = new Vector3(8f, 0f, 0f);
            CombatTestEnemySpawner spawner = spawnerObject.AddComponent<CombatTestEnemySpawner>();
            SetField(spawner, "combatBootstrap", bootstrap);
            SetField(spawner, "playerTransform", playerObject.transform);
            SetField(spawner, "spawnSpacing", 2f);
            SetField(spawner, "enemySpawnEntries", CreateSpawnEntries(slimeTemplate, goblinTemplate));

            yield return null;

            CollectSpawnedEnemies(slimeData, goblinData);
            Assert.That(spawnedEnemies.Count, Is.EqualTo(2));

            EnemyCombatInitializer slime = FindInitializer(slimeData);
            EnemyCombatInitializer goblin = FindInitializer(goblinData);
            CombatReceiver slimeReceiver = slime.GetComponent<CombatReceiver>();
            CombatReceiver goblinReceiver = goblin.GetComponent<CombatReceiver>();
            Assert.That(slime.Data, Is.SameAs(slimeData));
            Assert.That(goblin.Data, Is.SameAs(goblinData));
            Assert.That(slime.Data.MoveSpeed, Is.EqualTo(1.5f));
            Assert.That(goblin.Data.MoveSpeed, Is.EqualTo(2.6f));
            Assert.That(slime.Data.DetectionRange, Is.EqualTo(4.5f));
            Assert.That(slime.Data.PursuitEndRange, Is.EqualTo(5.5f));
            Assert.That(slime.Data.InvestigationDuration, Is.EqualTo(1.25f));
            Assert.That(goblin.Data.DetectionRange, Is.EqualTo(6f));
            Assert.That(goblin.Data.PursuitEndRange, Is.EqualTo(7.5f));
            Assert.That(goblin.Data.InvestigationDuration, Is.EqualTo(2f));
            Assert.That(slime.EntityId, Is.Not.EqualTo(goblin.EntityId));
            Assert.That(Vector2.Distance(slime.transform.position, goblin.transform.position), Is.GreaterThan(0f));
            Assert.That(slimeReceiver.CurrentHealth, Is.EqualTo(slimeData.MaxHealth));
            Assert.That(goblinReceiver.CurrentHealth, Is.EqualTo(goblinData.MaxHealth));

            Assert.That(slimeReceiver.ReceiveDamage(4), Is.True);
            Assert.That(slimeReceiver.CurrentHealth, Is.EqualTo(16));
            Assert.That(goblinReceiver.CurrentHealth, Is.EqualTo(45));

            playerObject.transform.position = slime.transform.position;
            yield return null;
            Assert.That(playerReceiver.CurrentHealth, Is.EqualTo(100 - slimeData.AttackPower));

            playerObject.transform.position = goblin.transform.position;
            yield return null;
            Assert.That(playerReceiver.CurrentHealth,
                Is.EqualTo(100 - slimeData.AttackPower - goblinData.AttackPower));

            EntityId slimeEntityId = slime.EntityId;
            EntityId goblinEntityId = goblin.EntityId;
            Assert.That(slimeReceiver.ReceiveDamage(100), Is.True);
            yield return null;

            Assert.That(slime == null, Is.True);
            Assert.That(bootstrap.EntityManager.GetState(slimeEntityId), Is.EqualTo(EntityState.Released));
            Assert.That(bootstrap.EntityManager.GetState(goblinEntityId), Is.EqualTo(EntityState.Alive));
            Assert.That(goblin != null, Is.True);
            Assert.That(goblinReceiver.CurrentHealth, Is.EqualTo(goblinData.MaxHealth));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyObjects(spawnedEnemies);
            DestroyObjects(objectsToDestroy);
            DestroyObjects(templateObjects);

            for (int i = 0; i < dataAssets.Count; i++)
            {
                if (dataAssets[i] != null)
                {
                    UnityEngine.Object.Destroy(dataAssets[i]);
                }
            }

            yield return null;
        }

        private EnemyData CreateEnemyData(
            string enemyName,
            int maxHealth,
            float moveSpeed,
            int attackPower,
            float attackRange,
            float attackInterval,
            float detectionRange,
            float pursuitEndRange,
            float investigationDuration)
        {
            EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
            dataAssets.Add(data);
            SetField(data, "enemyName", enemyName);
            SetField(data, "maxHealth", maxHealth);
            SetField(data, "moveSpeed", moveSpeed);
            SetField(data, "attackPower", attackPower);
            SetField(data, "attackRange", attackRange);
            SetField(data, "attackInterval", attackInterval);
            SetField(data, "detectionRange", detectionRange);
            SetField(data, "pursuitEndRange", pursuitEndRange);
            SetField(data, "investigationDuration", investigationDuration);
            return data;
        }

        private GameObject CreateEnemyTemplate(string objectName, EnemyData data)
        {
            GameObject template = new GameObject(objectName);
            template.SetActive(false);
            templateObjects.Add(template);
            template.AddComponent<CombatReceiver>();
            EnemyCombatInitializer initializer = template.AddComponent<EnemyCombatInitializer>();
            SetField(initializer, "enemyData", data);
            template.AddComponent<Rigidbody2D>();
            template.AddComponent<EnemyChaseAI>();
            template.AddComponent<EnemyAttackAI>();
            return template;
        }

        private static Array CreateSpawnEntries(GameObject slimePrefab, GameObject goblinPrefab)
        {
            Type entryType = typeof(CombatTestEnemySpawner).GetNestedType(
                "EnemySpawnEntry",
                BindingFlags.NonPublic);
            Assert.That(entryType, Is.Not.Null);

            Array entries = Array.CreateInstance(entryType, 2);
            entries.SetValue(CreateSpawnEntry(entryType, slimePrefab), 0);
            entries.SetValue(CreateSpawnEntry(entryType, goblinPrefab), 1);
            return entries;
        }

        private static object CreateSpawnEntry(Type entryType, GameObject prefab)
        {
            object entry = Activator.CreateInstance(entryType, true);
            SetField(entry, "enemyPrefab", prefab);
            SetField(entry, "spawnCount", 1);
            return entry;
        }

        private void CollectSpawnedEnemies(EnemyData slimeData, EnemyData goblinData)
        {
            EnemyCombatInitializer[] initializers = UnityEngine.Object.FindObjectsByType<EnemyCombatInitializer>(
                FindObjectsSortMode.None);
            for (int i = 0; i < initializers.Length; i++)
            {
                EnemyCombatInitializer initializer = initializers[i];
                if (initializer.Data == slimeData || initializer.Data == goblinData)
                {
                    spawnedEnemies.Add(initializer.gameObject);
                }
            }
        }

        private EnemyCombatInitializer FindInitializer(EnemyData data)
        {
            for (int i = 0; i < spawnedEnemies.Count; i++)
            {
                EnemyCombatInitializer initializer = spawnedEnemies[i].GetComponent<EnemyCombatInitializer>();
                if (initializer.Data == data)
                {
                    return initializer;
                }
            }

            Assert.Fail($"{data.EnemyName} 적 초기화 컴포넌트를 찾지 못했습니다.");
            return null;
        }

        private static void DestroyObjects(List<GameObject> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                {
                    UnityEngine.Object.Destroy(targets[i]);
                }
            }
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
