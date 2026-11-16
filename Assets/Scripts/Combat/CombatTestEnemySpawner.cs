using System;
using UnityEngine;

namespace Prohect_DOD_Undergird.Combat
{
    /// <summary>
    /// Main 씬에서 설정된 종류별 테스트 적을 배치하고 공유 전투 시스템에 등록합니다.
    /// </summary>
    public class CombatTestEnemySpawner : MonoBehaviour
    {
        [Serializable]
        private class EnemySpawnEntry
        {
            [SerializeField] private GameObject enemyPrefab;
            [SerializeField, Min(0)] private int spawnCount = 1;

            public GameObject EnemyPrefab => enemyPrefab;
            public int SpawnCount => spawnCount;
        }

        [SerializeField] private CombatBootstrap combatBootstrap;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private EnemySpawnEntry[] enemySpawnEntries;
        [SerializeField, Min(0.1f)] private float spawnSpacing = 1.25f;

        // 기존 씬과 Inspector 설정을 유지하는 단일 종류 생성용 fallback입니다.
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(1)] private int spawnCount = 1;

        private void Start()
        {
            SpawnTestEnemies();
        }

        private void SpawnTestEnemies()
        {
            if (combatBootstrap == null || playerTransform == null)
            {
                Debug.LogError("테스트 적 생성에 필요한 Bootstrap 또는 플레이어가 설정되지 않았습니다.", this);
                return;
            }

            CombatReceiver playerReceiver = playerTransform.GetComponent<CombatReceiver>();
            if (playerReceiver == null)
            {
                Debug.LogError("플레이어의 CombatReceiver가 설정되지 않았습니다.", this);
                return;
            }

            bool hasSpawnEntries = enemySpawnEntries != null && enemySpawnEntries.Length > 0;
            int totalCount = hasSpawnEntries ? GetConfiguredSpawnCount() : Mathf.Max(1, spawnCount);
            if (totalCount <= 0)
            {
                Debug.LogError("생성할 테스트 적 프리팹과 생성 수가 설정되지 않았습니다.", this);
                return;
            }

            int spawnIndex = 0;
            if (hasSpawnEntries)
            {
                SpawnConfiguredEnemyTypes(playerReceiver, totalCount, ref spawnIndex);
                return;
            }

            if (enemyPrefab == null)
            {
                Debug.LogError("테스트 적 프리팹이 설정되지 않았습니다.", this);
                return;
            }

            int count = Mathf.Max(1, spawnCount);
            for (int i = 0; i < count; i++)
            {
                SpawnTestEnemy(enemyPrefab, playerReceiver, GetSpawnPosition(spawnIndex++, totalCount));
            }
        }

        private int GetConfiguredSpawnCount()
        {
            int totalCount = 0;
            for (int i = 0; i < enemySpawnEntries.Length; i++)
            {
                EnemySpawnEntry entry = enemySpawnEntries[i];
                if (entry != null && entry.EnemyPrefab != null)
                {
                    totalCount += Mathf.Max(0, entry.SpawnCount);
                }
            }

            return totalCount;
        }

        private void SpawnConfiguredEnemyTypes(
            CombatReceiver playerReceiver,
            int totalCount,
            ref int spawnIndex)
        {
            for (int entryIndex = 0; entryIndex < enemySpawnEntries.Length; entryIndex++)
            {
                EnemySpawnEntry entry = enemySpawnEntries[entryIndex];
                if (entry == null || entry.SpawnCount <= 0)
                {
                    continue;
                }

                if (entry.EnemyPrefab == null)
                {
                    Debug.LogError("종류별 테스트 적 프리팹이 설정되지 않았습니다.", this);
                    continue;
                }

                for (int i = 0; i < entry.SpawnCount; i++)
                {
                    Vector3 spawnPosition = GetSpawnPosition(spawnIndex++, totalCount);
                    SpawnTestEnemy(entry.EnemyPrefab, playerReceiver, spawnPosition);
                }
            }
        }

        private void SpawnTestEnemy(GameObject prefab, CombatReceiver playerReceiver, Vector3 spawnPosition)
        {
            GameObject enemyObject = Instantiate(prefab, spawnPosition, Quaternion.identity);
            EnemyCombatInitializer initializer = enemyObject.GetComponent<EnemyCombatInitializer>();
            EnemyChaseAI chaseAI = enemyObject.GetComponent<EnemyChaseAI>();
            EnemyAttackAI attackAI = enemyObject.GetComponent<EnemyAttackAI>();
            SlimeChargeAttackAI chargeAttackAI = enemyObject.GetComponent<SlimeChargeAttackAI>();

            if (initializer == null || chaseAI == null || (attackAI == null && chargeAttackAI == null))
            {
                Debug.LogError("테스트 적의 전투 컴포넌트가 설정되지 않았습니다.", enemyObject);
                Destroy(enemyObject);
                return;
            }

            chaseAI.SetPlayerTarget(playerTransform);
            if (attackAI != null)
            {
                attackAI.SetPlayerTarget(playerReceiver);
            }

            if (chargeAttackAI != null)
            {
                chargeAttackAI.SetPlayerTarget(playerReceiver);
            }

            if (!initializer.Initialize(combatBootstrap))
            {
                Debug.LogError("테스트 적의 전투 초기화에 실패했습니다.", enemyObject);
                Destroy(enemyObject);
                return;
            }

            // 비활성 프리팹도 참조와 Entity 초기화를 마친 뒤 AI가 실행되도록 활성화합니다.
            enemyObject.SetActive(true);
        }

        private Vector3 GetSpawnPosition(int spawnIndex, int count)
        {
            int columnCount = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rowCount = Mathf.CeilToInt(count / (float)columnCount);
            int row = spawnIndex / columnCount;
            int column = spawnIndex % columnCount;
            int itemsInRow = Mathf.Min(columnCount, count - row * columnCount);
            float spacing = Mathf.Max(0.1f, spawnSpacing);

            // 전체 종류를 하나의 인덱스로 배치해 종류가 달라도 위치가 겹치지 않게 합니다.
            float offsetX = (column - (itemsInRow - 1) * 0.5f) * spacing;
            float offsetY = (row - (rowCount - 1) * 0.5f) * spacing;
            return transform.position + new Vector3(offsetX, offsetY, 0f);
        }
    }
}
