using System;
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

    private InputActions _inputAction;

    public Action OnUse;
    public Action OnOpenInventory;
    public Action OnPlayerFire;
    public Action OnPlayerSwithcWeapon;
    public Action<bool> OnPlayerAimChanged;

    public InputActions InputAction => _inputAction;
    public bool IsShooterModeActive => isRotateBodyInsteadOfCamera || aim || fireHeld;

    private void OnEnable()
    {
        _inputAction = new InputActions();
        EnsureDefaultShooterBindings();
        _inputAction.Enable();

        _inputAction.Player.Use.performed += OnActionPerformed;
        _inputAction.Player.OpenInventory.performed += OnOpenInventoryPerformed;

        _inputAction.Shooting.Fire.started += OnFireStarted;
        _inputAction.Shooting.Fire.performed += OnFirePerformed;
        _inputAction.Shooting.Fire.canceled += OnFireCanceled;
        _inputAction.Shooting.SwitchWeapon.performed += OnSwitchWeaponPerformed;
        _inputAction.Shooting.Aim.started += OnAimStarted;
        _inputAction.Shooting.Aim.performed += OnAimPerformed;
        _inputAction.Shooting.Aim.canceled += OnAimCanceled;
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
        _inputAction.Shooting.Aim.started -= OnAimStarted;
        _inputAction.Shooting.Aim.performed -= OnAimPerformed;
        _inputAction.Shooting.Aim.canceled -= OnAimCanceled;

        _inputAction.Disable();
        _inputAction = null;

        SetAimState(false);
        fireHeld = false;
    }

    private void EnsureDefaultShooterBindings()
    {
        if (!addDefaultAimBinding || _inputAction == null || HasActiveBinding(_inputAction.Shooting.Aim))
        {
            return;
        }

        _inputAction.Shooting.Aim.AddBinding("<Mouse>/rightButton").WithGroup("KeyboardMouse");
    }

    private static bool HasActiveBinding(InputAction action)
    {
        return action.bindings.Any(binding => !string.IsNullOrWhiteSpace(binding.path));
    }

    private void OnSwitchWeaponPerformed(InputAction.CallbackContext context)
    {
        OnPlayerSwithcWeapon?.Invoke();
    }

    private void OnFireStarted(InputAction.CallbackContext context)
    {
        fireHeld = true;
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        fireHeld = true;
        OnPlayerFire?.Invoke();
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        fireHeld = false;
    }

    private void OnAimStarted(InputAction.CallbackContext context)
    {
        SetAimState(context.ReadValueAsButton());
    }

    private void OnAimPerformed(InputAction.CallbackContext context)
    {
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
        OnPlayerAimChanged?.Invoke(aim);
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
        SetCursorState(cursorLocked);
    }

    private void SetCursorState(bool newState)
    {
        Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
    }
}
