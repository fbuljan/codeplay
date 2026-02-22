using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns obstacles ahead of the player at intervals.
/// Uses object pooling for performance. Recycles objects that fall behind the player.
/// Prefabs must have an Obstacle component and a trigger collider.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("Obstacle Prefabs")]
    [SerializeField] GameObject lowObstaclePrefab;
    [SerializeField] GameObject highObstaclePrefab;

    [Header("Enemy Prefabs")]
    [SerializeField] GameObject groundEnemyPrefab;
    [SerializeField] GameObject airEnemyPrefab;

    [Header("Spawn Settings")]
    [SerializeField] float spawnDistance = 80f;
    [SerializeField] float minSpawnInterval = 1.5f;
    [SerializeField] float maxSpawnInterval = 3f;
    [SerializeField] float despawnBehindDistance = 20f;
    [SerializeField] float highObstacleY = 1.5f;
    [SerializeField] [Range(0f, 1f)] float enemySpawnChance = 0.3f;
    [SerializeField] [Range(0f, 1f)] float airEnemySpawnChance = 0.15f;
    [SerializeField] float enemySpawnY = 1f;

    [Header("Pool")]
    [SerializeField] int poolSize = 10;

    Transform playerTransform;
    bool spawningEnabled;

    float nextSpawnZ;
    List<GameObject> lowPool = new();
    List<GameObject> highPool = new();
    List<GameObject> enemyPool = new();
    List<GameObject> airEnemyPool = new();

    public void Initialize(Transform player)
    {
        playerTransform = player;
        FillPool(lowPool, lowObstaclePrefab, "LowObstacle", poolSize);
        FillPool(highPool, highObstaclePrefab, "HighObstacle", poolSize);
        FillPool(enemyPool, groundEnemyPrefab, "GroundEnemy", poolSize);
        FillPool(airEnemyPool, airEnemyPrefab, "AirEnemy", poolSize / 2);
    }

    public void SetSpawningEnabled(bool enabled)
    {
        spawningEnabled = enabled;

        if (enabled)
        {
            nextSpawnZ = playerTransform.position.z + spawnDistance;
        }
        else
        {
            ReturnAllToPool();
        }
    }

    void Update()
    {
        if (!spawningEnabled || playerTransform == null) return;

        if (playerTransform.position.z + spawnDistance >= nextSpawnZ)
        {
            if (groundEnemyPrefab != null && Random.value < enemySpawnChance)
                SpawnGroundEnemy();
            else
                SpawnObstacle();

            if (airEnemyPrefab != null && Random.value < airEnemySpawnChance)
                SpawnAirEnemy();

            nextSpawnZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 10f;
        }

        RecycleBehindPlayer();
    }

    void SpawnObstacle()
    {
        ObstacleType type = Random.value > 0.5f ? ObstacleType.Low : ObstacleType.High;
        List<GameObject> pool = type == ObstacleType.Low ? lowPool : highPool;

        GameObject obj = GetFromPool(pool);
        if (obj == null) return;

        float yPos;
        if (type == ObstacleType.Low)
            yPos = obj.transform.localScale.y / 2f;
        else
            yPos = highObstacleY;

        obj.transform.position = new Vector3(0f, yPos, nextSpawnZ);

        var obstacle = obj.GetComponent<Obstacle>();
        obstacle.Initialize(type);

        obj.SetActive(true);
    }

    void SpawnGroundEnemy()
    {
        GameObject obj = GetFromPool(enemyPool);
        if (obj == null) return;

        obj.transform.position = new Vector3(0f, enemySpawnY, nextSpawnZ);

        var enemy = obj.GetComponent<GroundEnemy>();
        if (enemy != null)
        {
            enemy.SetPlayerTransform(playerTransform);
            enemy.Initialize();
            enemy.SetBlockingObstacles(GetObstaclesBetween(playerTransform.position.z, nextSpawnZ));
        }

        obj.SetActive(true);
    }

    void SpawnAirEnemy()
    {
        GameObject obj = GetFromPool(airEnemyPool);
        if (obj == null) return;

        // Position at player Z — AirEnemy.Update will keep it following
        obj.transform.position = new Vector3(0f, 5f, playerTransform.position.z);

        var airEnemy = obj.GetComponent<AirEnemy>();
        if (airEnemy != null)
        {
            airEnemy.SetPlayerTransform(playerTransform);
            airEnemy.Initialize();
        }

        obj.SetActive(true);
    }

    List<GameObject> GetObstaclesBetween(float minZ, float maxZ)
    {
        List<GameObject> result = new();
        CollectActiveInRange(lowPool, minZ, maxZ, result);
        CollectActiveInRange(highPool, minZ, maxZ, result);
        return result;
    }

    void CollectActiveInRange(List<GameObject> pool, float minZ, float maxZ, List<GameObject> result)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].activeSelf) continue;
            float z = pool[i].transform.position.z;
            if (z > minZ && z < maxZ)
                result.Add(pool[i]);
        }
    }

    void RecycleBehindPlayer()
    {
        float despawnZ = playerTransform.position.z - despawnBehindDistance;
        RecyclePool(lowPool, despawnZ);
        RecyclePool(highPool, despawnZ);
        RecyclePool(enemyPool, despawnZ);
        RecyclePool(airEnemyPool, despawnZ);
    }

    void RecyclePool(List<GameObject> pool, float despawnZ)
    {
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (pool[i].activeSelf && pool[i].transform.position.z < despawnZ)
                pool[i].SetActive(false);
        }
    }

    void FillPool(List<GameObject> pool, GameObject prefab, string baseName, int count)
    {
        if (prefab == null)
        {
            Debug.LogError($"[SpawnManager] {baseName} prefab is not assigned!");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(prefab, transform);
            obj.name = $"{baseName}_{i}";
            obj.SetActive(false);
            pool.Add(obj);

            var enemy = obj.GetComponent<Enemy>();
            if (enemy != null)
                enemy.OnDestroyed += OnEnemyDestroyed;
        }
    }

    void OnEnemyDestroyed(Enemy enemy)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnEnemyKilled(enemy);
    }

    GameObject GetFromPool(List<GameObject> pool)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (!pool[i].activeSelf)
                return pool[i];
        }
        return null;
    }

    void ReturnAllToPool()
    {
        foreach (var obj in lowPool) obj.SetActive(false);
        foreach (var obj in highPool) obj.SetActive(false);
        foreach (var obj in enemyPool) obj.SetActive(false);
        foreach (var obj in airEnemyPool) obj.SetActive(false);
    }
}
