using System.Collections.Generic;
using UnityEngine;

public enum ZombieHitboxBodyPart
{
    Head,
    Torso,
    Arm,
    Hand,
    Leg,
    Foot
}

public class ZombieHitbox : BaseDamagable
{
    [Header("Zombie Hitbox")]
    [SerializeField] private ZombieHealth owner;
    [SerializeField] private ZombieHitboxBodyPart bodyPart = ZombieHitboxBodyPart.Torso;
    [SerializeField] [Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private Transform sourceBone;
    [SerializeField] private Transform endBone;
    [SerializeField] [Min(0.01f)] private float hitboxRadius = 0.1f;
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private bool sphereHitbox = true;
    [SerializeField] private bool fallbackHitbox;

    private const float HeadPromotionMinPadding = 0.06f;
    private const float HeadPromotionRadiusFactor = 0.3f;
    private const float HeadPromotionVerticalFactor = 1.05f;
    private const float HeadPromotionDistancePadding = 0.2f;

    private SphereCollider sphereCollider;
    private CapsuleCollider capsuleCollider;
    private static readonly List<ZombieHitbox> ActiveHitboxes = new();

    public ZombieHealth Owner => owner;
    public ZombieHitboxBodyPart BodyPart => bodyPart;
    public float DamageMultiplier => damageMultiplier;
    public Transform SourceBone => sourceBone;
    public Transform EndBone => endBone;
    public float HitboxRadius => hitboxRadius;
    public Vector3 LocalOffset => localOffset;
    public bool IsSphereHitbox => sphereHitbox;
    public bool IsFallbackHitbox => fallbackHitbox;
    public override bool SuppressProjectileImpactEffect => owner != null && owner.SuppressProjectileImpactEffect;

    public bool TryGetWorldShape(out Vector3 start, out Vector3 end, out float radius, out bool isSphere)
    {
        start = default;
        end = default;
        radius = 0f;
        isSphere = true;

        RefreshPose();

        if (!sphereHitbox && sourceBone != null && endBone != null)
        {
            float scale = GetLargestAbsComponent(transform.lossyScale);
            start = sourceBone.position;
            end = endBone.position;
            radius = Mathf.Max(0.01f, hitboxRadius * scale);
            isSphere = false;
            return radius > 0f && (end - start).sqrMagnitude > 0.0001f;
        }

        if (!TryGetWorldSphere(out Vector3 center, out radius))
            return false;

        start = center;
        end = center;
        isSphere = true;
        return true;
    }

    public void Configure(ZombieHealth newOwner, ZombieHitboxBodyPart newBodyPart, float newDamageMultiplier, Transform newSourceBone)
    {
        Configure(newOwner, newBodyPart, newDamageMultiplier, newSourceBone, null, hitboxRadius, sphereHitbox);
    }

    public void Configure(
        ZombieHealth newOwner,
        ZombieHitboxBodyPart newBodyPart,
        float newDamageMultiplier,
        Transform newSourceBone,
        Transform newEndBone,
        float newHitboxRadius,
        bool newSphereHitbox)
    {
        Configure(
            newOwner,
            newBodyPart,
            newDamageMultiplier,
            newSourceBone,
            newEndBone,
            newHitboxRadius,
            newSphereHitbox,
            false,
            Vector3.zero);
    }

    public void Configure(
        ZombieHealth newOwner,
        ZombieHitboxBodyPart newBodyPart,
        float newDamageMultiplier,
        Transform newSourceBone,
        Transform newEndBone,
        float newHitboxRadius,
        bool newSphereHitbox,
        bool newFallbackHitbox)
    {
        Configure(
            newOwner,
            newBodyPart,
            newDamageMultiplier,
            newSourceBone,
            newEndBone,
            newHitboxRadius,
            newSphereHitbox,
            newFallbackHitbox,
            Vector3.zero);
    }

    public void Configure(
        ZombieHealth newOwner,
        ZombieHitboxBodyPart newBodyPart,
        float newDamageMultiplier,
        Transform newSourceBone,
        Transform newEndBone,
        float newHitboxRadius,
        bool newSphereHitbox,
        bool newFallbackHitbox,
        Vector3 newLocalOffset)
    {
        owner = newOwner;
        bodyPart = newBodyPart;
        damageMultiplier = Mathf.Max(0f, newDamageMultiplier);
        sourceBone = newSourceBone;
        endBone = newEndBone;
        hitboxRadius = Mathf.Max(0.01f, newHitboxRadius);
        localOffset = newLocalOffset;
        sphereHitbox = newSphereHitbox;
        fallbackHitbox = newFallbackHitbox;

        CacheColliders();
        RefreshPose();
    }

    protected override void Awake()
    {
        base.Awake();
        CacheColliders();
    }

    private void OnEnable()
    {
        if (!ActiveHitboxes.Contains(this))
            ActiveHitboxes.Add(this);
    }

    private void OnDisable()
    {
        ActiveHitboxes.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveHitboxes.Remove(this);
    }

    public static void SyncAllActiveHitboxes()
    {
        for (int i = ActiveHitboxes.Count - 1; i >= 0; i--)
        {
            ZombieHitbox hitbox = ActiveHitboxes[i];

            if (hitbox == null)
            {
                ActiveHitboxes.RemoveAt(i);
                continue;
            }

            hitbox.RefreshPose();
        }
    }

    public static void SyncOwnerHitboxes(ZombieHealth hitboxOwner)
    {
        if (hitboxOwner == null)
            return;

        for (int i = ActiveHitboxes.Count - 1; i >= 0; i--)
        {
            ZombieHitbox hitbox = ActiveHitboxes[i];

            if (hitbox == null)
            {
                ActiveHitboxes.RemoveAt(i);
                continue;
            }

            if (hitbox.owner == hitboxOwner)
                hitbox.RefreshPose();
        }
    }

    public static bool TryPromoteHitPointToHead(
        Collider hitCollider,
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 originalHitPoint,
        float hitDistance,
        out Vector3 promotedPoint,
        out Vector3 promotedNormal,
        out float promotedDistance)
    {
        promotedPoint = default;
        promotedNormal = default;
        promotedDistance = default;

        if (hitCollider == null || rayDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        ZombieHitbox hitbox = hitCollider.GetComponent<ZombieHitbox>();

        if (hitbox != null)
        {
            if (hitbox.bodyPart == ZombieHitboxBodyPart.Head)
            {
                return false;
            }

            if (hitbox.bodyPart != ZombieHitboxBodyPart.Torso && !hitbox.fallbackHitbox)
            {
                return false;
            }
        }

        ZombieHealth hitOwner = hitbox != null ? hitbox.owner : ResolveOwnerFromCollider(hitCollider);

        if (hitOwner == null)
        {
            return false;
        }

        ZombieHitbox headHitbox = FindOwnerHitbox(hitOwner, ZombieHitboxBodyPart.Head);

        if (headHitbox == null || headHitbox == hitbox)
        {
            return false;
        }

        headHitbox.RefreshPose();

        if (!headHitbox.TryGetWorldSphere(out Vector3 center, out float radius))
        {
            return false;
        }

        if (!IsPointInsideHeadVerticalBand(originalHitPoint, center, radius))
        {
            return false;
        }

        float maxDistance = Mathf.Max(0f, hitDistance)
            + radius * 2f
            + HeadPromotionDistancePadding;

        return TryGetRayPointInsideHeadPromotionZone(
            rayOrigin,
            rayDirection,
            maxDistance,
            center,
            radius,
            out promotedPoint,
            out promotedNormal,
            out promotedDistance);
    }

    private void LateUpdate()
    {
        RefreshPose();
    }

    protected override bool CanReceiveDamage(float damage)
    {
        return owner != null && !owner.IsDead && damage > 0f && damageMultiplier > 0f;
    }

    protected override void TakeDamageCore(float damage)
    {
        TakeDamageCore(damage, transform.position, Vector3.up);
    }

    protected override void TakeDamageCore(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (owner == null)
            return;

        SyncOwnerHitboxes(owner);
        owner.TakeDamage(damage * ResolveDamageMultiplier(hitPoint), hitPoint, hitNormal);
        SyncOwnerHitboxes(owner);
    }

    private void CacheColliders()
    {
        if (sphereCollider == null)
            sphereCollider = GetComponent<SphereCollider>();

        if (capsuleCollider == null)
            capsuleCollider = GetComponent<CapsuleCollider>();
    }

    private void RefreshPose()
    {
        if (sourceBone == null)
            return;

        if (!sphereHitbox && endBone != null && capsuleCollider != null)
        {
            RefreshCapsulePose();
            return;
        }

        if (transform.parent == sourceBone)
        {
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            transform.SetPositionAndRotation(sourceBone.TransformPoint(localOffset), sourceBone.rotation);
        }

        if (sphereCollider != null)
            sphereCollider.radius = hitboxRadius;
    }

    private void RefreshCapsulePose()
    {
        Vector3 start = sourceBone.position;
        Vector3 end = endBone.position;
        Vector3 segment = end - start;
        float length = segment.magnitude;

        if (length <= 0.001f)
            return;

        transform.SetPositionAndRotation(
            start + segment * 0.5f,
            Quaternion.FromToRotation(Vector3.up, segment / length));

        capsuleCollider.direction = 1;
        capsuleCollider.center = Vector3.zero;
        capsuleCollider.radius = hitboxRadius;
        capsuleCollider.height = Mathf.Max(hitboxRadius * 2.1f, length + hitboxRadius * 2f);
    }

    private float ResolveDamageMultiplier(Vector3 hitPoint)
    {
        if (bodyPart != ZombieHitboxBodyPart.Torso || owner == null)
        {
            return damageMultiplier;
        }

        ZombieHitbox headHitbox = FindOwnerHitbox(owner, ZombieHitboxBodyPart.Head);

        if (headHitbox == null || headHitbox == this)
        {
            return damageMultiplier;
        }

        headHitbox.RefreshPose();

        if (!headHitbox.IsPointInsideHeadPromotionZone(hitPoint))
        {
            return damageMultiplier;
        }

        return Mathf.Max(damageMultiplier, headHitbox.damageMultiplier);
    }

    private bool IsPointInsideHeadPromotionZone(Vector3 hitPoint)
    {
        if (bodyPart != ZombieHitboxBodyPart.Head || !TryGetWorldSphere(out Vector3 center, out float radius))
        {
            return false;
        }

        if (!IsPointInsideHeadVerticalBand(hitPoint, center, radius))
        {
            return false;
        }

        float padding = Mathf.Max(HeadPromotionMinPadding, radius * HeadPromotionRadiusFactor);
        float promotionRadius = radius + padding;
        return (hitPoint - center).sqrMagnitude <= promotionRadius * promotionRadius;
    }

    private static bool TryGetRayPointInsideHeadPromotionZone(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        float maxDistance,
        Vector3 center,
        float radius,
        out Vector3 promotedPoint,
        out Vector3 promotedNormal,
        out float promotedDistance)
    {
        promotedPoint = default;
        promotedNormal = default;
        promotedDistance = default;

        if (radius <= 0f || rayDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        rayDirection.Normalize();

        float closestDistance = Vector3.Dot(center - rayOrigin, rayDirection);

        if (closestDistance < 0f || closestDistance > maxDistance)
        {
            return false;
        }

        Vector3 closestPoint = rayOrigin + rayDirection * closestDistance;

        if (!IsPointInsideHeadVerticalBand(closestPoint, center, radius))
        {
            return false;
        }

        float padding = Mathf.Max(HeadPromotionMinPadding, radius * HeadPromotionRadiusFactor);
        float promotionRadius = radius + padding;

        if ((closestPoint - center).sqrMagnitude > promotionRadius * promotionRadius)
        {
            return false;
        }

        Vector3 normal = closestPoint - center;

        if (normal.sqrMagnitude <= 0.001f)
        {
            normal = -rayDirection;
        }

        promotedPoint = closestPoint;
        promotedNormal = normal.normalized;
        promotedDistance = closestDistance;
        return true;
    }

    private static bool IsPointInsideHeadVerticalBand(Vector3 point, Vector3 center, float radius)
    {
        return point.y >= center.y - radius * HeadPromotionVerticalFactor
            && point.y <= center.y + radius * (HeadPromotionVerticalFactor + 0.6f);
    }

    private bool TryGetWorldSphere(out Vector3 center, out float radius)
    {
        CacheColliders();

        if (sphereCollider != null)
        {
            center = sphereCollider.transform.TransformPoint(sphereCollider.center);
            radius = sphereCollider.radius * GetLargestAbsComponent(sphereCollider.transform.lossyScale);
            return radius > 0f;
        }

        center = transform.position;
        radius = hitboxRadius * GetLargestAbsComponent(transform.lossyScale);
        return radius > 0f;
    }

    private static ZombieHitbox FindOwnerHitbox(ZombieHealth hitboxOwner, ZombieHitboxBodyPart targetBodyPart)
    {
        for (int i = ActiveHitboxes.Count - 1; i >= 0; i--)
        {
            ZombieHitbox hitbox = ActiveHitboxes[i];

            if (hitbox == null)
            {
                ActiveHitboxes.RemoveAt(i);
                continue;
            }

            if (hitbox.owner == hitboxOwner && hitbox.bodyPart == targetBodyPart)
            {
                return hitbox;
            }
        }

        return null;
    }

    private static ZombieHealth ResolveOwnerFromCollider(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        ZombieHealth hitOwner = collider.GetComponentInParent<ZombieHealth>();
        return hitOwner != null ? hitOwner : collider.GetComponentInChildren<ZombieHealth>();
    }

    private static float GetLargestAbsComponent(Vector3 value)
    {
        return Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
