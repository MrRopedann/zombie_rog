using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
[RequireComponent(typeof(Weapon))]
public class WeaponShotEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private WeaponIKGrip weaponIKGrip;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform casingEjectionPoint;

    [HideInInspector] [SerializeField] private GameObject muzzleFlashPrefab;
    [HideInInspector] [SerializeField] private GameObject casingPrefab;

    private Weapon _subscribedWeapon;
    private bool _isSubscribed;
    private int _lastHandledShotFrame = -1;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (weapon == null)
        {
            return;
        }

        if (_isSubscribed && _subscribedWeapon == weapon)
        {
            return;
        }

        Unsubscribe();
        weapon.ShotFired += HandleShotFired;
        _subscribedWeapon = weapon;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (_isSubscribed && _subscribedWeapon != null)
        {
            _subscribedWeapon.ShotFired -= HandleShotFired;
        }

        _subscribedWeapon = null;
        _isSubscribed = false;
    }

    private void ResolveReferences()
    {
        if (weapon == null)
        {
            weapon = GetComponent<Weapon>();
        }

        if (weaponIKGrip == null)
        {
            weaponIKGrip = GetComponent<WeaponIKGrip>() ?? GetComponentInChildren<WeaponIKGrip>(true);
        }

        if (muzzle == null && weaponIKGrip != null)
        {
            muzzle = weaponIKGrip.Muzzle;
        }

        if (casingEjectionPoint == null && weaponIKGrip != null)
        {
            casingEjectionPoint = weaponIKGrip.CasingEjectionPoint;
        }
    }

    private void HandleShotFired(Weapon firedWeapon)
    {
        if (firedWeapon == null || (_subscribedWeapon != null && firedWeapon != _subscribedWeapon))
        {
            return;
        }

        if (_lastHandledShotFrame == Time.frameCount)
        {
            return;
        }

        _lastHandledShotFrame = Time.frameCount;
        SpawnMuzzleFlash(firedWeapon);
        SpawnCasing(firedWeapon);
    }

    private void SpawnMuzzleFlash(Weapon firedWeapon)
    {
        GameObject prefab = firedWeapon.MuzzleFlashPrefab != null
            ? firedWeapon.MuzzleFlashPrefab
            : muzzleFlashPrefab;

        if (prefab == null || muzzle == null)
        {
            return;
        }

        GameObject instance;

        if (firedWeapon.ParentMuzzleFlashToMuzzle)
        {
            instance = Instantiate(prefab, muzzle);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            instance = Instantiate(prefab, muzzle.position, muzzle.rotation);
        }

        PlayMuzzleFlashOnce(instance);
        DestroyAfterLifetime(instance, firedWeapon.MuzzleFlashLifetime);
    }

    private void SpawnCasing(Weapon firedWeapon)
    {
        GameObject prefab = firedWeapon.CasingPrefab != null ? firedWeapon.CasingPrefab : casingPrefab;
        Transform spawnPoint = casingEjectionPoint != null ? casingEjectionPoint : muzzle;

        if (prefab == null || spawnPoint == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        Rigidbody casingRigidbody = instance.GetComponent<Rigidbody>();

        if (casingRigidbody == null)
        {
            casingRigidbody = instance.AddComponent<Rigidbody>();
        }

        EnsureCollider(instance);

        Vector3 localDirection = firedWeapon.CasingEjectLocalDirection;
        Vector3 direction = localDirection.sqrMagnitude > 0.0001f
            ? spawnPoint.TransformDirection(localDirection.normalized)
            : spawnPoint.right;
        direction += Random.insideUnitSphere * firedWeapon.CasingRandomForce;

        casingRigidbody.AddForce(direction.normalized * firedWeapon.CasingEjectForce, ForceMode.VelocityChange);
        casingRigidbody.AddTorque(Random.insideUnitSphere * firedWeapon.CasingTorque, ForceMode.VelocityChange);

        DestroyAfterLifetime(instance, firedWeapon.CasingLifetime);
    }

    private static void EnsureCollider(GameObject instance)
    {
        if (instance.GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        instance.AddComponent<BoxCollider>();
    }

    private static void PlayMuzzleFlashOnce(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = false;
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }
    }

    private static void DestroyAfterLifetime(GameObject instance, float lifetime)
    {
        if (instance == null || lifetime <= 0f)
        {
            return;
        }

        Destroy(instance, lifetime);
    }
}
