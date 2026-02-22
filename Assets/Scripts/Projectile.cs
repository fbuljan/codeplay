using UnityEngine;

/// <summary>
/// Player projectile that flies forward and damages enemies on contact.
/// Deactivates on hit or after reaching max range.
/// Requires a kinematic Rigidbody + trigger collider on the prefab.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [SerializeField] float speed = 40f;
    [SerializeField] float maxRange = 100f;

    float spawnZ;

    void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void Fire(Vector3 position)
    {
        transform.position = position;
        spawnZ = position.z;
        gameObject.SetActive(true);
    }

    void Update()
    {
        transform.position += Vector3.forward * (speed * Time.deltaTime);

        if (transform.position.z - spawnZ > maxRange)
            gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var enemy = other.GetComponent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage();
            gameObject.SetActive(false);
            return;
        }

        var obstacle = other.GetComponent<Obstacle>();
        if (obstacle != null)
        {
            gameObject.SetActive(false);
        }
    }
}
