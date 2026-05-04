using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class PlayerWeaponController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject[] objWeapons;

    [SerializeField]  private InputsController _inputsController;
    private int _selectedIndexWeapon;
    private Weapon[] _weapons;

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

        _inputsController.OnPlayerFire += OnFire;
        _inputsController.OnPlayerSwithcWeapon += OnSwitchedWeapon;
    }

    private void OnDisable()
    {
        if (_inputsController == null)
        {
            return;
        }

        _inputsController.OnPlayerFire -= OnFire;
        _inputsController.OnPlayerSwithcWeapon -= OnSwitchedWeapon;
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
        }

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
                objWeapons[i].SetActive(false);
            }
        }

        if (objWeapons[_selectedIndexWeapon] != null)
        {
            objWeapons[_selectedIndexWeapon].SetActive(true);
        }
    }

    private void OnFire()
    {
        if (_weapons == null || _weapons.Length == 0)
        {
            return;
        }

        Weapon currentWeapon = _weapons[_selectedIndexWeapon];

        if (currentWeapon != null)
        {
            currentWeapon.TryShoot();
        }
    }

    private void OnSwitchedWeapon()
    { 
        if (_weapons == null || _weapons.Length == 0)
        {
            return;
        }

        _selectedIndexWeapon = _selectedIndexWeapon < _weapons.Length - 1 ? _selectedIndexWeapon + 1 : 0;
        ActivateSelectedWeapon();
    }
}
