using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;


[DisallowMultipleComponent]
public class PlayerWeaponController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject[] objWeapons;
    [SerializeField] private WeaponDefinition[] weaponDefinitions;
    [SerializeField] private Transform weaponInstancesParent;
    [SerializeField] private bool createWeaponsFromDefinitions = true;

    [SerializeField]  private InputsController _inputsController;
    private int _selectedIndexWeapon;
    private Weapon[] _weapons;
    private InputsController _subscribedInputsController;
    private bool _isSubscribedToInputs;

    public event Action<Weapon> CurrentWeaponChanged;
    public Weapon CurrentWeapon => GetCurrentWeapon();
    public int SelectedWeaponIndex => _selectedIndexWeapon;
    public IReadOnlyList<Weapon> Weapons => _weapons;

    private void Awake()
    {
        ResolveInputsController();
        InitWeapon();
        ActivateSelectedWeapon();
    }

    private void OnEnable()
    {
        ResolveInputsController();

        if (_inputsController == null)
        {
            Debug.LogError($"{nameof(PlayerWeaponController)} on {name} could not find {nameof(InputsController)}.", this);
            enabled = false;
            return;
        }

        SubscribeToInputs();
    }

    private void OnDisable()
    {
        UnsubscribeFromInputs();
    }

    private void SubscribeToInputs()
    {
        if (_inputsController == null)
        {
            return;
        }

        if (_isSubscribedToInputs && _subscribedInputsController == _inputsController)
        {
            return;
        }

        UnsubscribeFromInputs();
        _inputsController.OnPlayerFire += OnFire;
        _inputsController.OnPlayerSwithcWeapon += OnSwitchedWeapon;
        _inputsController.OnPlayerSwitchWeaponDelta += OnSwitchWeaponDelta;
        _inputsController.OnPlayerReload += OnReload;
        _subscribedInputsController = _inputsController;
        _isSubscribedToInputs = true;
    }

    private void UnsubscribeFromInputs()
    {
        if (_isSubscribedToInputs && _subscribedInputsController != null)
        {
            _subscribedInputsController.OnPlayerFire -= OnFire;
            _subscribedInputsController.OnPlayerSwithcWeapon -= OnSwitchedWeapon;
            _subscribedInputsController.OnPlayerSwitchWeaponDelta -= OnSwitchWeaponDelta;
            _subscribedInputsController.OnPlayerReload -= OnReload;
        }

        _subscribedInputsController = null;
        _isSubscribedToInputs = false;
    }

    private void Update()
    {
        Weapon currentWeapon = GetCurrentWeapon();

        if (_inputsController == null || currentWeapon == null || !currentWeapon.Automatic || !_inputsController.fireHeld || !_inputsController.aim)
        {
            return;
        }

        currentWeapon.TryShoot();
    }

    private void ResolveInputsController()
    {
        if (_inputsController != null)
        {
            return;
        }

        _inputsController = GetComponent<InputsController>();

        if (_inputsController == null)
        {
            _inputsController = GetComponentInParent<InputsController>();
        }
    }

    private void InitWeapon()
    {
        CreateRuntimeWeaponsFromDefinitions();

        if (objWeapons == null || objWeapons.Length == 0)
        {
            _weapons = System.Array.Empty<Weapon>();
            return;
        }

        _weapons = new Weapon[objWeapons.Length];

        for (int i = 0; i < objWeapons.Length; i++)
        {
            if (objWeapons[i] == null)
            {
                continue;
            }

            _weapons[i] = objWeapons[i].GetComponent<Weapon>();

            if (_weapons[i] != null && weaponDefinitions != null && i < weaponDefinitions.Length && weaponDefinitions[i] != null)
            {
                _weapons[i].SetDefinition(weaponDefinitions[i]);
            }
        }

    }

    private void CreateRuntimeWeaponsFromDefinitions()
    {
        if (!createWeaponsFromDefinitions || HasAssignedWeapons())
        {
            return;
        }

        if (weaponDefinitions == null || weaponDefinitions.Length == 0)
        {
            return;
        }

        List<GameObject> createdWeapons = new List<GameObject>();
        Transform parent = weaponInstancesParent != null ? weaponInstancesParent : transform;

        for (int i = 0; i < weaponDefinitions.Length; i++)
        {
            WeaponDefinition definition = weaponDefinitions[i];

            if (definition == null || definition.WeaponPrefab == null)
            {
                continue;
            }

            GameObject weaponObject = Instantiate(definition.WeaponPrefab, parent);
            Weapon weapon = weaponObject.GetComponent<Weapon>();

            if (weapon != null)
            {
                weapon.SetDefinition(definition);
            }

            createdWeapons.Add(weaponObject);
        }

        objWeapons = createdWeapons.ToArray();
    }

    private bool HasAssignedWeapons()
    {
        if (objWeapons == null || objWeapons.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < objWeapons.Length; i++)
        {
            if (objWeapons[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void ActivateSelectedWeapon()
    {
        if (objWeapons == null || objWeapons.Length == 0)
        {
            return;
        }

        _selectedIndexWeapon = Mathf.Clamp(_selectedIndexWeapon, 0, objWeapons.Length - 1);

        for (int i = 0; i < objWeapons.Length; i++)
        {
            if (objWeapons[i] != null)
            {
                Weapon weapon = i < _weapons.Length ? _weapons[i] : objWeapons[i].GetComponent<Weapon>();

                if (i != _selectedIndexWeapon && weapon != null)
                {
                    weapon.CancelReload();
                }

                objWeapons[i].SetActive(false);
            }
        }

        if (objWeapons[_selectedIndexWeapon] != null)
        {
            objWeapons[_selectedIndexWeapon].SetActive(true);
        }

        CurrentWeaponChanged?.Invoke(GetCurrentWeapon());
    }

    private void OnFire()
    {
        Weapon currentWeapon = GetCurrentWeapon();

        if (currentWeapon != null && _inputsController != null && _inputsController.aim)
        {
            currentWeapon.TryShoot();
        }
    }

    private void OnReload()
    {
        Weapon currentWeapon = GetCurrentWeapon();

        if (currentWeapon != null)
        {
            currentWeapon.TryReload();
        }
    }

    private void OnSwitchedWeapon()
    { 
        OnSwitchWeaponDelta(1);
    }

    private void OnSwitchWeaponDelta(int direction)
    {
        if (_weapons == null || _weapons.Length == 0 || direction == 0)
        {
            return;
        }

        Weapon currentWeapon = GetCurrentWeapon();

        if (currentWeapon != null)
        {
            currentWeapon.CancelReload();
        }

        int weaponCount = _weapons.Length;
        _selectedIndexWeapon = (_selectedIndexWeapon + direction) % weaponCount;

        if (_selectedIndexWeapon < 0)
        {
            _selectedIndexWeapon += weaponCount;
        }

        ActivateSelectedWeapon();
    }

    public bool SetSelectedWeaponIndex(int index)
    {
        if (_weapons == null || _weapons.Length == 0)
            return false;

        int clampedIndex = Mathf.Clamp(index, 0, _weapons.Length - 1);
        if (_selectedIndexWeapon == clampedIndex)
        {
            ActivateSelectedWeapon();
            return true;
        }

        Weapon currentWeapon = GetCurrentWeapon();
        if (currentWeapon != null)
            currentWeapon.CancelReload();

        _selectedIndexWeapon = clampedIndex;
        ActivateSelectedWeapon();
        return true;
    }

    public Weapon GetCurrentWeapon()
    {
        if (_weapons == null || _weapons.Length == 0)
        {
            return null;
        }

        if (_selectedIndexWeapon < 0 || _selectedIndexWeapon >= _weapons.Length)
        {
            return null;
        }

        return _weapons[_selectedIndexWeapon];
    }

    public bool TryAddReserveAmmo(WeaponDefinition weaponDefinition, string weaponID, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        Weapon targetWeapon = FindWeapon(weaponDefinition, weaponID);

        if (targetWeapon == null && weaponDefinition == null && string.IsNullOrWhiteSpace(weaponID))
        {
            targetWeapon = GetCurrentWeapon();
        }

        return targetWeapon != null && targetWeapon.TryAddReserveAmmo(amount);
    }

    private Weapon FindWeapon(WeaponDefinition weaponDefinition, string weaponID)
    {
        if (_weapons == null || _weapons.Length == 0)
        {
            return null;
        }

        string targetWeaponID = !string.IsNullOrWhiteSpace(weaponID)
            ? weaponID
            : weaponDefinition != null ? weaponDefinition.WeaponID : string.Empty;

        for (int i = 0; i < _weapons.Length; i++)
        {
            Weapon weapon = _weapons[i];

            if (weapon == null)
            {
                continue;
            }

            if (weaponDefinition != null && weapon.Definition == weaponDefinition)
            {
                return weapon;
            }

            if (!string.IsNullOrWhiteSpace(targetWeaponID) &&
                weapon.Definition != null &&
                string.Equals(weapon.Definition.WeaponID, targetWeaponID, StringComparison.OrdinalIgnoreCase))
            {
                return weapon;
            }
        }

        return null;
    }
}
