using System;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class Weapon : MonoBehaviour
{
    [Header("Definition")]
    [SerializeField] private WeaponDefinition weaponDefinition;

    [Header("Runtime References")]
    [SerializeField] private AudioSource audioSource;

    [HideInInspector] [SerializeField] protected float damage = 10f;
    [HideInInspector] [SerializeField] protected float fireRate = 0.2f;
    [HideInInspector] [SerializeField] protected bool automatic = true;
    [HideInInspector] [SerializeField] private bool infiniteAmmo = false;
    [HideInInspector] [SerializeField] private bool infiniteReserveAmmo = false;
    [HideInInspector] [SerializeField] [Min(1)] private int magazineSize = 30;
    [HideInInspector] [SerializeField] [Min(-1)] private int startingMagazineAmmo = -1;
    [HideInInspector] [SerializeField] [Min(0)] private int startingReserveAmmo = 90;
    [HideInInspector] [SerializeField] private bool autoReloadOnEmpty = true;
    [HideInInspector] [SerializeField] [Min(0.01f)] private float reloadDuration = 1.5f;
    [HideInInspector] [SerializeField] [Min(0.05f)] private float dryFireInterval = 0.25f;
    [HideInInspector] [SerializeField] private AudioClip[] shootSounds;
    [HideInInspector] [SerializeField] private AudioClip[] emptyMagazineSounds;
    [HideInInspector] [SerializeField] private AudioClip[] reloadStartSounds;
    [HideInInspector] [SerializeField] private AudioClip[] reloadCompleteSounds;
    [HideInInspector] [SerializeField] [Range(0f, 1f)] private float shootVolume = 1f;
    [HideInInspector] [SerializeField] [Range(0f, 1f)] private float emptyVolume = 0.9f;
    [HideInInspector] [SerializeField] [Range(0f, 1f)] private float reloadVolume = 1f;
    [HideInInspector] [SerializeField] [Range(0f, 0.5f)] private float pitchRandomness = 0.05f;
    [HideInInspector] [SerializeField] private bool emitGunshotNoise = true;
    [HideInInspector] [SerializeField] [Min(0f)] private float gunshotNoiseRadius = 45f;
    [HideInInspector] [SerializeField] [Range(0f, 1f)] private float gunshotSuspicion = 0.85f;

    private float _nextFireTime;
    private float _nextDryFireTime;
    private int _lastShotFrame = -1;
    private Coroutine _reloadCoroutine;

    private IShooter _shooter;

    public event Action<Weapon> ShotFired;
    public event Action<Weapon> DryFired;
    public event Action<Weapon> ReloadStarted;
    public event Action<Weapon> ReloadCompleted;
    public event Action<Weapon, int, int> AmmoChanged;

    public WeaponDefinition Definition => weaponDefinition;
    public bool HasDefinition => weaponDefinition != null;
    public float Damage => weaponDefinition != null ? weaponDefinition.Damage : damage;
    public float FireInterval => weaponDefinition != null ? weaponDefinition.FireInterval : fireRate;
    public bool Automatic => weaponDefinition != null ? weaponDefinition.Automatic : automatic;
    public bool InfiniteAmmo => weaponDefinition != null ? weaponDefinition.InfiniteAmmo : infiniteAmmo;
    public bool InfiniteReserveAmmo => weaponDefinition != null ? weaponDefinition.InfiniteReserveAmmo : infiniteReserveAmmo;
    public int MagazineSize => weaponDefinition != null ? weaponDefinition.MagazineSize : magazineSize;
    public int AmmoPerShot => weaponDefinition != null ? weaponDefinition.AmmoPerShot : 1;
    public bool AutoReloadOnEmpty => weaponDefinition != null ? weaponDefinition.AutoReloadOnEmpty : autoReloadOnEmpty;
    public float ReloadDuration => weaponDefinition != null ? weaponDefinition.ReloadDuration : reloadDuration;
    public float DryFireInterval => weaponDefinition != null ? weaponDefinition.DryFireInterval : dryFireInterval;
    public int CurrentAmmo { get; private set; }
    public int ReserveAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsMagazineFull => CurrentAmmo >= MagazineSize;
    public bool HasAmmoInMagazine => InfiniteAmmo || CurrentAmmo >= AmmoPerShot;
    public bool HasAmmoInReserve => InfiniteAmmo || InfiniteReserveAmmo || ReserveAmmo > 0;
    public float Range => weaponDefinition != null ? weaponDefinition.Range : 1000f;
    public LayerMask HitMask
    {
        get
        {
            if (weaponDefinition != null)
            {
                return weaponDefinition.HitMask;
            }

            LayerMask allLayers = ~0;
            return allLayers;
        }
    }
    public float SpreadAngle => weaponDefinition != null ? weaponDefinition.SpreadAngle : 0f;
    public Projectile ProjectilePrefab => weaponDefinition != null ? weaponDefinition.ProjectilePrefab : null;
    public float ProjectileSpeed => weaponDefinition != null ? weaponDefinition.ProjectileSpeed : 30f;
    public float ProjectileSpawnOffset => weaponDefinition != null ? weaponDefinition.ProjectileSpawnOffset : 0.05f;
    public float ProjectileLifetime => weaponDefinition != null ? weaponDefinition.ProjectileLifetime : 5f;
    public float ProjectileMass => weaponDefinition != null ? weaponDefinition.ProjectileMass : 0.008f;
    public float ProjectileDrag => weaponDefinition != null ? weaponDefinition.ProjectileDrag : 0.01f;
    public float ProjectileAngularDrag => weaponDefinition != null ? weaponDefinition.ProjectileAngularDrag : 0.05f;
    public bool ProjectileUseGravity => weaponDefinition == null || weaponDefinition.ProjectileUseGravity;
    public bool AlignProjectileToVelocity => weaponDefinition == null || weaponDefinition.AlignProjectileToVelocity;
    public GameObject[] ImpactEffectPrefabs => weaponDefinition != null ? weaponDefinition.ImpactEffectPrefabs : null;
    public float ImpactEffectLifetime => weaponDefinition != null ? weaponDefinition.ImpactEffectLifetime : 30f;
    public float ImpactSurfaceOffset => weaponDefinition != null ? weaponDefinition.ImpactSurfaceOffset : 0.01f;
    public bool ParentImpactEffectToHitObject => weaponDefinition == null || weaponDefinition.ParentImpactEffectToHitObject;
    public GameObject MuzzleFlashPrefab => weaponDefinition != null ? weaponDefinition.MuzzleFlashPrefab : null;
    public float MuzzleFlashLifetime => weaponDefinition != null ? weaponDefinition.MuzzleFlashLifetime : 1f;
    public bool ParentMuzzleFlashToMuzzle => weaponDefinition != null && weaponDefinition.ParentMuzzleFlashToMuzzle;
    public GameObject CasingPrefab => weaponDefinition != null ? weaponDefinition.CasingPrefab : null;
    public float CasingLifetime => weaponDefinition != null ? weaponDefinition.CasingLifetime : 8f;
    public Vector3 CasingEjectLocalDirection => weaponDefinition != null ? weaponDefinition.CasingEjectLocalDirection : new Vector3(1f, 0.45f, -0.15f);
    public float CasingEjectForce => weaponDefinition != null ? weaponDefinition.CasingEjectForce : 2.2f;
    public float CasingRandomForce => weaponDefinition != null ? weaponDefinition.CasingRandomForce : 0.35f;
    public float CasingTorque => weaponDefinition != null ? weaponDefinition.CasingTorque : 12f;
    public bool EmitGunshotNoise => weaponDefinition != null ? weaponDefinition.EmitGunshotNoise : emitGunshotNoise;
    public float GunshotNoiseRadius => weaponDefinition != null ? weaponDefinition.GunshotNoiseRadius : gunshotNoiseRadius;
    public float GunshotSuspicion => weaponDefinition != null ? weaponDefinition.GunshotSuspicion : gunshotSuspicion;

    private void Awake()
    {
        _shooter = GetComponent<IShooter>();
        ResolveAudioSource();
        ResetAmmo();
    }

    private void OnDisable()
    {
        CancelReload();
    }

    private void OnValidate()
    {
        fireRate = Mathf.Max(0.01f, fireRate);
        damage = Mathf.Max(0f, damage);
        magazineSize = Mathf.Max(1, magazineSize);
        startingMagazineAmmo = Mathf.Clamp(startingMagazineAmmo, -1, magazineSize);
        startingReserveAmmo = Mathf.Max(0, startingReserveAmmo);
        reloadDuration = Mathf.Max(0.01f, reloadDuration);
        dryFireInterval = Mathf.Max(0.05f, dryFireInterval);
        gunshotNoiseRadius = Mathf.Max(0f, gunshotNoiseRadius);
        gunshotSuspicion = Mathf.Clamp01(gunshotSuspicion);
    }

    public bool TryShoot()
    {
        if (_shooter == null || IsReloading || Time.time < _nextFireTime || _lastShotFrame == Time.frameCount)
        {
            return false;
        }

        if (!HasAmmoInMagazine)
        {
            DryFire();
            return false;
        }

        if (!_shooter.Shoot())
        {
            return false;
        }

        ConsumeAmmo();
        _lastShotFrame = Time.frameCount;
        _nextFireTime = Time.time + FireInterval;
        PlaySound(GetShootSounds(), GetShootVolume());
        ReportGunshotNoise();
        ShotFired?.Invoke(this);
        return true;
    }

    public bool TryReload()
    {
        if (IsReloading || InfiniteAmmo || IsMagazineFull)
        {
            return false;
        }

        if (!HasAmmoInReserve)
        {
            PlayEmptyFeedback();
            return false;
        }

        _reloadCoroutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    public void PlayRemoteShotFeedback()
    {
        PlaySound(GetShootSounds(), GetShootVolume());
        ReportGunshotNoise();
        ShotFired?.Invoke(this);
    }

    public void CancelReload()
    {
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }

        IsReloading = false;
    }

    public void SetDefinition(WeaponDefinition definition, bool resetAmmo = true)
    {
        weaponDefinition = definition;

        if (resetAmmo)
        {
            ResetAmmo();
        }
    }

    public void AddReserveAmmo(int amount)
    {
        TryAddReserveAmmo(amount);
    }

    public bool TryAddReserveAmmo(int amount)
    {
        if (amount <= 0 || InfiniteAmmo || InfiniteReserveAmmo)
        {
            return false;
        }

        ReserveAmmo += amount;
        NotifyAmmoChanged();
        return true;
    }

    public void ApplyNetworkAmmoState(int currentAmmo, int reserveAmmo, bool isReloading)
    {
        if (_reloadCoroutine != null)
        {
            StopCoroutine(_reloadCoroutine);
            _reloadCoroutine = null;
        }

        int nextCurrentAmmo = InfiniteAmmo
            ? MagazineSize
            : Mathf.Clamp(currentAmmo, 0, MagazineSize);
        int nextReserveAmmo = InfiniteAmmo || InfiniteReserveAmmo
            ? Mathf.Max(0, reserveAmmo)
            : Mathf.Max(0, reserveAmmo);

        bool ammoChanged = CurrentAmmo != nextCurrentAmmo || ReserveAmmo != nextReserveAmmo;
        bool reloadChanged = IsReloading != isReloading;

        CurrentAmmo = nextCurrentAmmo;
        ReserveAmmo = nextReserveAmmo;
        IsReloading = isReloading;

        if (reloadChanged)
        {
            if (isReloading)
            {
                PlaySound(GetReloadStartSounds(), GetReloadVolume());
                ReloadStarted?.Invoke(this);
            }
            else
            {
                PlaySound(GetReloadCompleteSounds(), GetReloadVolume());
                ReloadCompleted?.Invoke(this);
            }
        }

        if (ammoChanged)
            NotifyAmmoChanged();
    }

    public void ResetAmmo()
    {
        int startMagazineAmmo = weaponDefinition != null
            ? weaponDefinition.StartingMagazineAmmo
            : startingMagazineAmmo < 0 ? MagazineSize : Mathf.Min(startingMagazineAmmo, MagazineSize);

        CurrentAmmo = InfiniteAmmo ? MagazineSize : startMagazineAmmo;
        ReserveAmmo = weaponDefinition != null ? weaponDefinition.StartingReserveAmmo : startingReserveAmmo;
        NotifyAmmoChanged();
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        IsReloading = true;
        PlaySound(GetReloadStartSounds(), GetReloadVolume());
        ReloadStarted?.Invoke(this);

        yield return new WaitForSeconds(ReloadDuration);

        int neededAmmo = Mathf.Max(0, MagazineSize - CurrentAmmo);
        int loadedAmmo = InfiniteReserveAmmo ? neededAmmo : Mathf.Min(neededAmmo, ReserveAmmo);

        CurrentAmmo += loadedAmmo;

        if (!InfiniteReserveAmmo)
        {
            ReserveAmmo -= loadedAmmo;
        }

        IsReloading = false;
        _reloadCoroutine = null;
        PlaySound(GetReloadCompleteSounds(), GetReloadVolume());
        ReloadCompleted?.Invoke(this);
        NotifyAmmoChanged();
    }

    private void ConsumeAmmo()
    {
        if (InfiniteAmmo)
        {
            return;
        }

        CurrentAmmo = Mathf.Max(0, CurrentAmmo - AmmoPerShot);
        NotifyAmmoChanged();
    }

    private void DryFire()
    {
        PlayEmptyFeedback();
        _nextFireTime = Time.time + DryFireInterval;

        if (AutoReloadOnEmpty && HasAmmoInReserve)
        {
            TryReload();
        }
    }

    private void PlayEmptyFeedback()
    {
        if (Time.time < _nextDryFireTime)
        {
            return;
        }

        _nextDryFireTime = Time.time + DryFireInterval;
        PlaySound(GetEmptyMagazineSounds(), GetEmptyVolume());
        DryFired?.Invoke(this);
    }

    private void ResolveAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
    }

    private void PlaySound(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return;
        }

        ResolveAudioSource();

        AudioClip clip = clips[Random.Range(0, clips.Length)];

        if (clip == null || audioSource == null)
        {
            return;
        }

        float originalPitch = audioSource.pitch;
        audioSource.pitch = GetRandomPitch();
        audioSource.PlayOneShot(clip, volume);
        audioSource.pitch = originalPitch;
    }

    private float GetRandomPitch()
    {
        float randomness = weaponDefinition != null ? weaponDefinition.PitchRandomness : pitchRandomness;
        return 1f + Random.Range(-randomness, randomness);
    }

    private void ReportGunshotNoise()
    {
        if (!EmitGunshotNoise)
        {
            return;
        }

        Transform ownerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, null);
        ZombieNoiseSystem.ReportGunshot(transform.position, ownerRoot, GunshotNoiseRadius, GunshotSuspicion, this);
    }

    private AudioClip[] GetShootSounds()
    {
        return HasClips(weaponDefinition != null ? weaponDefinition.ShootSounds : null)
            ? weaponDefinition.ShootSounds
            : shootSounds;
    }

    private AudioClip[] GetEmptyMagazineSounds()
    {
        return HasClips(weaponDefinition != null ? weaponDefinition.EmptyMagazineSounds : null)
            ? weaponDefinition.EmptyMagazineSounds
            : emptyMagazineSounds;
    }

    private AudioClip[] GetReloadStartSounds()
    {
        return HasClips(weaponDefinition != null ? weaponDefinition.ReloadStartSounds : null)
            ? weaponDefinition.ReloadStartSounds
            : reloadStartSounds;
    }

    private AudioClip[] GetReloadCompleteSounds()
    {
        return HasClips(weaponDefinition != null ? weaponDefinition.ReloadCompleteSounds : null)
            ? weaponDefinition.ReloadCompleteSounds
            : reloadCompleteSounds;
    }

    private float GetShootVolume()
    {
        return weaponDefinition != null ? weaponDefinition.ShootVolume : shootVolume;
    }

    private float GetEmptyVolume()
    {
        return weaponDefinition != null ? weaponDefinition.EmptyVolume : emptyVolume;
    }

    private float GetReloadVolume()
    {
        return weaponDefinition != null ? weaponDefinition.ReloadVolume : reloadVolume;
    }

    private void NotifyAmmoChanged()
    {
        AmmoChanged?.Invoke(this, CurrentAmmo, ReserveAmmo);
    }

    private static bool HasClips(AudioClip[] clips)
    {
        return clips != null && clips.Length > 0;
    }
}
