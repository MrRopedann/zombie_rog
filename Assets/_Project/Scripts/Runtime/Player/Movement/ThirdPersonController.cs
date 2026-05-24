using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/*
 * Примечание: анимации вызываются через контроллер как для персонажа, так и для капсулы
 * с использованием проверок аниматора на null.
 */

[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
[RequireComponent(typeof(PlayerInput))]
#endif
public class ThirdPersonController : MonoBehaviour
{
    [Header("Игрок")]
    [Tooltip("Скорость движения персонажа в м/с")]
    public float WalkSpeed = 2.0f;

    [Tooltip("Скорость движения персонажа в м/с")]
    public float MoveSpeed = 4.0f;

    [Tooltip("Скорость спринта персонажа в м/с")]
    public float SprintSpeed = 6.5f;

    [Tooltip("Насколько быстро персонаж поворачивается в направлении движения")]
    [Range(0.0f, 0.3f)]
    public float RotationSmoothTime = 0.12f;

    [Tooltip("Ускорение и замедление")]
    public float SpeedChangeRate = 10.0f;

    [Header("Shooter Movement")]
    [Tooltip("Базовая скорость движения во время прицеливания и стрейфа")]
    public float AimMoveSpeed = 3.25f;

    [Tooltip("Насколько быстро тело доворачивается к направлению камеры")]
    [Range(0.0f, 0.2f)]
    public float AimRotationSmoothTime = 0.05f;

    [Tooltip("Замедление движения в сторону во время прицеливания")]
    [Range(0.35f, 1.0f)]
    public float AimStrafeSpeedMultiplier = 0.85f;

    [Tooltip("Замедление движения назад во время прицеливания")]
    [Range(0.25f, 1.0f)]
    public float AimBackwardSpeedMultiplier = 0.7f;

    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Space(10)]
    [Tooltip("Высота, на которую игрок может прыгнуть")]
    public float JumpHeight = 1.2f;

    [Tooltip("Персонаж использует собственное значение гравитации. Значение по умолчанию в движке равно -9.81f")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("Время, которое должно пройти перед тем, как можно будет снова прыгнуть")]
    public float JumpTimeout = 0.50f;

    [Tooltip("Время, которое должно пройти перед переходом в состояние падения")]
    public float FallTimeout = 0.15f;

    [Header("Персонаж на земле")]
    [Tooltip("Определяет, находится ли персонаж на земле")]
    public bool Grounded = true;

    [Tooltip("Полезно, когда поверхность неровная")]
    public float GroundedOffset = -0.14f;

    [Tooltip("Радиус проверки касания земли")]
    public float GroundedRadius = 0.28f;

    [Tooltip("Какие слои персонаж использует в качестве земли")]
    public LayerMask GroundLayers;

    [Header("Stairs")]
    public bool ConfigureControllerForStairs = true;
    [Min(0f)] public float StairStepOffset = 0.45f;
    [Range(0f, 89f)] public float StairSlopeLimit = 50f;
    [Min(0.001f)] public float MinControllerSkinWidth = 0.05f;
    public bool UseStairAssist = true;
    [Min(0.05f)] public float StairCheckDistance = 0.45f;
    [Min(0.01f)] public float StairProbeRadius = 0.12f;
    [Min(0f)] public float StairMaxLiftPerSecond = 10f;
    [Min(0f)] public float StairGroundClearance = 0.03f;
    public LayerMask StairLayers = ~0;

    [Header("Камера")]
    [Tooltip("Цель слежения, установленная в виртуальной камере")]
    public GameObject CinemachineCameraTarget;

    [Tooltip("Как далеко в градусах можно повернуть камеру вверх")]
    public float TopClamp = 70.0f;

    [Tooltip("Как далеко в градусах можно опустить камеру вниз")]
    public float BottomClamp = -30.0f;

    [Tooltip("Дополнительные градусы для переопределения поворота камеры")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("Для блокировки позиции камеры по всем осям")]
    public bool LockCameraPosition = false;

    [Header("TPS Camera Rig")]
    [SerializeField] private CinemachineVirtualCamera followVirtualCamera;
    [SerializeField] private Cinemachine3rdPersonFollow thirdPersonFollow;
    [SerializeField] private bool captureCurrentCameraAsNormal = true;
    [SerializeField] private Vector3 normalCameraTargetOffset = new Vector3(0f, 1.375f, 0f);
    [SerializeField] private Vector3 aimCameraTargetOffset = new Vector3(0f, 1.55f, 0f);
    [SerializeField] private Vector3 normalShoulderOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private Vector3 aimShoulderOffset = new Vector3(1.15f, 0.1f, 0f);
    [SerializeField] [Range(-1f, 1f)] private float normalCameraSide = 0.6f;
    [SerializeField] [Range(-1f, 1f)] private float aimCameraSide = 0.85f;
    [SerializeField] [Min(0.5f)] private float normalCameraDistance = 4f;
    [SerializeField] [Min(0.5f)] private float aimCameraDistance = 2.75f;
    [SerializeField] [Range(1f, 120f)] private float normalFieldOfView = 40f;
    [SerializeField] [Range(1f, 120f)] private float aimFieldOfView = 34f;
    [SerializeField] private Vector3 normalCameraDamping = new Vector3(0.1f, 0.25f, 0.3f);
    [SerializeField] private Vector3 aimCameraDamping = new Vector3(0.06f, 0.12f, 0.16f);
    [SerializeField] [Min(0.1f)] private float cameraSettingsBlendSpeed = 12f;
    [SerializeField] [Range(0.0f, 0.2f)] private float hipFireRotationSmoothTime = 0.08f;

    [Header("Death")]
    [SerializeField] private string deathAnimatorParameter = "isDead";
    [SerializeField] private string deathStateName = "Death";
    [SerializeField] private float deathInitialFallVelocity = -2f;
    [SerializeField] [Min(0.1f)] private float deathGroundProbeHeight = 2f;
    [SerializeField] [Min(0.1f)] private float deathGroundProbeDistance = 6f;
    [SerializeField] [Min(0f)] private float deathGroundClearance = 0.02f;
    [SerializeField] [Min(0.1f)] private float deathReloadFallbackDelay = 3f;
    [SerializeField] [Min(0f)] private float deathReloadExtraDelay = 0.1f;
    [SerializeField] private bool useRagdollOnDeath = true;
    [SerializeField] private RagdollController ragdollController;
    [SerializeField] private Vector3 ragdollDeathLocalImpulse = new Vector3(0f, 0.8f, -2f);

    [Header("Damage")]
    [SerializeField] private string damageAnimatorParameter = "isDamage";
    [SerializeField] private string damageStateName = "CombatDamage01";
    [SerializeField] private string damageAnimatorLayerName = "Base Layer";
    [SerializeField] [Min(0f)] private float damageAnimationFadeDuration = 0.04f;
    [SerializeField] [Min(0f)] private float damageBoolResetDelay = 0.05f;
    [SerializeField] [Min(0f)] private float damageAnimationMinDamage = 0.5f;
    [SerializeField] [Min(0f)] private float damageAnimationMinInterval = 0.6f;
    [SerializeField] private bool preserveWeaponPoseDuringDamageAnimation = true;

    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private float _cameraAimBlend;

    private float _speed;
    private float _animationBlend;
    private float _targetRotation;
    private float _moveRotationVelocity;
    private float _aimRotationVelocity;
    private float _verticalVelocity;
    private readonly float _terminalVelocity = 53.0f;
    private MovementState _currentMovementState = MovementState.Running;
    private CharacterStats _stats;

    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDJump;
    private int _animIDFreeFall;
    private int _animIDMotionSpeed;
    private int _animIDIsDead;
    private int _animIDIsDamage;


#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private Animator _animator;
    private CharacterController _controller;
    private InputsController _input;
    private PlayerWeaponController _weaponController;
    private PlayerWeaponIKController _weaponIKController;
    private GameObject _mainCamera;

    private const float _threshold = 0.01f;
    private readonly RaycastHit[] _stairHits = new RaycastHit[8];

    private bool _hasAnimator;
    private bool _wasRotateBodyMode;
    private bool _isDead;
    private bool _ragdollDeathActive;
    private bool _deathStartedGrounded;
    private bool _deathBodyGrounded;
    private Coroutine _sceneReloadCoroutine;
    private Coroutine _damageAnimationCoroutine;
    private float _lastDamageAnimationTime = float.NegativeInfinity;
    private SkinnedMeshRenderer[] _deathSkinnedRenderers;

    private bool IsCurrentDeviceMouse
    {
        get
        {

            return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
        }
    }

    private bool IsShooterBodyRotationActive => ShouldRotateBodyToAim;
    private bool ShouldRotateBodyToAim => _input != null && _input.IsShooterModeActive;
    private bool IsAimCameraActive => _input != null && _input.aim;
    private bool IsPrecisionMovementActive => _input != null && (_input.aim || _input.fireHeld);

    private void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
    }

    private void Start()
    {
        if (CinemachineCameraTarget != null)
        {
            _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        }

        ResolveAnimator();
        ResolveRagdollController();
        _controller = GetComponent<CharacterController>();
        ConfigureCharacterControllerForStairs(_controller);
        _input = GetComponent<InputsController>();
        ResolveWeaponReferences();
        _stats = GetComponent<CharacterStats>();

        if (_stats != null)
        {
            _stats.OnDeath += HandleDeath;
            _stats.OnDamaged += HandleDamageTaken;
        }

        if (_input == null)
        {
            Debug.LogError($"{nameof(InputsController)} не найден!", this);
        }

        _playerInput = GetComponent<PlayerInput>();

        ResolveTpsCameraRig();
        CaptureNormalCameraSettings();
        _cameraAimBlend = IsAimCameraActive ? 1f : 0f;
        ApplyTpsCameraSettings(_cameraAimBlend);

        AssignAnimationIDs();

        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;

        if (_stats != null && _stats.IsDead)
        {
            HandleDeath();
        }
    }

    private void Update()
    {
        ResolveAnimator();

        if (_stats != null && _stats.IsDead && !_isDead)
        {
            HandleDeath();
        }

        if (_isDead)
        {
            if (!_ragdollDeathActive)
            {
                ApplyDeathFall();
            }

            return;
        }

        GroundedCheck();
        UpdateMovementState();
        JumpAndGravity();
        Move();
    }

    private void LateUpdate()
    {
        if (_isDead)
        {
            if (!_ragdollDeathActive)
            {
                ApplyDeathVisualGrounding();
            }

            return;
        }

        CameraRotation();
        UpdateTpsCameraRig();
    }

    private void OnDestroy()
    {
        if (_stats != null)
        {
            _stats.OnDeath -= HandleDeath;
            _stats.OnDamaged -= HandleDamageTaken;
        }
    }

    private void OnValidate()
    {
        StairStepOffset = Mathf.Max(0f, StairStepOffset);
        StairSlopeLimit = Mathf.Clamp(StairSlopeLimit, 0f, 89f);
        MinControllerSkinWidth = Mathf.Max(0.001f, MinControllerSkinWidth);
        StairCheckDistance = Mathf.Max(0.05f, StairCheckDistance);
        StairProbeRadius = Mathf.Max(0.01f, StairProbeRadius);
        StairMaxLiftPerSecond = Mathf.Max(0f, StairMaxLiftPerSecond);
        StairGroundClearance = Mathf.Max(0f, StairGroundClearance);

        ConfigureCharacterControllerForStairs(GetComponent<CharacterController>());
    }

    private void ConfigureCharacterControllerForStairs(CharacterController controller)
    {
        if (!ConfigureControllerForStairs || controller == null)
        {
            return;
        }

        float maxStepOffset = Mathf.Max(0f, controller.height - controller.radius * 2f);
        float resolvedStepOffset = Mathf.Min(Mathf.Max(controller.stepOffset, StairStepOffset), maxStepOffset);
        float resolvedSkinWidth = Mathf.Min(
            Mathf.Max(controller.skinWidth, MinControllerSkinWidth),
            Mathf.Max(0.001f, controller.radius * 0.5f));

        controller.stepOffset = resolvedStepOffset;
        controller.slopeLimit = Mathf.Clamp(Mathf.Max(controller.slopeLimit, StairSlopeLimit), 0f, 89f);
        controller.skinWidth = resolvedSkinWidth;
    }

    private void ResolveTpsCameraRig()
    {
        if (followVirtualCamera == null)
        {
            followVirtualCamera = GetComponentInChildren<CinemachineVirtualCamera>(true);
        }

        if (thirdPersonFollow == null && followVirtualCamera != null)
        {
            thirdPersonFollow = followVirtualCamera.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        }

        if (thirdPersonFollow == null)
        {
            thirdPersonFollow = GetComponentInChildren<Cinemachine3rdPersonFollow>(true);
        }
    }

    private void CaptureNormalCameraSettings()
    {
        if (!captureCurrentCameraAsNormal)
        {
            return;
        }

        if (CinemachineCameraTarget != null)
        {
            normalCameraTargetOffset = CinemachineCameraTarget.transform.localPosition;
        }

        if (followVirtualCamera != null)
        {
            normalFieldOfView = followVirtualCamera.m_Lens.FieldOfView;
        }

        if (thirdPersonFollow == null)
        {
            return;
        }

        normalShoulderOffset = thirdPersonFollow.ShoulderOffset;
        normalCameraSide = thirdPersonFollow.CameraSide;
        normalCameraDistance = thirdPersonFollow.CameraDistance;
        normalCameraDamping = thirdPersonFollow.Damping;
    }

    private void UpdateTpsCameraRig()
    {
        float targetBlend = IsAimCameraActive ? 1f : 0f;
        float blendStep = 1f - Mathf.Exp(-cameraSettingsBlendSpeed * Time.deltaTime);
        _cameraAimBlend = Mathf.Lerp(_cameraAimBlend, targetBlend, blendStep);

        ApplyTpsCameraSettings(_cameraAimBlend);
    }

    private void ApplyTpsCameraSettings(float aimBlend)
    {
        aimBlend = Mathf.Clamp01(aimBlend);

        if (CinemachineCameraTarget != null)
        {
            CinemachineCameraTarget.transform.localPosition = Vector3.Lerp(
                normalCameraTargetOffset,
                aimCameraTargetOffset,
                aimBlend);
        }

        if (followVirtualCamera != null)
        {
            followVirtualCamera.m_Lens.FieldOfView = Mathf.Lerp(
                normalFieldOfView,
                aimFieldOfView,
                aimBlend);
        }

        if (thirdPersonFollow == null)
        {
            return;
        }

        thirdPersonFollow.ShoulderOffset = Vector3.Lerp(
            normalShoulderOffset,
            aimShoulderOffset,
            aimBlend);
        thirdPersonFollow.Damping = Vector3.Lerp(
            normalCameraDamping,
            aimCameraDamping,
            aimBlend);
        thirdPersonFollow.CameraSide = Mathf.Lerp(
            normalCameraSide,
            aimCameraSide,
            aimBlend);
        thirdPersonFollow.CameraDistance = Mathf.Lerp(
            normalCameraDistance,
            aimCameraDistance,
            aimBlend);
    }

    private void AssignAnimationIDs()
    {
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDFreeFall = Animator.StringToHash("FreeFall");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        _animIDIsDead = Animator.StringToHash(deathAnimatorParameter);
        _animIDIsDamage = Animator.StringToHash(damageAnimatorParameter);
    }

    private void ResolveAnimator()
    {
        if (_animator == null || !_animator.gameObject.activeInHierarchy)
        {
            _hasAnimator = TryGetComponent(out _animator);

            if (!_hasAnimator)
            {
                _animator = GetComponentInChildren<Animator>();
                _hasAnimator = _animator != null;
            }
        }
        else
        {
            _hasAnimator = true;
        }

        if (_hasAnimator)
        {
            _animator.applyRootMotion = false;
        }
    }

    private void ResolveRagdollController()
    {
        if (!useRagdollOnDeath)
        {
            return;
        }

        if (ragdollController == null)
        {
            ragdollController = GetComponent<RagdollController>();
        }

        if (ragdollController == null)
        {
            ragdollController = GetComponentInChildren<RagdollController>(true);
        }

        if (ragdollController == null)
        {
            ragdollController = gameObject.AddComponent<RagdollController>();
        }

        if (ragdollController.animator == null && _animator != null)
        {
            ragdollController.animator = _animator;
        }

        ragdollController.InitializeIfNeeded();
    }

    private void HandleDamageTaken(float damage)
    {
        if (_isDead || damage < damageAnimationMinDamage)
        {
            return;
        }

        if (Time.time < _lastDamageAnimationTime + damageAnimationMinInterval)
        {
            return;
        }

        if (!_hasAnimator || _animator == null)
        {
            return;
        }

        bool shouldPreserveWeaponPose = ShouldPreserveWeaponPoseDuringDamageAnimation();
        _lastDamageAnimationTime = Time.time;

        if (_damageAnimationCoroutine != null)
        {
            StopCoroutine(_damageAnimationCoroutine);
        }

        if (shouldPreserveWeaponPose)
        {
            RestoreWeaponPoseAfterDamageAnimation();
        }

        _animator.SetBool(_animIDIsDamage, true);
        PlayDamageAnimatorState();
        _damageAnimationCoroutine = StartCoroutine(ResetDamageAnimatorBool(shouldPreserveWeaponPose));
    }

    private void ResolveWeaponReferences()
    {
        if (_weaponController == null)
        {
            _weaponController = GetComponent<PlayerWeaponController>();
        }

        if (_weaponController == null)
        {
            _weaponController = GetComponentInChildren<PlayerWeaponController>(true);
        }

        if (_weaponController == null)
        {
            _weaponController = GetComponentInParent<PlayerWeaponController>();
        }

        if (_weaponIKController == null)
        {
            _weaponIKController = GetComponent<PlayerWeaponIKController>();
        }

        if (_weaponIKController == null)
        {
            _weaponIKController = GetComponentInChildren<PlayerWeaponIKController>(true);
        }

        if (_weaponIKController == null)
        {
            _weaponIKController = GetComponentInParent<PlayerWeaponIKController>();
        }
    }

    private bool ShouldPreserveWeaponPoseDuringDamageAnimation()
    {
        if (!preserveWeaponPoseDuringDamageAnimation)
        {
            return false;
        }

        ResolveWeaponReferences();

        Weapon currentWeapon = _weaponController != null ? _weaponController.CurrentWeapon : null;
        return currentWeapon != null && currentWeapon.gameObject.activeInHierarchy
            || (_input != null && _input.IsShooterModeActive);
    }

    private void RestoreWeaponPoseAfterDamageAnimation()
    {
        ResolveWeaponReferences();

        if (_weaponIKController != null)
        {
            _weaponIKController.SnapCurrentWeaponPose();
        }
    }

    private bool IsEquippedBodyRotationWeaponActive()
    {
        ResolveWeaponReferences();

        Weapon currentWeapon = _weaponController != null ? _weaponController.CurrentWeapon : null;

        if (currentWeapon == null || !currentWeapon.gameObject.activeInHierarchy)
        {
            return false;
        }

        WeaponIKGrip grip = currentWeapon.GetComponent<WeaponIKGrip>()
            ?? currentWeapon.GetComponentInChildren<WeaponIKGrip>(true);

        return grip != null && !string.IsNullOrWhiteSpace(grip.EquippedAnimatorBool);
    }

    private void PlayDamageAnimatorState()
    {
        if (_animator == null || string.IsNullOrWhiteSpace(damageStateName))
        {
            return;
        }

        int layerIndex = ResolveAnimatorLayerIndex(damageAnimatorLayerName);

        if (layerIndex < 0)
        {
            return;
        }

        string resolvedLayerName = _animator.GetLayerName(layerIndex);
        string fullStateName = string.IsNullOrWhiteSpace(resolvedLayerName)
            ? damageStateName
            : $"{resolvedLayerName}.{damageStateName}";

        if (TryCrossFadeAnimatorState(fullStateName, layerIndex, damageAnimationFadeDuration)
            || TryCrossFadeAnimatorState(damageStateName, layerIndex, damageAnimationFadeDuration))
        {
            return;
        }
    }

    private bool TryCrossFadeAnimatorState(string stateName, int layerIndex, float transitionDuration)
    {
        int stateHash = Animator.StringToHash(stateName);

        if (!_animator.HasState(layerIndex, stateHash))
        {
            return false;
        }

        _animator.CrossFadeInFixedTime(stateHash, transitionDuration, layerIndex, 0f);
        return true;
    }

    private int ResolveAnimatorLayerIndex(string layerName)
    {
        if (_animator == null)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(layerName))
        {
            int layerIndex = _animator.GetLayerIndex(layerName);

            if (layerIndex >= 0)
            {
                return layerIndex;
            }
        }

        return _animator.layerCount > 0 ? 0 : -1;
    }

    private IEnumerator ResetDamageAnimatorBool(bool restoreWeaponPose)
    {
        if (damageBoolResetDelay > 0f)
        {
            yield return new WaitForSeconds(damageBoolResetDelay);
        }
        else
        {
            yield return null;
        }

        if (_animator != null)
        {
            _animator.SetBool(_animIDIsDamage, false);
        }

        if (restoreWeaponPose)
        {
            RestoreWeaponPoseAfterDamageAnimation();
        }

        _damageAnimationCoroutine = null;
    }

    private void HandleDeath()
    {
        ResolveDeathRuntimeReferences();

        if (_isDead)
        {
            return;
        }

        _isDead = true;
        _deathStartedGrounded = IsDeathGrounded();
        _deathBodyGrounded = false;
        ResolveDeathRenderers();

        _speed = 0f;
        _animationBlend = 0f;
        _verticalVelocity = Mathf.Min(_verticalVelocity, deathInitialFallVelocity);

        if (_damageAnimationCoroutine != null)
        {
            StopCoroutine(_damageAnimationCoroutine);
            _damageAnimationCoroutine = null;
        }

        if (_controller != null)
        {
            _controller.enabled = false;
        }

        if (_input != null)
        {
            _input.MoveInput(Vector2.zero);
            _input.LookInput(Vector2.zero);
            _input.JumpInput(false);
            _input.SprintInput(false);
            _input.WalkInput(false);
            _input.fireHeld = false;
            _input.enabled = false;
        }

        PlayerWeaponIKController weaponIKController = GetComponent<PlayerWeaponIKController>();

        if (weaponIKController != null)
        {
            weaponIKController.enabled = false;
        }

        bool activatedRagdoll = useRagdollOnDeath && ActivateRagdollDeath();

        if (!activatedRagdoll && _hasAnimator && _animator != null)
        {
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
            _animator.SetBool(_animIDJump, false);
            _animator.SetBool(_animIDFreeFall, false);
            _animator.SetBool(_animIDIsDamage, false);
            _animator.SetBool(_animIDIsDead, true);
        }

        if (_sceneReloadCoroutine != null)
        {
            StopCoroutine(_sceneReloadCoroutine);
        }

        if (CoopSessionState.IsCoopSession)
        {
            _sceneReloadCoroutine = null;
            return;
        }

        _sceneReloadCoroutine = null;
        DeathChoiceMenu.ShowSinglePlayer();
    }

    public void ActivateNetworkRagdollDeath()
    {
        ResolveDeathRuntimeReferences();

        if (_isDead)
        {
            if (!_ragdollDeathActive)
            {
                ActivateRagdollDeath();
            }

            return;
        }

        HandleDeath();
    }

    public void ReviveFromNetwork(Vector3 position, Quaternion rotation, bool enableLocalControl)
    {
        ResolveDeathRuntimeReferences();

        if (_sceneReloadCoroutine != null)
        {
            StopCoroutine(_sceneReloadCoroutine);
            _sceneReloadCoroutine = null;
        }

        if (_damageAnimationCoroutine != null)
        {
            StopCoroutine(_damageAnimationCoroutine);
            _damageAnimationCoroutine = null;
        }

        bool controllerWasEnabled = _controller != null && _controller.enabled;
        if (_controller != null)
        {
            _controller.enabled = false;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (ragdollController != null)
        {
            ragdollController.DisableRagdoll();
        }

        _isDead = false;
        _ragdollDeathActive = false;
        _deathBodyGrounded = false;
        _deathStartedGrounded = true;
        _verticalVelocity = 0f;
        _speed = 0f;
        _animationBlend = 0f;
        Grounded = true;

        if (_hasAnimator && _animator != null)
        {
            _animator.enabled = true;
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
            _animator.SetBool(_animIDGrounded, true);
            _animator.SetBool(_animIDJump, false);
            _animator.SetBool(_animIDFreeFall, false);
            _animator.SetBool(_animIDIsDamage, false);
            _animator.SetBool(_animIDIsDead, false);
        }

        if (_input != null)
        {
            _input.MoveInput(Vector2.zero);
            _input.LookInput(Vector2.zero);
            _input.JumpInput(false);
            _input.SprintInput(false);
            _input.WalkInput(false);
            _input.fireHeld = false;
            _input.enabled = enableLocalControl;
        }

        if (_weaponIKController != null)
        {
            _weaponIKController.enabled = true;
            _weaponIKController.SnapCurrentWeaponPose();
        }

        if (_controller != null)
        {
            _controller.enabled = enableLocalControl || controllerWasEnabled;
        }

        enabled = enableLocalControl || enabled;
    }

    private void ResolveDeathRuntimeReferences()
    {
        ResolveAnimator();
        AssignAnimationIDs();
        ResolveRagdollController();

        if (_controller == null)
        {
            _controller = GetComponent<CharacterController>();
        }

        if (_input == null)
        {
            _input = GetComponent<InputsController>();
        }

        if (_stats == null)
        {
            _stats = GetComponent<CharacterStats>();
        }

        ResolveWeaponReferences();
    }

    private bool ActivateRagdollDeath()
    {
        ResolveRagdollController();

        if (ragdollController == null || !ragdollController.PrepareRagdoll())
        {
            return false;
        }

        _ragdollDeathActive = true;

        if (_hasAnimator && _animator != null)
        {
            _animator.SetFloat(_animIDSpeed, 0f);
            _animator.SetFloat(_animIDMotionSpeed, 0f);
            _animator.SetBool(_animIDJump, false);
            _animator.SetBool(_animIDFreeFall, false);
            _animator.SetBool(_animIDIsDamage, false);
            _animator.SetBool(_animIDIsDead, false);
        }

        ragdollController.EnableRagdoll();

        Vector3 impulse = transform.TransformDirection(ragdollDeathLocalImpulse);

        if (impulse.sqrMagnitude > 0.0001f)
        {
            ragdollController.ApplyImpulse(impulse, GetRagdollImpulsePoint());
        }

        return true;
    }

    private Vector3 GetRagdollImpulsePoint()
    {
        Transform bodyCenter = GetAnimatorBone(HumanBodyBones.Chest)
            ?? GetAnimatorBone(HumanBodyBones.UpperChest)
            ?? GetAnimatorBone(HumanBodyBones.Spine)
            ?? GetAnimatorBone(HumanBodyBones.Hips);

        return bodyCenter != null
            ? bodyCenter.position
            : transform.position + Vector3.up;
    }

    private Transform GetAnimatorBone(HumanBodyBones bone)
    {
        if (_animator == null || !_animator.isHuman)
        {
            return null;
        }

        return _animator.GetBoneTransform(bone);
    }

    private void ApplyDeathFall()
    {
        if (_deathBodyGrounded)
        {
            return;
        }

        if (_verticalVelocity > -_terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -_terminalVelocity);
        }

        transform.position += Vector3.up * (_verticalVelocity * Time.deltaTime);
    }

    private void ApplyDeathVisualGrounding()
    {
        if (!TryGetDeathGroundY(out float groundY) || !TryGetDeathRendererMinY(out float rendererMinY))
        {
            return;
        }

        float targetMinY = groundY + deathGroundClearance;
        float deltaY = targetMinY - rendererMinY;
        bool shouldGroundBody = _deathStartedGrounded || _deathBodyGrounded || rendererMinY <= targetMinY;

        if (!shouldGroundBody)
        {
            return;
        }

        if (Mathf.Abs(deltaY) > 0.001f)
        {
            transform.position += Vector3.up * deltaY;
        }

        _deathBodyGrounded = true;
        Grounded = true;
        _verticalVelocity = Mathf.Min(_verticalVelocity, deathInitialFallVelocity);
    }

    private bool IsDeathGrounded()
    {
        if (Grounded || (_controller != null && _controller.enabled && _controller.isGrounded))
        {
            return true;
        }

        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y - GroundedOffset,
            transform.position.z);

        return Physics.CheckSphere(
            spherePosition,
            GroundedRadius,
            GroundLayers,
            QueryTriggerInteraction.Ignore);
    }

    private void ResolveDeathRenderers()
    {
        _deathSkinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
    }

    private bool TryGetDeathRendererMinY(out float minY)
    {
        if (_deathSkinnedRenderers == null || _deathSkinnedRenderers.Length == 0)
        {
            ResolveDeathRenderers();
        }

        minY = float.PositiveInfinity;
        bool foundRenderer = false;

        for (int i = 0; i < _deathSkinnedRenderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = _deathSkinnedRenderers[i];

            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            minY = Mathf.Min(minY, renderer.bounds.min.y);
            foundRenderer = true;
        }

        return foundRenderer;
    }

    private bool TryGetDeathGroundY(out float groundY)
    {
        LayerMask groundMask = GroundLayers.value != 0 ? GroundLayers : ~0;
        Vector3 rayOrigin = transform.position + Vector3.up * deathGroundProbeHeight;
        float rayDistance = deathGroundProbeHeight + deathGroundProbeDistance;

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private IEnumerator ReloadSceneAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

        ReloadCurrentScene();
    }

    private IEnumerator ReloadSceneAfterDeathAnimation()
    {
        yield return null;

        float waitStartedAt = Time.time;

        while (Time.time - waitStartedAt < deathReloadFallbackDelay)
        {
            if (TryGetDeathStateInfo(out AnimatorStateInfo deathStateInfo))
            {
                float animationLength = Mathf.Max(0.1f, deathStateInfo.length);
                yield return new WaitForSeconds(animationLength + deathReloadExtraDelay);
                ReloadCurrentScene();
                yield break;
            }

            yield return null;
        }

        ReloadCurrentScene();
    }

    private bool TryGetDeathStateInfo(out AnimatorStateInfo stateInfo)
    {
        stateInfo = default;

        if (_animator == null || string.IsNullOrEmpty(deathStateName))
        {
            return false;
        }

        if (_animator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = _animator.GetNextAnimatorStateInfo(0);

            if (nextStateInfo.IsName(deathStateName))
            {
                stateInfo = nextStateInfo;
                return true;
            }
        }

        AnimatorStateInfo currentStateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        if (!currentStateInfo.IsName(deathStateName))
        {
            return false;
        }

        stateInfo = currentStateInfo;
        return true;
    }

    private static void ReloadCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y - GroundedOffset,
            transform.position.z);

        bool controllerGrounded = _controller != null && _controller.enabled && _controller.isGrounded;
        Grounded = controllerGrounded || Physics.CheckSphere(
            spherePosition,
            GroundedRadius,
            GroundLayers,
            QueryTriggerInteraction.Ignore);

        if (_hasAnimator)
        {
            _animator.SetBool(_animIDGrounded, Grounded);
        }
    }

    private void CameraRotation()
    {
        if (_input == null || CinemachineCameraTarget == null)
        {
            return;
        }

        UpdateCameraYawPitchFromInput();

        if (!ShouldRotateBodyToAim)
        {
            _wasRotateBodyMode = false;
            _aimRotationVelocity = 0f;

            SetCameraTargetWorldRotation();
            return;
        }

        if (!_wasRotateBodyMode)
        {
            _wasRotateBodyMode = true;
            _aimRotationVelocity = 0f;
        }

        float targetBodyYaw = GetAimBodyYaw();

        float bodyYaw = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            targetBodyYaw,
            ref _aimRotationVelocity,
            GetBodyRotationSmoothTime());

        transform.rotation = Quaternion.Euler(0f, bodyYaw, 0f);
        SetCameraTargetWorldRotation();
    }

    private void UpdateCameraYawPitchFromInput()
    {
        if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
            _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
        }

        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
    }

    private void SetCameraTargetWorldRotation()
    {
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
            _cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw,
            0.0f);
    }

    private float GetAimBodyYaw()
    {
        Camera aimCamera = _mainCamera != null ? _mainCamera.GetComponent<Camera>() : Camera.main;

        if (aimCamera != null)
        {
            Vector3 aimPoint = GetCameraAimPoint(aimCamera);
            Vector3 aimDirection = aimPoint - transform.position;
            aimDirection.y = 0f;

            if (aimDirection.sqrMagnitude > _threshold)
            {
                return Mathf.Atan2(aimDirection.x, aimDirection.z) * Mathf.Rad2Deg;
            }
        }

        return _cinemachineTargetYaw;
    }

    private Vector3 GetCameraAimPoint(Camera aimCamera)
    {
        Weapon currentWeapon = _weaponController != null ? _weaponController.CurrentWeapon : null;
        float range = currentWeapon != null ? currentWeapon.Range : 5000f;
        LayerMask hitMask = currentWeapon != null ? currentWeapon.HitMask : ~0;
        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (ShooterAimUtility.TryRaycastIgnoringOwner(
                aimRay.origin,
                aimRay.direction,
                range,
                hitMask,
                transform.root,
                out RaycastHit hit))
        {
            return hit.point;
        }

        return aimRay.origin + aimRay.direction * range;
    }

    private float GetBodyRotationSmoothTime()
    {
        return IsAimCameraActive ? AimRotationSmoothTime : hipFireRotationSmoothTime;
    }

    private void UpdateMovementState()
    {
        if (_input == null)
        {
            _currentMovementState = MovementState.Running;
            return;
        }

        if (_stats != null && _stats.AreStaminaActionsLocked)
        {
            StopStaminaActions();
            return;
        }

        bool wantsToSprint = !IsShooterBodyRotationActive && _input.sprint && _input.move != Vector2.zero;

        if (wantsToSprint)
        {
            float staminaToConsume = GetSprintStaminaCostPerFrame();

            if (_stats != null && _stats.currentStamina < staminaToConsume)
            {
                _stats.UseStamina(_stats.currentStamina);
                StopSprinting();
                return;
            }

            _currentMovementState = MovementState.Sprinting;
            return;
        }

        ApplyDefaultMovementState();
    }

    private float GetSprintStaminaCostPerFrame()
    {
        return _stats == null ? 0f : _stats.sprintCostPerSecond * Time.deltaTime;
    }

    private void ApplyDefaultMovementState()
    {
        if (_input == null)
        {
            _currentMovementState = MovementState.Running;
            return;
        }

        _currentMovementState = _input.walk ? MovementState.Walking : MovementState.Running;
    }

    private void StopSprinting()
    {
        ApplyDefaultMovementState();

        if (_input != null && _input.sprint)
        {
            _input.SprintInput(false);
        }
    }

    private void StopJumping()
    {
        if (_input != null && _input.jump)
        {
            _input.JumpInput(false);
        }
    }

    private void StopStaminaActions()
    {
        StopSprinting();
        StopJumping();
    }

    private void Move()
    {
        if (_input == null || _controller == null)
        {
            return;
        }

        float targetSpeed = _currentMovementState switch
        {
            MovementState.Walking => WalkSpeed,
            MovementState.Running => MoveSpeed,
            MovementState.Sprinting => SprintSpeed,
            _ => MoveSpeed
        };

        if (IsPrecisionMovementActive)
        {
            targetSpeed = Mathf.Min(targetSpeed, AimMoveSpeed);
        }

        if (_input.move == Vector2.zero)
        {
            targetSpeed = 0.0f;
        }

        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;
        float speedOffset = 0.1f;
        float inputMagnitude = _input.move == Vector2.zero
            ? 0f
            : (_input.analogMovement ? Mathf.Clamp01(_input.move.magnitude) : 1f);
        float directionalSpeedMultiplier = GetDirectionalSpeedMultiplier(_input.move);
        float desiredSpeed = targetSpeed * inputMagnitude * directionalSpeedMultiplier;

        if (currentHorizontalSpeed < desiredSpeed - speedOffset ||
            currentHorizontalSpeed > desiredSpeed + speedOffset)
        {
            _speed = Mathf.Lerp(currentHorizontalSpeed, desiredSpeed, Time.deltaTime * SpeedChangeRate);
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = desiredSpeed;
        }

        _animationBlend = Mathf.Lerp(_animationBlend, desiredSpeed, Time.deltaTime * SpeedChangeRate);
        if (_animationBlend < 0.01f)
        {
            _animationBlend = 0f;
        }

        Vector3 moveDirection = GetCameraRelativeMoveDirection(_input.move);

        if (moveDirection.sqrMagnitude > _threshold && !ShouldRotateBodyToAim)
        {
            RotateTowardsMoveDirection(moveDirection);
        }

        Vector3 horizontalMove = moveDirection * (_speed * Time.deltaTime);
        ApplyStairAssist(moveDirection, horizontalMove.magnitude);
        _controller.Move(horizontalMove + Vector3.up * (_verticalVelocity * Time.deltaTime));

        if (_hasAnimator)
        {
            _animator.SetFloat(_animIDSpeed, _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, inputMagnitude * directionalSpeedMultiplier);
        }

        if (_currentMovementState == MovementState.Sprinting &&
            _input.move != Vector2.zero &&
            _stats != null)
        {
            float staminaToConsume = GetSprintStaminaCostPerFrame();

            if (!_stats.UseStamina(staminaToConsume))
            {
                StopSprinting();
            }
            else
            {
                _stats.ConsumeSprintNeeds(Time.deltaTime);
            }
        }
    }

    private void ApplyStairAssist(Vector3 moveDirection, float horizontalDistance)
    {
        if (!CanUseStairAssist(moveDirection, horizontalDistance))
        {
            return;
        }

        if (!TryGetStairLift(moveDirection, horizontalDistance, out float lift))
        {
            return;
        }

        _controller.Move(Vector3.up * lift);

        if (_verticalVelocity < 0f)
        {
            _verticalVelocity = -0.5f;
        }
    }

    private bool CanUseStairAssist(Vector3 moveDirection, float horizontalDistance)
    {
        return UseStairAssist
            && Grounded
            && _controller != null
            && _controller.enabled
            && _verticalVelocity <= 0.01f
            && horizontalDistance > 0.001f
            && moveDirection.sqrMagnitude > _threshold;
    }

    private bool TryGetStairLift(Vector3 moveDirection, float horizontalDistance, out float lift)
    {
        lift = 0f;

        float maxProbeRadius = Mathf.Max(0.02f, _controller.radius - _controller.skinWidth);
        float probeRadius = Mathf.Clamp(StairProbeRadius, 0.02f, maxProbeRadius);
        Vector3 controllerCenter = GetControllerWorldCenter();
        float controllerBottomY = GetControllerBottomY(controllerCenter);
        Vector3 lowerOrigin = new Vector3(
            controllerCenter.x,
            controllerBottomY + probeRadius + StairGroundClearance,
            controllerCenter.z);
        int stairLayers = GetStairCollisionLayers();
        float checkDistance = Mathf.Max(StairCheckDistance, horizontalDistance + probeRadius);

        if (!TrySphereCastIgnoringSelf(lowerOrigin, probeRadius, moveDirection, checkDistance, stairLayers, out RaycastHit lowerHit))
        {
            return false;
        }

        float upperCastDistance = Mathf.Max(0.05f, lowerHit.distance + probeRadius);
        Vector3 upperOrigin = lowerOrigin + Vector3.up * StairStepOffset;

        if (TrySphereCastIgnoringSelf(upperOrigin, probeRadius, moveDirection, upperCastDistance, stairLayers, out _))
        {
            return false;
        }

        Vector3 downOrigin = new Vector3(controllerCenter.x, controllerBottomY, controllerCenter.z)
            + moveDirection.normalized * (lowerHit.distance + probeRadius + StairGroundClearance)
            + Vector3.up * (StairStepOffset + StairGroundClearance);
        float downDistance = StairStepOffset + StairGroundClearance * 2f + 0.1f;

        if (!TryRaycastIgnoringSelf(downOrigin, Vector3.down, downDistance, stairLayers, out RaycastHit landingHit))
        {
            return false;
        }

        if (Vector3.Angle(landingHit.normal, Vector3.up) > _controller.slopeLimit)
        {
            return false;
        }

        float stepHeight = landingHit.point.y - controllerBottomY;

        if (stepHeight <= StairGroundClearance || stepHeight > StairStepOffset + StairGroundClearance)
        {
            return false;
        }

        float requestedLift = stepHeight + StairGroundClearance;
        lift = StairMaxLiftPerSecond <= 0f
            ? requestedLift
            : Mathf.Min(requestedLift, StairMaxLiftPerSecond * Time.deltaTime);

        return lift > 0.001f;
    }

    private Vector3 GetControllerWorldCenter()
    {
        return transform.TransformPoint(_controller.center);
    }

    private float GetControllerBottomY(Vector3 controllerCenter)
    {
        return controllerCenter.y - _controller.height * 0.5f;
    }

    private int GetStairCollisionLayers()
    {
        return StairLayers.value != 0 ? StairLayers.value : GroundLayers.value;
    }

    private bool TrySphereCastIgnoringSelf(
        Vector3 origin,
        float radius,
        Vector3 direction,
        float distance,
        int layerMask,
        out RaycastHit closestHit)
    {
        closestHit = default;
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            direction,
            _stairHits,
            distance,
            layerMask,
            QueryTriggerInteraction.Ignore);

        return TryGetClosestNonSelfHit(hitCount, out closestHit);
    }

    private bool TryRaycastIgnoringSelf(
        Vector3 origin,
        Vector3 direction,
        float distance,
        int layerMask,
        out RaycastHit closestHit)
    {
        closestHit = default;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            _stairHits,
            distance,
            layerMask,
            QueryTriggerInteraction.Ignore);

        return TryGetClosestNonSelfHit(hitCount, out closestHit);
    }

    private bool TryGetClosestNonSelfHit(int hitCount, out RaycastHit closestHit)
    {
        closestHit = default;
        float closestDistance = float.PositiveInfinity;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _stairHits[i];

            if (hit.collider == null || hit.collider.transform.root == transform.root)
            {
                continue;
            }

            if (hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            closestHit = hit;
            foundHit = true;
        }

        return foundHit;
    }

    private Vector3 GetCameraRelativeMoveDirection(Vector2 moveInput)
    {
        if (moveInput == Vector2.zero)
        {
            return Vector3.zero;
        }

        Quaternion cameraYaw = Quaternion.Euler(0f, GetCameraReferenceYaw(), 0f);
        Vector3 forward = cameraYaw * Vector3.forward;
        Vector3 right = cameraYaw * Vector3.right;

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        return direction.sqrMagnitude > _threshold ? direction.normalized : Vector3.zero;
    }

    private float GetCameraReferenceYaw()
    {
        if (CinemachineCameraTarget != null)
        {
            return _cinemachineTargetYaw;
        }

        if (_mainCamera != null)
        {
            return _mainCamera.transform.eulerAngles.y;
        }

        return transform.eulerAngles.y;
    }

    private float GetDirectionalSpeedMultiplier(Vector2 moveInput)
    {
        if (!IsPrecisionMovementActive || moveInput == Vector2.zero)
        {
            return 1f;
        }

        float strafeMultiplier = Mathf.Lerp(1f, AimStrafeSpeedMultiplier, Mathf.Abs(moveInput.x));
        float backwardMultiplier = moveInput.y < 0f
            ? Mathf.Lerp(1f, AimBackwardSpeedMultiplier, Mathf.Abs(moveInput.y))
            : 1f;

        return Mathf.Min(strafeMultiplier, backwardMultiplier);
    }

    private void RotateTowardsMoveDirection(Vector3 moveDirection)
    {
        _targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;

        float rotation = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            _targetRotation,
            ref _moveRotationVelocity,
            RotationSmoothTime);

        transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
    }

    private void JumpAndGravity()
    {
        bool staminaActionsLocked = _stats != null && _stats.AreStaminaActionsLocked;

        if (Grounded)
        {
            _fallTimeoutDelta = FallTimeout;

            if (_hasAnimator)
            {
                _animator.SetBool(_animIDJump, false);
                _animator.SetBool(_animIDFreeFall, false);
            }

            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }

            if (staminaActionsLocked)
            {
                StopJumping();
            }

            if (!staminaActionsLocked && _input != null && _input.jump && _jumpTimeoutDelta <= 0.0f)
            {
                if (_stats == null || _stats.UseStamina(_stats.jumpCost))
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    _stats?.ConsumeJumpNeeds();

                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }
            }

            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            _jumpTimeoutDelta = JumpTimeout;

            if (_fallTimeoutDelta >= 0.0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }
            else if (_hasAnimator)
            {
                _animator.SetBool(_animIDFreeFall, true);
            }

            if (_input != null)
            {
                _input.jump = false;
            }
        }

        if (_verticalVelocity < _terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }
    }

    private static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f)
        {
            angle += 360f;
        }

        if (angle > 360f)
        {
            angle -= 360f;
        }

        return Mathf.Clamp(angle, min, max);
    }

    private void OnDrawGizmosSelected()
    {
        Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

        Gizmos.color = Grounded ? transparentGreen : transparentRed;
        Gizmos.DrawSphere(
            new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
            GroundedRadius);
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (FootstepAudioClips == null || FootstepAudioClips.Length == 0 || _controller == null)
        {
            return;
        }

        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            int index = Random.Range(0, FootstepAudioClips.Length);
            AudioSource.PlayClipAtPoint(
                FootstepAudioClips[index],
                transform.TransformPoint(_controller.center),
                FootstepAudioVolume);
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (LandingAudioClip == null || _controller == null)
        {
            return;
        }

        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(
                LandingAudioClip,
                transform.TransformPoint(_controller.center),
                FootstepAudioVolume);
        }
    }
}

public class DeathChoiceMenu : MonoBehaviour
{
    private const string MainMenuSceneName = "MainScene";

    private static DeathChoiceMenu instance;

    private RectTransform root;
    private Text titleText;
    private Text subtitleText;
    private Text statusText;
    private Button restartButton;
    private Button lobbyButton;
    private Font font;
    private bool cursorGuardActive;

    public static void ShowSinglePlayer()
    {
        DeathChoiceMenu menu = EnsureActive();
        menu.Configure(
            "Вы погибли",
            "Что делаем дальше?",
            "Начать заново",
            "Главное меню",
            null,
            RestartCurrentScene,
            ReturnToMainMenu);
    }

    public static void ShowCoopVote(System.Action<int> onVote)
    {
        DeathChoiceMenu menu = EnsureActive();
        menu.Configure(
            "Все игроки погибли",
            "Выберите дальнейшее действие. Решение принимается голосованием.",
            "Начать заново",
            "Вернуться в лобби",
            "Ожидание голосов...",
            () => onVote?.Invoke(1),
            () => onVote?.Invoke(2));
    }

    public static void SetStatus(string status)
    {
        if (instance == null || instance.statusText == null)
            return;

        instance.statusText.text = status ?? string.Empty;
        instance.statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(status));
    }

    public static void Hide()
    {
        if (instance == null || instance.root == null)
            return;

        instance.root.gameObject.SetActive(false);
        instance.DeactivateCursorGuard();
    }

    private static DeathChoiceMenu EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject menuObject = new GameObject("Death Choice Menu");
        instance = menuObject.AddComponent<DeathChoiceMenu>();
        DontDestroyOnLoad(menuObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void Configure(
        string title,
        string subtitle,
        string restartLabel,
        string lobbyLabel,
        string status,
        UnityEngine.Events.UnityAction restartAction,
        UnityEngine.Events.UnityAction lobbyAction)
    {
        if (root == null)
            BuildUI();

        root.gameObject.SetActive(true);
        ActivateCursorGuard();

        titleText.text = title;
        subtitleText.text = subtitle;
        statusText.text = status ?? string.Empty;
        statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(status));

        SetButton(restartButton, restartLabel, restartAction);
        SetButton(lobbyButton, lobbyLabel, lobbyAction);
    }

    private void Update()
    {
        if (root != null && root.gameObject.activeSelf)
            GameCursorGuard.ApplyUiCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (root != null && root.gameObject.activeSelf)
            GameCursorGuard.ApplyUiCursor();
    }

    private void OnDisable()
    {
        DeactivateCursorGuard();
    }

    private void OnDestroy()
    {
        DeactivateCursorGuard();
    }

    private static void SetButton(Button button, string label, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        Text text = button.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = label;

        button.interactable = true;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private static void RestartCurrentScene()
    {
        Time.timeScale = 1f;
        Hide();

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
            return;
        }

        SceneManager.LoadScene(activeScene.name);
    }

    private static void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        Hide();
        CoopSessionState.Clear();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void BuildUI()
    {
        if (root != null)
            return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject("Root", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(canvasObject.transform, false);
        root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image overlay = rootObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelObject.transform.SetParent(root, false);

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(560f, 360f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.045f, 0.052f, 0.06f, 0.92f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(38, 38, 34, 34);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText = CreateText("Title", panelObject.transform, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
        titleText.rectTransform.sizeDelta = new Vector2(0f, 52f);

        subtitleText = CreateText("Subtitle", panelObject.transform, 20, TextAnchor.MiddleCenter, FontStyle.Normal);
        subtitleText.rectTransform.sizeDelta = new Vector2(0f, 58f);

        restartButton = CreateButton("Restart", panelObject.transform, "Начать заново");
        lobbyButton = CreateButton("Lobby", panelObject.transform, "Главное меню");

        statusText = CreateText("Status", panelObject.transform, 17, TextAnchor.MiddleCenter, FontStyle.Normal);
        statusText.color = new Color(0.78f, 0.82f, 0.88f, 1f);
        statusText.rectTransform.sizeDelta = new Vector2(0f, 28f);

        root.gameObject.SetActive(false);
    }

    private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.preferredHeight = text.rectTransform.sizeDelta.y;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 56f);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 56f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.16f, 0.19f, 0.95f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.19f, 0.23f, 0.27f, 1f);
        colors.pressedColor = new Color(0.09f, 0.11f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text text = CreateText("Text", buttonObject.transform, 22, TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;
        return button;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(eventSystemObject);
    }

    private void ActivateCursorGuard()
    {
        if (cursorGuardActive)
        {
            GameCursorGuard.ApplyUiCursor();
            return;
        }

        cursorGuardActive = true;
        GameCursorGuard.PushUiCursor();
    }

    private void DeactivateCursorGuard()
    {
        if (!cursorGuardActive)
            return;

        cursorGuardActive = false;
        GameCursorGuard.PopUiCursor();
    }
}
