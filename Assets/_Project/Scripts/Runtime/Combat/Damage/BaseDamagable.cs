using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseDamagable : MonoBehaviour
{
    [Header("BaseDamagable Settings")]
    [SerializeField] protected float maxHealth = 100f;

    protected float _currentHealth;
    private bool _isDead;

    public Action<float> OnHealthChanged;
    public Action OnDeath;

    public virtual bool SuppressProjectileImpactEffect => false;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => _isDead;

    protected virtual void Awake()
    {
        maxHealth = Mathf.Max(0f, maxHealth);
        _currentHealth = maxHealth;
        _isDead = _currentHealth <= 0f;
    }

    public void TakeDamage(float damage) 
    {
        if (!CanReceiveDamage(damage))
            return;

        TakeDamageCore(damage);

        NotifyHealthChangedAndDeath();
    }

    public void TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!CanReceiveDamage(damage))
            return;

        TakeDamageCore(damage, hitPoint, hitNormal);

        NotifyHealthChangedAndDeath();
    }

    protected virtual bool CanReceiveDamage(float damage)
    {
        return !_isDead && damage > 0f;
    }

    protected void NotifyHealthChangedAndDeath()
    {
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);
        OnHealthChanged?.Invoke(_currentHealth);

        if (!_isDead && _currentHealth <= 0f)
        {
            _isDead = true;
            OnDeath?.Invoke();
        }
    }

    protected abstract void TakeDamageCore(float damage);
    protected abstract void TakeDamageCore(float damage, Vector3 hitPoint, Vector3 hitNormal);

}
