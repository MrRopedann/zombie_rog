using Cinemachine;
using UnityEngine;
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

#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private Animator _animator;
    private CharacterController _controller;
    private InputsController _input;
    private GameObject _mainCamera;

    private const float _threshold = 0.01f;

    private bool _hasAnimator;
    private bool _wasRotateBodyMode;

    private bool IsCurrentDeviceMouse
    {
        get
        {

            return _playerInput != null && _playerInput.currentControlScheme == "KeyboardMouse";
        }
    }

    private bool IsShooterBodyRotationActive => _input != null && _input.IsShooterModeActive;
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

        _hasAnimator = TryGetComponent(out _animator);
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<InputsController>();
        _stats = GetComponent<CharacterStats>();

        if (_input == null)
        {
            Debug.LogError($"{nameof(InputsController)} не найден!", this);
        }

        if (_stats == null)
        {
            Debug.LogError($"{nameof(CharacterStats)} не найден!", this);
        }

#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#endif

        ResolveTpsCameraRig();
        CaptureNormalCameraSettings();
        _cameraAimBlend = IsAimCameraActive ? 1f : 0f;
        ApplyTpsCameraSettings(_cameraAimBlend);

        AssignAnimationIDs();

        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    private void Update()
    {
        _hasAnimator = TryGetComponent(out _animator);

        GroundedCheck();
        UpdateMovementState();
        JumpAndGravity();
        Move();
    }

    private void LateUpdate()
    {
        CameraRotation();
        UpdateTpsCameraRig();
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
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = new Vector3(
            transform.position.x,
            transform.position.y - GroundedOffset,
            transform.position.z);

        Grounded = Physics.CheckSphere(
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

        if (!IsShooterBodyRotationActive)
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

        float bodyYaw = Mathf.SmoothDampAngle(
            transform.eulerAngles.y,
            _cinemachineTargetYaw,
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

        if (moveDirection.sqrMagnitude > _threshold && !IsShooterBodyRotationActive)
        {
            RotateTowardsMoveDirection(moveDirection);
        }

        _controller.Move(moveDirection * (_speed * Time.deltaTime) + Vector3.up * (_verticalVelocity * Time.deltaTime));

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
