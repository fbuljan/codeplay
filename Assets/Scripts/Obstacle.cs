using UnityEngine;

public enum ObstacleType { Low, High, FullLane }

/// <summary>
/// Obstacle that damages the player on collision.
/// Low obstacles must be jumped over, high obstacles must be slid under.
/// FullLane blockers span the entire lane — must lane-switch to dodge (Phase 3).
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
