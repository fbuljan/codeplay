using UnityEngine;

/// <summary>
/// Ramps game difficulty over distance traveled.
/// Adjusts player speed, spawn intervals, and enemy frequency.
/// All values are parameterized and tunable in the inspector.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    [Header("Distance Thresholds")]
    [SerializeField] float rampStartDistance = 50f;
    [SerializeField] float rampEndDistance = 2000f;

    [Header("Player Speed")]
    [SerializeField] float startSpeed = 10f;
    [SerializeField] float maxSpeed = 25f;

    [Header("Spawn Interval")]
    [SerializeField] float startMinInterval = 1.5f;
    [SerializeField] float startMaxInterval = 3f;
    [SerializeField] float endMinInterval = 0.6f;
    [SerializeField] float endMaxInterval = 1.2f;

    [Header("Enemy Frequency")]
    [SerializeField] float startEnemyChance = 0.3f;
    [SerializeField] float endEnemyChance = 0.55f;
    [SerializeField] float startAirEnemyChance = 0.15f;
    [SerializeField] float endAirEnemyChance = 0.35f;

    [Header("Collectibles")]
    [SerializeField] float startCoinChance = 0.4f;
    [SerializeField] float endCoinChance = 0.25f;
    [SerializeField] float startHealthChance = 0.05f;
    [SerializeField] float endHealthChance = 0.08f;

    Transform playerTransform;
    PlayerController playerController;
    SpawnManager spawnManager;

    float startZ;
    bool active;

    public float DifficultyT { get; private set; }

    public void Initialize(Transform player, PlayerController controller, SpawnManager spawner)
    {
        playerTransform = player;
        playerController = controller;
        spawnManager = spawner;
    }

    public void SetActive(bool enabled)
    {
        active = enabled;
        if (enabled && playerTransform != null)
        {
            startZ = playerTransform.position.z;
            DifficultyT = 0f;
            ApplyDifficulty();
        }
    }

    void Update()
    {
        if (!active || playerTransform == null) return;

        float distance = playerTransform.position.z - startZ;
        DifficultyT = Mathf.InverseLerp(rampStartDistance, rampEndDistance, distance);

        ApplyDifficulty();
    }

    void ApplyDifficulty()
    {
        float t = DifficultyT;

        if (playerController != null)
            playerController.SetForwardSpeed(Mathf.Lerp(startSpeed, maxSpeed, t));

        if (spawnManager != null)
        {
            spawnManager.SetSpawnIntervals(
                Mathf.Lerp(startMinInterval, endMinInterval, t),
                Mathf.Lerp(startMaxInterval, endMaxInterval, t)
            );
            spawnManager.SetEnemyChances(
                Mathf.Lerp(startEnemyChance, endEnemyChance, t),
                Mathf.Lerp(startAirEnemyChance, endAirEnemyChance, t)
            );
            spawnManager.SetCollectibleChances(
                Mathf.Lerp(startCoinChance, endCoinChance, t),
                Mathf.Lerp(startHealthChance, endHealthChance, t)
            );
        }
    }
}
