using System;
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

		[Header("Настройки движения")]
		public bool analogMovement;

		[Header("Настройки курсора мыши")]
		[Range(0.1f, 10f)] public float mouseSensitivity = 1.0f;
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		private InputActions _inputAction;

    public Action OnUse;
    public Action OnOpenInventory;

	public Action OnPlayerFire;
	public Action OnPlayerSwithcWeapon;

	public InputActions InputAction => _inputAction;

    private void OnEnable()
    {
		_inputAction = new();
		_inputAction.Enable();

		_inputAction.Player.Use.performed += OnActionPerformed;
		_inputAction.Player.OpenInventory.performed += OnOpenInventoryPerformed;

		_inputAction.Shooting.Fire.performed += OnFirePerformed;
		_inputAction.Shooting.SwitchWeapon.performed += OnSwitchWeaponPerformed;
    }


    private void OnDisable() 
	{ 
        _inputAction.Player.Use.performed -= OnActionPerformed;
        _inputAction.Player.OpenInventory.performed -= OnOpenInventoryPerformed;

        _inputAction.Shooting.Fire.performed -= OnFirePerformed;
        _inputAction.Shooting.SwitchWeapon.performed -= OnSwitchWeaponPerformed;

        _inputAction.Disable();
    }

    private void OnSwitchWeaponPerformed(InputAction.CallbackContext context)
    {
		OnPlayerSwithcWeapon?.Invoke();
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
		OnPlayerFire?.Invoke();
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
			if(cursorInputForLook)
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