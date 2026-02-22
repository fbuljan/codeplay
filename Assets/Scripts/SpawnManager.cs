using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns obstacles, enemies, and collectibles ahead of the player.
/// Uses segment-based patterns for organic, varied spawning.
/// Supports single-lane (Phase 1) and multi-lane (Phase 2+).
/// </summary>
public class SpawnManager : MonoBehaviour
{
    [Header("Obstacle Prefabs")]
    [SerializeField] GameObject lowObstaclePrefab;
    [SerializeField] GameObject highObstaclePrefab;
    [SerializeField] GameObject fullLaneBlockerPrefab;

    [Header("Enemy Prefabs")]
    [SerializeField] GameObject groundEnemyPrefab;
    [SerializeField] GameObject airEnemyPrefab;

    [Header("Collectible Prefabs")]
    [SerializeField] GameObject coinPrefab;
    [SerializeField] GameObject healthPickupPrefab;

    [Header("Spawn Settings")]
    [SerializeField] float firstSpawnDistance = 30f;
    [SerializeField] float spawnDistance = 150f;
    [SerializeField] float despawnBehindDistance = 20f;
    [SerializeField] float highObstacleY = 1.5f;
    [SerializeField] float fullLaneBlockerY = 1f;
    [SerializeField] float enemySpawnY = 1f;
    [SerializeField] float coinY = 1f;

    [Header("Pool")]
    [SerializeField] int poolSize = 10;

    // Controlled by DifficultyManager
    float minSpawnInterval = 1.5f;
    float maxSpawnInterval = 3f;
    float enemySpawnChance = 0.3f;
    float airEnemySpawnChance = 0.15f;
    float coinSpawnChance = 0.4f;
    float healthPickupSpawnChance = 0.05f;

    Transform playerTransform;
    bool spawningEnabled;
    bool multiLaneEnabled = true;
    float[] lanePositions;

    // Segment queue — upcoming spawns at specific Z positions
    List<SpawnEvent> spawnQueue = new();
    float nextSegmentZ;
    ObstacleType lastObstacleType = ObstacleType.Low;
    int segmentsSinceBreather;

    List<GameObject> lowPool = new();
    List<GameObject> highPool = new();
    List<GameObject> blockerPool = new();
    List<GameObject> enemyPool = new();
    List<GameObject> airEnemyPool = new();
    List<GameObject> coinPool = new();
    List<GameObject> healthPool = new();

    struct SpawnEvent
    {
        public float z;
        public float x;
        public SpawnType type;
    }

    enum SpawnType
    {
        ObstacleLow, ObstacleHigh, ObstacleFullLane,
        GroundEnemy, AirEnemy,
        Coin, HealthPickup
    }

    public void Initialize(Transform player)
    {
        playerTransform = player;
        lanePositions = GameManager.Instance != null ? GameManager.Instance.LanePositions : new float[] { 0f };

        FillPool(lowPool, lowObstaclePrefab, "LowObstacle", poolSize);
        FillPool(highPool, highObstaclePrefab, "HighObstacle", poolSize);
        FillPool(blockerPool, fullLaneBlockerPrefab, "FullLaneBlocker", poolSize / 2);
        FillPool(enemyPool, groundEnemyPrefab, "GroundEnemy", poolSize);
        FillPool(airEnemyPool, airEnemyPrefab, "AirEnemy", poolSize / 2);
        FillPool(coinPool, coinPrefab, "Coin", poolSize * 3);
        FillPool(healthPool, healthPickupPrefab, "HealthPickup", poolSize / 4);
    }

    public void SetMultiLaneEnabled(bool enabled)
    {
        multiLaneEnabled = enabled;
    }

    public void SetSpawningEnabled(bool enabled)
    {
        spawningEnabled = enabled;

        if (enabled)
        {
            nextSegmentZ = playerTransform.position.z + firstSpawnDistance;
            spawnQueue.Clear();
            segmentsSinceBreather = 0;
        }
        else
        {
            spawnQueue.Clear();
            ReturnAllToPool();
        }
    }

    void Update()
    {
        if (!spawningEnabled || playerTransform == null) return;

        // Generate new segments when needed
        while (nextSegmentZ < playerTransform.position.z + spawnDistance + 100f)
        {
            GenerateSegment();
        }

        // Process queued spawns that the player is approaching
        float activateZ = playerTransform.position.z + spawnDistance;
        for (int i = spawnQueue.Count - 1; i >= 0; i--)
        {
            if (spawnQueue[i].z <= activateZ)
            {
                ExecuteSpawn(spawnQueue[i]);
                spawnQueue.RemoveAt(i);
            }
        }

        RecycleBehindPlayer();
    }

    // ---- Segment Generation ----

    void GenerateSegment()
    {
        float z = nextSegmentZ;

        // Force a breather after 3-4 intense segments
        if (segmentsSinceBreather >= Random.Range(3, 5))
        {
            GenerateBreather(z);
            segmentsSinceBreather = 0;
            nextSegmentZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 6f;
            return;
        }

        // Pick a segment type
        float roll = Random.value;
        float cumulative = 0f;

        // Enemy ambush segment
        cumulative += enemySpawnChance;
        if (roll < cumulative && groundEnemyPrefab != null)
        {
            if (Random.value < 0.4f)
                GenerateEnemyBehindCover(z);
            else
                GenerateEnemyStandalone(z);

            segmentsSinceBreather++;
            nextSegmentZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 5f;
            return;
        }

        // Rapid sequence (jump-slide or slide-jump)
        cumulative += 0.2f;
        if (roll < cumulative)
        {
            GenerateRapidSequence(z);
            segmentsSinceBreather++;
            nextSegmentZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 7f;
            return;
        }

        // Coin trail reward
        cumulative += 0.1f;
        if (roll < cumulative && coinPrefab != null)
        {
            GenerateCoinTrail(z);
            segmentsSinceBreather = Mathf.Max(0, segmentsSinceBreather - 1);
            nextSegmentZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 5f;
            return;
        }

        // Default: single obstacle with optional coin reward after
        GenerateSingleObstacle(z);
        segmentsSinceBreather++;
        nextSegmentZ += Random.Range(minSpawnInterval, maxSpawnInterval) * 5f;

        // Independent air enemy roll
        if (airEnemyPrefab != null && Random.value < airEnemySpawnChance)
        {
            QueueSpawn(z, 0f, SpawnType.AirEnemy);
        }
    }

    // ---- Segment Patterns ----

    void GenerateSingleObstacle(float z)
    {
        // Obstacle on 1-2 lanes, coins/collectibles on the free lane(s)
        float[] lanes = PickMultipleLanes(3);
        int obstacleCount = Random.Range(1, 3); // 1 or 2 obstacles

        for (int i = 0; i < obstacleCount && i < lanes.Length; i++)
        {
            ObstacleType type = PickObstacleType();
            QueueObstacle(type, lanes[i], z + Random.Range(-1f, 1f));
        }

        // Place coins on remaining lane(s)
        for (int i = obstacleCount; i < lanes.Length; i++)
        {
            if (coinPrefab != null && Random.value < coinSpawnChance)
            {
                QueueSpawn(z, lanes[i], SpawnType.Coin);
                QueueSpawn(z + 2f, lanes[i], SpawnType.Coin);
            }
        }

        // Rare health pickup on a random lane after obstacles
        if (healthPickupPrefab != null && Random.value < healthPickupSpawnChance)
        {
            QueueSpawn(z + Random.Range(3f, 5f), PickLaneX(), SpawnType.HealthPickup);
        }
    }

    void GenerateRapidSequence(float z)
    {
        // Obstacles in quick succession, each row uses 1-2 lanes
        int count = Random.Range(2, 4);
        float gap = Random.Range(3f, 5f);

        for (int i = 0; i < count; i++)
        {
            float[] lanes = PickMultipleLanes(3);
            int obstacleLanes = Random.Range(1, 3); // 1 or 2 lanes blocked per row

            for (int l = 0; l < obstacleLanes && l < lanes.Length; l++)
            {
                ObstacleType type = (i % 2 == 0) ? ObstacleType.Low : ObstacleType.High;
                if (Random.value < 0.2f) type = type == ObstacleType.Low ? ObstacleType.High : ObstacleType.Low;
                QueueObstacle(type, lanes[l], z + i * gap);
            }

            // Coins on free lane(s)
            if (coinPrefab != null)
            {
                for (int l = obstacleLanes; l < lanes.Length; l++)
                {
                    if (Random.value < 0.5f)
                        QueueSpawn(z + i * gap, lanes[l], SpawnType.Coin);
                }
            }
        }

        // Reward coins after the sequence spread across lanes
        if (coinPrefab != null)
        {
            float rewardZ = z + count * gap + 2f;
            for (int i = 0; i < 3; i++)
            {
                QueueSpawn(rewardZ + i * 2f, PickLaneX(), SpawnType.Coin);
            }
        }
    }

    void GenerateEnemyBehindCover(float z)
    {
        float[] lanes = PickMultipleLanes(3);

        // Obstacle + enemy on one lane
        float enemyLane = lanes[0];
        ObstacleType type = PickObstacleType();
        QueueObstacle(type, enemyLane, z);
        QueueSpawn(z + Random.Range(5f, 9f), enemyLane, SpawnType.GroundEnemy);

        // Extra obstacles on other lanes at similar Z
        if (lanes.Length > 1)
        {
            QueueObstacle(PickObstacleType(), lanes[1], z + Random.Range(-1f, 1.5f));
        }

        // Coins between obstacle and enemy on free lane(s)
        if (coinPrefab != null)
        {
            float coinLane = lanes.Length > 2 ? lanes[2] : PickOtherLaneX(enemyLane);
            QueueSpawn(z + 2.5f, coinLane, SpawnType.Coin);
            QueueSpawn(z + 4f, coinLane, SpawnType.Coin);
            QueueSpawn(z + 5.5f, coinLane, SpawnType.Coin);
        }
    }

    void GenerateEnemyStandalone(float z)
    {
        float[] lanes = PickMultipleLanes(3);

        // Enemy on one lane
        QueueSpawn(z, lanes[0], SpawnType.GroundEnemy);

        // Obstacle or second enemy on another lane
        if (lanes.Length > 1)
        {
            if (Random.value < 0.5f && groundEnemyPrefab != null)
                QueueSpawn(z + Random.Range(-1f, 1f), lanes[1], SpawnType.GroundEnemy);
            else
                QueueObstacle(PickObstacleType(), lanes[1], z + Random.Range(-1f, 1f));
        }

        // Coin lure on remaining lane
        if (coinPrefab != null && lanes.Length > 2)
        {
            QueueSpawn(z - 2f, lanes[2], SpawnType.Coin);
            QueueSpawn(z, lanes[2], SpawnType.Coin);
        }
    }

    void GenerateCoinTrail(float z)
    {
        // Main coin trail on one lane, secondary content on others
        float[] lanes = PickMultipleLanes(3);
        float mainLane = lanes[0];
        int count = Random.Range(4, 8);
        float spacing = Random.Range(1.5f, 2.5f);

        for (int i = 0; i < count; i++)
        {
            QueueSpawn(z + i * spacing, mainLane, SpawnType.Coin);
        }

        // Obstacles alongside the coin trail on other lanes
        if (lanes.Length > 1)
        {
            int obstacleCount = Random.Range(1, 3);
            for (int i = 0; i < obstacleCount; i++)
            {
                float oz = z + Random.Range(1f, count * spacing * 0.8f);
                float ol = lanes[Random.Range(1, Mathf.Min(lanes.Length, 3))];
                QueueObstacle(PickObstacleType(), ol, oz);
            }
        }

        // Sometimes an obstacle at the end to catch you off guard
        if (Random.value < 0.3f)
        {
            QueueObstacle(PickObstacleType(), mainLane, z + count * spacing + 1.5f);
        }
    }

    void GenerateBreather(float z)
    {
        // Coins spread across multiple lanes — no threats
        int count = Random.Range(3, 6);
        for (int i = 0; i < count; i++)
        {
            QueueSpawn(z + i * 2f, PickLaneX(), SpawnType.Coin);
        }

        // Chance for health pickup during breather
        if (healthPickupPrefab != null && Random.value < 0.25f)
        {
            QueueSpawn(z + count * 1f, PickLaneX(), SpawnType.HealthPickup);
        }
    }

    // ---- Multi-Lane Helpers ----

    float PickLaneX()
    {
        if (!multiLaneEnabled) return 0f;
        return lanePositions[Random.Range(0, lanePositions.Length)];
    }

    float PickOtherLaneX(float excludeX)
    {
        if (!multiLaneEnabled || lanePositions.Length < 2) return 0f;
        float x;
        int attempts = 0;
        do { x = lanePositions[Random.Range(0, lanePositions.Length)]; attempts++; }
        while (Mathf.Approximately(x, excludeX) && attempts < 10);
        return x;
    }

    /// Returns 2-3 lane X positions in shuffled order
    float[] PickMultipleLanes(int count)
    {
        if (!multiLaneEnabled || lanePositions.Length < 2)
            return new float[] { 0f };

        count = Mathf.Min(count, lanePositions.Length);
        float[] lanes = (float[])lanePositions.Clone();
        // Fisher-Yates shuffle
        for (int i = lanes.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (lanes[i], lanes[j]) = (lanes[j], lanes[i]);
        }
        float[] result = new float[count];
        System.Array.Copy(lanes, result, count);
        return result;
    }

    // ---- Queue & Execute ----

    void QueueSpawn(float z, float x, SpawnType type)
    {
        spawnQueue.Add(new SpawnEvent { z = z, x = x, type = type });
    }

    void QueueObstacle(ObstacleType obstacleType, float x, float z)
    {
        SpawnType st = obstacleType switch
        {
            ObstacleType.High => SpawnType.ObstacleHigh,
            ObstacleType.FullLane => SpawnType.ObstacleFullLane,
            _ => SpawnType.ObstacleLow
        };
        QueueSpawn(z, x, st);
    }

    void ExecuteSpawn(SpawnEvent evt)
    {
        switch (evt.type)
        {
            case SpawnType.ObstacleLow:
                SpawnObstacleAt(ObstacleType.Low, evt.x, evt.z);
                break;
            case SpawnType.ObstacleHigh:
                SpawnObstacleAt(ObstacleType.High, evt.x, evt.z);
                break;
            case SpawnType.ObstacleFullLane:
                SpawnObstacleAt(ObstacleType.FullLane, evt.x, evt.z);
                break;
            case SpawnType.GroundEnemy:
                SpawnGroundEnemyAt(evt.x, evt.z);
                break;
            case SpawnType.AirEnemy:
                SpawnAirEnemy();
                break;
            case SpawnType.Coin:
                SpawnCoinAt(evt.x, evt.z);
                break;
            case SpawnType.HealthPickup:
                SpawnHealthPickupAt(evt.x, evt.z);
                break;
        }
    }

    // ---- Spawn Primitives ----

    ObstacleType PickObstacleType()
    {
        ObstacleType type = Random.value > 0.5f ? ObstacleType.Low : ObstacleType.High;
        if (type == lastObstacleType && Random.value > 0.3f)
            type = type == ObstacleType.Low ? ObstacleType.High : ObstacleType.Low;
        lastObstacleType = type;
        return type;
    }

    void SpawnObstacleAt(ObstacleType type, float x, float z)
    {
        List<GameObject> pool;
        float yPos;

        switch (type)
        {
            case ObstacleType.FullLane:
                pool = blockerPool;
                yPos = fullLaneBlockerY;
                break;
            case ObstacleType.High:
                pool = highPool;
                yPos = highObstacleY;
                break;
            default:
                pool = lowPool;
                yPos = 0f;
                break;
        }

        GameObject obj = GetFromPool(pool);
        if (obj == null) return;

        if (type == ObstacleType.Low)
            yPos = obj.transform.localScale.y / 2f;

        obj.transform.position = new Vector3(x, yPos, z);

        var obstacle = obj.GetComponent<Obstacle>();
        if (obstacle != null)
            obstacle.Initialize(type);

        obj.SetActive(true);
    }

    void SpawnGroundEnemyAt(float x, float z)
    {
        GameObject obj = GetFromPool(enemyPool);
        if (obj == null) return;

        obj.transform.position = new Vector3(x, enemySpawnY, z);

        var enemy = obj.GetComponent<GroundEnemy>();
        if (enemy != null)
        {
            enemy.SetPlayerTransform(playerTransform);
            enemy.Initialize();
            enemy.SetBlockingObstacles(GetObstaclesBetween(playerTransform.position.z, z));
        }

        obj.SetActive(true);
    }

    void SpawnAirEnemy()
    {
        GameObject obj = GetFromPool(airEnemyPool);
        if (obj == null) return;

        obj.transform.position = new Vector3(0f, 5f, playerTransform.position.z);

        var airEnemy = obj.GetComponent<AirEnemy>();
        if (airEnemy != null)
        {
            airEnemy.SetPlayerTransform(playerTransform);
            airEnemy.Initialize();
        }

        obj.SetActive(true);
    }

    void SpawnCoinAt(float x, float z)
    {
        GameObject obj = GetFromPool(coinPool);
        if (obj == null) return;

        obj.transform.position = new Vector3(x, coinY, z);
        obj.SetActive(true);
    }

    void SpawnHealthPickupAt(float x, float z)
    {
        GameObject obj = GetFromPool(healthPool);
        if (obj == null) return;

        obj.transform.position = new Vector3(x, coinY, z);
        obj.SetActive(true);
    }

    // ---- Helpers ----

    List<GameObject> GetObstaclesBetween(float minZ, float maxZ)
    {
        List<GameObject> result = new();
        CollectActiveInRange(lowPool, minZ, maxZ, result);
        CollectActiveInRange(highPool, minZ, maxZ, result);
        CollectActiveInRange(blockerPool, minZ, maxZ, result);
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
        RecyclePool(blockerPool, despawnZ);
        RecyclePool(enemyPool, despawnZ);
        RecyclePool(airEnemyPool, despawnZ);
        RecyclePool(coinPool, despawnZ);
        RecyclePool(healthPool, despawnZ);
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
        if (prefab == null) return;

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

    // ---- Difficulty Setters ----

    public void SetSpawnIntervals(float min, float max)
    {
        minSpawnInterval = min;
        maxSpawnInterval = max;
    }

    public void SetEnemyChances(float ground, float air)
    {
        enemySpawnChance = ground;
        airEnemySpawnChance = air;
    }

    public void SetCollectibleChances(float coin, float health)
    {
        coinSpawnChance = coin;
        healthPickupSpawnChance = health;
    }

    void ReturnAllToPool()
    {
        foreach (var obj in lowPool) obj.SetActive(false);
        foreach (var obj in highPool) obj.SetActive(false);
        foreach (var obj in blockerPool) obj.SetActive(false);
        foreach (var obj in enemyPool) obj.SetActive(false);
        foreach (var obj in airEnemyPool) obj.SetActive(false);
        foreach (var obj in coinPool) obj.SetActive(false);
        foreach (var obj in healthPool) obj.SetActive(false);
    }
}
