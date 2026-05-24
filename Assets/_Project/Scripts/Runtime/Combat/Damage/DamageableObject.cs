using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageableObject : BaseDamagable
{
    [Header("DamageableObject Settings")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] [Min(0f)] private float hitEffectLifetime = 3f;
    [SerializeField] private bool suppressProjectileImpactEffect = false;

    [Header("Destroy Settings")]
    [SerializeField] private bool isDestroyAfterDeath = false;
    [SerializeField] [Min(0.1f)] private float destroyDelay = 2f;

    [Header("Debug Settings")]
    [SerializeField]private bool isViewDebug = false;

    public override bool SuppressProjectileImpactEffect => suppressProjectileImpactEffect && hitEffect != null;

    protected override void TakeDamageCore(float damage)
    {
        TakeDamageCore(damage, transform.position, Vector3.up);
    }

    protected override void TakeDamageCore(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        _currentHealth = Mathf.Max(_currentHealth - damage, 0);

        if(isViewDebug)
            Debug.Log($"DamageableObject took {damage} damage. Current Health: {_currentHealth}");

        if (hitEffect != null)
        {
            GameObject effect = Instantiate(hitEffect, hitPoint, Quaternion.LookRotation(hitNormal));
            effect.transform.SetParent(transform);

            if (hitEffectLifetime > 0f)
            {
                Destroy(effect, hitEffectLifetime);
            }
        }

        if (_currentHealth <= 0 )
        {
            Die();
        }
    }

    private void Die()
    {
        if(isViewDebug)
            Debug.Log("DamageableObject died.");
        if (isDestroyAfterDeath)
        { 
            Destroy(gameObject, destroyDelay);
        }
    }
}
