using System;
using UnityEngine;

/// <summary>
/// Base enemy class. Has health, damages player on collision (unless shielded).
/// Destroyed by player projectiles (Step 7).
/// </summary>
public class Enemy : MonoBehaviour
{
    [SerializeField] int maxHealth = 1;

    public int CurrentHealth { get; private set; }

    public event Action<Enemy> OnDestroyed;

    public virtual void Initialize()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount = 1)
    {
        CurrentHealth -= amount;

        if (CurrentHealth <= 0)
            Die();
    }

    void Die()
    {
        OnDestroyed?.Invoke(this);
        gameObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponent<PlayerHealth>();
        if (health != null)
            health.TakeDamage();
    }
}
