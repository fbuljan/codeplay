using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ground enemy that stands in the lane or slowly moves toward the player.
/// Extends Enemy for health/collision. Destroyed by shooting (Step 7).
/// Waits until the player clears all obstacles that were between them at spawn time.
/// </summary>
public class GroundEnemy : Enemy
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 2f;

    Transform playerTransform;
    bool movesTowardPlayer;
    bool waitingToCharge;
    List<GameObject> blockingObstacles;

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }

    /// <summary>
    /// Give the enemy a list of obstacles that stand between it and the player.
    /// Once the player passes all of them, the enemy starts charging.
    /// </summary>
    public void SetBlockingObstacles(List<GameObject> obstacles)
    {
        blockingObstacles = obstacles;
        waitingToCharge = obstacles != null && obstacles.Count > 0;
        movesTowardPlayer = false;
    }

    public override void Initialize()
    {
        base.Initialize();
        movesTowardPlayer = false;
        waitingToCharge = false;
        blockingObstacles = null;
    }

    void Update()
    {
        if (playerTransform == null) return;

        if (waitingToCharge)
        {
            if (AllObstaclesCleared())
            {
                waitingToCharge = false;
                movesTowardPlayer = true;
            }
            return;
        }

        if (!movesTowardPlayer) return;

        float direction = Mathf.Sign(playerTransform.position.z - transform.position.z);
        transform.position += Vector3.forward * (direction * moveSpeed * Time.deltaTime);
    }

    bool AllObstaclesCleared()
    {
        float playerZ = playerTransform.position.z;

        for (int i = 0; i < blockingObstacles.Count; i++)
        {
            GameObject obs = blockingObstacles[i];
            // Recycled/deactivated obstacles count as cleared
            if (!obs.activeSelf) continue;
            // Player hasn't passed this one yet
            if (obs.transform.position.z > playerZ) return false;
        }

        return true;
    }
}
