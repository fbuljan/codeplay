using UnityEngine;

/// <summary>
/// Rare collectible that restores 1 HP to the player.
/// Bobs up and down visually. Collected on trigger contact.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] float bobHeight = 0.3f;

    Vector3 basePosition;

    void OnEnable()
    {
        basePosition = transform.position;
    }

    void Update()
    {
        Vector3 pos = basePosition;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = pos;
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        if (health.CurrentHealth < health.MaxHealth)
            health.Heal(1);

        if (GameManager.Instance != null)
            GameManager.Instance.OnHealthPickup();

        gameObject.SetActive(false);
    }
}
