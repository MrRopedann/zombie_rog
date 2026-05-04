using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Weapon))]
public class ProjectileShooter : MonoBehaviour, IShooter
{
    [Header("Reference Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private ProjectilePool projectilePool;

    [Header("Projectile Settings")]
    [SerializeField] private float projectileSpeed = 30f;
    [SerializeField] private float maxAimDistance = 1000f;
    [SerializeField] private LayerMask aimLayerMask;
    [SerializeField] private float spawnOffset = 0.05f;

    private Weapon _weapon;

    public void Awake()
    {
        _weapon = GetComponent<Weapon>();
        if(playerCamera == null)
            playerCamera = Camera.main;
    }

    public void Shoot()
    {
        if (shootPoint == null || projectilePool == null || playerCamera == null || _weapon == null)
        {
            return;
        }

        Vector3 aimPoint = GetAimPointFromCamera();
        aimPoint = ResolveMuzzleAimPoint(aimPoint);

        Vector3 direction = aimPoint - shootPoint.position;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : shootPoint.forward;
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
    }

    public Vector3 GetAimPointFromCamera()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if(Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask, QueryTriggerInteraction.Ignore))
            return hit.point;

        return ray.origin + ray.direction * maxAimDistance;
    }

    private Vector3 ResolveMuzzleAimPoint(Vector3 desiredAimPoint)
    {
        Vector3 direction = desiredAimPoint - shootPoint.position;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return desiredAimPoint;
        }

        direction /= distance;

        if (Physics.Raycast(shootPoint.position, direction, out RaycastHit hit, distance, aimLayerMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return desiredAimPoint;
    }
}
