using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct HitscanShotInfo
{
    public bool IsValid;
    public Vector3 Origin;
    public Vector3 Direction;
    public float Range;
    public float Damage;
    public int HitMask;
    public Vector3 HitPoint;
    public Vector3 HitNormal;
    public int PredictedZombieId;
    public bool HasPredictedZombieHit;
}

[RequireComponent(typeof(Weapon))]
public class RaycastShooter : MonoBehaviour, IShooter
{
    [Header("RaycastShooter Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootOrigin;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private bool debugDraw = true;

    [HideInInspector] [SerializeField] private float range = 1000f;
    [HideInInspector] [SerializeField] private LayerMask hitMask;
    [HideInInspector] [SerializeField] [Min(0f)] private float spreadAngle = 0f;

    private Weapon _weapon;
    private WeaponIKGrip _weaponIKGrip;
    private Transform _resolvedOwnerRoot;
    private HitscanShotInfo _lastShotInfo;

    public bool TryGetLastShotInfo(out HitscanShotInfo shotInfo)
    {
        shotInfo = _lastShotInfo;
        return shotInfo.IsValid;
    }

    private void Awake()
    {
        _weapon = GetComponent<Weapon>();
        _weaponIKGrip = GetComponent<WeaponIKGrip>() ?? GetComponentInChildren<WeaponIKGrip>(true);
        
        if(playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (shootOrigin == null)
        {
            shootOrigin = _weaponIKGrip != null && _weaponIKGrip.Muzzle != null
                ? _weaponIKGrip.Muzzle
                : transform;
        }

        _resolvedOwnerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, ownerRoot);
    }

    public bool Shoot()
    {
        if (playerCamera == null || _weapon == null)
        {
            return false;
        }

        Vector3 aimPoint = GetAimPointFromCamera(out Ray cameraAimRay);
        Vector3 origin = shootOrigin != null ? shootOrigin.position : transform.position;
        LayerMask resolvedHitMask = GetHitMask();
        float resolvedRange = GetRange();

        aimPoint = ShooterAimUtility.ResolveMuzzleAimPoint(origin, aimPoint, resolvedHitMask, _resolvedOwnerRoot);
        Vector3 fallbackDirection = cameraAimRay.direction.sqrMagnitude > 0.001f
            ? cameraAimRay.direction
            : transform.forward;
        Vector3 direction = ShooterAimUtility.GetDirectionToAimPoint(origin, aimPoint, fallbackDirection);
        _lastShotInfo = new HitscanShotInfo
        {
            IsValid = true,
            Origin = origin,
            Direction = direction,
            Range = resolvedRange,
            Damage = _weapon.Damage,
            HitMask = resolvedHitMask.value
        };

        if (ShooterAimUtility.TryRaycastIgnoringOwner(origin, direction, resolvedRange, resolvedHitMask, _resolvedOwnerRoot, out RaycastHit hit))
        {
            BaseDamagable damageable = ShooterAimUtility.FindDamageable(hit.collider);
            RecordLastShotHit(hit, damageable);

            if (damageable != null)
            {
                damageable.TakeDamage(_weapon.Damage, hit.point, hit.normal);
            }

            if (debugDraw)
            {
                Debug.DrawLine(origin, hit.point, Color.red, 0.2f);
            }

            return true;
        }

        if (debugDraw)
        {
            Debug.DrawRay(origin, direction * resolvedRange, Color.red, 0.2f);
        }

        return true;
    }

    private void RecordLastShotHit(RaycastHit hit, BaseDamagable damageable)
    {
        HitscanShotInfo shotInfo = _lastShotInfo;
        shotInfo.HitPoint = hit.point;
        shotInfo.HitNormal = hit.normal.sqrMagnitude > 0.001f ? hit.normal.normalized : Vector3.up;

        if (TryResolveZombieId(hit.collider, damageable, out int zombieId))
        {
            shotInfo.PredictedZombieId = zombieId;
            shotInfo.HasPredictedZombieHit = true;
        }

        _lastShotInfo = shotInfo;
    }

    private static bool TryResolveZombieId(Collider collider, BaseDamagable damageable, out int zombieId)
    {
        zombieId = 0;

        CoopNetworkIdentity identity = damageable != null
            ? damageable.GetComponentInParent<CoopNetworkIdentity>()
            : null;

        if (identity == null && collider != null)
            identity = collider.GetComponentInParent<CoopNetworkIdentity>();

        if (identity == null || identity.Kind != CoopNetworkObjectKind.Zombie || identity.NetworkId <= 0)
            return false;

        zombieId = identity.NetworkId;
        return true;
    }

    private Vector3 GetAimPointFromCamera()
    {
        return GetAimPointFromCamera(out Ray unusedAimRay);
    }

    private Vector3 GetAimPointFromCamera(out Ray aimRay)
    {
        return ShooterAimUtility.GetCameraAimPoint(
            playerCamera,
            GetRange(),
            GetHitMask(),
            _resolvedOwnerRoot,
            GetSpreadAngle(),
            out aimRay);
    }

    private float GetRange()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.Range : range;
    }

    private LayerMask GetHitMask()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.HitMask : hitMask;
    }

    private float GetSpreadAngle()
    {
        return _weapon != null && _weapon.HasDefinition ? _weapon.SpreadAngle : spreadAngle;
    }
}
