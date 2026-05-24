using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemSO : ScriptableObject
{
    [Header("Main Settings")]
    public string itemID;
    public string itemName = "New Item";
    public float weight = 1f;

    [TextArea(3, 5)]
    public string description = "Описание предмета";

    [Header("Visual Settings")]
    public Sprite icon;
    public GameObject worldPrefab;
    public Vector3 inventoryRotation = Vector3.zero;
    public Vector3 worldVisualOffset = Vector3.zero;
    public Vector3 worldVisualRotation = Vector3.zero;
    public Vector3 worldVisualScale = Vector3.one;

    [Header("Attribute Settings")]
    public ItemType itemType = ItemType.Generic;
    public Rarity rarity = Rarity.Common;
    public int value = 0;

    [Header("Pickup Settings")]
    [Min(0.1f)] public float pickupRadius = 2f;
    public bool createRuntimeHint = true;
    public string pickupHintText = "E";

    [Header("Stack Setting")]
    [SerializeField] public bool isStackable = true;
    [SerializeField] public int maxStack = 99;

    [Header("Using Settings")]
    public bool isUsable = false;
    public bool isConsumable = false;

    [Header("Ammo Settings")]
    public WeaponDefinition ammoWeaponDefinition;
    public string ammoWeaponID;
    [Min(1)] public int ammoAmount = 10;

    [Header("Character Restore Settings")]
    [Min(0f)] public float thirstRestoreAmount = 25f;
    [Min(0f)] public float hungerRestoreAmount = 25f;
    [Min(0f)] public float healthRestoreAmount = 25f;

    public virtual bool TryUse()
    {
        if (!isUsable)
        {
            Debug.Log($"Данный предмет не для использования: {itemName}");
            return false;
        }

        Debug.Log($"Используется предмет: {itemName}");
        return true;
    }

    public virtual bool TryDrop(Vector3 position, Quaternion rotation)
    {
        return SpawnWorldItem(position, rotation) != null;
    }

    public WorldItem SpawnWorldItem(Vector3 position, Quaternion rotation)
    {
        return WorldItem.Spawn(this, position, rotation);
    }

    private void OnValidate()
    {
        maxStack = Mathf.Max(1, maxStack);
        ammoAmount = Mathf.Max(1, ammoAmount);
        thirstRestoreAmount = Mathf.Max(0f, thirstRestoreAmount);
        hungerRestoreAmount = Mathf.Max(0f, hungerRestoreAmount);
        healthRestoreAmount = Mathf.Max(0f, healthRestoreAmount);
        pickupRadius = Mathf.Max(0.1f, pickupRadius);

        if (worldVisualScale == Vector3.zero)
        {
            worldVisualScale = Vector3.one;
        }

        if (itemType == ItemType.Ammo ||
            itemType == ItemType.Drink ||
            itemType == ItemType.Food ||
            itemType == ItemType.Healing)
        {
            isUsable = true;
            isConsumable = true;
        }
    }
}

public enum ItemType
{
    Generic,
    Weapon,
    Consumable,
    Material,
    Quest,
    Key,
    Armor,
    Thing,
    Ammo,
    Drink,
    Food,
    Healing
}

public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}
