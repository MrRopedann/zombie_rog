using System.Collections.Generic;
using UnityEngine;

public interface IShooter
{
    bool Shoot();
}

public static class ShooterAimUtility
{
    private const int MaxRaycastHits = 128;
    private const float HitboxGraceRadius = 0.06f;
    private static readonly RaycastHit[] RaycastHits = new RaycastHit[MaxRaycastHits];
    private static readonly RaycastHit[] SphereCastHits = new RaycastHit[MaxRaycastHits];

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

        ZombieHitbox.SyncAllActiveHitboxes();
        Physics.SyncTransforms();

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            RaycastHits,
            distance,
            hitMask,
            QueryTriggerInteraction.Collide);
        hitCount = AppendSphereCastHits(origin, direction, distance, hitMask, hitCount);

        System.Array.Sort(RaycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        if (!TryResolvePreferredHit(0, hitCount, ownerRoot, out closestHit))
        {
            return false;
        }

        PromoteHitPointToHeadIfNeeded(origin, direction, ref closestHit);
        return true;
    }

    private static int AppendSphereCastHits(
        Vector3 origin,
        Vector3 direction,
        float distance,
        LayerMask hitMask,
        int hitCount)
    {
        if (HitboxGraceRadius <= 0f || hitCount >= MaxRaycastHits)
        {
            return hitCount;
        }

        int remainingCapacity = MaxRaycastHits - hitCount;
        int sphereHitCount = Physics.SphereCastNonAlloc(
            origin,
            HitboxGraceRadius,
            direction,
            SphereCastHits,
            distance,
            hitMask,
            QueryTriggerInteraction.Collide);

        if (sphereHitCount <= 0)
        {
            return hitCount;
        }

        int copied = 0;

        for (int i = 0; i < sphereHitCount && copied < remainingCapacity; i++)
        {
            RaycastHit hit = SphereCastHits[i];

            if (!IsZombieDamageHit(hit.collider))
            {
                continue;
            }

            RaycastHits[hitCount + copied] = hit;
            copied++;
        }

        return hitCount + copied;
    }

    private static bool TryResolvePreferredHit(
        int startIndex,
        int hitCount,
        Transform ownerRoot,
        out RaycastHit preferredHit)
    {
        preferredHit = default;
        bool hasDamageableFallback = false;
        RaycastHit damageableFallback = default;
        bool hasFallbackHitbox = false;
        RaycastHit fallbackHitbox = default;
        bool hasHitboxCandidate = false;
        RaycastHit hitboxCandidate = default;
        int hitboxCandidatePriority = int.MinValue;
        Transform damageableRoot = null;

        for (int i = startIndex; i < hitCount; i++)
        {
            RaycastHit hit = RaycastHits[i];

            if (hit.collider == null || IsOwnerCollider(hit.collider, ownerRoot))
                continue;

            ZombieHitbox hitbox = hit.collider.GetComponent<ZombieHitbox>();

            if (hitbox != null)
            {
                Transform hitboxRoot = hitbox.Owner != null ? hitbox.Owner.transform.root : hit.collider.transform.root;

                if (damageableRoot != null && hitboxRoot != damageableRoot)
                {
                    preferredHit = ResolveCurrentPreferredHit(
                        hasHitboxCandidate,
                        hitboxCandidate,
                        hasFallbackHitbox,
                        fallbackHitbox,
                        hasDamageableFallback,
                        damageableFallback);
                    return true;
                }

                damageableRoot ??= hitboxRoot;

                if (hitbox.IsFallbackHitbox)
                {
                    if (!hasFallbackHitbox)
                    {
                        hasFallbackHitbox = true;
                        fallbackHitbox = hit;
                    }

                    continue;
                }

                int hitboxPriority = GetHitboxPriority(hitbox);

                if (!hasHitboxCandidate
                    || hitboxPriority > hitboxCandidatePriority
                    || hitboxPriority == hitboxCandidatePriority && hit.distance < hitboxCandidate.distance)
                {
                    hasHitboxCandidate = true;
                    hitboxCandidate = hit;
                    hitboxCandidatePriority = hitboxPriority;
                }

                continue;
            }

            BaseDamagable damageable = FindDamageable(hit.collider);

            if (damageable != null)
            {
                Transform currentDamageableRoot = damageable.transform.root;

                if (damageableRoot != null && currentDamageableRoot != damageableRoot)
                {
                    preferredHit = ResolveCurrentPreferredHit(
                        hasHitboxCandidate,
                        hitboxCandidate,
                        hasFallbackHitbox,
                        fallbackHitbox,
                        hasDamageableFallback,
                        damageableFallback);
                    return true;
                }

                if (!hasDamageableFallback)
                {
                    hasDamageableFallback = true;
                    damageableFallback = hit;
                    damageableRoot = currentDamageableRoot;
                }

                continue;
            }

            if (hit.collider.isTrigger)
                continue;

            preferredHit = ResolveCurrentPreferredHit(
                hasHitboxCandidate,
                hitboxCandidate,
                hasFallbackHitbox,
                fallbackHitbox,
                hasDamageableFallback,
                damageableFallback,
                hit);
            return true;
        }

        if (TryResolveCurrentPreferredHit(
            hasHitboxCandidate,
            hitboxCandidate,
            hasFallbackHitbox,
            fallbackHitbox,
            hasDamageableFallback,
            damageableFallback,
            out preferredHit))
            return true;

        return false;
    }

    private static void PromoteHitPointToHeadIfNeeded(
        Vector3 origin,
        Vector3 direction,
        ref RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        if (!ZombieHitbox.TryPromoteHitPointToHead(
            hit.collider,
            origin,
            direction,
            hit.point,
            hit.distance,
            out Vector3 promotedPoint,
            out Vector3 promotedNormal,
            out float promotedDistance))
        {
            return;
        }

        hit.point = promotedPoint;
        hit.normal = promotedNormal;
        hit.distance = promotedDistance;
    }

    private static bool TryResolveCurrentPreferredHit(
        bool hasHitboxCandidate,
        RaycastHit hitboxCandidate,
        bool hasFallbackHitbox,
        RaycastHit fallbackHitbox,
        bool hasDamageableFallback,
        RaycastHit damageableFallback,
        out RaycastHit preferredHit)
    {
        preferredHit = default;

        if (hasHitboxCandidate)
        {
            preferredHit = hitboxCandidate;
            return true;
        }

        if (hasFallbackHitbox)
        {
            preferredHit = fallbackHitbox;
            return true;
        }

        if (hasDamageableFallback)
        {
            preferredHit = damageableFallback;
            return true;
        }

        return false;
    }

    private static RaycastHit ResolveCurrentPreferredHit(
        bool hasHitboxCandidate,
        RaycastHit hitboxCandidate,
        bool hasFallbackHitbox,
        RaycastHit fallbackHitbox,
        bool hasDamageableFallback,
        RaycastHit damageableFallback,
        RaycastHit solidFallback = default)
    {
        return TryResolveCurrentPreferredHit(
            hasHitboxCandidate,
            hitboxCandidate,
            hasFallbackHitbox,
            fallbackHitbox,
            hasDamageableFallback,
            damageableFallback,
            out RaycastHit preferredHit)
            ? preferredHit
            : solidFallback;
    }

    private static int GetHitboxPriority(ZombieHitbox hitbox)
    {
        return hitbox.BodyPart switch
        {
            ZombieHitboxBodyPart.Head => 40,
            ZombieHitboxBodyPart.Arm => 30,
            ZombieHitboxBodyPart.Hand => 30,
            ZombieHitboxBodyPart.Leg => 30,
            ZombieHitboxBodyPart.Foot => 30,
            ZombieHitboxBodyPart.Torso => 10,
            _ => 0
        };
    }

    private static bool IsZombieDamageHit(Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        if (collider.GetComponent<ZombieHitbox>() != null)
        {
            return true;
        }

        return collider.GetComponentInParent<ZombieHealth>() != null
            || collider.GetComponentInChildren<ZombieHealth>() != null;
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

    public static bool IsOwnerCollider(Collider collider, Transform ownerRoot)
    {
        return collider != null && ownerRoot != null && collider.transform.IsChildOf(ownerRoot);
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();

        public int Compare(RaycastHit x, RaycastHit y)
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
