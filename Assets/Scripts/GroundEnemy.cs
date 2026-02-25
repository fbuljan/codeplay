using UnityEngine;

/// <summary>
/// Ground enemy that charges toward the player when within detection range.
/// Accelerates as it gets closer for an aggressive feel.
/// </summary>
public class GroundEnemy : Enemy
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float chargeSpeed = 8f;

    [Header("Detection")]
    [SerializeField] float detectionRange = 20f;

    Transform playerTransform;
    bool charging;

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }

    public override void Initialize()
    {
        base.Initialize();
        charging = false;
    }

    void Update()
    {
        if (playerTransform == null) return;

        if (!charging)
        {
            float dist = transform.position.z - playerTransform.position.z;
            if (dist <= detectionRange)
                charging = true;
            else
                return;
        }

        // Always face the player while charging (model faces -Z, so look away then flip 180)
        Vector3 dirToPlayer = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z) - transform.position;
        if (dirToPlayer.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(-dirToPlayer);

        // Accelerate as we get closer — lerp from moveSpeed to chargeSpeed
        float distance = Mathf.Abs(transform.position.z - playerTransform.position.z);
        float t = 1f - Mathf.Clamp01(distance / detectionRange);
        float speed = Mathf.Lerp(moveSpeed, chargeSpeed, t);

        float direction = Mathf.Sign(playerTransform.position.z - transform.position.z);
        transform.position += Vector3.forward * (direction * speed * Time.deltaTime);
    }
}
