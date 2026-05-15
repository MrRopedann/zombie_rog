using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Zombie Rogue/Zombie Spawner")]
public class ZombieSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject[] zombiePrefabs;
    [SerializeField] private Transform player;
    [SerializeField] private Camera spawnCamera;
    [SerializeField] private Transform spawnParent = null;

    [Header("Population")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] [Min(0)] private int initialSpawnCount = 4;
    [SerializeField] [Min(0)] private int maxAlive = 12;
    [SerializeField] [Min(0.1f)] private float spawnInterval = 4f;
    [SerializeField] [Min(1)] private int spawnPerInterval = 1;

    [Header("Placement")]
    [SerializeField] [Min(0f)] private float minDistanceFromPlayer = 18f;
    [SerializeField] [Min(1f)] private float maxDistanceFromPlayer = 55f;
    [SerializeField] [Min(0f)] private float maxHeightDifferenceFromPlayer = 5f;
    [SerializeField] [Min(0.1f)] private float navMeshSampleRadius = 8f;
    [SerializeField] [Min(1)] private int maxAttemptsPerZombie = 40;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas;
    [SerializeField] private bool requirePathToPlayer = true;

    [Header("Clearance")]
    [SerializeField] private LayerMask blockingMask = ~0;
    [SerializeField] [Min(0.1f)] private float spawnCapsuleHeight = 1.8f;
    [SerializeField] [Min(0.05f)] private float spawnCapsuleRadius = 0.45f;
    [SerializeField] [Min(0f)] private float groundSkin = 0.05f;
    [SerializeField] [Min(0f)] private float minDistanceBetweenZombies = 1.5f;

    [Header("Camera Safety")]
    [SerializeField] private bool requireOutsideCameraView = true;
    [SerializeField] [Range(0f, 0.4f)] private float screenMargin = 0.08f;
    [SerializeField] private bool refreshCameraIfLost = true;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private readonly List<ZombieHealth> aliveZombies = new();
    private readonly RaycastHit[] cameraRayHits = new RaycastHit[32];
    private NavMeshPath reusablePath;
    private Coroutine spawnRoutine;
    private bool warnedNoPlayer;
    private bool warnedNoPrefabs;
    private bool warnedNoSpawnPoint;

    private void Awake()
    {
        reusablePath = new NavMeshPath();
        ResolveReferences();
        LoadDefaultPrefabsIfNeeded();
    }

    private void Start()
    {
        if (!spawnOnStart)
            return;

        SpawnWave(initialSpawnCount);
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    public void SpawnWave(int amount)
    {
        if (amount <= 0)
            return;

        PruneSpawnedZombies();
        ResolveReferences();
        LoadDefaultPrefabsIfNeeded();

        for (int i = 0; i < amount && AliveCount < maxAlive; i++)
            TrySpawnZombie();
    }

    private IEnumerator SpawnLoop()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, spawnInterval));
            SpawnWave(spawnPerInterval);
        }
    }

    private bool TrySpawnZombie()
    {
        GameObject prefab = GetRandomZombiePrefab();
        if (prefab == null)
        {
            WarnOnce(ref warnedNoPrefabs, "ZombieSpawner has no zombie prefabs. Assign them or keep prefabs under Resources/Prefabs/Zombie.");
            return false;
        }

        if (!TryFindSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation))
        {
            WarnOnce(ref warnedNoSpawnPoint, "ZombieSpawner could not find a safe off-camera NavMesh point for a zombie.");
            return false;
        }

        GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation, spawnParent);
        ConfigureSpawnedZombie(instance, spawnPosition, spawnRotation);

        ZombieHealth health = instance.GetComponentInChildren<ZombieHealth>();
        if (health != null)
            aliveZombies.Add(health);

        warnedNoSpawnPoint = false;
        return true;
    }

    private bool TryFindSpawnPoint(out Vector3 spawnPosition, out Quaternion spawnRotation)
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        if (player == null)
        {
            WarnOnce(ref warnedNoPlayer, "ZombieSpawner cannot spawn because no Player transform was found.");
            return false;
        }

        float minDistance = Mathf.Min(minDistanceFromPlayer, maxDistanceFromPlayer);
        float maxDistance = Mathf.Max(minDistanceFromPlayer, maxDistanceFromPlayer);
        float navMeshProbeRadius = Mathf.Max(maxDistance + navMeshSampleRadius, navMeshSampleRadius);

        ZombieNavMeshBootstrap.EnsureBuilt(player.position, navMeshProbeRadius);

        int attemptBudget = Mathf.Max(maxAttemptsPerZombie, requireOutsideCameraView ? 160 : 80);

        for (int attempt = 0; attempt < attemptBudget; attempt++)
        {
            Vector3 candidate = GetSpawnCandidate(attempt, attemptBudget, minDistance, maxDistance);

            if (!TryAcceptSpawnCandidate(candidate, minDistance, maxDistance, out spawnPosition, out spawnRotation))
                continue;

            return true;
        }

        int ringSamples = Mathf.Max(48, attemptBudget / 2);

        for (int sample = 0; sample < ringSamples; sample++)
        {
            float distanceT = ringSamples <= 1 ? 1f : (float)sample / (ringSamples - 1);
            float distance = Mathf.Lerp(maxDistance, minDistance, distanceT);
            float angle = sample * 137.50776f * Mathf.Deg2Rad;
            Vector3 candidate = player.position + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

            if (!TryAcceptSpawnCandidate(candidate, minDistance, maxDistance, out spawnPosition, out spawnRotation))
                continue;

            return true;
        }

        return false;
    }

    private bool TryAcceptSpawnCandidate(
        Vector3 candidate,
        float minDistance,
        float maxDistance,
        out Vector3 spawnPosition,
        out Quaternion spawnRotation)
    {
        spawnPosition = Vector3.zero;
        spawnRotation = Quaternion.identity;

        if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, navMeshAreaMask))
            return false;

        if (!IsSpawnPointValid(hit.position, minDistance, maxDistance))
            return false;

        spawnPosition = hit.position;
        spawnRotation = GetRotationFacingPlayer(hit.position);
        return true;
    }

    private Vector3 GetSpawnCandidate(int attempt, int attemptBudget, float minDistance, float maxDistance)
    {
        ResolveCamera();

        if (requireOutsideCameraView && spawnCamera != null && attempt < attemptBudget * 0.65f)
            return GetRandomPointAwayFromCamera(minDistance, maxDistance);

        return GetRandomPointAroundPlayer(minDistance, maxDistance);
    }

    private Vector3 GetRandomPointAwayFromCamera(float minDistance, float maxDistance)
    {
        Vector3 cameraForward = spawnCamera.transform.forward;
        cameraForward.y = 0f;

        if (cameraForward.sqrMagnitude <= 0.0001f)
            return GetRandomPointAroundPlayer(minDistance, maxDistance);

        Vector3 baseDirection = -cameraForward.normalized;
        float angleOffset = Random.Range(-95f, 95f);
        Vector3 direction = Quaternion.Euler(0f, angleOffset, 0f) * baseDirection;

        float minSqr = minDistance * minDistance;
        float maxSqr = maxDistance * maxDistance;
        float distance = Mathf.Sqrt(Random.Range(minSqr, maxSqr));

        return player.position + direction * distance;
    }

    private bool IsSpawnPointValid(Vector3 position, float minDistance, float maxDistance)
    {
        if (Mathf.Abs(position.y - player.position.y) > maxHeightDifferenceFromPlayer)
            return false;

        float distanceToPlayerSqr = HorizontalSqrDistance(position, player.position);
        if (distanceToPlayerSqr < minDistance * minDistance || distanceToPlayerSqr > maxDistance * maxDistance)
            return false;

        if (requireOutsideCameraView && IsCapsuleVisibleToCamera(position))
            return false;

        if (!HasSpawnClearance(position))
            return false;

        if (IsTooCloseToAnotherZombie(position))
            return false;

        return !requirePathToPlayer || HasCompletePathToPlayer(position);
    }

    private Vector3 GetRandomPointAroundPlayer(float minDistance, float maxDistance)
    {
        float minSqr = minDistance * minDistance;
        float maxSqr = maxDistance * maxDistance;
        float distance = Mathf.Sqrt(Random.Range(minSqr, maxSqr));
        float angle = Random.Range(0f, Mathf.PI * 2f);

        return player.position + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
    }

    private Quaternion GetRotationFacingPlayer(Vector3 position)
    {
        Vector3 direction = player.position - position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return transform.rotation;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private bool HasSpawnClearance(Vector3 position)
    {
        GetSpawnCapsule(position, out Vector3 bottom, out Vector3 top, out float radius);
        return !Physics.CheckCapsule(bottom, top, radius, blockingMask, QueryTriggerInteraction.Ignore);
    }

    private void GetSpawnCapsule(Vector3 position, out Vector3 bottom, out Vector3 top, out float radius)
    {
        radius = Mathf.Max(0.05f, spawnCapsuleRadius);
        float height = Mathf.Max(spawnCapsuleHeight, radius * 2f + groundSkin);

        bottom = position + Vector3.up * (radius + groundSkin);
        top = position + Vector3.up * (height - radius);

        if (top.y < bottom.y)
            top = bottom;
    }

    private bool IsCapsuleVisibleToCamera(Vector3 position)
    {
        ResolveCamera();

        if (spawnCamera == null)
            return false;

        GetSpawnCapsule(position, out Vector3 bottom, out Vector3 top, out float radius);

        Vector3 center = (bottom + top) * 0.5f;
        Vector3 cameraRight = spawnCamera.transform.right * radius;
        Vector3 cameraForward = spawnCamera.transform.forward * radius;

        return IsPointVisibleToCamera(bottom)
            || IsPointVisibleToCamera(top)
            || IsPointVisibleToCamera(center)
            || IsPointVisibleToCamera(center + cameraRight)
            || IsPointVisibleToCamera(center - cameraRight)
            || IsPointVisibleToCamera(center + cameraForward)
            || IsPointVisibleToCamera(center - cameraForward);
    }

    private bool IsPointVisibleToCamera(Vector3 worldPoint)
    {
        if (!IsPointInCameraView(worldPoint))
            return false;

        return !IsPointOccludedFromCamera(worldPoint);
    }

    private bool IsPointOccludedFromCamera(Vector3 worldPoint)
    {
        Vector3 origin = spawnCamera.transform.position;
        Vector3 toPoint = worldPoint - origin;
        float distance = toPoint.magnitude;

        if (distance <= 0.001f)
            return false;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            toPoint / distance,
            cameraRayHits,
            Mathf.Max(0f, distance - 0.1f),
            blockingMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraRayHits[i];

            if (hit.collider == null || IsIgnoredCameraOccluder(hit.collider))
                continue;

            closestDistance = Mathf.Min(closestDistance, hit.distance);
        }

        return closestDistance < distance - 0.1f;
    }

    private bool IsIgnoredCameraOccluder(Collider collider)
    {
        if (collider == null)
            return true;

        Transform root = collider.transform.root;

        if (player != null && root == player.root)
            return true;

        return spawnCamera != null && root == spawnCamera.transform.root;
    }

    private bool IsPointInCameraView(Vector3 worldPoint)
    {
        Vector3 viewportPoint = spawnCamera.WorldToViewportPoint(worldPoint);

        if (viewportPoint.z <= spawnCamera.nearClipPlane || viewportPoint.z >= spawnCamera.farClipPlane)
            return false;

        return viewportPoint.x >= -screenMargin
            && viewportPoint.x <= 1f + screenMargin
            && viewportPoint.y >= -screenMargin
            && viewportPoint.y <= 1f + screenMargin;
    }

    private bool IsTooCloseToAnotherZombie(Vector3 position)
    {
        if (minDistanceBetweenZombies <= 0f)
            return false;

        float minDistanceSqr = minDistanceBetweenZombies * minDistanceBetweenZombies;

        for (int i = 0; i < aliveZombies.Count; i++)
        {
            ZombieHealth zombie = aliveZombies[i];
            if (zombie == null || zombie.IsDead)
                continue;

            if (HorizontalSqrDistance(position, zombie.transform.position) < minDistanceSqr)
                return true;
        }

        return false;
    }

    private bool HasCompletePathToPlayer(Vector3 position)
    {
        if (reusablePath == null)
            reusablePath = new NavMeshPath();

        if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerHit, navMeshSampleRadius, navMeshAreaMask))
            return false;

        if (!NavMesh.CalculatePath(position, playerHit.position, navMeshAreaMask, reusablePath))
            return false;

        return reusablePath.status == NavMeshPathStatus.PathComplete;
    }

    private void ConfigureSpawnedZombie(GameObject instance, Vector3 position, Quaternion rotation)
    {
        instance.transform.SetPositionAndRotation(position, rotation);

        ZombieAI ai = instance.GetComponentInChildren<ZombieAI>();
        if (ai != null && player != null)
            ai.target = player;

        NavMeshAgent agent = instance.GetComponentInChildren<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(position);
            if (agent.isOnNavMesh)
                agent.ResetPath();
        }
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = FindTaggedObject("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        ResolveCamera();
    }

    private void ResolveCamera()
    {
        if (spawnCamera != null && (!refreshCameraIfLost || spawnCamera.isActiveAndEnabled))
            return;

        spawnCamera = Camera.main;
    }

    private GameObject FindTaggedObject(string tagName)
    {
        try
        {
            return GameObject.FindGameObjectWithTag(tagName);
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private void LoadDefaultPrefabsIfNeeded()
    {
        if (HasAnyPrefab())
            return;

        GameObject[] loadedPrefabs = Resources.LoadAll<GameObject>("Prefabs/Zombie");
        List<GameObject> validPrefabs = new();

        for (int i = 0; i < loadedPrefabs.Length; i++)
        {
            GameObject prefab = loadedPrefabs[i];
            if (prefab != null && prefab.GetComponentInChildren<ZombieAI>(true) != null)
                validPrefabs.Add(prefab);
        }

        if (validPrefabs.Count > 0)
            zombiePrefabs = validPrefabs.ToArray();
    }

    private bool HasAnyPrefab()
    {
        if (zombiePrefabs == null)
            return false;

        for (int i = 0; i < zombiePrefabs.Length; i++)
        {
            if (zombiePrefabs[i] != null)
                return true;
        }

        return false;
    }

    private GameObject GetRandomZombiePrefab()
    {
        if (zombiePrefabs == null || zombiePrefabs.Length == 0)
            return null;

        int startIndex = Random.Range(0, zombiePrefabs.Length);

        for (int offset = 0; offset < zombiePrefabs.Length; offset++)
        {
            GameObject prefab = zombiePrefabs[(startIndex + offset) % zombiePrefabs.Length];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private void PruneSpawnedZombies()
    {
        for (int i = aliveZombies.Count - 1; i >= 0; i--)
        {
            ZombieHealth zombie = aliveZombies[i];
            if (zombie == null || zombie.IsDead)
                aliveZombies.RemoveAt(i);
        }
    }

    private int AliveCount
    {
        get
        {
            PruneSpawnedZombies();
            return aliveZombies.Count;
        }
    }

    private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private void WarnOnce(ref bool flag, string message)
    {
        if (flag)
            return;

        flag = true;
        Debug.LogWarning(message, this);
    }

    private void OnValidate()
    {
        if (maxDistanceFromPlayer < minDistanceFromPlayer)
            maxDistanceFromPlayer = minDistanceFromPlayer;

        spawnCapsuleRadius = Mathf.Max(0.05f, spawnCapsuleRadius);
        spawnCapsuleHeight = Mathf.Max(spawnCapsuleHeight, spawnCapsuleRadius * 2f + groundSkin);
        maxAttemptsPerZombie = Mathf.Max(1, maxAttemptsPerZombie);
        spawnPerInterval = Mathf.Max(1, spawnPerInterval);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Transform origin = player != null ? player : transform;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(origin.position, minDistanceFromPlayer);

        Gizmos.color = new Color(0.1f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(origin.position, maxDistanceFromPlayer);
    }
}
