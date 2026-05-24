using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class ZombieAI : MonoBehaviour
{
    public enum State { Idle, Patrol, Alert, Chase, Attack, Search, Dead }
    public State state = State.Idle;

    [Header("Refs")]
    public Transform[] patrolPoints;
    public Transform eyes;
    public Transform target;
    public Animator animator;
    public ZombieHealth health;
    public LayerMask obstacleMask = ~0;

    [Header("Vision")]
    public float viewDistance = 18f;
    [Range(0, 180)] public float viewAngle = 80f;
    [Range(0, 180)] public float peripheralAngle = 140f;
    public float peripheralDistance = 10f;
    public float closeSightDistance = 2.2f;

    [Header("Suspicion")]
    public float suspicionRise = 2.0f;
    public float suspicionFall = 1.0f;
    public float suspicionToChase = 1.0f;
    float suspicion;

    [Header("Hearing (3 rings)")]
    public bool ignoreHearingWhenTargetCrouched = true;
    public float hearNear = 3f;
    public float hearMid = 6f;
    public float hearFar = 10f;
    public float memoryTime = 4f;

    [Header("Gunshot Hearing")]
    public bool reactToGunshots = true;
    public bool onlyReactToTargetGunshots = false;
    [Range(0f, 1f)] public float minGunshotSuspicion = 0.55f;
    public float gunshotPositionRandomness = 1.5f;

    [Header("Move")]
    public float walkSpeed = 1.2f;
    public float runSpeed = 2.6f;
    public float turnLerp = 12f;

    [Header("Attack")]
    public float attackRange = 1.6f;
    public float attackCooldown = 1.0f;
    public float attackDamage = 15f;
    public float attackHitDelay = 0.35f;
    public float attackHitRadius = 0.35f;

    [Header("Off-Mesh Jump / Drop")]
    public bool useOffMeshLinks = true;
    public float jumpTime = 0.55f;
    public float extraUpVelocity = 1.0f;
    public float reprojectRadius = 2f;

    [Header("Search+Investigate")]
    public float investigateTime = 2.0f;
    public float investigateYawSpeed = 220f;
    public int wedgePoints = 4;
    [Range(15, 120)] public float wedgeHalfAngle = 45f;
    public float wedgeStepDist = 3.0f;
    public int innerRingPoints = 3;
    public float innerRingRadius = 2.0f;
    public float searchMaxTime = 25f;
    [Min(0)] public int revisitLostCount = 2;
    public float revisitSpacing = 2.5f;

    [Header("Prediction / Breadcrumbs")]
    public float predictionTime = 0.6f;
    public float breadcrumbRecordStep = 0.3f;
    public int breadcrumbMax = 8;

    [Header("Animation")]
    public string speedParameter = "Speed";
    public string attackTriggerParameter = "Attack";
    public string hitTriggerParameter = "Hit";
    public string deadParameter = "isDead";
    public string attackBoolParameter = "isAttacking";
    public string deathStateName = "Death";
    public float animatorSpeedDampTime = 0.12f;

    [Header("Death")]
    public bool disableAgentOnDeath = true;
    public bool disableMainColliderOnDeath = false;
    public bool despawnCorpseAfterDeath = true;
    [Min(0f)] public float corpseDespawnMinDelay = 5f;
    [Min(0f)] public float corpseDespawnMaxDelay = 10f;
    [HideInInspector]
    public float destroyAfterDeathDelay = 0f;
    [Range(0.8f, 1f)] public float deathFreezeNormalizedTime = 0.98f;

    NavMeshAgent agent;
    Rigidbody rb;
    Collider mainCollider;
    CharacterStats targetStats;
    ICrouchProvider targetCrouch;
    IMovementNoiseProvider targetMove;

    int patrolIx;
    float atkTimer;
    float lastSeenT = float.NegativeInfinity;
    Vector3 lastSeenPos;
    Vector3 lastSeenDir;
    bool hasLOS;
    bool traversing;
    bool lastMemoryIsTarget = true;

    readonly List<Vector3> searchPlan = new();
    int searchIdx;
    float searchTimer;
    float waitTimer;

    readonly Queue<Vector3> breadcrumbs = new();
    Vector3 lastTargetPos;
    Vector3 targetVel;
    float breadcrumbTimer;
    bool dead;
    bool deathAnimationFrozen;
    Coroutine attackRoutine;

    int animIDSpeed;
    int animIDAttack;
    int animIDHit;
    int animIDDead;
    int animIDIsAttacking;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCollider = GetComponent<Collider>();
        animator = animator ? animator : GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        health = health ? health : GetComponent<ZombieHealth>();

        if (health == null)
            health = gameObject.AddComponent<ZombieHealth>();

        health.OnDeath += HandleDeath;
        health.OnDamageTaken += HandleDamageTaken;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.isKinematic = true;

        if (animator)
            animator.speed = 1f;

        if (!eyes) eyes = transform;
        ResolveTarget();

        ZombieNavMeshBootstrap.EnsureBuilt(transform.position, Mathf.Max(32f, reprojectRadius * 8f));

        if (!agent.isOnNavMesh) SnapToNavMesh();
        agent.baseOffset = 0f;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.autoTraverseOffMeshLink = false;
        agent.stoppingDistance = Mathf.Max(agent.stoppingDistance, attackRange * 0.85f);

        CacheAnimatorParameters();

        SetState(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
    }

    void OnEnable()
    {
        ZombieNoiseSystem.GunshotReported += HandleGunshotReported;
    }

    void OnDisable()
    {
        ZombieNoiseSystem.GunshotReported -= HandleGunshotReported;
    }

    void OnDestroy()
    {
        ZombieNoiseSystem.GunshotReported -= HandleGunshotReported;

        if (health == null)
            return;

        health.OnDeath -= HandleDeath;
        health.OnDamageTaken -= HandleDamageTaken;
    }

    void OnValidate()
    {
        corpseDespawnMinDelay = Mathf.Max(0f, corpseDespawnMinDelay);
        corpseDespawnMaxDelay = Mathf.Max(corpseDespawnMinDelay, corpseDespawnMaxDelay);
        destroyAfterDeathDelay = Mathf.Max(0f, destroyAfterDeathDelay);
    }

    void Update()
    {
        if (dead)
        {
            UpdateDeathAnimation();
            return;
        }

        if (!target || targetStats == null || targetStats.IsDead || CoopSessionState.IsCoopSession)
            ResolveTarget();

        if (agent == null || !agent.enabled)
            return;

        if (useOffMeshLinks && !traversing && agent.isOnOffMeshLink)
        {
            StartCoroutine(TraverseLinkBallistic());
            return;
        }

        SenseAndThink();
        atkTimer -= Time.deltaTime;

        if (state != State.Attack && CanAttackTarget())
            SetState(State.Attack);

        switch (state)
        {
            case State.Idle:
                if (CanEngage()) SetState(State.Chase);
                break;

            case State.Patrol:
                agent.speed = walkSpeed;
                if (CanEngage()) { SetState(State.Chase); break; }
                if (!agent.hasPath || agent.remainingDistance < 0.3f) NextPatrol();
                break;

            case State.Alert:
                agent.speed = runSpeed;
                SafeSetDestination(lastSeenPos);
                if (CanEngage()) { SetState(State.Chase); break; }
                if (Arrived(lastSeenPos, 0.5f)) SetState(State.Search);
                break;

            case State.Chase:
                agent.speed = runSpeed;
                if (target)
                {
                    Vector3 pred = PredictTargetPosition();
                    SafeSetDestination(pred);
                    if (CanAttackTarget())
                        SetState(State.Attack);
                    else if (!Remembering() && suspicion < 0.25f)
                        SetState(State.Alert);
                }
                break;

            case State.Attack:
                SafeResetPath();
                FaceTo(target ? target.position : transform.position + transform.forward);
                if (!CanAttackTarget())
                { SetState(State.Chase); break; }
                if (atkTimer <= 0f) StartAttack();
                break;

            case State.Search:
                RunSearch();
                break;
        }

        UpdateAnimator();
    }

    void ResolveTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform bestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;

        for (int i = 0; i < players.Length; i++)
        {
            GameObject player = players[i];
            if (player == null)
                continue;

            Transform playerTarget = ResolvePlayerTargetTransform(player);
            if (playerTarget == null)
                continue;

            CharacterStats stats = playerTarget.GetComponentInParent<CharacterStats>() ?? playerTarget.GetComponentInChildren<CharacterStats>(true);
            if (stats != null && stats.IsDead)
                continue;

            float distanceSqr = (playerTarget.position - transform.position).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
                continue;

            bestDistanceSqr = distanceSqr;
            bestTarget = playerTarget;
        }

        if (bestTarget != null)
            target = bestTarget;

        if (!target)
            return;

        targetStats = target.GetComponentInParent<CharacterStats>();
        targetCrouch = target.GetComponentInParent<ICrouchProvider>();
        targetMove = target.GetComponentInParent<IMovementNoiseProvider>();
        lastTargetPos = target.position;
    }

    static Transform ResolvePlayerTargetTransform(GameObject player)
    {
        if (player == null)
            return null;

        CharacterController controller = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        if (controller != null)
            return controller.transform;

        ThirdPersonController thirdPersonController = player.GetComponent<ThirdPersonController>() ?? player.GetComponentInChildren<ThirdPersonController>(true);
        if (thirdPersonController != null)
            return thirdPersonController.transform;

        CharacterStats stats = player.GetComponent<CharacterStats>() ?? player.GetComponentInChildren<CharacterStats>(true);
        if (stats != null)
            return stats.transform;

        return player.transform;
    }

    void CacheAnimatorParameters()
    {
        animIDSpeed = Animator.StringToHash(speedParameter);
        animIDAttack = Animator.StringToHash(attackTriggerParameter);
        animIDHit = Animator.StringToHash(hitTriggerParameter);
        animIDDead = Animator.StringToHash(deadParameter);
        animIDIsAttacking = Animator.StringToHash(attackBoolParameter);
    }

    void UpdateAnimator()
    {
        if (!animator)
            return;

        float speed = !dead && agent != null && agent.enabled ? agent.velocity.XZ().magnitude : 0f;
        animator.SetFloat(animIDSpeed, speed, animatorSpeedDampTime, Time.deltaTime);
        animator.SetBool(animIDDead, dead);
    }

    void UpdateDeathAnimation()
    {
        UpdateAnimator();

        if (!animator || deathAnimationFrozen || animator.IsInTransition(0))
            return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(deathStateName) || stateInfo.normalizedTime < deathFreezeNormalizedTime)
            return;

        animator.Play(deathStateName, 0, deathFreezeNormalizedTime);
        animator.speed = 0f;
        deathAnimationFrozen = true;
    }

    void StartAttack()
    {
        atkTimer = attackCooldown;

        if (attackRoutine != null)
            return;

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        if (animator)
        {
            animator.SetBool(animIDIsAttacking, true);
            animator.ResetTrigger(animIDAttack);
            animator.SetTrigger(animIDAttack);
        }

        float delay = Mathf.Max(0f, attackHitDelay);
        float elapsed = 0f;

        while (elapsed < delay)
        {
            if (dead)
            {
                attackRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        DealAttackDamage();

        if (animator)
            animator.SetBool(animIDIsAttacking, false);

        attackRoutine = null;
    }

    void DealAttackDamage()
    {
        if (dead || !target || attackDamage <= 0f)
            return;

        if (DistToTarget() > attackRange + attackHitRadius)
            return;

        targetStats = targetStats ? targetStats : target.GetComponentInParent<CharacterStats>();

        if (targetStats == null || targetStats.IsDead)
            return;

        targetStats.ChangeHealth(-attackDamage);
    }

    void HandleDamageTaken(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (dead)
            return;

        if (animator)
        {
            animator.ResetTrigger(animIDHit);
            animator.SetTrigger(animIDHit);
        }

        if (target)
        {
            Remember(target.position);
            suspicion = 1f;
            SetState(State.Chase);
        }
        else
        {
            RememberSound(SampleOnNavMesh(hitPoint));
            SetState(State.Alert);
        }
    }

    void HandleGunshotReported(ZombieNoiseSystem.GunshotNoise noise)
    {
        if (!reactToGunshots || dead || noise.Radius <= 0f)
        {
            return;
        }

        if (onlyReactToTargetGunshots && (target == null || noise.OwnerRoot != target.root))
        {
            return;
        }

        float radius = noise.Radius;
        float sqrDistance = (transform.position.XZ() - noise.Position.XZ()).sqrMagnitude;

        if (sqrDistance > radius * radius)
        {
            return;
        }

        float distance = Mathf.Sqrt(sqrDistance);
        float distanceFactor = 1f - Mathf.Clamp01(distance / radius);
        float heardSuspicion = Mathf.Lerp(Mathf.Clamp01(minGunshotSuspicion), Mathf.Clamp01(noise.Suspicion), distanceFactor);
        Vector3 heardPosition = SampleGunshotPosition(noise.Position, distanceFactor);

        RememberSound(heardPosition);
        suspicion = Mathf.Max(suspicion, heardSuspicion);

        if (agent != null && agent.enabled && state != State.Attack && state != State.Chase)
        {
            SetState(State.Alert);
        }
    }

    void HandleDeath()
    {
        if (dead)
            return;

        dead = true;
        deathAnimationFrozen = false;
        state = State.Dead;
        suspicion = 0f;
        breadcrumbs.Clear();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (animator)
        {
            animator.speed = 1f;
            animator.SetBool(animIDIsAttacking, false);
            animator.ResetTrigger(animIDAttack);
            animator.ResetTrigger(animIDHit);
            animator.SetBool(animIDDead, true);
            animator.CrossFadeInFixedTime(deathStateName, 0.1f);
        }

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }

            if (disableAgentOnDeath && agent.enabled)
                agent.enabled = false;
        }

        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
        }

        if (mainCollider != null && disableMainColliderOnDeath)
            mainCollider.enabled = false;

        ScheduleCorpseDespawn();
    }

    void ScheduleCorpseDespawn()
    {
        if (!despawnCorpseAfterDeath)
            return;

        float delay = GetCorpseDespawnDelay();

        if (delay > 0f)
            Destroy(gameObject, delay);
    }

    float GetCorpseDespawnDelay()
    {
        if (destroyAfterDeathDelay > 0f)
            return destroyAfterDeathDelay;

        float minDelay = Mathf.Max(0f, corpseDespawnMinDelay);
        float maxDelay = Mathf.Max(minDelay, corpseDespawnMaxDelay);

        return Mathf.Approximately(minDelay, maxDelay)
            ? minDelay
            : Random.Range(minDelay, maxDelay);
    }

    void SenseAndThink()
    {
        hasLOS = false;
        if (!target) return;

        float dist = DistToTarget();

        Vector3 curPos = target.position;
        targetVel = (curPos - lastTargetPos) / Mathf.Max(0.0001f, Time.deltaTime);
        lastTargetPos = curPos;

        if (!(ignoreHearingWhenTargetCrouched && TargetIsCrouching()))
        {
            var n = ReadNoise();
            if (dist <= hearFar && (n.sprinting || n.justJumped)) { Remember(target.position); suspicion = Mathf.Min(1f, suspicion + suspicionRise * Time.deltaTime); }
            else if (dist <= hearMid && (n.running || n.sprinting || n.justJumped)) { Remember(target.position); suspicion = Mathf.Min(1f, suspicion + 0.7f * suspicionRise * Time.deltaTime); }
            else if (dist <= hearNear && n.moving) { Remember(target.position); suspicion = Mathf.Min(1f, suspicion + 0.4f * suspicionRise * Time.deltaTime); }
        }

        bool seen = false;
        if (CheckVision(viewAngle, viewDistance)) { seen = true; suspicion = 1f; }
        else if (CheckVision(peripheralAngle, peripheralDistance)) { suspicion = Mathf.Min(1f, suspicion + suspicionRise * Time.deltaTime); }

        if (!seen) suspicion = Mathf.Max(0f, suspicion - suspicionFall * Time.deltaTime);

        if (seen || state == State.Chase || (state == State.Alert && lastMemoryIsTarget))
        {
            breadcrumbTimer -= Time.deltaTime;
            if (breadcrumbTimer <= 0f)
            {
                breadcrumbTimer = breadcrumbRecordStep;
                PushBreadcrumb(SampleOnNavMesh(target.position));
            }
        }

        if (lastMemoryIsTarget && suspicion >= suspicionToChase && state != State.Chase && state != State.Attack)
            SetState(State.Chase);
    }

    bool CheckVision(float fov, float maxDist)
    {
        Vector3 eye = eyes.position;
        Vector3 head = target.position + Vector3.up * 0.8f;
        Vector3 dir = (head - eye);
        float dist = DistToTarget();
        bool closeEnough = dist <= Mathf.Max(closeSightDistance, attackRange + attackHitRadius);

        if (!closeEnough && dir.sqrMagnitude > maxDist * maxDist) return false;

        Vector3 flatDir = dir.XZ();
        Vector3 flatForward = (eyes ? eyes.forward : transform.forward).XZ();
        if (!closeEnough && flatDir.sqrMagnitude > 0.001f && flatForward.sqrMagnitude > 0.001f)
        {
            if (Vector3.Angle(flatForward.normalized, flatDir.normalized) > fov * 0.5f)
                return false;
        }

        if (!HasLineOfSightToTarget(eye, head)) return false;

        hasLOS = true;
        Remember(target.position);
        return true;
    }

    bool HasLineOfSightToTarget(Vector3 from, Vector3 to)
    {
        Vector3 ray = to - from;
        float distance = ray.magnitude;

        if (distance <= 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            from,
            ray / distance,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitRoot = hit.collider.transform.root;

            if (hitRoot == transform.root)
                continue;

            if (target && hitRoot == target.root)
                return true;

            return false;
        }

        return true;
    }

    struct Noise { public bool moving, running, sprinting, justJumped; }
    Noise ReadNoise()
    {
        if (targetMove != null)
            return new Noise { moving = targetMove.IsMoving, running = targetMove.IsRunning, sprinting = targetMove.IsSprinting, justJumped = targetMove.JustJumped };

        float v = targetVel.XZ().magnitude;
        return new Noise { moving = v > 0.05f, running = v > 1.5f, sprinting = v > 3.0f, justJumped = false };
    }

    bool TargetIsCrouching()
    {
        if (!target) return false;
        if (targetCrouch != null) return targetCrouch.IsCrouching;
        var cc = target.GetComponentInParent<CharacterController>();
        return cc && cc.height < 1.4f;
    }

    void Remember(Vector3 pos)
    {
        RememberPosition(pos, true);
    }

    void RememberSound(Vector3 pos)
    {
        RememberPosition(pos, false);
    }

    void RememberPosition(Vector3 pos, bool memoryIsTarget)
    {
        lastMemoryIsTarget = memoryIsTarget;
        lastSeenDir = pos - (eyes ? eyes.position : transform.position);
        lastSeenDir.y = 0f;
        if (lastSeenDir.sqrMagnitude > 1e-4f) lastSeenDir.Normalize();

        lastSeenPos = pos;
        lastSeenT = Time.time;
    }

    Vector3 SampleGunshotPosition(Vector3 noisePosition, float distanceFactor)
    {
        float randomness = Mathf.Max(0f, gunshotPositionRandomness) * (1f - Mathf.Clamp01(distanceFactor));

        if (randomness > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * randomness;
            noisePosition += new Vector3(offset.x, 0f, offset.y);
        }

        return SampleOnNavMesh(noisePosition);
    }

    Vector3 PredictTargetPosition()
    {
        Vector3 pred = target ? target.position + targetVel * predictionTime : lastSeenPos;
        return SampleOnNavMesh(pred);
    }

    void PushBreadcrumb(Vector3 p)
    {
        if (breadcrumbs.Count == 0 || (breadcrumbs.Peek() - p).sqrMagnitude > 0.3f * 0.3f)
        {
            breadcrumbs.Enqueue(p);
            while (breadcrumbs.Count > breadcrumbMax) breadcrumbs.Dequeue();
        }
    }

    void BuildSearchPlan()
    {
        searchPlan.Clear();
        Vector3 lost = SampleOnNavMesh(lastSeenPos);

        if (breadcrumbs.Count > 0)
        {
            var tmp = new List<Vector3>(breadcrumbs);
            for (int i = tmp.Count - 1; i >= 0; --i) TryAddSearchPoint(tmp[i]);
        }

        TryAddSearchPoint(lost);

        if (lastSeenDir.sqrMagnitude < 1e-4f) lastSeenDir = transform.forward;

        for (int i = 1; i <= wedgePoints; i++)
        {
            float d = wedgeStepDist * i;
            Vector3 dir0 = lastSeenDir;
            Vector3 dirL = Quaternion.Euler(0, -wedgeHalfAngle, 0) * lastSeenDir;
            Vector3 dirR = Quaternion.Euler(0, wedgeHalfAngle, 0) * lastSeenDir;
            TryAddSearchPoint(lost + dir0 * d);
            TryAddSearchPoint(lost + dirL * d * 0.9f);
            TryAddSearchPoint(lost + dirR * d * 0.9f);
        }

        float step = 360f / Mathf.Max(1, innerRingPoints);
        for (int i = 0; i < innerRingPoints; i++)
        {
            Vector3 dir = Quaternion.Euler(0, step * i, 0) * Vector3.forward;
            TryAddSearchPoint(lost + dir * innerRingRadius);
        }

        for (int k = 0; k < Mathf.Max(0, revisitLostCount); k++)
        {
            Vector2 rnd = Random.insideUnitCircle.normalized * (0.25f + 0.15f * k);
            TryAddSearchPoint(lost + new Vector3(rnd.x, 0, rnd.y));
            float ringR = innerRingRadius + revisitSpacing * (k + 1);
            for (int i = 0; i < 3; i++)
            {
                float ang = (120f * i + 37f * k);
                Vector3 dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
                TryAddSearchPoint(lost + dir * ringR);
            }
            TryAddSearchPoint(lost);
        }

        DedupPlan(0.5f);
    }

    void RunSearch()
    {
        agent.speed = walkSpeed;
        searchTimer += Time.deltaTime;

        if (CanEngage()) { SetState(State.Chase); return; }
        if (searchTimer >= searchMaxTime) { CalmDown(); return; }
        if (searchIdx >= searchPlan.Count) { CalmDown(); return; }

        Vector3 goal = searchPlan[searchIdx];
        bool atGoal = Arrived(goal, 0.35f);
        if (!agent.hasPath && !atGoal) SafeSetDestination(goal);

        if (atGoal)
        {
            SafeResetPath();

            bool atLost = (goal - lastSeenPos).sqrMagnitude <= 0.5f * 0.5f;
            if (atLost && waitTimer < investigateTime)
            {
                waitTimer += Time.deltaTime;
                float yaw = Mathf.Sin(Time.time * investigateYawSpeed * Mathf.Deg2Rad) * wedgeHalfAngle;
                Quaternion look = Quaternion.LookRotation(Quaternion.Euler(0, yaw, 0) * (lastSeenDir.sqrMagnitude > 0 ? lastSeenDir : transform.forward));
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
                return;
            }

            waitTimer = 0f;
            searchIdx++;
            if (searchIdx < searchPlan.Count) SafeSetDestination(searchPlan[searchIdx]);
        }
    }

    void TryAddSearchPoint(Vector3 pos)
    {
        Vector3 snap = SampleOnNavMesh(pos);
        if ((snap - pos).sqrMagnitude <= 4f) searchPlan.Add(snap);
    }

    void DedupPlan(float minDist)
    {
        for (int i = 0; i < searchPlan.Count; ++i)
            for (int j = searchPlan.Count - 1; j > i; --j)
                if ((searchPlan[i] - searchPlan[j]).sqrMagnitude < minDist * minDist)
                    searchPlan.RemoveAt(j);
    }

    void CalmDown()
    {
        suspicion = 0f;
        breadcrumbs.Clear();
        lastSeenT = float.NegativeInfinity;
        lastMemoryIsTarget = true;
        if (patrolPoints != null && patrolPoints.Length > 0) SetState(State.Patrol);
        else SetState(State.Idle);
    }

    System.Collections.IEnumerator TraverseLinkBallistic()
    {
        traversing = true;

        var data = agent.currentOffMeshLinkData;
        Vector3 start = transform.position;
        Vector3 end = data.endPos;

        agent.isStopped = true;
        agent.updatePosition = false;
        agent.updateRotation = false;
        rb.isKinematic = false;

        float t = Mathf.Max(0.1f, jumpTime);
        Vector3 delta = end - start;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        float g = Mathf.Abs(Physics.gravity.y);

        Vector3 vXZ = deltaXZ / t;
        float vy = (delta.y + 0.5f * g * t * t) / t + extraUpVelocity;
        rb.velocity = vXZ + Vector3.up * vy;

        if (deltaXZ.sqrMagnitude > 1e-4f)
            transform.rotation = Quaternion.LookRotation(deltaXZ.normalized, Vector3.up);

        float elapsed = 0f;
        while (elapsed < t * 1.25f) { elapsed += Time.deltaTime; yield return null; }

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        agent.CompleteOffMeshLink();
        agent.Warp(SampleOnNavMesh(transform.position));
        agent.updatePosition = true; agent.updateRotation = true; agent.isStopped = false;

        traversing = false;
    }

    void SetState(State s)
    {
        if (dead && s != State.Dead)
            return;

        if (agent == null || !agent.enabled)
            return;

        state = s;
        if (s == State.Idle || s == State.Patrol) { lastSeenT = float.NegativeInfinity; lastMemoryIsTarget = true; suspicion = 0f; }

        switch (s)
        {
            case State.Idle:
                SafeResetPath(); agent.speed = 0f; break;
            case State.Patrol:
                agent.isStopped = false; agent.speed = walkSpeed; NextPatrol(); break;
            case State.Alert:
                agent.isStopped = false; break;
            case State.Chase:
                agent.isStopped = false; break;
            case State.Attack:
                agent.isStopped = true; FaceTo(target ? target.position : transform.position + transform.forward); break;
            case State.Search:
                agent.isStopped = false;
                BuildSearchPlan();
                searchIdx = 0; searchTimer = 0f; waitTimer = 0f;
                if (searchPlan.Count > 0) SafeSetDestination(searchPlan[0]);
                break;
            case State.Dead:
                agent.isStopped = true;
                SafeResetPath();
                break;
        }
    }

    void NextPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) { SetState(State.Idle); return; }
        SafeSetDestination(patrolPoints[patrolIx].position);
        patrolIx = (patrolIx + 1) % patrolPoints.Length;
    }

    void FaceTo(Vector3 p)
    {
        Vector3 dir = p - transform.position; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        Quaternion to = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, to, 1f - Mathf.Exp(-turnLerp * Time.deltaTime));
    }

    Vector3 SampleOnNavMesh(Vector3 pos)
    {
        return NavMesh.SamplePosition(pos, out var hit, reprojectRadius, NavMesh.AllAreas) ? hit.position : pos;
    }

    void SnapToNavMesh()
    {
        if (agent == null || !agent.enabled)
            return;

        if (NavMesh.SamplePosition(transform.position, out var hit, 5f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    void SafeResetPath()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    bool SafeSetDestination(Vector3 dst)
    {
        if (agent == null || !agent.enabled || dead) return false;
        if (!agent.isOnNavMesh) { SnapToNavMesh(); if (!agent.isOnNavMesh) return false; }
        return agent.SetDestination(dst);
    }

    bool CanEngage() => !dead && target && (targetStats == null || !targetStats.IsDead) && (hasLOS || RememberingTarget() || (lastMemoryIsTarget && suspicion >= suspicionToChase * 0.8f));
    bool CanAttackTarget() => CanEngage() && hasLOS && DistToTarget() <= attackRange + attackHitRadius;
    bool Remembering() => lastSeenT > 0f && (Time.time - lastSeenT) <= memoryTime;
    bool RememberingTarget() => lastMemoryIsTarget && Remembering();
    float DistToTarget() => target ? Vector3.Distance(transform.position.XZ(), target.position.XZ()) : Mathf.Infinity;
    bool Arrived(Vector3 p, float r) => (transform.position.XZ() - p.XZ()).sqrMagnitude <= r * r;

    void OnDrawGizmosSelected()
    {
        if (!eyes) eyes = transform;
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f); Gizmos.DrawWireSphere(transform.position, hearFar);
        Gizmos.color = new Color(1f, 0.65f, 0f, 0.25f); Gizmos.DrawWireSphere(transform.position, hearMid);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.25f); Gizmos.DrawWireSphere(transform.position, hearNear);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, 0.1f);
        Vector3 dir = eyes ? eyes.forward : transform.forward;
        Vector3 L = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * dir;
        Vector3 R = Quaternion.Euler(0, viewAngle * 0.5f, 0) * dir;
        Gizmos.DrawLine(eyes.position, eyes.position + L * viewDistance);
        Gizmos.DrawLine(eyes.position, eyes.position + R * viewDistance);
        Gizmos.color = Color.gray;
        Vector3 Lp = Quaternion.Euler(0, -peripheralAngle * 0.5f, 0) * dir;
        Vector3 Rp = Quaternion.Euler(0, peripheralAngle * 0.5f, 0) * dir;
        Gizmos.DrawLine(eyes.position, eyes.position + Lp * peripheralDistance);
        Gizmos.DrawLine(eyes.position, eyes.position + Rp * peripheralDistance);
        Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(lastSeenPos, 0.25f);
    }
}

static class VecXZ
{
    public static Vector3 XZ(this Vector3 v) => new Vector3(v.x, 0f, v.z);
}

public interface ICrouchProvider
{
    bool IsCrouching { get; }
}

public interface IMovementNoiseProvider
{
    bool IsMoving { get; }
    bool IsRunning { get; }
    bool IsSprinting { get; }
    bool JustJumped { get; }
}

static class ZombieNavMeshBootstrap
{
    static bool built;
    static NavMeshData runtimeData;
    static NavMeshDataInstance runtimeInstance;

    public static void EnsureBuilt(Vector3 probePosition, float probeRadius)
    {
        if (built)
            return;

        if (NavMesh.SamplePosition(probePosition, out _, Mathf.Max(1f, probeRadius), NavMesh.AllAreas))
        {
            built = true;
            return;
        }

        List<NavMeshBuildSource> sources = new();
        List<NavMeshBuildMarkup> markups = new();
        NavMeshBuilder.CollectSources(
            null,
            ~0,
            NavMeshCollectGeometry.RenderMeshes,
            0,
            markups,
            sources);

        sources.RemoveAll(IsDynamicActorSource);

        if (sources.Count == 0)
            return;

        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
        Bounds bounds = CalculateBounds(sources, probePosition, probeRadius);
        runtimeData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);

        if (runtimeData == null)
            return;

        runtimeInstance = NavMesh.AddNavMeshData(runtimeData);
        built = runtimeInstance.valid;
    }

    static bool IsDynamicActorSource(NavMeshBuildSource source)
    {
        if (source.component is not Component component)
            return false;

        Transform sourceTransform = component.transform;
        return sourceTransform.GetComponentInParent<NavMeshAgent>() != null
            || sourceTransform.GetComponentInParent<NavMeshObstacle>() != null
            || sourceTransform.GetComponentInParent<CharacterController>() != null
            || sourceTransform.GetComponentInParent<Rigidbody>() != null;
    }

    static Bounds CalculateBounds(List<NavMeshBuildSource> sources, Vector3 fallbackCenter, float fallbackRadius)
    {
        Bounds bounds = new(fallbackCenter, Vector3.one * Mathf.Max(1f, fallbackRadius));
        bool hasBounds = false;

        foreach (NavMeshBuildSource source in sources)
        {
            if (!TryGetSourceBounds(source, out Bounds sourceBounds))
                continue;

            if (!hasBounds)
            {
                bounds = sourceBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(sourceBounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(fallbackCenter, Vector3.one * Mathf.Max(64f, fallbackRadius));

        bounds.Expand(2f);
        return bounds;
    }

    static bool TryGetSourceBounds(NavMeshBuildSource source, out Bounds bounds)
    {
        if (source.component is Collider collider)
        {
            bounds = collider.bounds;
            return true;
        }

        if (source.component is Terrain terrain && terrain.terrainData != null)
        {
            bounds = terrain.terrainData.bounds;
            bounds.center += terrain.transform.position;
            return true;
        }

        if (source.component is Renderer renderer)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }
}
