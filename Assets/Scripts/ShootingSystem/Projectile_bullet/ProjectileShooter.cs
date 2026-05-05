using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Weapon))]
public class ProjectileShooter : MonoBehaviour, IShooter
{
    [Header("Reference Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private ProjectilePool projectilePool;

    [Header("Projectile Settings")]
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] [Min(0f)] private float spreadAngle = 0f;
    [SerializeField] private float spawnOffset = 0.05f;
    [SerializeField] private bool debugDraw = true;

    private Weapon _weapon;
    private Transform _resolvedOwnerRoot;

    public void Awake()
    {
        _weapon = GetComponent<Weapon>();

        if(playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (shootPoint == null)
        {
            shootPoint = transform;
        }

        _resolvedOwnerRoot = ShooterAimUtility.ResolveOwnerRoot(transform, ownerRoot);
    }

    public void Shoot()
    {
        if (shootPoint == null || projectilePool == null || playerCamera == null || _weapon == null)
        {
            return;
        }

        Vector3 aimPoint = GetAimPointFromCamera(out Ray cameraAimRay);
        aimPoint = ShooterAimUtility.ResolveMuzzleAimPoint(
            shootPoint.position,
            aimPoint,
            aimLayerMask,
            _resolvedOwnerRoot);

        Vector3 direction = ShooterAimUtility.GetDirectionToAimPoint(
            shootPoint.position,
            aimPoint,
            cameraAimRay.direction);
        Vector3 spawnPosition = shootPoint.position + direction * spawnOffset;

        Projectile projectile = projectilePool.Get();
        if (projectile == null)
        {
            return;
        }

        projectile.Startup(
            _weapon.Damage,
            spawnPosition,
            direction,
            projectileSpeed,
            projectilePool,
            projectilePool.MaxLifetime
            );

        if (debugDraw)
        {
            Debug.DrawLine(shootPoint.position, aimPoint, Color.yellow, 0.2f);
        }
    }

    public Vector3 GetAimPointFromCamera()
    {
        return GetAimPointFromCamera(out Ray unusedAimRay);
    }

    private Vector3 GetAimPointFromCamera(out Ray aimRay)
    {
        return ShooterAimUtility.GetCameraAimPoint(
            playerCamera,
            maxAimDistance,
            aimLayerMask,
            _resolvedOwnerRoot,
            spreadAngle,
            out aimRay);
    }
}
