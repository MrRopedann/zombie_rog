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

    private Weapon _weapon;

    public void Awake()
    {
        _weapon = GetComponent<Weapon>();
        if(playerCamera == null)
            playerCamera = Camera.main;
    }

    public void Shoot()
    {
        Vector3 aimPoint = GetAimPointFromCamera();
        Vector3 direction = (aimPoint - shootPoint.position).normalized;

        Projectile projectile = projectilePool.Get();
        projectile.transform.position = shootPoint.position;
        projectile.transform.rotation = Quaternion.LookRotation(direction);

        projectile.Startup(
            _weapon.Damage,
            direction,
            projectileSpeed,
            projectilePool,
            projectilePool.MaxLifetime
            );
    }

    public Vector3 GetAimPointFromCamera()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if(Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimLayerMask))
            return hit.point;

        return ray.origin + ray.direction * maxAimDistance;
    }
}
