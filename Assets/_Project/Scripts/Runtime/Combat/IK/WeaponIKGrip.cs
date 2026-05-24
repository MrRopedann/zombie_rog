using UnityEngine;

[DisallowMultipleComponent]
public class WeaponIKGrip : MonoBehaviour
{
    [Header("Grip Points")]
    [SerializeField] private Transform rightHandGrip;
    [SerializeField] private Transform leftHandGrip;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform aimReference;
    [SerializeField] private Transform casingEjectionPoint;

    [Header("Mounting")]
    [SerializeField] private bool attachToWeaponMount = true;
    [SerializeField] private bool alignRightGripToMount = false;
    [SerializeField] private bool snapMountedAimRotation = true;
    [SerializeField] private bool useDirectionAxesForMountRotation = false;
    [SerializeField] private Vector3 localPositionInMount = Vector3.zero;
    [SerializeField] private Vector3 localEulerAnglesInMount = Vector3.zero;
    [SerializeField] private Vector3 localForwardInMount = Vector3.forward;
    [SerializeField] private Vector3 localUpInMount = Vector3.up;
    [SerializeField] private Vector3 mountedPosePositionOffset = Vector3.zero;
    [SerializeField] private Vector3 mountedPoseEulerOffset = Vector3.zero;

    [Header("IK")]
    [SerializeField] private WeaponIKActivationMode activationMode = WeaponIKActivationMode.AlwaysEquipped;
    [SerializeField, Range(0f, 1f)] private float rigWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float rightHandWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftHandWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float bodyAimWeight = 1f;

    [Header("IK Target Weights")]
    [SerializeField, Range(0f, 1f)] private float rightHandTargetPositionWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float rightHandTargetRotationWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftHandTargetPositionWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftHandTargetRotationWeight = 1f;

    [Header("IK Target Rotation")]
    [SerializeField] private bool copyRightHandGripRotation = true;
    [SerializeField] private bool copyLeftHandGripRotation = true;
    [SerializeField] private Vector3 rightHandTargetEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 leftHandTargetEulerOffset = Vector3.zero;

    [Header("Animator")]
    [SerializeField] private string equippedAnimatorBool;
    [SerializeField] private string aimAnimatorBool;
    [SerializeField] private string animatorLayerName;
    [SerializeField, Range(0f, 1f)] private float animatorLayerActiveWeight = 1f;

    [HideInInspector] [SerializeField] private bool overrideControllerAimSettings = false;
    [HideInInspector] [SerializeField, Min(0.1f)] private float maxAimDistance = 1000f;
    [HideInInspector] [SerializeField] private LayerMask aimLayerMask = ~0;

    public Transform RightHandGrip => rightHandGrip != null ? rightHandGrip : transform;
    public Transform LeftHandGrip => leftHandGrip;
    public Transform Muzzle => muzzle != null ? muzzle : aimReference;
    public Transform AimReference => aimReference != null ? aimReference : Muzzle != null ? Muzzle : transform;
    public Transform CasingEjectionPoint => casingEjectionPoint != null ? casingEjectionPoint : Muzzle;
    public bool AttachToWeaponMount => attachToWeaponMount;
    public float RigWeight => rigWeight;
    public float RightHandWeight => rightHandWeight;
    public float LeftHandWeight => leftHandGrip != null ? leftHandWeight : 0f;
    public float BodyAimWeight => bodyAimWeight;
    public float RightHandTargetPositionWeight => rightHandTargetPositionWeight;
    public float RightHandTargetRotationWeight => rightHandTargetRotationWeight;
    public float LeftHandTargetPositionWeight => leftHandTargetPositionWeight;
    public float LeftHandTargetRotationWeight => leftHandTargetRotationWeight;
    public bool CopyRightHandGripRotation => copyRightHandGripRotation;
    public bool CopyLeftHandGripRotation => copyLeftHandGripRotation;
    public bool OverrideControllerAimSettings => overrideControllerAimSettings;
    public float MaxAimDistance => maxAimDistance;
    public LayerMask AimLayerMask => aimLayerMask;
    public string EquippedAnimatorBool => equippedAnimatorBool;
    public string AimAnimatorBool => aimAnimatorBool;
    public string AnimatorLayerName => animatorLayerName;
    public float AnimatorLayerActiveWeight => animatorLayerActiveWeight;

    public Quaternion GetRightHandTargetRotation()
    {
        return GetGripTargetRotation(RightHandGrip, rightHandTargetEulerOffset);
    }

    public Quaternion GetLeftHandTargetRotation()
    {
        return GetGripTargetRotation(LeftHandGrip, leftHandTargetEulerOffset);
    }

    private void Reset()
    {
        rightHandGrip = FindPoint("RightHandGrip", "Right Hand Grip", "GripRight", "Grip_R");
        leftHandGrip = FindPoint("LeftHandGrip", "Left Hand Grip", "GripLeft", "Grip_L");
        muzzle = FindPoint("Muzzle", "ShootPoint", "Shoot Point", "FirePoint", "Barrel");
        aimReference = muzzle != null ? muzzle : FindPoint("AimReference", "Aim Reference", "AimPoint");
        casingEjectionPoint = FindPoint("CasingEjectionPoint", "ShellEjectionPoint", "EjectionPoint", "ShellPoint");
    }

    private void OnValidate()
    {
        maxAimDistance = Mathf.Max(0.1f, maxAimDistance);
        rigWeight = Mathf.Clamp01(rigWeight);
        rightHandWeight = Mathf.Clamp01(rightHandWeight);
        leftHandWeight = Mathf.Clamp01(leftHandWeight);
        bodyAimWeight = Mathf.Clamp01(bodyAimWeight);
        rightHandTargetPositionWeight = Mathf.Clamp01(rightHandTargetPositionWeight);
        rightHandTargetRotationWeight = Mathf.Clamp01(rightHandTargetRotationWeight);
        leftHandTargetPositionWeight = Mathf.Clamp01(leftHandTargetPositionWeight);
        leftHandTargetRotationWeight = Mathf.Clamp01(leftHandTargetRotationWeight);
        animatorLayerActiveWeight = Mathf.Clamp01(animatorLayerActiveWeight);
    }

    [ContextMenu("Create Missing IK Points")]
    public void CreateMissingIKPoints()
    {
        if (rightHandGrip == null)
        {
            rightHandGrip = CreatePoint("RightHandGrip", new Vector3(0f, -0.05f, -0.06f));
        }

        if (leftHandGrip == null)
        {
            leftHandGrip = CreatePoint("LeftHandGrip", new Vector3(0f, -0.05f, 0.22f));
        }

        if (muzzle == null)
        {
            muzzle = CreatePoint("Muzzle", new Vector3(0f, 0f, 0.45f));
        }

        if (aimReference == null)
        {
            aimReference = muzzle;
        }

        if (casingEjectionPoint == null)
        {
            casingEjectionPoint = CreatePoint("CasingEjectionPoint", new Vector3(0.055f, 0.055f, 0.15f));
        }
    }

    public void AttachToMount(Transform weaponMount)
    {
        ApplyMountPose(weaponMount);
    }

    public void ApplyMountPose(Transform weaponMount)
    {
        if (!attachToWeaponMount || weaponMount == null)
        {
            return;
        }

        if (alignRightGripToMount && rightHandGrip != null && rightHandGrip != transform)
        {
            ApplyGripMountPose(weaponMount);
            return;
        }

        if (transform.parent != weaponMount)
        {
            transform.SetParent(weaponMount, false);
        }

        transform.localPosition = localPositionInMount;
        transform.localRotation = ResolveMountRotation();
        ApplyMountedPoseOffset();
    }

    private void ApplyGripMountPose(Transform weaponMount)
    {
        Quaternion gripRotationInWeapon = Quaternion.Inverse(transform.rotation) * rightHandGrip.rotation;
        Vector3 gripPositionInWeapon = transform.InverseTransformPoint(rightHandGrip.position);

        if (transform.parent != weaponMount)
        {
            transform.SetParent(weaponMount, false);
        }

        Quaternion desiredGripRotation = ResolveMountRotation();
        Quaternion rootRotation = desiredGripRotation * Quaternion.Inverse(gripRotationInWeapon);

        transform.localRotation = rootRotation;
        transform.localPosition = localPositionInMount - rootRotation * gripPositionInWeapon;
        ApplyMountedPoseOffset();
    }

    private Quaternion ResolveMountRotation()
    {
        if (!useDirectionAxesForMountRotation)
        {
            return Quaternion.Euler(localEulerAnglesInMount);
        }

        Vector3 forward = localForwardInMount.sqrMagnitude > 0.0001f
            ? localForwardInMount.normalized
            : Vector3.forward;
        Vector3 up = localUpInMount.sqrMagnitude > 0.0001f
            ? localUpInMount.normalized
            : Vector3.up;

        if (Vector3.Cross(forward, up).sqrMagnitude <= 0.0001f)
        {
            up = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) < 0.99f ? Vector3.up : Vector3.right;
        }

        return Quaternion.LookRotation(forward, up);
    }

    public bool ShouldUseIK(InputsController inputsController)
    {
        switch (activationMode)
        {
            case WeaponIKActivationMode.AlwaysEquipped:
                return true;
            case WeaponIKActivationMode.AimOrFire:
                return inputsController != null && (inputsController.aim || inputsController.fireHeld);
            case WeaponIKActivationMode.AimOnly:
                return inputsController != null && inputsController.aim;
            case WeaponIKActivationMode.FireOnly:
                return inputsController != null && inputsController.fireHeld;
            default:
                return true;
        }
    }

    private static Quaternion GetGripTargetRotation(Transform grip, Vector3 eulerOffset)
    {
        if (grip == null)
        {
            return Quaternion.identity;
        }

        return grip.rotation * Quaternion.Euler(eulerOffset);
    }

    public void RotateWeaponTowards(Vector3 aimPoint, Vector3 worldUp, float turnSpeed, float deltaTime)
    {
        Transform reference = AimReference;
        Vector3 direction = aimPoint - reference.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (worldUp.sqrMagnitude <= 0.0001f)
        {
            worldUp = Vector3.up;
        }

        Quaternion referenceTargetRotation = Quaternion.LookRotation(direction.normalized, worldUp.normalized);
        Quaternion rootToReferenceRotation = Quaternion.Inverse(transform.rotation) * reference.rotation;
        Quaternion targetRootRotation = referenceTargetRotation * Quaternion.Inverse(rootToReferenceRotation);
        Vector3 gripPivot = alignRightGripToMount && rightHandGrip != null && rightHandGrip != transform
            ? rightHandGrip.position
            : transform.position;
        bool snapRotation = turnSpeed <= 0f || deltaTime <= 0f || (snapMountedAimRotation && alignRightGripToMount);

        transform.rotation = snapRotation
            ? targetRootRotation
            : Quaternion.Slerp(transform.rotation, targetRootRotation, 1f - Mathf.Exp(-turnSpeed * deltaTime));

        if (alignRightGripToMount && rightHandGrip != null && rightHandGrip != transform)
        {
            transform.position += gripPivot - rightHandGrip.position;
        }

    }

    private void ApplyMountedPoseOffset()
    {
        bool hasRotationOffset = mountedPoseEulerOffset.sqrMagnitude > 0.0001f;
        bool hasPositionOffset = mountedPosePositionOffset.sqrMagnitude > 0.0001f;

        if (!hasRotationOffset && !hasPositionOffset)
        {
            return;
        }

        Transform pivotTransform = alignRightGripToMount && rightHandGrip != null && rightHandGrip != transform
            ? rightHandGrip
            : transform;
        Vector3 pivotPosition = pivotTransform.position;

        if (hasRotationOffset)
        {
            transform.rotation *= Quaternion.Euler(mountedPoseEulerOffset);

            if (pivotTransform != transform)
            {
                transform.position += pivotPosition - pivotTransform.position;
            }
        }

        if (hasPositionOffset)
        {
            transform.position += transform.TransformVector(mountedPosePositionOffset);
        }
    }

    private Transform CreatePoint(string pointName, Vector3 localPosition)
    {
        GameObject point = new GameObject(pointName);
        Transform pointTransform = point.transform;
        pointTransform.SetParent(transform, false);
        pointTransform.localPosition = localPosition;
        pointTransform.localRotation = Quaternion.identity;
        pointTransform.localScale = Vector3.one;
        return pointTransform;
    }

    private Transform FindPoint(params string[] names)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);

        foreach (string pointName in names)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != transform && children[i].name == pointName)
                {
                    return children[i];
                }
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        DrawPoint(RightHandGrip, Color.green, 0.035f);
        DrawPoint(leftHandGrip, Color.cyan, 0.035f);
        DrawPoint(Muzzle, Color.yellow, 0.03f);

        Transform reference = AimReference;
        if (reference != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(reference.position, reference.forward * 0.35f);
        }
    }

    private static void DrawPoint(Transform point, Color color, float radius)
    {
        if (point == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawSphere(point.position, radius);
    }
}
