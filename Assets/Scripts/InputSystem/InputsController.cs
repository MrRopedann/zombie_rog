using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InputsController : MonoBehaviour
{
    [Header("Параметры ввода персонажа")]
    public Vector2 move;
    public Vector2 look;
    public bool jump;
    public bool sprint;
    public bool walk;
    public bool aim;
    public bool fireHeld;

    [Header("Настройки движения")]
    public bool analogMovement;

    [Header("Настройки мыши")]
    [SerializeField] public bool isRotateBodyInsteadOfCamera = false;
    [Range(0.1f, 10f)] public float mouseSensitivity = 1.0f;
    public bool cursorLocked = true;
    public bool cursorInputForLook = true;

    [Header("Настройки стрельбы")]
    [SerializeField] private bool addDefaultAimBinding = true;
    [SerializeField] private bool addDefaultReloadBinding = true;
    [SerializeField] private bool switchWeaponWithMouseWheel = true;
    [SerializeField] [Min(0f)] private float mouseWheelSwitchThreshold = 0.01f;
    [SerializeField] [Min(0f)] private float shooterModeHoldTime = 0.35f;

    private InputActions _inputAction;
    private float _lastFireInputTime = float.NegativeInfinity;
    private int _lastMouseWheelSwitchFrame = -1;
#if ENABLE_INPUT_SYSTEM
    private readonly List<InputAction> _hotbarSlotActions = new();
#endif

    private static readonly string[] HotbarActionNames =
    {
        "Hotbar1",
        "Hotbar2",
        "Hotbar3",
        "Hotbar4",
        "Hotbar5",
        "Hotbar6",
        "Hotbar7",
        "Hotbar8",
        "Hotbar9",
        "Hotbar0"
    };

    private static readonly string[] HotbarBindingPaths =
    {
        "<Keyboard>/1",
        "<Keyboard>/2",
        "<Keyboard>/3",
        "<Keyboard>/4",
        "<Keyboard>/5",
        "<Keyboard>/6",
        "<Keyboard>/7",
        "<Keyboard>/8",
        "<Keyboard>/9",
        "<Keyboard>/0"
    };

    public Action OnUse;
    public Action OnOpenInventory;
    public Action OnPlayerFire;
    public Action OnPlayerSwithcWeapon;
    public Action<int> OnPlayerSwitchWeaponDelta;
    public Action<int> OnHotbarSlot;
    public Action OnPlayerReload;
    public Action<bool> OnPlayerAimChanged;

    public InputActions InputAction => _inputAction;
    public bool IsRecentFireInput => Time.time - _lastFireInputTime <= shooterModeHoldTime;
    public bool IsShooterModeActive => isRotateBodyInsteadOfCamera || aim;
    public bool ShootingInputBlocked { get; private set; }

    private void OnEnable()
    {
        _inputAction = new InputActions();
        EnsureDefaultShooterBindings();
        EnsureDefaultHotbarBindings();
        _inputAction.Enable();

        _inputAction.Player.Use.performed += OnActionPerformed;
        _inputAction.Player.OpenInventory.performed += OnOpenInventoryPerformed;

        _inputAction.Shooting.Fire.started += OnFireStarted;
        _inputAction.Shooting.Fire.performed += OnFirePerformed;
        _inputAction.Shooting.Fire.canceled += OnFireCanceled;
        _inputAction.Shooting.SwitchWeapon.performed += OnSwitchWeaponPerformed;
        _inputAction.Shooting.Reload.performed += OnReloadPerformed;
        _inputAction.Shooting.Aim.started += OnAimStarted;
        _inputAction.Shooting.Aim.performed += OnAimPerformed;
        _inputAction.Shooting.Aim.canceled += OnAimCanceled;
        SubscribeHotbarActions();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        HandleMouseWheelWeaponSwitch();
#endif
    }

    private void OnDisable()
    {
        if (_inputAction == null)
        {
            return;
        }

        _inputAction.Player.Use.performed -= OnActionPerformed;
        _inputAction.Player.OpenInventory.performed -= OnOpenInventoryPerformed;

        _inputAction.Shooting.Fire.started -= OnFireStarted;
        _inputAction.Shooting.Fire.performed -= OnFirePerformed;
        _inputAction.Shooting.Fire.canceled -= OnFireCanceled;
        _inputAction.Shooting.SwitchWeapon.performed -= OnSwitchWeaponPerformed;
        _inputAction.Shooting.Reload.performed -= OnReloadPerformed;
        _inputAction.Shooting.Aim.started -= OnAimStarted;
        _inputAction.Shooting.Aim.performed -= OnAimPerformed;
        _inputAction.Shooting.Aim.canceled -= OnAimCanceled;
        UnsubscribeHotbarActions();

        _inputAction.Disable();
        _inputAction = null;

        SetAimState(false);
        fireHeld = false;
        _lastFireInputTime = float.NegativeInfinity;
    }

    private void EnsureDefaultShooterBindings()
    {
        if (_inputAction == null)
        {
            return;
        }

        if (addDefaultAimBinding && !HasActiveBinding(_inputAction.Shooting.Aim))
        {
            _inputAction.Shooting.Aim.AddBinding("<Mouse>/rightButton").WithGroup("KeyboardMouse");
        }

        if (addDefaultReloadBinding && !HasActiveBinding(_inputAction.Shooting.Reload))
        {
            _inputAction.Shooting.Reload.AddBinding("<Keyboard>/r").WithGroup("KeyboardMouse");
        }
    }

    private void EnsureDefaultHotbarBindings()
    {
#if ENABLE_INPUT_SYSTEM
        InputActionMap playerMap = _inputAction?.asset.FindActionMap("Player", false);

        if (playerMap == null)
        {
            return;
        }

        for (int i = 0; i < HotbarActionNames.Length; i++)
        {
            InputAction action = playerMap.FindAction(HotbarActionNames[i], false);

            if (action == null)
            {
                action = playerMap.AddAction(HotbarActionNames[i], InputActionType.Button);
            }

            if (!HasBinding(action, HotbarBindingPaths[i]))
            {
                action.AddBinding(HotbarBindingPaths[i]).WithGroup("KeyboardMouse");
            }
        }
#endif
    }

    private static bool HasActiveBinding(InputAction action)
    {
        return action.bindings.Any(binding => !string.IsNullOrWhiteSpace(binding.path));
    }

    private static bool HasBinding(InputAction action, string path)
    {
        return action.bindings.Any(binding => binding.path == path);
    }

    private void SubscribeHotbarActions()
    {
#if ENABLE_INPUT_SYSTEM
        _hotbarSlotActions.Clear();

        for (int i = 0; i < HotbarActionNames.Length; i++)
        {
            InputAction action = _inputAction?.asset.FindAction($"Player/{HotbarActionNames[i]}", false);

            if (action == null)
            {
                continue;
            }

            action.performed += OnHotbarSlotPerformed;
            _hotbarSlotActions.Add(action);
        }
#endif
    }

    private void UnsubscribeHotbarActions()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (InputAction action in _hotbarSlotActions)
        {
            action.performed -= OnHotbarSlotPerformed;
        }

        _hotbarSlotActions.Clear();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void OnHotbarSlotPerformed(InputAction.CallbackContext context)
    {
        for (int i = 0; i < _hotbarSlotActions.Count; i++)
        {
            if (_hotbarSlotActions[i] == context.action)
            {
                OnHotbarSlot?.Invoke(i);
                return;
            }
        }
    }
#endif

    private void OnSwitchWeaponPerformed(InputAction.CallbackContext context)
    {
#if ENABLE_INPUT_SYSTEM
        if (context.control != null && string.Equals(context.control.path, "/Keyboard/tab", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
#endif

        OnPlayerSwithcWeapon?.Invoke();
    }

#if ENABLE_INPUT_SYSTEM
    private void HandleMouseWheelWeaponSwitch()
    {
        if (!switchWeaponWithMouseWheel || Mouse.current == null || _lastMouseWheelSwitchFrame == Time.frameCount)
        {
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) <= mouseWheelSwitchThreshold)
        {
            return;
        }

        int direction = scrollY > 0f ? -1 : 1;
        _lastMouseWheelSwitchFrame = Time.frameCount;
        OnPlayerSwitchWeaponDelta?.Invoke(direction);
    }
#endif

    private void OnReloadPerformed(InputAction.CallbackContext context)
    {
        if (ShootingInputBlocked)
        {
            return;
        }

        OnPlayerReload?.Invoke();
    }

    private void OnFireStarted(InputAction.CallbackContext context)
    {
        if (ShootingInputBlocked)
        {
            fireHeld = false;
            return;
        }

        fireHeld = true;
        RegisterFireInput();
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        if (ShootingInputBlocked)
        {
            fireHeld = false;
            return;
        }

        fireHeld = true;
        RegisterFireInput();

        if (aim)
        {
            OnPlayerFire?.Invoke();
        }
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        fireHeld = false;
    }

    private void RegisterFireInput()
    {
        _lastFireInputTime = Time.time;
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        if (ShootingInputBlocked)
        {
            SetAimState(false);
            return;
        }

        SetAimState(context.ReadValueAsButton());
    }

    private void OnAimPerformed(InputAction.CallbackContext context)
    {
        if (ShootingInputBlocked)
        {
            SetAimState(false);
            return;
        }

        SetAimState(context.ReadValueAsButton());
    }

    private void OnAimCanceled(InputAction.CallbackContext context)
    {
        SetAimState(false);
    }

    private void SetAimState(bool newAimState)
    {
        if (aim == newAimState)
        {
            return;
        }

        aim = newAimState;

        if (!aim)
        {
            fireHeld = false;
        }

        OnPlayerAimChanged?.Invoke(aim);
    }

    public void SetShootingInputBlocked(bool blocked)
    {
        if (ShootingInputBlocked == blocked)
        {
            return;
        }

        ShootingInputBlocked = blocked;

        if (!blocked)
        {
            return;
        }

        fireHeld = false;
        _lastFireInputTime = float.NegativeInfinity;
        SetAimState(false);
    }

    private void OnActionPerformed(InputAction.CallbackContext callbackContext)
    {
        OnUse?.Invoke();
    }

    private void OnOpenInventoryPerformed(InputAction.CallbackContext context)
    {
        OnOpenInventory?.Invoke();
    }

    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnLook(InputValue value)
    {
        if (cursorInputForLook)
        {
            LookInput(value.Get<Vector2>());
        }
    }

    public void OnJump(InputValue value)
    {
        JumpInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }

    public void OnWalk(InputValue value)
    {
        WalkInput(value.isPressed);
    }

    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    }

    public void LookInput(Vector2 newLookDirection)
    {
        look = newLookDirection;
        look *= mouseSensitivity;
    }

    public void JumpInput(bool newJumpState)
    {
        jump = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        sprint = newSprintState;
    }

    public void WalkInput(bool newWalkState)
    {
        walk = newWalkState;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (GameCursorGuard.IsUiCursorRequested)
        {
            GameCursorGuard.ApplyUiCursor();
            return;
        }

        SetCursorState(cursorLocked);
    }

    private void SetCursorState(bool newState)
    {
        if (GameCursorGuard.IsUiCursorRequested)
        {
            GameCursorGuard.ApplyUiCursor();
            return;
        }

        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}

public static class GameCursorGuard
{
    private static int uiCursorRequests;

    public static bool IsUiCursorRequested => uiCursorRequests > 0;

    public static void PushUiCursor()
    {
        uiCursorRequests++;
        ApplyUiCursor();
    }

    public static void PopUiCursor()
    {
        uiCursorRequests = Mathf.Max(0, uiCursorRequests - 1);

        if (IsUiCursorRequested)
            ApplyUiCursor();
    }

    public static void ApplyUiCursor()
    {
        if (!IsUiCursorRequested)
            return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
