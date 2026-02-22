using UnityEngine;

/// <summary>
/// Air enemy that flies alongside the player at a fixed Y height.
/// Matches player Z speed, shoots projectiles at intervals.
/// Destroyed by player shooting. Despawns after a duration.
/// </summary>
public class AirEnemy : Enemy
{
    [Header("Flight")]
    [SerializeField] float hoverHeight = 5f;
    [SerializeField] float xOffset = 3f;
    [SerializeField] float zOffset = 15f;
    [SerializeField] float despawnTime = 8f;

    [Header("Shooting")]
    [SerializeField] float shootInterval = 2f;
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] int projectilePoolSize = 5;

    Transform playerTransform;
    float shootTimer;
    float despawnTimer;
    bool isActive;
    GameObject[] projectilePool;

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }

    public void SetLane(float laneX)
    {
        xOffset = laneX;
    }

    public override void Initialize()
    {
        base.Initialize();
        shootTimer = shootInterval;
        despawnTimer = despawnTime;
        isActive = true;
        InitProjectilePool();
    }

    void Update()
    {
        if (!isActive || playerTransform == null) return;

        // Follow player Z ahead, stay at hover height with X offset
        transform.position = new Vector3(
            xOffset,
            hoverHeight,
            playerTransform.position.z + zOffset
        );

        // Shoot at intervals
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            ShootAtPlayer();
            shootTimer = shootInterval;
        }

        // Despawn timer
        despawnTimer -= Time.deltaTime;
        if (despawnTimer <= 0f)
        {
            isActive = false;
            gameObject.SetActive(false);
        }
    }

    void ShootAtPlayer()
    {
        if (projectilePool == null) return;

        GameObject obj = GetInactiveProjectile();
        if (obj == null) return;

        var proj = obj.GetComponent<EnemyProjectile>();
        Vector3 playerPos = playerTransform.position;

        var playerRb = playerTransform.GetComponent<Rigidbody>();
        Vector3 playerVelocity = playerRb != null ? playerRb.velocity : Vector3.forward * 10f;

        Vector3 targetPos = SolveIntercept(transform.position, playerPos, playerVelocity, proj.Speed);
        proj.Fire(transform.position, targetPos);
    }

    /// <summary>
    /// Solves the quadratic intercept: finds where a projectile at constant speed
    /// will meet a target moving at constant velocity.
    /// </summary>
    Vector3 SolveIntercept(Vector3 firePos, Vector3 targetPos, Vector3 targetVel, float projectileSpeed)
    {
        Vector3 d = targetPos - firePos;
        float a = Vector3.Dot(targetVel, targetVel) - projectileSpeed * projectileSpeed;
        float b = 2f * Vector3.Dot(d, targetVel);
        float c = Vector3.Dot(d, d);

        float discriminant = b * b - 4f * a * c;

        if (discriminant < 0f || Mathf.Approximately(a, 0f))
            return targetPos; // No solution, just aim directly

        float sqrtDisc = Mathf.Sqrt(discriminant);
        float t1 = (-b - sqrtDisc) / (2f * a);
        float t2 = (-b + sqrtDisc) / (2f * a);

        // Pick the smallest positive time
        float t = (t1 > 0f && t2 > 0f) ? Mathf.Min(t1, t2) :
                  (t1 > 0f) ? t1 :
                  (t2 > 0f) ? t2 : 0f;

        return targetPos + targetVel * t;
    }

    void InitProjectilePool()
    {
        if (projectilePool != null) return;
        if (projectilePrefab == null) return;

        projectilePool = new GameObject[projectilePoolSize];
        for (int i = 0; i < projectilePoolSize; i++)
        {
            var obj = Instantiate(projectilePrefab);
            obj.name = $"EnemyProjectile_{gameObject.name}_{i}";
            obj.SetActive(false);
            projectilePool[i] = obj;
        }
    }

    GameObject GetInactiveProjectile()
    {
        for (int i = 0; i < projectilePool.Length; i++)
        {
            if (!projectilePool[i].activeSelf)
                return projectilePool[i];
        }
        return null;
    }
}
