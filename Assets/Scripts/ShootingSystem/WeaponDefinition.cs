using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon Definition", menuName = "Shooting/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string weaponID = "weapon";
    [SerializeField] private string displayName = "Weapon";
    [SerializeField] private GameObject weaponPrefab;

    [Header("Fire")]
    [SerializeField] private WeaponFireMode fireMode = WeaponFireMode.Automatic;
    [SerializeField, Min(0.01f)] private float fireInterval = 0.2f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(1)] private int ammoPerShot = 1;

    [Header("Ammo")]
    [SerializeField] private bool infiniteAmmo = false;
    [SerializeField] private bool infiniteReserveAmmo = false;
    [SerializeField, Min(1)] private int magazineSize = 30;
    [SerializeField, Min(-1)] private int startingMagazineAmmo = -1;
    [SerializeField, Min(0)] private int startingReserveAmmo = 90;
    [SerializeField] private bool autoReloadOnEmpty = true;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;
    [SerializeField, Min(0.05f)] private float dryFireInterval = 0.25f;

    [Header("Aim")]
    [SerializeField, Min(0.1f)] private float range = 1000f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField, Min(0f)] private float spreadAngle = 0f;

    [Header("Projectile")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 715f;
    [SerializeField, Min(0f)] private float projectileSpawnOffset = 0.05f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;

    [Header("Ballistics")]
    [SerializeField, Min(0.001f)] private float projectileMass = 0.008f;
    [SerializeField, Min(0f)] private float projectileDrag = 0.01f;
    [SerializeField, Min(0f)] private float projectileAngularDrag = 0.05f;
    [SerializeField] private bool projectileUseGravity = true;
    [SerializeField] private bool alignProjectileToVelocity = true;

    [Header("Impact Effects")]
    [SerializeField] private GameObject[] impactEffectPrefabs;
    [SerializeField, Min(0f)] private float impactEffectLifetime = 30f;
    [SerializeField, Min(0f)] private float impactSurfaceOffset = 0.01f;
    [SerializeField] private bool parentImpactEffectToHitObject = true;

    [Header("Shot Effects")]
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField, Min(0f)] private float muzzleFlashLifetime = 1f;
    [SerializeField] private bool parentMuzzleFlashToMuzzle = false;
    [SerializeField] private GameObject casingPrefab;
    [SerializeField, Min(0f)] private float casingLifetime = 8f;
    [SerializeField] private Vector3 casingEjectLocalDirection = new Vector3(1f, 0.45f, -0.15f);
    [SerializeField, Min(0f)] private float casingEjectForce = 2.2f;
    [SerializeField, Min(0f)] private float casingRandomForce = 0.35f;
    [SerializeField, Min(0f)] private float casingTorque = 12f;

    [Header("Audio")]
    [SerializeField] private AudioClip[] shootSounds;
    [SerializeField] private AudioClip[] emptyMagazineSounds;
    [SerializeField] private AudioClip[] reloadStartSounds;
    [SerializeField] private AudioClip[] reloadCompleteSounds;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float emptyVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float reloadVolume = 1f;
    [SerializeField, Range(0f, 0.5f)] private float pitchRandomness = 0.05f;

    [Header("AI Noise")]
    [SerializeField] private bool emitGunshotNoise = true;
    [SerializeField, Min(0f)] private float gunshotNoiseRadius = 45f;
    [SerializeField, Range(0f, 1f)] private float gunshotSuspicion = 0.85f;

    public string WeaponID => weaponID;
    public string DisplayName => displayName;
    public GameObject WeaponPrefab => weaponPrefab;
    public WeaponFireMode FireMode => fireMode;
    public bool Automatic => fireMode == WeaponFireMode.Automatic;
    public float FireInterval => fireInterval;
    public float Damage => damage;
    public int AmmoPerShot => ammoPerShot;
    public bool InfiniteAmmo => infiniteAmmo;
    public bool InfiniteReserveAmmo => infiniteReserveAmmo;
    public int MagazineSize => magazineSize;
    public int StartingMagazineAmmo => startingMagazineAmmo < 0 ? magazineSize : Mathf.Min(startingMagazineAmmo, magazineSize);
    public int StartingReserveAmmo => startingReserveAmmo;
    public bool AutoReloadOnEmpty => autoReloadOnEmpty;
    public float ReloadDuration => reloadDuration;
    public float DryFireInterval => dryFireInterval;
    public float Range => range;
    public LayerMask HitMask => hitMask;
    public float SpreadAngle => spreadAngle;
    public Projectile ProjectilePrefab => projectilePrefab;
    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileSpawnOffset => projectileSpawnOffset;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileMass => projectileMass;
    public float ProjectileDrag => projectileDrag;
    public float ProjectileAngularDrag => projectileAngularDrag;
    public bool ProjectileUseGravity => projectileUseGravity;
    public bool AlignProjectileToVelocity => alignProjectileToVelocity;
    public GameObject[] ImpactEffectPrefabs => impactEffectPrefabs;
    public float ImpactEffectLifetime => impactEffectLifetime;
    public float ImpactSurfaceOffset => impactSurfaceOffset;
    public bool ParentImpactEffectToHitObject => parentImpactEffectToHitObject;
    public GameObject MuzzleFlashPrefab => muzzleFlashPrefab;
    public float MuzzleFlashLifetime => muzzleFlashLifetime;
    public bool ParentMuzzleFlashToMuzzle => parentMuzzleFlashToMuzzle;
    public GameObject CasingPrefab => casingPrefab;
    public float CasingLifetime => casingLifetime;
    public Vector3 CasingEjectLocalDirection => casingEjectLocalDirection;
    public float CasingEjectForce => casingEjectForce;
    public float CasingRandomForce => casingRandomForce;
    public float CasingTorque => casingTorque;
    public AudioClip[] ShootSounds => shootSounds;
    public AudioClip[] EmptyMagazineSounds => emptyMagazineSounds;
    public AudioClip[] ReloadStartSounds => reloadStartSounds;
    public AudioClip[] ReloadCompleteSounds => reloadCompleteSounds;
    public float ShootVolume => shootVolume;
    public float EmptyVolume => emptyVolume;
    public float ReloadVolume => reloadVolume;
    public float PitchRandomness => pitchRandomness;
    public bool EmitGunshotNoise => emitGunshotNoise;
    public float GunshotNoiseRadius => gunshotNoiseRadius;
    public float GunshotSuspicion => gunshotSuspicion;

    private void OnValidate()
    {
        fireInterval = Mathf.Max(0.01f, fireInterval);
        damage = Mathf.Max(0f, damage);
        ammoPerShot = Mathf.Max(1, ammoPerShot);
        magazineSize = Mathf.Max(1, magazineSize);
        startingMagazineAmmo = Mathf.Clamp(startingMagazineAmmo, -1, magazineSize);
        startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
        reloadDuration = Mathf.Max(0.01f, reloadDuration);
        dryFireInterval = Mathf.Max(0.05f, dryFireInterval);
        range = Mathf.Max(0.1f, range);
        spreadAngle = Mathf.Max(0f, spreadAngle);
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileSpawnOffset = Mathf.Max(0f, projectileSpawnOffset);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        projectileMass = Mathf.Max(0.001f, projectileMass);
        projectileDrag = Mathf.Max(0f, projectileDrag);
        projectileAngularDrag = Mathf.Max(0f, projectileAngularDrag);
        impactEffectLifetime = Mathf.Max(0f, impactEffectLifetime);
        impactSurfaceOffset = Mathf.Max(0f, impactSurfaceOffset);
        muzzleFlashLifetime = Mathf.Max(0f, muzzleFlashLifetime);
        casingLifetime = Mathf.Max(0f, casingLifetime);
        casingEjectForce = Mathf.Max(0f, casingEjectForce);
        casingRandomForce = Mathf.Max(0f, casingRandomForce);
        casingTorque = Mathf.Max(0f, casingTorque);
        gunshotNoiseRadius = Mathf.Max(0f, gunshotNoiseRadius);
        gunshotSuspicion = Mathf.Clamp01(gunshotSuspicion);
    }
}
