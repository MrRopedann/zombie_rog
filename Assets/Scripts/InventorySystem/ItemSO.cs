using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemSO : ScriptableObject
{
    [Header("Main Settings")]
    public string itemID;
    public string itemName = "New Item";
    public float weight = 1;

    [TextArea(3, 5)]
    public string description = "Описание предмета";
    

    [Header("Visual Settings")]
    public Sprite icon;
    public GameObject worldPrefab;
    public Vector3 inventoryRotation = Vector3.zero;

    [Header("Attribute Settings")]
    public ItemType itemType = ItemType.Generic;
    public Rarity rarity = Rarity.Common;
    public int value = 0;

    [Header("Stack Setting")]
    [SerializeField]
    public bool isStackable = true;
    [SerializeField]
    public int maxStack = 99;
    

    [Header("Using Settings")]
    public bool isUsable = false;
    public bool isConsumable = false;

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
        if (worldPrefab != null)
        {
            Instantiate(worldPrefab, position, rotation);
            return true;
        }

        Debug.Log($"У данного предмета нет префаба, выбрасить не возможно [{itemName}]");
        return false;
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
    Thing
}

public enum Rarity
{ 
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}