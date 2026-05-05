using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Weapon))]
public class RaycastShooter : MonoBehaviour, IShooter
{
    [Header("RaycastShooter Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootOrigin;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private float range = 1000f;
    [SerializeField] private LayerMask hitMask;
    [SerializeField] [Min(0f)] private float spreadAngle = 0f;
    [SerializeField] private bool debugDraw = true;

    private Weapon _weapon;
    private Transform _resolvedOwnerRoot;

    private void Awake()
    {
        _weapon = GetComponent<Weapon>();
        
        if(playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (shootOrigin == null)
        {
            shootOrigin = transform;
        }

        _resolvedOwnerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, ownerRoot);
    }

    public void Shoot()
    {
        if (playerCamera == null || _weapon == null)
        {
            return;
        }

        Vector3 aimPoint = GetAimPointFromCamera(out Ray cameraAimRay);
        Vector3 origin = shootOrigin != null ? shootOrigin.position : transform.position;
        aimPoint = ShooterAimUtility.ResolveMuzzleAimPoint(origin, aimPoint, hitMask, _resolvedOwnerRoot);
        Vector3 fallbackDirection = cameraAimRay.direction.sqrMagnitude > 0.001f
            ? cameraAimRay.direction
            : transform.forward;
        Vector3 direction = ShooterAimUtility.GetDirectionToAimPoint(origin, aimPoint, fallbackDirection);

        if (ShooterAimUtility.TryRaycastIgnoringOwner(origin, direction, range, hitMask, _resolvedOwnerRoot, out RaycastHit hit))
        {
            BaseDamagable damageable = ShooterAimUtility.FindDamageable(hit.collider);

            if (damageable != null)
            {
                damageable.TakeDamage(_weapon.Damage, hit.point, hit.normal);
            }

            if (debugDraw)
            {
                Debug.DrawLine(origin, hit.point, Color.red, 0.2f);
            }

            return;
        }

        if (debugDraw)
        {
            Debug.DrawRay(origin, direction * range, Color.red, 0.2f);
        }
    }

    private Vector3 GetAimPointFromCamera()
    {
        return GetAimPointFromCamera(out Ray unusedAimRay);
    }

    private Vector3 GetAimPointFromCamera(out Ray aimRay)
    {
        return ShooterAimUtility.GetCameraAimPoint(
            playerCamera,
            range,
            hitMask,
            _resolvedOwnerRoot,
            spreadAngle,
            out aimRay);
    }
}
