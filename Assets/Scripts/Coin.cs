using UnityEngine;

/// <summary>
/// Collectible coin that gives score and feeds the multiplier.
/// Rotates visually. Collected on trigger contact with player.
/// </summary>
public class Coin : MonoBehaviour
{
    [SerializeField] float rotateSpeed = 180f;
    [SerializeField] int scoreValue = 50;

    public int ScoreValue => scoreValue;

    void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<PlayerHealth>();
        if (health == null) return;

        var fx = ParticleEffectManager.Instance;
        if (fx != null)
            fx.PlayCoinCollect(transform.position);

        if (GameManager.Instance != null)
            GameManager.Instance.OnCoinCollected(this);

        gameObject.SetActive(false);
    }
}
