using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ProjectileShotInfo
{
    public bool IsValid;
    public Vector3 Position;
    public Vector3 Direction;
    public float Speed;
    public float Damage;
    public float Lifetime;
    public float Range;
    public int HitMask;
    public bool UseGravity;
    public bool AlignToVelocity;
}

[RequireComponent(typeof(Weapon))]
public class ProjectileShooter : MonoBehaviour, IShooter
{
    [Header("Reference Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private ProjectilePool projectilePool;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = true;

    [HideInInspector] [SerializeField] private float projectileSpeed = 30f;
    [HideInInspector] [SerializeField] private float maxAimDistance = 1000f;
    [HideInInspector] [SerializeField] private LayerMask aimLayerMask;
    [HideInInspector] [SerializeField] [Min(0f)] private float spreadAngle = 0f;
    [HideInInspector] [SerializeField] private float spawnOffset = 0.05f;

    private Weapon _weapon;
    private WeaponIKGrip _weaponIKGrip;
    private Transform _resolvedOwnerRoot;
    private ProjectileShotInfo _lastShotInfo;

    public bool TryGetLastShotInfo(out ProjectileShotInfo shotInfo)
    {
        shotInfo = _lastShotInfo;
        return shotInfo.IsValid;
    }

    public void Awake()
    {
        _weapon = GetComponent<Weapon>();
        _weaponIKGrip = GetComponent<WeaponIKGrip>() ?? GetComponentInChildren<WeaponIKGrip>(true);

        if(playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (shootPoint == null)
        {
            shootPoint = _weaponIKGrip != null && _weaponIKGrip.Muzzle != null
                ? _weaponIKGrip.Muzzle
                : transform;
        }

        _resolvedOwnerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, ownerRoot);
    }

    public bool Shoot()
    {
        if (shootPoint == null || playerCamera == null || _weapon == null)
        {
            return false;
        }

        Vector3 aimPoint = GetAimPointFromCamera(out Ray cameraAimRay);
        LayerMask resolvedAimMask = GetAimLayerMask();

        aimPoint = ShooterAimUtility.ResolveMuzzleAimPoint(
            shootPoint.position,
            aimPoint,
            resolvedAimMask,
            _resolvedOwnerRoot);

        Vector3 directDirection = ShooterAimUtility.GetDirectionToAimPoint(
            shootPoint.position,
            aimPoint,
            cameraAimRay.direction);
        Vector3 spawnPosition = shootPoint.position + directDirection * GetSpawnOffset();

        Projectile projectile = GetProjectile();
        if (projectile == null)
        {
            return false;
        }

        ProjectilePool activePool = GetProjectilePrefab() == null ? projectilePool : null;
        float projectileSpeed = GetProjectileSpeed();
        float maxRange = GetMaxAimDistance();
        float lifetime = GetProjectileLifetime();
        Vector3 direction = GetProjectileDirection(spawnPosition, aimPoint, directDirection, projectileSpeed);

        projectile.Startup(
            _weapon,
            spawnPosition,
            direction,
            projectileSpeed,
            activePool,
            lifetime,
            _resolvedOwnerRoot,
            GetAimLayerMask(),
            maxRange
            );

        _lastShotInfo = new ProjectileShotInfo
        {
            IsValid = true,
            Position = spawnPosition,
            Direction = direction,
            Speed = projectileSpeed,
            Damage = _weapon.Damage,
            Lifetime = lifetime,
            Range = maxRange,
            HitMask = GetAimLayerMask().value,
            UseGravity = _weapon.ProjectileUseGravity,
            AlignToVelocity = _weapon.AlignProjectileToVelocity
        };

        CoopGameplaySync.RegisterLocalProjectileShot(_weapon, projectile, _lastShotInfo);

        if (debugDraw)
        {
            Debug.DrawLine(shootPoint.position, aimPoint, Color.yellow, 0.2f);
        }

        return true;
    }

    public Vector3 GetAimPointFromCamera()
    {
        return GetAimPointFromCamera(out Ray unusedAimRay);
    }

    private Vector3 GetAimPointFromCamera(out Ray aimRay)
    {
        return ShooterAimUtility.GetCameraAimPoint(
            playerCamera,
            GetMaxAimDistance(),
            GetAimLayerMask(),
            _resolvedOwnerRoot,
            GetSpreadAngle(),
            out aimRay);
    }

    private float GetProjectileSpeed()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.ProjectileSpeed : projectileSpeed;
    }

    private Projectile GetProjectile()
    {
        Projectile projectilePrefab = GetProjectilePrefab();

        if (projectilePrefab != null)
        {
            return Instantiate(projectilePrefab);
        }

        return projectilePool != null ? projectilePool.Get() : null;
    }

    private Projectile GetProjectilePrefab()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.ProjectilePrefab : null;
    }

    public Projectile ResolveProjectilePrefab()
    {
        Projectile projectilePrefab = GetProjectilePrefab();
        if (projectilePrefab != null)
            return projectilePrefab;

        return projectilePool != null ? projectilePool.BulletPrefab : null;
    }

    private float GetProjectileLifetime()
    {
        float lifetime;

        if (_weapon != null && _weapon.HasDefinition)
        {
            lifetime = _weapon.ProjectileLifetime;
        }
        else
        {
            lifetime = projectilePool != null ? projectilePool.MaxLifetime : 5f;
        }

        float speed = Mathf.Max(0.1f, GetProjectileSpeed());
        float lifetimeToReachRange = GetMaxAimDistance() / speed + 0.25f;

        return Mathf.Max(lifetime, lifetimeToReachRange);
    }

    private Vector3 GetProjectileDirection(Vector3 origin, Vector3 aimPoint, Vector3 fallbackDirection, float speed)
    {
        if (_weapon == null || !_weapon.ProjectileUseGravity)
        {
            return fallbackDirection;
        }

        if (TryGetBallisticDirection(origin, aimPoint, speed, out Vector3 ballisticDirection))
        {
            return ballisticDirection;
        }

        return fallbackDirection;
    }

    private static bool TryGetBallisticDirection(Vector3 origin, Vector3 target, float speed, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (speed <= 0.1f || Physics.gravity.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector3 gravityUp = -Physics.gravity.normalized;
        Vector3 toTarget = target - origin;
        float height = Vector3.Dot(toTarget, gravityUp);
        Vector3 lateral = toTarget - gravityUp * height;
        float distance = lateral.magnitude;

        if (distance <= 0.001f)
        {
            return false;
        }

        float gravity = Physics.gravity.magnitude;
        float speedSquared = speed * speed;
        float root = speedSquared * speedSquared - gravity * (gravity * distance * distance + 2f * height * speedSquared);

        if (root < 0f)
        {
            return false;
        }

        float lowArcTan = (speedSquared - Mathf.Sqrt(root)) / (gravity * distance);
        float angle = Mathf.Atan(lowArcTan);
        Vector3 lateralDirection = lateral / distance;
        Vector3 velocity = lateralDirection * (Mathf.Cos(angle) * speed) + gravityUp * (Mathf.Sin(angle) * speed);

        if (velocity.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        direction = velocity.normalized;
        return true;
    }

    private float GetMaxAimDistance()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.Range : maxAimDistance;
    }

    private LayerMask GetAimLayerMask()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.HitMask : aimLayerMask;
    }

    private float GetSpreadAngle()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.SpreadAngle : spreadAngle;
    }

    private float GetSpawnOffset()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.ProjectileSpawnOffset : spawnOffset;
    }
}
