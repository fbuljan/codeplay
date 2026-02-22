using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns obstacles ahead of the player at intervals.
/// Uses object pooling for performance. Recycles objects that fall behind the player.
/// Prefabs must have an Obstacle component and a trigger collider.
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] GameObject lowObstaclePrefab;
    [SerializeField] GameObject highObstaclePrefab;

    [Header("Spawn Settings")]
    [SerializeField] float spawnDistance = 80f;
    [SerializeField] float minSpawnInterval = 1.5f;
    [SerializeField] float maxSpawnInterval = 3f;
    [SerializeField] float despawnBehindDistance = 20f;
    [SerializeField] float highObstacleY = 1.5f;

    [Header("Pool")]
    [SerializeField] int poolSize = 10;

    Transform playerTransform;
    bool spawningEnabled;

    float nextSpawnZ;
    List<GameObject> lowPool = new();
    List<GameObject> highPool = new();

    public void Initialize(Transform player)
    {
        playerTransform = player;
        FillPool(lowPool, lowObstaclePrefab, "LowObstacle", poolSize);
        FillPool(highPool, highObstaclePrefab, "HighObstacle", poolSize);
    }

    public void SetSpawningEnabled(bool enabled)
    {
        spawningEnabled = enabled;

        if (enabled)
            nextSpawnZ = playerTransform.position.z + spawnDistance;
        else
            ReturnAllToPool();
    }

    void Update()
    {
        if (!spawningEnabled || playerTransform == null) return;

        if (playerTransform.position.z + spawnDistance >= nextSpawnZ)
        {
            SpawnObstacle();
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

    void RecycleBehindPlayer()
    {
        float despawnZ = playerTransform.position.z - despawnBehindDistance;
        RecyclePool(lowPool, despawnZ);
        RecyclePool(highPool, despawnZ);
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
        }
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
    }
}
