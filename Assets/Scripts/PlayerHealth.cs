using System;
using UnityEngine;

/// <summary>
/// Manages player health, damage, invincibility frames, and death.
/// Notifies GameManager when health reaches zero.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] int maxHealth = 3;

    [Header("Invincibility")]
    [SerializeField] float invincibilityDuration = 1.5f;
    [SerializeField] float blinkInterval = 0.15f;

    public int CurrentHealth { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsShielded { get; private set; }

    public event Action<int> OnHealthChanged;
    public event Action OnDied;

    Renderer playerRenderer;
    float invincibilityTimer;
    float blinkTimer;
    bool blinkVisible;

    void Awake()
    {
        playerRenderer = GetComponentInChildren<Renderer>();
        CurrentHealth = maxHealth;
    }

    void Update()
    {
        if (!IsInvincible) return;

        invincibilityTimer -= Time.deltaTime;

        if (invincibilityTimer <= 0f)
        {
            IsInvincible = false;
            SetVisible(true);
            return;
        }

        // Blink effect
        blinkTimer -= Time.deltaTime;
        if (blinkTimer <= 0f)
        {
            blinkVisible = !blinkVisible;
            SetVisible(blinkVisible);
            blinkTimer = blinkInterval;
        }
    }

    public void SetShielded(bool shielded)
    {
        IsShielded = shielded;
    }

    public void TakeDamage(int amount = 1, bool isEnemyDamage = false)
    {
        if (IsInvincible) return;
        if (IsShielded && isEnemyDamage) return;
        if (CurrentHealth <= 0) return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth);

        if (CurrentHealth <= 0)
        {
            OnDied?.Invoke();
            return;
        }

        // Start invincibility frames
        IsInvincible = true;
        invincibilityTimer = invincibilityDuration;
        blinkTimer = blinkInterval;
        blinkVisible = true;
    }

    public int MaxHealth => maxHealth;

    public void Heal(int amount)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsInvincible = false;
        IsShielded = false;
        SetVisible(true);
        OnHealthChanged?.Invoke(CurrentHealth);
    }

    void SetVisible(bool visible)
    {
        if (playerRenderer != null)
            playerRenderer.enabled = visible;
    }
}
