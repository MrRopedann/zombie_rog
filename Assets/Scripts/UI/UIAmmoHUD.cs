using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIAmmoHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Text legacyAmmoText;
    [SerializeField] private PlayerWeaponController weaponController;

    [Header("Auto Find")]
    [SerializeField] private string hudObjectName = "HUD_Ammo";

    [Header("Format")]
    [SerializeField] private string ammoFormat = "{0} / {1}";
    [SerializeField] private string noWeaponText = "-- / --";
    [SerializeField] private string infiniteAmmoText = "INF";
    [SerializeField] private bool hideWhenNoWeapon = false;

    private Weapon _currentWeapon;
    private PlayerWeaponController _subscribedWeaponController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToSceneHud()
    {
        if (FindObjectOfType<UIAmmoHUD>(true) != null)
        {
            return;
        }

        GameObject ammoHud = FindSceneObjectByName("HUD_Ammo")
            ?? FindSceneObjectByName("HUD_ammo");

        if (ammoHud != null)
        {
            ammoHud.AddComponent<UIAmmoHUD>();
        }
    }

    private void Awake()
    {
        ResolveTextReferences();
    }

    private void OnEnable()
    {
        ResolveTextReferences();
        TryBindWeaponController();
        Refresh();
    }

    private void Update()
    {
        if (weaponController == null)
        {
            TryBindWeaponController();
        }
    }

    private void OnDisable()
    {
        UnsubscribeWeaponController();
        SetCurrentWeapon(null);
    }

    private void TryBindWeaponController()
    {
        PlayerWeaponController resolvedController = weaponController;

        if (resolvedController == null)
        {
            resolvedController = FindObjectOfType<PlayerWeaponController>();
        }

        if (resolvedController == null)
        {
            SetCurrentWeapon(null);
            return;
        }

        if (_subscribedWeaponController != resolvedController)
        {
            UnsubscribeWeaponController();
            weaponController = resolvedController;
            _subscribedWeaponController = resolvedController;
            _subscribedWeaponController.CurrentWeaponChanged += SetCurrentWeaponFromEvent;
        }

        SetCurrentWeapon(resolvedController.CurrentWeapon);
    }

    private void UnsubscribeWeaponController()
    {
        if (_subscribedWeaponController != null)
        {
            _subscribedWeaponController.CurrentWeaponChanged -= SetCurrentWeaponFromEvent;
        }

        _subscribedWeaponController = null;
    }

    private void SetCurrentWeaponFromEvent(Weapon weapon)
    {
        SetCurrentWeapon(weapon);
    }

    private void SetCurrentWeapon(Weapon weapon)
    {
        if (_currentWeapon == weapon)
        {
            Refresh();
            return;
        }

        if (_currentWeapon != null)
        {
            _currentWeapon.AmmoChanged -= UpdateAmmo;
        }

        _currentWeapon = weapon;

        if (_currentWeapon != null)
        {
            _currentWeapon.AmmoChanged += UpdateAmmo;
        }

        Refresh();
    }

    private void UpdateAmmo(Weapon weapon, int currentAmmo, int reserveAmmo)
    {
        SetText(FormatAmmo(weapon, currentAmmo, reserveAmmo));
    }

    private void Refresh()
    {
        if (_currentWeapon == null)
        {
            SetText(noWeaponText);
            SetVisible(!hideWhenNoWeapon);
            return;
        }

        SetVisible(true);
        SetText(FormatAmmo(_currentWeapon, _currentWeapon.CurrentAmmo, _currentWeapon.ReserveAmmo));
    }

    private string FormatAmmo(Weapon weapon, int currentAmmo, int reserveAmmo)
    {
        string magazine = weapon.InfiniteAmmo ? infiniteAmmoText : currentAmmo.ToString();
        string reserve = weapon.InfiniteAmmo || weapon.InfiniteReserveAmmo
            ? infiniteAmmoText
            : reserveAmmo.ToString();

        return string.Format(ammoFormat, magazine, reserve);
    }

    private void ResolveTextReferences()
    {
        if (ammoText == null)
        {
            ammoText = GetComponent<TextMeshProUGUI>();
        }

        if (ammoText == null)
        {
            ammoText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (legacyAmmoText == null)
        {
            legacyAmmoText = GetComponent<Text>();
        }

        if (legacyAmmoText == null)
        {
            legacyAmmoText = GetComponentInChildren<Text>(true);
        }

        if (ammoText != null || legacyAmmoText != null || string.IsNullOrEmpty(hudObjectName))
        {
            return;
        }

        GameObject hudObject = FindSceneObjectByName(hudObjectName);

        if (hudObject == null || hudObject == gameObject)
        {
            return;
        }

        ammoText = hudObject.GetComponent<TextMeshProUGUI>();
        legacyAmmoText = hudObject.GetComponent<Text>();

        if (ammoText == null)
        {
            ammoText = hudObject.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (legacyAmmoText == null)
        {
            legacyAmmoText = hudObject.GetComponentInChildren<Text>(true);
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject exactMatch = GameObject.Find(objectName);

        if (exactMatch != null)
        {
            return exactMatch;
        }

        Transform[] sceneTransforms = FindObjectsOfType<Transform>(true);

        for (int i = 0; i < sceneTransforms.Length; i++)
        {
            Transform sceneTransform = sceneTransforms[i];

            if (sceneTransform == null || !sceneTransform.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(
                sceneTransform.name,
                objectName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return sceneTransform.gameObject;
            }
        }

        return null;
    }

    private void SetText(string value)
    {
        if (ammoText != null)
        {
            ammoText.text = value;
        }

        if (legacyAmmoText != null)
        {
            legacyAmmoText.text = value;
        }
    }

    private void SetVisible(bool isVisible)
    {
        if (ammoText != null)
        {
            ammoText.enabled = isVisible;
        }

        if (legacyAmmoText != null)
        {
            legacyAmmoText.enabled = isVisible;
        }
    }
}
