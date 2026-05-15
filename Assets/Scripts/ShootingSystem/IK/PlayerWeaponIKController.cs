using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerWeaponIKController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private InputsController inputsController;
    [SerializeField] private Animator animator;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private Rig weaponRig;
    [SerializeField] private TwoBoneIKConstraint rightHandIK;
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private MultiAimConstraint bodyAimConstraint;

    [Header("Behaviour")]
    [SerializeField] private bool assignTargetsToConstraints = true;
    [SerializeField] private bool attachWeaponToMount = true;
    [SerializeField] private bool keepWeaponAttachedEveryFrame = true;
    [SerializeField] private bool rotateWeaponToAim = true;
    [SerializeField] private bool rotateWeaponOnlyWhileAiming = true;
    [SerializeField, Min(0f)] private float ikBlendSpeed = 12f;
    [SerializeField, Min(0f)] private float weaponTurnSpeed = 25f;
    [SerializeField] private bool syncRigLayersAfterUpdate = true;

    [Header("Animation")]
    [SerializeField] private string reloadAnimatorTrigger = "Reload";
    [SerializeField] private string reloadAnimatorStateName = "reload";
    [SerializeField] private string reloadAnimatorLayerName = "Aiming";
    [SerializeField] private string reloadAnimatorSpeedParameter = "ReloadSpeed";
    [SerializeField] private string reloadAnimationClipName = "reload";
    [SerializeField, Min(0f)] private float reloadAnimationFadeDuration = 0.05f;
    [SerializeField, Min(0.01f)] private float reloadAnimationSpeed = 0.85f;
    [SerializeField] private bool syncReloadAnimationToWeaponDuration = false;
    [SerializeField, Min(0.01f)] private float maxSyncedReloadAnimationSpeed = 1.1f;
    [SerializeField] private bool reduceIKDuringReload = true;
    [SerializeField, Range(0f, 1f)] private float reloadIKWeightMultiplier = 0.15f;
    [SerializeField, Min(0f)] private float reloadIKExtraDuration = 0.15f;

    [Header("Aim")]
    [HideInInspector] [SerializeField, Min(0.1f)] private float fallbackAimDistance = 80f;
    [HideInInspector] [SerializeField, Min(0.1f)] private float maxAimDistance = 1000f;
    [HideInInspector] [SerializeField] private LayerMask aimLayerMask = ~0;

    private Weapon _currentWeapon;
    private WeaponIKGrip _currentGrip;
    private float _ikBlend;
    private bool _isSubscribed;
    private bool _isSubscribedToCurrentWeaponReload;
    private string _equippedAnimatorBool;
    private string _aimAnimatorBool;
    private bool _aimAnimatorBoolState;
    private int _weaponAnimatorLayerIndex = -1;
    private float _weaponAnimatorLayerActiveWeight = 1f;
    private Quaternion _rightHandTargetDefaultLocalRotation;
    private Quaternion _leftHandTargetDefaultLocalRotation;
    private bool _hasRightHandTargetDefaultRotation;
    private bool _hasLeftHandTargetDefaultRotation;
    private float _reloadIKTimer;
    private bool _useExternalAimPoint;
    private Vector3 _externalAimPoint;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        BindConstraintTargets();
        ApplyIKWeights(0f);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToWeaponController();
        SetCurrentWeapon(weaponController != null ? weaponController.CurrentWeapon : _currentWeapon, true);
    }

    private void Start()
    {
        ResolveReferences();
        SubscribeToWeaponController();
        SetCurrentWeapon(weaponController != null ? weaponController.CurrentWeapon : null, true);
    }

    private void OnDisable()
    {
        UnsubscribeFromCurrentWeaponReload();
        UnsubscribeFromWeaponController();
        SetAnimatorBool(_aimAnimatorBool, false);
        SetAnimatorBool(_equippedAnimatorBool, false);
        SetWeaponAnimatorLayerWeight(0f);
        _aimAnimatorBoolState = false;
        _reloadIKTimer = 0f;
        ApplyIKWeights(0f);
    }

    private void Update()
    {
        if (weaponController == null || inputsController == null || playerCamera == null)
        {
            ResolveReferences();
            SubscribeToWeaponController();
        }

        if (weaponController != null && weaponController.CurrentWeapon != _currentWeapon)
        {
            SetCurrentWeapon(weaponController.CurrentWeapon, true);
        }

        UpdateIK(Time.deltaTime);
    }

    private void ResolveReferences()
    {
        if (weaponController == null)
        {
            weaponController = GetComponent<PlayerWeaponController>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponentInParent<PlayerWeaponController>();
        }

        if (inputsController == null)
        {
            inputsController = GetComponent<InputsController>();
        }

        if (inputsController == null)
        {
            inputsController = GetComponentInParent<InputsController>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (rigBuilder == null)
        {
            rigBuilder = GetComponent<RigBuilder>();
        }

        if (rigBuilder == null)
        {
            rigBuilder = GetComponentInChildren<RigBuilder>(true);
        }

        if (weaponMount == null)
        {
            weaponMount = FindChildByName(transform.root, "WeaponMount_R");
        }

        if (weaponMount == null)
        {
            weaponMount = FindChildByName(transform.root, "Hand_R");
        }

        if (weaponRig == null)
        {
            weaponRig = GetComponentInChildren<Rig>(true);
        }

        ResolveIKConstraints();
        ResolveTargetsFromConstraints();
        CaptureTargetDefaultRotations();
    }

    private void ResolveIKConstraints()
    {
        TwoBoneIKConstraint[] handConstraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);

        if (rightHandIK == null)
        {
            rightHandIK = FindConstraintByName(handConstraints, "right");
        }

        if (leftHandIK == null)
        {
            leftHandIK = FindConstraintByName(handConstraints, "left");
        }

        if (rightHandIK == null && handConstraints.Length > 0)
        {
            rightHandIK = handConstraints[0];
        }

        if (leftHandIK == null)
        {
            for (int i = 0; i < handConstraints.Length; i++)
            {
                if (handConstraints[i] != rightHandIK)
                {
                    leftHandIK = handConstraints[i];
                    break;
                }
            }
        }

        if (bodyAimConstraint == null)
        {
            MultiAimConstraint[] aimConstraints = GetComponentsInChildren<MultiAimConstraint>(true);
            bodyAimConstraint = FindConstraintByName(aimConstraints, "aim");

            if (bodyAimConstraint == null && aimConstraints.Length > 0)
            {
                bodyAimConstraint = aimConstraints[0];
            }
        }
    }

    private void ResolveTargetsFromConstraints()
    {
        if (rightHandTarget == null && rightHandIK != null)
        {
            rightHandTarget = rightHandIK.data.target;
        }

        if (leftHandTarget == null && leftHandIK != null)
        {
            leftHandTarget = leftHandIK.data.target;
        }
    }

    private void BindConstraintTargets()
    {
        if (!assignTargetsToConstraints)
        {
            return;
        }

        if (rightHandIK != null && rightHandTarget != null)
        {
            rightHandIK.data.target = rightHandTarget;
            rightHandIK.data.targetPositionWeight = 1f;
            rightHandIK.data.targetRotationWeight = 1f;
        }

        if (leftHandIK != null && leftHandTarget != null)
        {
            leftHandIK.data.target = leftHandTarget;
            leftHandIK.data.targetPositionWeight = 1f;
            leftHandIK.data.targetRotationWeight = 1f;
        }
    }

    private void SubscribeToWeaponController()
    {
        if (_isSubscribed || weaponController == null)
        {
            return;
        }

        weaponController.CurrentWeaponChanged += SetCurrentWeaponFromEvent;
        _isSubscribed = true;
    }

    private void UnsubscribeFromWeaponController()
    {
        if (!_isSubscribed || weaponController == null)
        {
            return;
        }

        weaponController.CurrentWeaponChanged -= SetCurrentWeaponFromEvent;
        _isSubscribed = false;
    }

    private void SetCurrentWeaponFromEvent(Weapon weapon)
    {
        SetCurrentWeapon(weapon, true);
    }

    public void SnapCurrentWeaponPose()
    {
        ResolveReferences();

        if (weaponController != null && weaponController.CurrentWeapon != _currentWeapon)
        {
            SetCurrentWeapon(weaponController.CurrentWeapon, true);
        }

        if (_currentWeapon == null)
        {
            return;
        }

        AttachCurrentWeaponToMount();

        Vector3 aimPoint = ResolveAimPoint();

        if (aimTarget != null)
        {
            aimTarget.position = aimPoint;
        }

        if (ShouldRotateWeaponToAim())
        {
            UpdateWeaponAim(aimPoint, 0f);
        }

        CopyGripTargets();

        _ikBlend = ShouldUseCurrentWeaponIK() ? 1f : 0f;
        ApplyIKWeights(_ikBlend);
        UpdateAimAnimator(_currentWeapon != null && inputsController != null && inputsController.aim);

        if (syncRigLayersAfterUpdate && rigBuilder != null)
        {
            rigBuilder.SyncLayers();
        }
    }

    public void SetExternalAimPoint(Vector3 aimPoint)
    {
        _externalAimPoint = aimPoint;
        _useExternalAimPoint = true;
    }

    public void ClearExternalAimPoint()
    {
        _useExternalAimPoint = false;
    }

    private void SetCurrentWeapon(Weapon weapon, bool snapTargets)
    {
        if (_currentWeapon == weapon)
        {
            SubscribeToCurrentWeaponReload();
            UpdateEquippedAnimator();
            UpdateAimAnimator(IsAimAnimatorActive());
            UpdateWeaponAnimatorLayer();

            if (snapTargets)
            {
                SnapCurrentWeaponIKState();
            }

            return;
        }

        UnsubscribeFromCurrentWeaponReload();
        _reloadIKTimer = 0f;
        SetAnimatorBool(_aimAnimatorBool, false);
        SetAnimatorBool(_equippedAnimatorBool, false);
        SetWeaponAnimatorLayerWeight(0f);
        _aimAnimatorBoolState = false;

        _currentWeapon = weapon;
        _currentGrip = weapon != null
            ? weapon.GetComponent<WeaponIKGrip>() ?? weapon.GetComponentInChildren<WeaponIKGrip>(true)
            : null;

        _equippedAnimatorBool = _currentGrip != null ? _currentGrip.EquippedAnimatorBool : null;
        _aimAnimatorBool = _currentGrip != null ? _currentGrip.AimAnimatorBool : null;
        ResolveWeaponAnimatorLayer();
        SubscribeToCurrentWeaponReload();
        UpdateEquippedAnimator();
        UpdateAimAnimator(IsAimAnimatorActive());
        UpdateWeaponAnimatorLayer();

        if (_currentWeapon != null && attachWeaponToMount && weaponMount != null)
        {
            AttachCurrentWeaponToMount();
        }

        if (snapTargets)
        {
            SnapCurrentWeaponIKState();
        }
    }

    private void SnapCurrentWeaponIKState()
    {
        if (_currentWeapon == null)
        {
            _ikBlend = 0f;
            ApplyIKWeights(0f);
            UpdateAimAnimator(false);
            UpdateWeaponAnimatorLayer();
            return;
        }

        AttachCurrentWeaponToMount();

        Vector3 aimPoint = ResolveAimPoint();

        if (aimTarget != null)
        {
            aimTarget.position = aimPoint;
        }

        UpdateWeaponAim(aimPoint, 0f);
        CopyGripTargets();

        float reloadIKMultiplier = IsReloadIKReduced() ? reloadIKWeightMultiplier : 1f;
        _ikBlend = ShouldUseCurrentWeaponIK() ? reloadIKMultiplier : 0f;
        ApplyIKWeights(_ikBlend);
        UpdateEquippedAnimator();
        UpdateWeaponAnimatorLayer();
        UpdateAimAnimator(IsAimAnimatorActive());

        if (syncRigLayersAfterUpdate && rigBuilder != null)
        {
            rigBuilder.SyncLayers();
        }
    }

    private void SubscribeToCurrentWeaponReload()
    {
        if (_isSubscribedToCurrentWeaponReload || _currentWeapon == null)
        {
            return;
        }

        _currentWeapon.ReloadStarted += HandleWeaponReloadStarted;
        _isSubscribedToCurrentWeaponReload = true;
    }

    private void UnsubscribeFromCurrentWeaponReload()
    {
        if (!_isSubscribedToCurrentWeaponReload)
        {
            return;
        }

        if (_currentWeapon != null)
        {
            _currentWeapon.ReloadStarted -= HandleWeaponReloadStarted;
        }

        _isSubscribedToCurrentWeaponReload = false;
    }

    private void HandleWeaponReloadStarted(Weapon weapon)
    {
        if (weapon != _currentWeapon)
        {
            return;
        }

        float reloadAnimationSpeed = GetReloadAnimationSpeed(weapon);
        _reloadIKTimer = GetReloadIKReductionDuration(weapon, reloadAnimationSpeed);
        SetAnimatorFloat(reloadAnimatorSpeedParameter, reloadAnimationSpeed);

        if (!PlayAnimatorState(reloadAnimatorStateName, reloadAnimatorLayerName, reloadAnimationFadeDuration))
        {
            SetAnimatorTrigger(reloadAnimatorTrigger);
        }
    }

    private void UpdateIK(float deltaTime)
    {
        if (_reloadIKTimer > 0f)
        {
            _reloadIKTimer = Mathf.Max(0f, _reloadIKTimer - deltaTime);
        }

        if (keepWeaponAttachedEveryFrame)
        {
            AttachCurrentWeaponToMount();
        }

        Vector3 aimPoint = ResolveAimPoint();

        if (aimTarget != null)
        {
            aimTarget.position = aimPoint;
        }

        bool shouldUseIK = ShouldUseCurrentWeaponIK();
        UpdateWeaponAim(aimPoint, deltaTime);

        if (shouldUseIK || _ikBlend > 0.001f)
        {
            CopyGripTargets();
        }

        float reloadIKMultiplier = IsReloadIKReduced() ? reloadIKWeightMultiplier : 1f;
        float targetBlend = shouldUseIK ? reloadIKMultiplier : 0f;
        _ikBlend = shouldUseIK
            ? ikBlendSpeed <= 0f
                ? targetBlend
                : Mathf.Lerp(_ikBlend, targetBlend, 1f - Mathf.Exp(-ikBlendSpeed * deltaTime))
            : 0f;

        ApplyIKWeights(_ikBlend);
        UpdateEquippedAnimator();
        UpdateAimAnimator(IsAimAnimatorActive());
        UpdateWeaponAnimatorLayer();

        if (syncRigLayersAfterUpdate && rigBuilder != null)
        {
            rigBuilder.SyncLayers();
        }
    }

    private void LateUpdate()
    {
        if (keepWeaponAttachedEveryFrame)
        {
            AttachCurrentWeaponToMount();
        }

        if (ShouldRotateWeaponToAim())
        {
            Vector3 aimPoint = ResolveAimPoint();
            UpdateWeaponAim(aimPoint, Time.deltaTime);
            CopyGripTargets();

            if (syncRigLayersAfterUpdate && rigBuilder != null)
            {
                rigBuilder.SyncLayers();
            }
        }
    }

    private bool ShouldUseCurrentWeaponIK()
    {
        if (_currentWeapon == null || !_currentWeapon.gameObject.activeInHierarchy)
        {
            return false;
        }

        return _currentGrip == null || _currentGrip.ShouldUseIK(inputsController);
    }

    private bool IsReloadIKReduced()
    {
        return reduceIKDuringReload && _reloadIKTimer > 0f;
    }

    private Vector3 ResolveAimPoint()
    {
        if (_useExternalAimPoint)
        {
            return _externalAimPoint;
        }

        Camera aimCamera = playerCamera != null ? playerCamera : Camera.main;

        if (aimCamera == null)
        {
            Transform aimReference = GetAimReference();
            return aimReference.position + aimReference.forward * GetFallbackAimDistance();
        }

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float distance = GetMaxAimDistance();
        LayerMask mask = GetAimLayerMask();

        if (ShooterAimUtility.TryRaycastIgnoringOwner(
                aimRay.origin,
                aimRay.direction,
                distance,
                mask,
                transform.root,
                out RaycastHit hit))
        {
            return hit.point;
        }

        return aimRay.origin + aimRay.direction * distance;
    }

    private float GetFallbackAimDistance()
    {
        if (_currentWeapon != null && _currentWeapon.HasDefinition)
        {
            return _currentWeapon.Range;
        }

        return Mathf.Max(0.1f, fallbackAimDistance);
    }

    private float GetMaxAimDistance()
    {
        if (_currentGrip != null && _currentGrip.OverrideControllerAimSettings)
        {
            return _currentGrip.MaxAimDistance;
        }

        if (_currentWeapon != null && _currentWeapon.HasDefinition)
        {
            return _currentWeapon.Range;
        }

        return maxAimDistance;
    }

    private LayerMask GetAimLayerMask()
    {
        if (_currentGrip != null && _currentGrip.OverrideControllerAimSettings)
        {
            return _currentGrip.AimLayerMask;
        }

        if (_currentWeapon != null && _currentWeapon.HasDefinition)
        {
            return _currentWeapon.HitMask;
        }

        return aimLayerMask;
    }

    private void UpdateWeaponAim(Vector3 aimPoint, float deltaTime)
    {
        if (!ShouldRotateWeaponToAim())
        {
            return;
        }

        Vector3 worldUp = playerCamera != null ? playerCamera.transform.up : transform.up;

        if (_currentGrip != null)
        {
            _currentGrip.RotateWeaponTowards(aimPoint, worldUp, weaponTurnSpeed, deltaTime);
            return;
        }

        RotateTransformTowards(_currentWeapon.transform, aimPoint, worldUp, weaponTurnSpeed, deltaTime);
    }

    private bool ShouldRotateWeaponToAim()
    {
        if (!rotateWeaponToAim || _currentWeapon == null)
        {
            return false;
        }

        if (IsReloadIKReduced())
        {
            return false;
        }

        return !rotateWeaponOnlyWhileAiming || (inputsController != null && inputsController.aim);
    }

    private void CopyGripTargets()
    {
        if (_currentWeapon == null)
        {
            return;
        }

        Transform rightGrip = _currentGrip != null ? _currentGrip.RightHandGrip : _currentWeapon.transform;
        Transform leftGrip = _currentGrip != null ? _currentGrip.LeftHandGrip : null;

        if (_currentGrip == null)
        {
            CopyPose(rightHandTarget, rightGrip);
            CopyPose(leftHandTarget, leftGrip);
            return;
        }

        if (_currentGrip.RightHandWeight > 0f)
        {
            CopyGripPose(
                rightHandTarget,
                rightGrip,
                _currentGrip.CopyRightHandGripRotation,
                _currentGrip.GetRightHandTargetRotation(),
                _rightHandTargetDefaultLocalRotation,
                _hasRightHandTargetDefaultRotation);
        }

        CopyGripPose(
            leftHandTarget,
            leftGrip,
            _currentGrip.CopyLeftHandGripRotation,
            _currentGrip.GetLeftHandTargetRotation(),
            _leftHandTargetDefaultLocalRotation,
            _hasLeftHandTargetDefaultRotation);
    }

    private void AttachCurrentWeaponToMount()
    {
        if (!attachWeaponToMount || _currentWeapon == null || weaponMount == null)
        {
            return;
        }

        if (_currentGrip != null)
        {
            _currentGrip.ApplyMountPose(weaponMount);
            return;
        }

        Transform weaponTransform = _currentWeapon.transform;

        if (weaponTransform.parent != weaponMount)
        {
            weaponTransform.SetParent(weaponMount, false);
        }

        weaponTransform.localPosition = Vector3.zero;
        weaponTransform.localRotation = Quaternion.identity;
    }

    private void ApplyIKWeights(float blend)
    {
        float rigWeight = blend * (_currentGrip != null ? _currentGrip.RigWeight : 1f);
        float rightWeight = blend * (_currentGrip != null ? _currentGrip.RightHandWeight : 1f);
        float leftWeight = blend * (_currentGrip != null ? _currentGrip.LeftHandWeight : 0f);
        float bodyAimWeight = blend * (_currentGrip != null ? _currentGrip.BodyAimWeight : 1f);
        float rightPositionWeight = _currentGrip != null ? _currentGrip.RightHandTargetPositionWeight : 1f;
        float rightRotationWeight = _currentGrip != null ? _currentGrip.RightHandTargetRotationWeight : 1f;
        float leftPositionWeight = _currentGrip != null ? _currentGrip.LeftHandTargetPositionWeight : 1f;
        float leftRotationWeight = _currentGrip != null ? _currentGrip.LeftHandTargetRotationWeight : 1f;

        if (weaponRig != null)
        {
            weaponRig.weight = rigWeight;
        }

        if (rightHandIK != null)
        {
            rightHandIK.weight = rightWeight;
            rightHandIK.data.targetPositionWeight = rightPositionWeight;
            rightHandIK.data.targetRotationWeight = rightRotationWeight;
        }

        if (leftHandIK != null)
        {
            leftHandIK.weight = leftWeight;
            leftHandIK.data.targetPositionWeight = leftPositionWeight;
            leftHandIK.data.targetRotationWeight = leftRotationWeight;
        }

        if (bodyAimConstraint != null)
        {
            bodyAimConstraint.weight = bodyAimWeight;
        }
    }

    private void UpdateEquippedAnimator()
    {
        SetAnimatorBool(_equippedAnimatorBool, IsCurrentWeaponEquippedForAnimator());
    }

    private void ResolveWeaponAnimatorLayer()
    {
        _weaponAnimatorLayerIndex = -1;
        _weaponAnimatorLayerActiveWeight = 1f;

        if (animator == null || _currentGrip == null || string.IsNullOrWhiteSpace(_currentGrip.AnimatorLayerName))
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(_currentGrip.AnimatorLayerName);

        if (layerIndex < 0)
        {
            return;
        }

        _weaponAnimatorLayerIndex = layerIndex;
        _weaponAnimatorLayerActiveWeight = _currentGrip.AnimatorLayerActiveWeight;
    }

    private void UpdateWeaponAnimatorLayer()
    {
        SetWeaponAnimatorLayerWeight(IsCurrentWeaponEquippedForAnimator()
            ? _weaponAnimatorLayerActiveWeight
            : 0f);
    }

    private void SetWeaponAnimatorLayerWeight(float weight)
    {
        if (animator == null || _weaponAnimatorLayerIndex < 0)
        {
            return;
        }

        animator.SetLayerWeight(_weaponAnimatorLayerIndex, Mathf.Clamp01(weight));
    }

    private bool IsCurrentWeaponEquippedForAnimator()
    {
        return _currentWeapon != null && _currentWeapon.gameObject.activeInHierarchy;
    }

    private bool IsAimAnimatorActive()
    {
        return IsCurrentWeaponEquippedForAnimator()
            && inputsController != null
            && inputsController.aim;
    }

    private void UpdateAimAnimator(bool isActive)
    {
        _aimAnimatorBoolState = isActive;
        SetAnimatorBool(_aimAnimatorBool, isActive);
    }

    private Transform GetAimReference()
    {
        if (_currentGrip != null)
        {
            return _currentGrip.AimReference;
        }

        if (_currentWeapon != null)
        {
            return _currentWeapon.transform;
        }

        return transform;
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        string resolvedParameterName = ResolveAnimatorBool(parameterName);

        if (string.IsNullOrEmpty(resolvedParameterName))
        {
            return;
        }

        animator.SetBool(resolvedParameterName, value);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        string resolvedParameterName = ResolveAnimatorTrigger(parameterName);

        if (string.IsNullOrEmpty(resolvedParameterName))
        {
            return;
        }

        animator.ResetTrigger(resolvedParameterName);
        animator.SetTrigger(resolvedParameterName);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        string resolvedParameterName = ResolveAnimatorFloat(parameterName);

        if (string.IsNullOrEmpty(resolvedParameterName))
        {
            return;
        }

        animator.SetFloat(resolvedParameterName, value);
    }

    private bool PlayAnimatorState(string stateName, string layerName, float transitionDuration)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int layerIndex = ResolveAnimatorLayerIndex(layerName);

        if (layerIndex < 0)
        {
            return false;
        }

        string titleCaseStateName = char.ToUpperInvariant(stateName[0]) + stateName[1..];
        string resolvedLayerName = animator.GetLayerName(layerIndex);
        string fullStateName = string.IsNullOrWhiteSpace(resolvedLayerName)
            ? stateName
            : $"{resolvedLayerName}.{stateName}";
        string titleCaseFullStateName = string.IsNullOrWhiteSpace(resolvedLayerName)
            ? titleCaseStateName
            : $"{resolvedLayerName}.{titleCaseStateName}";

        if (TryCrossFadeAnimatorState(fullStateName, layerIndex, transitionDuration)
            || TryCrossFadeAnimatorState(stateName, layerIndex, transitionDuration)
            || TryCrossFadeAnimatorState(titleCaseFullStateName, layerIndex, transitionDuration)
            || TryCrossFadeAnimatorState(titleCaseStateName, layerIndex, transitionDuration))
        {
            return true;
        }

        return false;
    }

    private bool TryCrossFadeAnimatorState(string stateName, int layerIndex, float transitionDuration)
    {
        int stateHash = Animator.StringToHash(stateName);

        if (!animator.HasState(layerIndex, stateHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(stateHash, transitionDuration, layerIndex, 0f);
        return true;
    }

    private int ResolveAnimatorLayerIndex(string layerName)
    {
        if (animator == null)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(layerName))
        {
            int layerIndex = animator.GetLayerIndex(layerName);

            if (layerIndex >= 0)
            {
                return layerIndex;
            }
        }

        return animator.layerCount > 0 ? 0 : -1;
    }

    private string ResolveAnimatorBool(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool && parameters[i].name == parameterName)
            {
                return parameters[i].name;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Bool
                && string.Equals(parameters[i].name, parameterName, System.StringComparison.OrdinalIgnoreCase))
            {
                return parameters[i].name;
            }
        }

        if (IsAssaultAimParameter(parameterName))
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool
                    && IsAssaultAimParameter(parameters[i].name))
                {
                    return parameters[i].name;
                }
            }
        }

        return null;
    }

    private string ResolveAnimatorTrigger(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == parameterName)
            {
                return parameters[i].name;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger
                && string.Equals(parameters[i].name, parameterName, System.StringComparison.OrdinalIgnoreCase))
            {
                return parameters[i].name;
            }
        }

        return null;
    }

    private string ResolveAnimatorFloat(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return null;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Float && parameters[i].name == parameterName)
            {
                return parameters[i].name;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Float
                && string.Equals(parameters[i].name, parameterName, System.StringComparison.OrdinalIgnoreCase))
            {
                return parameters[i].name;
            }
        }

        return null;
    }

    private float GetReloadAnimationSpeed(Weapon weapon)
    {
        float speed = reloadAnimationSpeed;

        if (syncReloadAnimationToWeaponDuration
            && weapon != null
            && TryGetAnimationClipLength(reloadAnimationClipName, out float clipLength))
        {
            float reloadDuration = Mathf.Max(0.01f, weapon.ReloadDuration);
            speed *= Mathf.Min(maxSyncedReloadAnimationSpeed, clipLength / reloadDuration);
        }

        return Mathf.Max(0.01f, speed);
    }

    private float GetReloadIKReductionDuration(Weapon weapon, float animationSpeed)
    {
        float duration = weapon != null ? Mathf.Max(0f, weapon.ReloadDuration) : 0f;

        if (TryGetAnimationClipLength(reloadAnimationClipName, out float clipLength))
        {
            duration = Mathf.Max(duration, clipLength / Mathf.Max(0.01f, animationSpeed));
        }

        return duration + reloadIKExtraDuration;
    }

    private bool TryGetAnimationClipLength(string clipName, out float length)
    {
        length = 0f;

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];

            if (clip != null && string.Equals(clip.name, clipName, System.StringComparison.OrdinalIgnoreCase))
            {
                length = clip.length;
                return length > 0f;
            }
        }

        return false;
    }

    private static bool IsAssaultAimParameter(string parameterName)
    {
        string normalizedName = parameterName
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        return normalizedName == "assaultaim" || normalizedName == "assasultaim";
    }

    private static T FindConstraintByName<T>(T[] constraints, string namePart) where T : Component
    {
        for (int i = 0; i < constraints.Length; i++)
        {
            if (constraints[i].name.ToLowerInvariant().Contains(namePart))
            {
                return constraints[i];
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildByName(root.GetChild(i), childName);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void CopyPose(Transform target, Transform source)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.SetPositionAndRotation(source.position, source.rotation);
    }

    private static void CopyGripPose(
        Transform target,
        Transform source,
        bool copyRotation,
        Quaternion targetRotation,
        Quaternion defaultLocalRotation,
        bool hasDefaultLocalRotation)
    {
        if (target == null || source == null)
        {
            return;
        }

        target.position = source.position;

        if (copyRotation)
        {
            target.rotation = targetRotation;
        }
        else if (hasDefaultLocalRotation)
        {
            target.localRotation = defaultLocalRotation;
        }
    }

    private void CaptureTargetDefaultRotations()
    {
        if (!_hasRightHandTargetDefaultRotation && rightHandTarget != null)
        {
            _rightHandTargetDefaultLocalRotation = rightHandTarget.localRotation;
            _hasRightHandTargetDefaultRotation = true;
        }

        if (!_hasLeftHandTargetDefaultRotation && leftHandTarget != null)
        {
            _leftHandTargetDefaultLocalRotation = leftHandTarget.localRotation;
            _hasLeftHandTargetDefaultRotation = true;
        }
    }

    private static void RotateTransformTowards(
        Transform target,
        Vector3 aimPoint,
        Vector3 worldUp,
        float turnSpeed,
        float deltaTime)
    {
        Vector3 direction = aimPoint - target.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (worldUp.sqrMagnitude <= 0.0001f)
        {
            worldUp = Vector3.up;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, worldUp.normalized);
        target.rotation = turnSpeed <= 0f || deltaTime <= 0f
            ? targetRotation
            : Quaternion.Slerp(target.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * deltaTime));
    }
}
