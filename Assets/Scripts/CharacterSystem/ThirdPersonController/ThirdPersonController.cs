 using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

/* 
 * Примечание: анимации вызываются через контроллер как для персонажа, так и для капсулы с использованием проверок аниматора на null
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

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("Высота, на которую игрок может прыгнуть")]
        public float JumpHeight = 1.2f;

        [Tooltip("Персонаж использует собственное значение гравитации. Значение по умолчанию в движке равно −9,81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Время, которое должно пройти перед тем, как можно будет снова прыгнуть. Установите значение 0f, чтобы прыгать мгновенно снова")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Время, которое должно пройти перед переходом в состояние падения. Полезно, например, при спуске по лестнице")]
        public float FallTimeout = 0.15f;

        [Header("Персонаж на земле")]
        [Tooltip("Определяет, находится ли персонаж на земле. Не использует встроенную проверку grounded компонента CharacterController")]
        public bool Grounded = true;

        [Tooltip("Полезно, когда поверхность неровная")]
        public float GroundedOffset = -0.14f;

        [Tooltip("Радиус проверки касания земли. Должен соответствовать радиусу CharacterController")]
        public float GroundedRadius = 0.28f;

        [Tooltip("Какие слои персонаж использует в качестве земли")]
        public LayerMask GroundLayers;

        [Header("Камера")]
        [Tooltip("Цель слежения, установленная в виртуальной камере, за которой камера будет следовать")]
        public GameObject CinemachineCameraTarget;

        [Tooltip("Как далеко в градусах можно повернуть камеру вверх")]
        public float TopClamp = 70.0f;

        [Tooltip("Как далеко в градусах можно опустить камеру вниз")]
        public float BottomClamp = -30.0f;

        [Tooltip("Дополнительные градусы для переопределения поворота камеры. Полезно для точной настройки позиции камеры при блокировке")]
        public float CameraAngleOverride = 0.0f;

        [Tooltip("Для блокировки позиции камеры по всем осям")]
        public bool LockCameraPosition = false;

        // Камера
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // Персонаж
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;
        private MovementState _currentMovementState = MovementState.Running;
        private CharacterStats _stats;


    // Время, прошедшее с предыдущего кадра
    private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // ID анимаций
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

        private bool IsCurrentDeviceMouse
        {
            get
            {
                #if ENABLE_INPUT_SYSTEM
                    return _playerInput.currentControlScheme == "KeyboardMouse";
                #else
				    return false;
                #endif
            }
        }


        private void Awake()
        {
            // Получаем ссылку на нашу основную камеру
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

    private void Start()
    {
        _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;

        _hasAnimator = TryGetComponent(out _animator);
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<InputsController>();
        _stats = GetComponent<CharacterStats>();

        if (_stats == null)
        {
            Debug.LogError("CharacterStats не найден!");
        }

        _playerInput = GetComponent<PlayerInput>();

        AssignAnimationIDs();

        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    private void Update()
        {
            _hasAnimator = TryGetComponent(out _animator);

            UpdateMovementState();
        JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
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
            // Устанавливаем позицию сферы со смещением
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            // Обновляем аниматор, если используется персонаж
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void CameraRotation()
        {
            // если есть ввод и позиция камеры не зафиксирована
            if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
            {
                // Мышь: не используем deltaTime — значение уже зависит от частоты кадров
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetYaw += _input.look.x * deltaTimeMultiplier;
                _cinemachineTargetPitch += _input.look.y * deltaTimeMultiplier;
            }

            // Ограничиваем наши повороты, чтобы значения находились в пределах 360 градусов
            _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Камера будет следовать за этой целью
            CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
                _cinemachineTargetYaw, 0.0f);
        }

    private void UpdateMovementState()
    {
        if (_stats == null)
        {
            ApplyDefaultMovementState();
            return;
        }

        if (_stats.AreStaminaActionsLocked)
        {
            StopStaminaActions();
            return;
        }

        bool wantsToSprint = _input.sprint && _input.move != Vector2.zero;

        if (wantsToSprint)
        {
            float staminaToConsume = GetSprintStaminaCostPerFrame();

            if (_stats.currentStamina < staminaToConsume)
            {
                // Списываем остаток стамины в ноль, чтобы спринт не включался снова на следующем кадре.
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
        _currentMovementState = _input != null && _input.walk
            ? MovementState.Walking
            : MovementState.Running;
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
            // Устанавливаем целевую скорость на основе скорости движения, скорости спринта и того, зажат ли спринт
            float targetSpeed = _currentMovementState switch
            {
                MovementState.Walking => WalkSpeed,
                MovementState.Running => MoveSpeed,
                MovementState.Sprinting => SprintSpeed,
                _ => MoveSpeed
            };

            // Простое ускорение и замедление, созданное для простоты удаления, замены или доработки

            // примечание: оператор == Vector2 использует приближение, поэтому он не подвержен ошибкам плавающей точки и дешевле, чем вычисление magnitude
            // если ввода нет, устанавливаем целевую скорость в 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // Ссылка на текущую горизонтальную скорость игрока
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // Ускоряемся или замедляемся до целевой скорости
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // Создаёт изогнутый результат вместо линейного, обеспечивая более органичное изменение скорости
                // примечание: значение T в Lerp автоматически ограничивается, поэтому нам не нужно ограничивать нашу скорость
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // Округляем скорость до 3 десятичных знаков
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // Нормализуем направление ввода
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // примечание: оператор != Vector2 использует приближение, поэтому он не подвержен ошибкам плавающей точки и дешевле, чем вычисление magnitude
            // если есть ввод перемещения, поворачиваем игрока при движении
            if (_input.move != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg +
                                  _mainCamera.transform.eulerAngles.y;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // Поворачиваемся в направлении ввода относительно позиции камеры
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // Перемещаем игрока
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // Обновляем аниматор, если используется персонаж
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
                
            }

        // === РАСХОД СТАМИНЫ ПРИ СПРИНТЕ ===
        if (_currentMovementState == MovementState.Sprinting &&
            _input.move != Vector2.zero &&
            _stats != null)
        {
            float staminaToConsume = GetSprintStaminaCostPerFrame();

            if (!_stats.UseStamina(staminaToConsume))
            {
                // Если стамины не хватило — принудительно переводим в обычный бег
                StopSprinting();
            }
            else
            {
                _stats.ConsumeSprintNeeds(Time.deltaTime);
            }
        }
    }

        private void JumpAndGravity()
        {
            bool staminaActionsLocked = _stats != null && _stats.AreStaminaActionsLocked;

            if (Grounded)
            {
                // Сбрасываем таймер таймаута падения
                _fallTimeoutDelta = FallTimeout;

                // Обновляем аниматор, если используется персонаж
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // Останавливаем бесконечное падение скорости, когда персонаж на земле
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

            if (staminaActionsLocked)
            {
                StopJumping();
            }

            // Прыжок
            if (!staminaActionsLocked && _input.jump && _jumpTimeoutDelta <= 0.0f)
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

            // Таймаут прыжка
            if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // Сбрасываем таймер таймаута прыжка
                _jumpTimeoutDelta = JumpTimeout;

                // Таймаут падения
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // Обновляем аниматор, если используется персонаж
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }

                // Если мы не на земле, не прыгаем
                _input.jump = false;
            }

            // Применяем гравитацию с течением времени, если скорость ниже терминальной
            // (умножаем на дельту времени дважды для линейного ускорения с течением времени)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // При выборе рисуем гизмо в позиции коллайдера касания земли и с радиусом, соответствующим ему
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);
        }


        /// <summary>
        /// Вызывается из анимации при шаге. Воспроизводит случайный звук шага.
        /// </summary>
        /// <param name="animationEvent">Данные события анимации</param>
        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (LandingAudioClip == null) return;

            // Воспроизводим звук только если вес анимации выше 0.5 (чтобы избежать дублей при переходах)
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    // Выбираем случайный звук шага из массива
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    // Воспроизводим звук в позиции центра коллайдера персонажа
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }


        /// <summary>
        /// Вызывается из анимации при приземлении. Воспроизводит звук удара о землю.
        /// </summary>
        /// <param name="animationEvent">Данные события анимации</param>
        private void OnLand(AnimationEvent animationEvent)
        {
            if (LandingAudioClip == null) return;
            // Воспроизводим звук только если вес анимации выше 0.5
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                // Воспроизводим звук приземления в позиции центра коллайдера
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
