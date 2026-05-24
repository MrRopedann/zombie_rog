using System;
using UnityEngine;

public class ZombieHealth : BaseDamagable
{
    [Header("Zombie Health")]
    [SerializeField] private GameObject hitEffect;
    [SerializeField] [Min(0f)] private float hitEffectLifetime = 3f;
    [SerializeField] private bool suppressProjectileImpactEffect = false;
    [SerializeField] private bool debugDamage = false;

    [Header("Damage Numbers")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private GameObject damageNumberPrefab;
    [SerializeField] [Min(0.05f)] private float damageNumberLifetime = 0.9f;
    [SerializeField] [Min(0f)] private float damageNumberFloatSpeed = 1.25f;
    [SerializeField] [Min(0f)] private float damageNumberHitNormalOffset = 0.08f;
    [SerializeField] private Vector3 damageNumberWorldOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] [Min(0.1f)] private float fallbackDamageNumberFontSize = 3f;
    [SerializeField] private Color damageNumberColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private string damageNumberFormat = "0";

    [Header("Bone Hitboxes")]
    [SerializeField] private bool autoCreateBoneHitboxes = true;
    [SerializeField] [Min(0f)] private float headDamageMultiplier = 3f;
    [SerializeField] [Min(0f)] private float torsoDamageMultiplier = 1f;
    [SerializeField] [Min(0f)] private float armDamageMultiplier = 0.5f;
    [SerializeField] [Min(0f)] private float legDamageMultiplier = 0.75f;
    [SerializeField] [Min(0.1f)] private float hitboxScale = 1f;

    public event Action<float, Vector3, Vector3> OnDamageTaken;

    public override bool SuppressProjectileImpactEffect => suppressProjectileImpactEffect && hitEffect != null;

    protected override void Awake()
    {
        if (maxHealth <= 0f)
            maxHealth = 100f;

        base.Awake();

        if (autoCreateBoneHitboxes)
            ZombieHitboxBuilder.EnsureHitboxes(this, CreateHitboxSettings());
    }

    protected override void TakeDamageCore(float damage)
    {
        TakeDamageCore(damage, transform.position + Vector3.up, Vector3.up);
    }

    protected override void TakeDamageCore(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (CoopGameplaySync.TryRequestZombieDamage(this, damage, hitPoint, hitNormal))
            return;

        ApplyDamageInternal(damage, hitPoint, hitNormal);
    }

    public void ApplyNetworkDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsDead || damage <= 0f)
            return;

        ApplyDamageInternal(damage, hitPoint, hitNormal);
        NotifyHealthChangedAndDeath();
    }

    public void SetNetworkHealth(float health)
    {
        float nextHealth = Mathf.Clamp(health, 0f, maxHealth);
        if (Mathf.Approximately(_currentHealth, nextHealth))
            return;

        _currentHealth = nextHealth;
        OnHealthChanged?.Invoke(_currentHealth);

        if (_currentHealth <= 0f)
            NotifyHealthChangedAndDeath();
    }

    private void ApplyDamageInternal(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        _currentHealth = Mathf.Max(_currentHealth - damage, 0f);

        if (debugDamage)
            Debug.Log($"{name} took {damage} damage. Health: {_currentHealth}/{maxHealth}", this);

        SpawnHitEffect(hitPoint, hitNormal);
        SpawnDamageNumber(damage, hitPoint, hitNormal);
        OnDamageTaken?.Invoke(damage, hitPoint, hitNormal);
    }

    private void SpawnHitEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitEffect == null)
            return;

        if (hitNormal.sqrMagnitude <= 0.001f)
            hitNormal = Vector3.up;

        GameObject effect = Instantiate(hitEffect, hitPoint, Quaternion.LookRotation(hitNormal.normalized));

        if (hitEffectLifetime > 0f)
            Destroy(effect, hitEffectLifetime);
    }

    private void SpawnDamageNumber(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!showDamageNumbers || damage <= 0f)
            return;

        if (hitNormal.sqrMagnitude <= 0.001f)
            hitNormal = Vector3.up;

        Vector3 position = hitPoint
            + hitNormal.normalized * damageNumberHitNormalOffset
            + damageNumberWorldOffset;
        Quaternion rotation = Quaternion.identity;

        if (Camera.main != null)
        {
            Vector3 cameraDirection = position - Camera.main.transform.position;

            if (cameraDirection.sqrMagnitude > 0.001f)
                rotation = Quaternion.LookRotation(cameraDirection.normalized, Vector3.up);
        }

        ZombieDamageNumberPopup popup = null;

        if (damageNumberPrefab != null)
        {
            GameObject instance = Instantiate(damageNumberPrefab, position, rotation);
            popup = instance.GetComponent<ZombieDamageNumberPopup>()
                ?? instance.GetComponentInChildren<ZombieDamageNumberPopup>(true)
                ?? instance.AddComponent<ZombieDamageNumberPopup>();
        }
        else
        {
            popup = ZombieDamageNumberPopup.CreateDefault(position, rotation, fallbackDamageNumberFontSize);
        }

        popup.Initialize(damage, damageNumberColor, damageNumberLifetime, damageNumberFloatSpeed, damageNumberFormat);
    }

    private ZombieHitboxBuilder.Settings CreateHitboxSettings()
    {
        return new ZombieHitboxBuilder.Settings
        {
            headMultiplier = headDamageMultiplier,
            torsoMultiplier = torsoDamageMultiplier,
            armMultiplier = armDamageMultiplier,
            legMultiplier = legDamageMultiplier,
            scale = hitboxScale
        };
    }

    private void OnValidate()
    {
        headDamageMultiplier = Mathf.Max(0f, headDamageMultiplier);
        torsoDamageMultiplier = Mathf.Max(0f, torsoDamageMultiplier);
        armDamageMultiplier = Mathf.Max(0f, armDamageMultiplier);
        legDamageMultiplier = Mathf.Max(0f, legDamageMultiplier);
        hitboxScale = Mathf.Max(0.1f, hitboxScale);
        damageNumberLifetime = Mathf.Max(0.05f, damageNumberLifetime);
        damageNumberFloatSpeed = Mathf.Max(0f, damageNumberFloatSpeed);
        damageNumberHitNormalOffset = Mathf.Max(0f, damageNumberHitNormalOffset);
        fallbackDamageNumberFontSize = Mathf.Max(0.1f, fallbackDamageNumberFontSize);
    }
}
