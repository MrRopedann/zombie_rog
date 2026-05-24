using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [Header("Fallback Impact Effects")]
    [SerializeField] private GameObject[] impactEffectPrefabs;
    [SerializeField] [Min(0f)] private float impactEffectLifetime = 30f;
    [SerializeField] [Min(0f)] private float impactSurfaceOffset = 0.01f;
    [SerializeField] private bool parentImpactEffectToHitObject = true;

    [Header("Fallback Ballistics")]
    [SerializeField] [Min(0.001f)] private float projectileMass = 0.008f;
    [SerializeField] [Min(0f)] private float projectileDrag = 0.01f;
    [SerializeField] [Min(0f)] private float projectileAngularDrag = 0.05f;
    [SerializeField] private bool projectileUseGravity = true;
    [SerializeField] private bool alignProjectileToVelocity = true;

    private float _maxLifetime;
    private float _damage;
    private Rigidbody _rigidbody;
    private ProjectilePool _pool;
    private float _timeElapsed = 0f;
    private Transform _ownerRoot;
    private LayerMask _hitMask = ~0;
    private Vector3 _previousPosition;
    private float _maxTravelDistance = float.PositiveInfinity;
    private float _traveledDistance;
    private bool _hasPreviousPosition;
    private bool _hasImpacted;
    private GameObject[] _runtimeImpactEffectPrefabs;
    private float _runtimeImpactEffectLifetime;
    private float _runtimeImpactSurfaceOffset;
    private bool _runtimeParentImpactEffectToHitObject;
    private bool _runtimeAlignProjectileToVelocity;
    private int _networkOwnerId;
    private int _networkProjectileId;
    private bool _networkLocalAuthority;

    public int NetworkOwnerId => _networkOwnerId;
    public int NetworkProjectileId => _networkProjectileId;
    public bool NetworkLocalAuthority => _networkLocalAuthority;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        _hasImpacted = false;
        _hasPreviousPosition = false;
    }

    private void Update()
    {
        CountdownLifeTime();
    }

    private void FixedUpdate()
    {
        SweepForImpact();
        AlignWithVelocity();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasImpacted || collision == null || ShooterAimUtility.IsOwnerCollider(collision.collider, _ownerRoot))
        {
            return;
        }

        ContactPoint contact = collision.contactCount > 0
            ? collision.GetContact(0)
            : default;

        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        if (collision.contactCount == 0)
        {
            hitPoint = transform.position;
            hitNormal = -transform.forward;
        }

        Collider hitCollider = collision.collider;

        if (TryResolvePreferredImpact(hitPoint, out RaycastHit preferredHit))
        {
            hitCollider = preferredHit.collider;
            hitPoint = preferredHit.point;
            hitNormal = preferredHit.normal;
        }

        HandleImpact(hitCollider, hitPoint, hitNormal);
    }

    private void CountdownLifeTime()
    { 
        if (_hasImpacted)
        {
            return;
        }

        _timeElapsed += Time.deltaTime;

        if(_timeElapsed >= _maxLifetime)
        {
            Release();
        }
    }

    private void SweepForImpact()
    {
        if (_hasImpacted)
        {
            return;
        }

        Vector3 currentPosition = transform.position;

        if (!_hasPreviousPosition)
        {
            _previousPosition = currentPosition;
            _hasPreviousPosition = true;
            return;
        }

        Vector3 segment = currentPosition - _previousPosition;
        float distance = segment.magnitude;

        if (distance <= 0.001f)
        {
            _previousPosition = currentPosition;
            return;
        }

        float remainingDistance = _maxTravelDistance - _traveledDistance;

        if (remainingDistance <= 0f)
        {
            Release();
            return;
        }

        Vector3 direction = segment / distance;
        float sweepDistance = Mathf.Min(distance, remainingDistance);

        if (ShooterAimUtility.TryRaycastIgnoringOwner(
            _previousPosition,
            direction,
            sweepDistance,
            _hitMask,
            _ownerRoot,
            out RaycastHit hit))
        {
            HandleImpact(hit.collider, hit.point, hit.normal);
            return;
        }

        _traveledDistance += distance;

        if (_traveledDistance >= _maxTravelDistance)
        {
            Release();
            return;
        }

        _previousPosition = currentPosition;
    }

    private void AlignWithVelocity()
    {
        if (!_runtimeAlignProjectileToVelocity || _rigidbody == null || _hasImpacted)
        {
            return;
        }

        Vector3 velocity = _rigidbody.velocity;

        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }
    }

    private void HandleImpact(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (_hasImpacted)
        {
            return;
        }

        _hasImpacted = true;

        if (hitNormal.sqrMagnitude <= 0.001f)
        {
            hitNormal = -transform.forward;
        }

        hitNormal.Normalize();

        BaseDamagable damageable = ShooterAimUtility.FindDamageable(hitCollider);
        bool suppressImpactEffect = false;

        if(damageable != null)
        {
            damageable.TakeDamage(_damage, hitPoint, hitNormal);
            suppressImpactEffect = damageable.SuppressProjectileImpactEffect;
        }

        if (!suppressImpactEffect)
        {
            SpawnImpactEffect(hitCollider, hitPoint, hitNormal);
        }

        CoopGameplaySync.NotifyProjectileImpact(this, hitPoint, hitNormal, suppressImpactEffect);
        Release();
    }

    private bool TryResolvePreferredImpact(Vector3 fallbackHitPoint, out RaycastHit preferredHit)
    {
        preferredHit = default;

        if (!_hasPreviousPosition)
            return false;

        Vector3 segment = fallbackHitPoint - _previousPosition;
        float distance = segment.magnitude;

        if (distance <= 0.001f && _rigidbody != null && _rigidbody.velocity.sqrMagnitude > 0.001f)
        {
            segment = _rigidbody.velocity.normalized * Mathf.Max(0.1f, _rigidbody.velocity.magnitude * Time.fixedDeltaTime);
            distance = segment.magnitude;
        }

        if (distance <= 0.001f)
            return false;

        return ShooterAimUtility.TryRaycastIgnoringOwner(
            _previousPosition,
            segment / distance,
            distance + 0.2f,
            _hitMask,
            _ownerRoot,
            out preferredHit);
    }

    private void SpawnImpactEffect(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal)
    {
        GameObject prefab = GetImpactEffectPrefab();

        if (prefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hitNormal)
            * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
        Vector3 position = hitPoint + hitNormal * _runtimeImpactSurfaceOffset;
        Transform parent = _runtimeParentImpactEffectToHitObject && hitCollider != null
            ? hitCollider.transform
            : null;

        GameObject effect = Instantiate(prefab, position, rotation, parent);

        if (_runtimeImpactEffectLifetime > 0f)
        {
            Destroy(effect, _runtimeImpactEffectLifetime);
        }
    }

    private GameObject GetImpactEffectPrefab()
    {
        GameObject[] prefabs = _runtimeImpactEffectPrefabs != null && _runtimeImpactEffectPrefabs.Length > 0
            ? _runtimeImpactEffectPrefabs
            : impactEffectPrefabs;

        if (prefabs == null || prefabs.Length == 0)
        {
            return null;
        }

        int startIndex = Random.Range(0, prefabs.Length);

        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[(startIndex + i) % prefabs.Length];

            if (prefab != null)
            {
                return prefab;
            }
        }

        return null;
    }

    private void Release()
    {
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_pool != null)
        {
            _pool.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Startup(
        Weapon weapon,
        Vector3 spawnPosition,
        Vector3 direction,
        float speed,
        ProjectilePool pool,
        float maxLifetime,
        Transform ownerRoot,
        LayerMask hitMask,
        float maxTravelDistance)
    {
        if (weapon != null)
        {
            _damage = weapon.Damage;
            _runtimeImpactEffectPrefabs = weapon.ImpactEffectPrefabs;
            _runtimeImpactEffectLifetime = weapon.ImpactEffectLifetime;
            _runtimeImpactSurfaceOffset = weapon.ImpactSurfaceOffset;
            _runtimeParentImpactEffectToHitObject = weapon.ParentImpactEffectToHitObject;
            _runtimeAlignProjectileToVelocity = weapon.AlignProjectileToVelocity;
            ApplyBallistics(
                weapon.ProjectileMass,
                weapon.ProjectileDrag,
                weapon.ProjectileAngularDrag,
                weapon.ProjectileUseGravity);
        }
        else
        {
            _damage = 0f;
            ApplyFallbackSettings();
        }

        Launch(spawnPosition, direction, speed, pool, maxLifetime, ownerRoot, hitMask, maxTravelDistance);
    }

    public void Startup(float damage, Vector3 spawnPosition, Vector3 direction, float speed, ProjectilePool pool, float maxLifetime)
    {
        _damage = damage;
        ApplyFallbackSettings();
        Launch(spawnPosition, direction, speed, pool, maxLifetime, null, ~0, speed * maxLifetime);
    }

    public void StartupVisual(
        Weapon weapon,
        Vector3 spawnPosition,
        Vector3 direction,
        float speed,
        float maxLifetime,
        Transform ownerRoot,
        LayerMask hitMask,
        float maxTravelDistance)
    {
        if (weapon != null)
        {
            _damage = 0f;
            _runtimeImpactEffectPrefabs = weapon.ImpactEffectPrefabs;
            _runtimeImpactEffectLifetime = weapon.ImpactEffectLifetime;
            _runtimeImpactSurfaceOffset = weapon.ImpactSurfaceOffset;
            _runtimeParentImpactEffectToHitObject = weapon.ParentImpactEffectToHitObject;
            _runtimeAlignProjectileToVelocity = weapon.AlignProjectileToVelocity;
            ApplyBallistics(
                weapon.ProjectileMass,
                weapon.ProjectileDrag,
                weapon.ProjectileAngularDrag,
                weapon.ProjectileUseGravity);
        }
        else
        {
            _damage = 0f;
            ApplyFallbackSettings();
        }

        Launch(spawnPosition, direction, speed, null, maxLifetime, ownerRoot, hitMask, maxTravelDistance);
    }

    public void ConfigureNetwork(int ownerId, int projectileId, bool localAuthority)
    {
        _networkOwnerId = ownerId;
        _networkProjectileId = projectileId;
        _networkLocalAuthority = localAuthority;
    }

    public void ForceVisualImpact(Vector3 hitPoint, Vector3 hitNormal, bool suppressEffect)
    {
        if (_hasImpacted)
            return;

        _hasImpacted = true;

        if (hitNormal.sqrMagnitude <= 0.001f)
            hitNormal = -transform.forward;

        hitNormal.Normalize();

        if (!suppressEffect)
            SpawnImpactEffect(null, hitPoint, hitNormal);

        Release();
    }

    private void Launch(
        Vector3 spawnPosition,
        Vector3 direction,
        float speed,
        ProjectilePool pool,
        float maxLifetime,
        Transform ownerRoot,
        LayerMask hitMask,
        float maxTravelDistance)
    {
        _pool = pool;
        _maxLifetime = Mathf.Max(0.1f, maxLifetime);
        _timeElapsed = 0;
        _ownerRoot = ownerRoot;
        _hitMask = hitMask;
        _maxTravelDistance = maxTravelDistance > 0f
            ? maxTravelDistance + Mathf.Max(10f, speed * Time.fixedDeltaTime * 2f)
            : float.PositiveInfinity;
        _traveledDistance = 0f;
        _hasImpacted = false;
        _networkOwnerId = 0;
        _networkProjectileId = 0;
        _networkLocalAuthority = false;

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        Quaternion rotation = Quaternion.LookRotation(direction);
        transform.SetPositionAndRotation(spawnPosition, rotation);

        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = spawnPosition;
        _rigidbody.rotation = rotation;
        _rigidbody.velocity = direction * speed;

        _previousPosition = spawnPosition;
        _hasPreviousPosition = true;
    }

    private void ApplyFallbackSettings()
    {
        _runtimeImpactEffectPrefabs = impactEffectPrefabs;
        _runtimeImpactEffectLifetime = impactEffectLifetime;
        _runtimeImpactSurfaceOffset = impactSurfaceOffset;
        _runtimeParentImpactEffectToHitObject = parentImpactEffectToHitObject;
        _runtimeAlignProjectileToVelocity = alignProjectileToVelocity;
        ApplyBallistics(projectileMass, projectileDrag, projectileAngularDrag, projectileUseGravity);
    }

    private void ApplyBallistics(float mass, float drag, float angularDrag, bool useGravity)
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        _rigidbody.mass = Mathf.Max(0.001f, mass);
        _rigidbody.drag = Mathf.Max(0f, drag);
        _rigidbody.angularDrag = Mathf.Max(0f, angularDrag);
        _rigidbody.useGravity = useGravity;
        _rigidbody.isKinematic = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }
}
