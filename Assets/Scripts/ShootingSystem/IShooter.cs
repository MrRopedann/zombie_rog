using UnityEngine;

public interface IShooter
{
    void Shoot();
}

public static class ShooterAimUtility
{
    private const int MaxRaycastHits = 32;
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[MaxRaycastHits];

    public static Vector3 GetCameraAimPoint(
        Camera camera,
        float maxDistance,
        LayerMask hitMask,
        Transform ownerRoot,
        float spreadAngle,
        out Ray aimRay)
    {
        aimRay = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (spreadAngle > 0f)
        {
            aimRay.direction = GetSpreadDirection(camera.transform, aimRay.direction, spreadAngle);
        }

        if (TryRaycastIgnoringOwner(aimRay.origin, aimRay.direction, maxDistance, hitMask, ownerRoot, out RaycastHit hit))
        {
            return hit.point;
        }

        return aimRay.origin + aimRay.direction * maxDistance;
    }

    public static Vector3 ResolveMuzzleAimPoint(
        Vector3 muzzlePosition,
        Vector3 desiredAimPoint,
        LayerMask hitMask,
        Transform ownerRoot)
    {
        Vector3 direction = desiredAimPoint - muzzlePosition;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return desiredAimPoint;
        }

        direction /= distance;

        if (TryRaycastIgnoringOwner(muzzlePosition, direction, distance, hitMask, ownerRoot, out RaycastHit hit))
        {
            return hit.point;
        }

        return desiredAimPoint;
    }

    public static Vector3 GetDirectionToAimPoint(
        Vector3 origin,
        Vector3 aimPoint,
        Vector3 fallbackDirection)
    {
        Vector3 direction = aimPoint - origin;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = fallbackDirection;
        }

        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }

    public static bool TryRaycastIgnoringOwner(
        Vector3 origin,
        Vector3 direction,
        float distance,
        LayerMask hitMask,
        Transform ownerRoot,
        out RaycastHit closestHit)
    {
        closestHit = default;

        if (direction.sqrMagnitude <= 0.001f || distance <= 0f)
        {
            return false;
        }

        direction.Normalize();

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            RaycastHits,
            distance,
            hitMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = RaycastHits[i];

            if (hit.collider == null || IsOwnerCollider(hit.collider, ownerRoot))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    public static BaseDamagable FindDamageable(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        BaseDamagable damageable = collider.GetComponent<BaseDamagable>();

        if (damageable != null)
        {
            return damageable;
        }

        damageable = collider.GetComponentInParent<BaseDamagable>();

        return damageable != null ? damageable : collider.GetComponentInChildren<BaseDamagable>();
    }

    public static Transform ResolveOwnerRoot(Transform shooterTransform, Transform ownerRoot)
    {
        if (ownerRoot != null)
        {
            return ownerRoot;
        }

        PlayerWeaponController weaponController = shooterTransform != null
            ? shooterTransform.GetComponentInParent<PlayerWeaponController>()
            : null;

        if (weaponController != null)
        {
            return weaponController.transform.root;
        }

        return shooterTransform != null ? shooterTransform.root : null;
    }

    private static Vector3 GetSpreadDirection(Transform cameraTransform, Vector3 forward, float spreadAngle)
    {
        float clampedSpread = Mathf.Clamp(spreadAngle, 0f, 45f);
        float spreadRadius = Mathf.Tan(clampedSpread * Mathf.Deg2Rad);
        Vector2 spread = Random.insideUnitCircle * spreadRadius;

        Vector3 direction = forward
            + cameraTransform.right * spread.x
            + cameraTransform.up * spread.y;

        return direction.sqrMagnitude > 0.001f ? direction.normalized : forward;
    }

    private static bool IsOwnerCollider(Collider collider, Transform ownerRoot)
    {
        return ownerRoot != null && collider.transform.IsChildOf(ownerRoot);
    }
}
