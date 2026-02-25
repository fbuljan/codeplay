using UnityEngine;

/// <summary>
/// Projectile fired by air enemies toward the player.
/// Blocked by shield, damages player on hit.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    [SerializeField] float speed = 20f;
    [SerializeField] float maxLifetime = 5f;

    public float Speed => speed;

    Vector3 direction;
    float timer;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void Fire(Vector3 from, Vector3 targetPosition)
    {
        transform.position = from;
        direction = (targetPosition - from).normalized;
        timer = maxLifetime;
        gameObject.SetActive(true);
    }

    void Update()
    {
        transform.position += direction * (speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0f)
            gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        health.TakeDamage(1, isEnemyDamage: true, attackerPosition: transform.position);
        gameObject.SetActive(false);
    }
}
