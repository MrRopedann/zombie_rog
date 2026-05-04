using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Weapon))]
public class RaycastShooter : MonoBehaviour, IShooter
{
    [Header("RaycastShooter Settings")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform shootOrigin;
    [SerializeField] private float range = 1000f;
    [SerializeField] private LayerMask hitMask;

    private Weapon _weapon;

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
    }

    public void Shoot()
    {
        if (playerCamera == null || _weapon == null)
        {
            return;
        }

        Vector3 aimPoint = GetAimPointFromCamera();
        Vector3 origin = shootOrigin != null ? shootOrigin.position : transform.position;
        Vector3 direction = (aimPoint - origin).sqrMagnitude > 0.001f
            ? (aimPoint - origin).normalized
            : transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            BaseDamagable damageable = hit.collider.GetComponent<BaseDamagable>();

            if (damageable == null)
            {
                damageable = hit.collider.GetComponentInParent<BaseDamagable>();

                if (damageable == null)
                {
                    damageable = hit.collider.GetComponentInChildren<BaseDamagable>();
                }

            }

            if (damageable != null)
            {
                damageable.TakeDamage(_weapon.Damage, hit.point, hit.normal);
            }

            Debug.DrawLine(origin, hit.point, Color.red, 0.2f);
            return;
        }

        Debug.DrawRay(origin, direction * range, Color.red, 0.2f);
    }

    private Vector3 GetAimPointFromCamera()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return ray.origin + ray.direction * range;
    }
}
