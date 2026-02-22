using UnityEngine;

public enum ObstacleType { Low, High }

/// <summary>
/// Obstacle that damages the player on collision.
/// Low obstacles must be jumped over, high obstacles must be slid under.
/// </summary>
public class Obstacle : MonoBehaviour
{
    public ObstacleType Type { get; private set; }

    public void Initialize(ObstacleType type)
    {
        Type = type;
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<PlayerHealth>();
        if (health != null)
            health.TakeDamage();
    }
}
