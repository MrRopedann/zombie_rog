using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RagdollController : MonoBehaviour
{
    private struct BoneSegment
    {
        public HumanBodyBones Bone;
        public HumanBodyBones EndBone;
        public float Radius;
        public float MassShare;
        public bool Sphere;

        public BoneSegment(HumanBodyBones bone, HumanBodyBones endBone, float radius, float massShare, bool sphere = false)
        {
            Bone = bone;
            EndBone = endBone;
            Radius = radius;
            MassShare = massShare;
            Sphere = sphere;
        }
    }

    [Header("Ragdoll parts")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;
    public Animator animator;

    [Header("Auto build")]
    [SerializeField] private bool autoBuildFromHumanoidBones = true;
    [SerializeField] [Min(1f)] private float totalMass = 70f;
    [SerializeField] [Min(0.1f)] private float colliderRadiusScale = 1f;
    [SerializeField] private bool disableCollidersWhileAnimated = true;
    [SerializeField] private bool logBuildWarnings = true;

    private bool initialized;
    private bool autoBuildAttempted;
    private bool ragdollEnabled;
    private const int MinUsableBodyCount = 3;

    public bool HasRagdollParts
    {
        get
        {
            InitializeIfNeeded(false);
            return HasUsableRagdollParts();
        }
    }

    private static readonly BoneSegment[] HumanoidSegments =
    {
        new BoneSegment(HumanBodyBones.Hips, HumanBodyBones.Spine, 0.16f, 0.16f),
        new BoneSegment(HumanBodyBones.Spine, HumanBodyBones.Chest, 0.17f, 0.14f),
        new BoneSegment(HumanBodyBones.Chest, HumanBodyBones.UpperChest, 0.18f, 0.16f),
        new BoneSegment(HumanBodyBones.UpperChest, HumanBodyBones.Neck, 0.16f, 0.08f),
        new BoneSegment(HumanBodyBones.Head, HumanBodyBones.LastBone, 0.13f, 0.08f, true),

        new BoneSegment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 0.055f, 0.035f),
        new BoneSegment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 0.045f, 0.03f),
        new BoneSegment(HumanBodyBones.LeftHand, HumanBodyBones.LastBone, 0.045f, 0.015f, true),
        new BoneSegment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.055f, 0.035f),
        new BoneSegment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 0.045f, 0.03f),
        new BoneSegment(HumanBodyBones.RightHand, HumanBodyBones.LastBone, 0.045f, 0.015f, true),

        new BoneSegment(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, 0.075f, 0.08f),
        new BoneSegment(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, 0.06f, 0.06f),
        new BoneSegment(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 0.05f, 0.025f),
        new BoneSegment(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 0.075f, 0.08f),
        new BoneSegment(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, 0.06f, 0.06f),
        new BoneSegment(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 0.05f, 0.025f)
    };

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>(true);
        CollectRagdollParts();
    }

    private void Awake()
    {
        InitializeIfNeeded(false);

        if (!ragdollEnabled)
        {
            DisableRagdoll();
        }
    }

    private void Start()
    {
        InitializeIfNeeded(false);

        if (!ragdollEnabled)
        {
            DisableRagdoll();
        }
    }

    public void InitializeIfNeeded()
    {
        InitializeIfNeeded(false);
    }

    public bool PrepareRagdoll()
    {
        InitializeIfNeeded(true);
        return HasUsableRagdollParts();
    }

    private void InitializeIfNeeded(bool allowAutoBuild)
    {
        if (initialized)
        {
            if (allowAutoBuild)
            {
                TryAutoBuildIfNeeded();
            }

            return;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        CollectRagdollParts();
        initialized = true;
        TryAutoBuildIfNeeded(allowAutoBuild);
    }

    private void TryAutoBuildIfNeeded(bool allowAutoBuild = true)
    {
        if (!allowAutoBuild || autoBuildAttempted || HasUsableRagdollParts() || !autoBuildFromHumanoidBones)
        {
            return;
        }

        autoBuildAttempted = true;
        TryBuildHumanoidRagdoll();
        CollectRagdollParts();
    }

    public void EnableRagdoll()
    {
        if (!PrepareRagdoll())
        {
            return;
        }

        ragdollEnabled = true;

        if (animator != null)
        {
            animator.enabled = false;
        }

        if (ragdollBodies != null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                {
                    continue;
                }

                body.isKinematic = false;
                StopBodyMotion(body);
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.WakeUp();
            }
        }

        if (ragdollColliders != null)
        {
            foreach (Collider ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider == null)
                {
                    continue;
                }

                ragdollCollider.enabled = true;
                ragdollCollider.isTrigger = false;
            }
        }
    }

    public void DisableRagdoll()
    {
        InitializeIfNeeded(false);
        ragdollEnabled = false;

        if (ragdollBodies != null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                {
                    continue;
                }

                StopBodyMotion(body);
                body.isKinematic = true;
                body.detectCollisions = false;
                body.interpolation = RigidbodyInterpolation.None;
            }
        }

        if (disableCollidersWhileAnimated && ragdollColliders != null)
        {
            foreach (Collider ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider == null)
                {
                    continue;
                }

                ragdollCollider.enabled = false;
            }
        }

        if (animator != null)
        {
            animator.enabled = true;
        }
    }

    public void ApplyImpulse(Vector3 impulse, Vector3 worldPoint)
    {
        PrepareRagdoll();

        if (ragdollBodies == null || impulse.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Rigidbody nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(body.worldCenterOfMass - worldPoint);

            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearest = body;
        }

        if (nearest != null)
        {
            nearest.AddForceAtPosition(impulse, worldPoint, ForceMode.Impulse);
        }
    }

    private void CollectRagdollParts()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        List<Rigidbody> filteredBodies = new List<Rigidbody>(bodies.Length);

        foreach (Rigidbody body in bodies)
        {
            if (!IsValidRagdollBody(body))
            {
                continue;
            }

            filteredBodies.Add(body);
        }

        HashSet<Rigidbody> bodySet = new HashSet<Rigidbody>(filteredBodies);
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        List<Collider> filteredColliders = new List<Collider>(colliders.Length);

        foreach (Collider childCollider in colliders)
        {
            if (!IsValidRagdollCollider(childCollider, bodySet))
            {
                continue;
            }

            filteredColliders.Add(childCollider);
        }

        ragdollBodies = filteredBodies.ToArray();
        ragdollColliders = filteredColliders.ToArray();
    }

    private bool HasUsableRagdollParts()
    {
        if (ragdollBodies == null)
        {
            return false;
        }

        int usableBodyCount = 0;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (IsValidRagdollBody(body))
            {
                usableBodyCount++;
            }
        }

        return usableBodyCount >= MinUsableBodyCount;
    }

    private bool IsValidRagdollBody(Rigidbody body)
    {
        if (body == null || body.transform == transform)
        {
            return false;
        }

        return animator == null || body.transform.IsChildOf(animator.transform);
    }

    private bool IsValidRagdollCollider(Collider childCollider, HashSet<Rigidbody> bodySet)
    {
        if (childCollider == null || childCollider is CharacterController || childCollider.transform == transform)
        {
            return false;
        }

        Rigidbody attachedBody = childCollider.attachedRigidbody;

        if (attachedBody != null)
        {
            return bodySet.Contains(attachedBody);
        }

        Rigidbody parentBody = childCollider.GetComponentInParent<Rigidbody>();
        return parentBody != null && bodySet.Contains(parentBody);
    }

    private void TryBuildHumanoidRagdoll()
    {
        if (animator == null || animator.avatar == null || !animator.isHuman)
        {
            if (logBuildWarnings)
            {
                Debug.LogWarning($"{nameof(RagdollController)} could not auto-build ragdoll: humanoid Animator not found.", this);
            }

            return;
        }

        Dictionary<Transform, Rigidbody> bodyByBone = new Dictionary<Transform, Rigidbody>();
        List<Rigidbody> builtBodies = new List<Rigidbody>();
        float massShareTotal = 0f;

        foreach (BoneSegment segment in HumanoidSegments)
        {
            Transform bone = GetBone(segment.Bone);

            if (bone == null)
            {
                continue;
            }

            Rigidbody body = bone.GetComponent<Rigidbody>();

            if (body == null)
            {
                body = bone.gameObject.AddComponent<Rigidbody>();
            }

            ConfigureBody(body);
            body.mass = Mathf.Max(0.1f, totalMass * segment.MassShare);
            bodyByBone[bone] = body;
            builtBodies.Add(body);
            massShareTotal += segment.MassShare;

            if (segment.Sphere)
            {
                ConfigureSphereCollider(bone, segment.Radius * colliderRadiusScale);
            }
            else
            {
                Transform endBone = GetSegmentEndBone(segment);
                ConfigureCapsuleCollider(bone, endBone, segment.Radius * colliderRadiusScale);
            }
        }

        if (builtBodies.Count == 0)
        {
            if (logBuildWarnings)
            {
                Debug.LogWarning($"{nameof(RagdollController)} could not auto-build ragdoll: no supported humanoid bones were found.", this);
            }

            return;
        }

        if (massShareTotal > 0f)
        {
            float massScale = totalMass / Mathf.Max(0.1f, GetCurrentMassTotal(builtBodies));

            foreach (Rigidbody body in builtBodies)
            {
                body.mass *= massScale;
            }
        }

        foreach (KeyValuePair<Transform, Rigidbody> pair in bodyByBone)
        {
            Rigidbody parentBody = FindParentBody(pair.Key, bodyByBone);

            if (parentBody == null)
            {
                continue;
            }

            ConfigureJoint(pair.Value, parentBody);
        }
    }

    private Transform GetBone(HumanBodyBones bone)
    {
        if (bone == HumanBodyBones.LastBone || animator == null || !animator.isHuman)
        {
            return null;
        }

        return animator.GetBoneTransform(bone);
    }

    private Transform GetSegmentEndBone(BoneSegment segment)
    {
        Transform endBone = GetBone(segment.EndBone);

        if (endBone != null)
        {
            return endBone;
        }

        if (segment.EndBone == HumanBodyBones.Chest)
        {
            return GetBone(HumanBodyBones.UpperChest) ?? GetBone(HumanBodyBones.Neck);
        }

        if (segment.EndBone == HumanBodyBones.UpperChest)
        {
            return GetBone(HumanBodyBones.Neck);
        }

        return null;
    }

    private static float GetCurrentMassTotal(List<Rigidbody> bodies)
    {
        float mass = 0f;

        foreach (Rigidbody body in bodies)
        {
            if (body != null)
            {
                mass += body.mass;
            }
        }

        return mass;
    }

    private static void ConfigureBody(Rigidbody body)
    {
        body.detectCollisions = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        StopBodyMotion(body);
        body.isKinematic = true;
    }

    private static void StopBodyMotion(Rigidbody body)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private static void ConfigureSphereCollider(Transform bone, float radius)
    {
        SphereCollider sphere = bone.GetComponent<SphereCollider>();

        if (sphere == null)
        {
            sphere = bone.gameObject.AddComponent<SphereCollider>();
        }

        sphere.center = Vector3.zero;
        sphere.radius = Mathf.Max(0.01f, radius);
        sphere.isTrigger = false;
        sphere.enabled = false;
    }

    private static void ConfigureCapsuleCollider(Transform bone, Transform endBone, float radius)
    {
        CapsuleCollider capsule = bone.GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        }

        Vector3 localEnd = endBone != null
            ? bone.InverseTransformPoint(endBone.position)
            : Vector3.up * Mathf.Max(radius * 3f, 0.15f);

        float length = Mathf.Max(localEnd.magnitude, radius * 2f);
        capsule.direction = GetDominantAxis(localEnd);
        capsule.center = localEnd * 0.5f;
        capsule.radius = Mathf.Max(0.01f, radius);
        capsule.height = Mathf.Max(length, capsule.radius * 2f);
        capsule.isTrigger = false;
        capsule.enabled = false;
    }

    private static int GetDominantAxis(Vector3 vector)
    {
        Vector3 abs = new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

        if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return 0;
        }

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return 1;
        }

        return 2;
    }

    private static Rigidbody FindParentBody(Transform bone, Dictionary<Transform, Rigidbody> bodyByBone)
    {
        Transform current = bone.parent;

        while (current != null)
        {
            if (bodyByBone.TryGetValue(current, out Rigidbody body))
            {
                return body;
            }

            current = current.parent;
        }

        return null;
    }

    private static void ConfigureJoint(Rigidbody body, Rigidbody connectedBody)
    {
        CharacterJoint joint = body.GetComponent<CharacterJoint>();

        if (joint == null)
        {
            joint = body.gameObject.AddComponent<CharacterJoint>();
        }

        joint.connectedBody = connectedBody;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        joint.enableProjection = true;
        joint.projectionDistance = 0.1f;
        joint.projectionAngle = 20f;

        SoftJointLimit lowTwistLimit = joint.lowTwistLimit;
        lowTwistLimit.limit = -25f;
        joint.lowTwistLimit = lowTwistLimit;

        SoftJointLimit highTwistLimit = joint.highTwistLimit;
        highTwistLimit.limit = 25f;
        joint.highTwistLimit = highTwistLimit;

        SoftJointLimit swingLimit = joint.swing1Limit;
        swingLimit.limit = 35f;
        joint.swing1Limit = swingLimit;

        swingLimit = joint.swing2Limit;
        swingLimit.limit = 25f;
        joint.swing2Limit = swingLimit;
    }
}
