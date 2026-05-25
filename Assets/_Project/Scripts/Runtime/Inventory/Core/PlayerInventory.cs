using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField] private bool isViewDebug = false;
    [SerializeField, Min(1)] private int maxSlots = 32;
    [SerializeField, Min(0f)] private float maxWeight = 50f;
    [SerializeField] private bool createDefaultInventoryUI = true;
    [SerializeField] private bool createDefaultLootInteractor = true;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private CharacterStats characterStats;

    private readonly List<InventorySlot> slots = new();

    public event Action InventoryChanged;
    public event Action<ItemSO, int> ItemAdded;
    public IReadOnlyList<InventorySlot> Slots => slots;
    public int MaxSlots => maxSlots;
    public float MaxWeight => GetEffectiveMaxWeight();

    private void Awake()
    {
        ResolveWeaponController();
        ResolveCharacterStats();

        if (createDefaultInventoryUI && GetComponent<InventoryUIController>() == null)
        {
            gameObject.AddComponent<InventoryUIController>();
        }

        if (createDefaultLootInteractor && GetComponent<LootInteractor>() == null)
        {
            gameObject.AddComponent<LootInteractor>();
        }
    }

    public InventorySlot GetSlot(int index)
    {
        return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    public void ClearInventory()
    {
        if (slots.Count == 0)
        {
            return;
        }

        slots.Clear();
        NotifyInventoryChanged();
    }

    public void ReplaceContents(IEnumerable<InventorySlot> newSlots)
    {
        slots.Clear();

        if (newSlots != null)
        {
            foreach (InventorySlot slot in newSlots)
            {
                if (slot == null || slot.item == null || slot.amount <= 0)
                {
                    continue;
                }

                AddItem(slot.item, slot.amount);
            }
        }

        NotifyInventoryChanged();
    }

    public int GetItemAmount(ItemSO item)
    {
        if (item == null)
        {
            return 0;
        }

        int amount = 0;

        foreach (InventorySlot slot in slots)
        {
            if (slot.item == item)
            {
                amount += slot.amount;
            }
        }

        return amount;
    }

    public bool HasItem(ItemSO item, int count = 1)
    {
        return item != null && count > 0 && GetItemAmount(item) >= count;
    }

    public bool HasItems(IEnumerable<ItemAmount> items)
    {
        if (items == null)
            return true;

        foreach (ItemAmount itemAmount in items)
        {
            if (itemAmount == null || itemAmount.amount <= 0)
                continue;

            ItemSO item = ResolveItemAmountItem(itemAmount);
            if (item == null || !HasItem(item, itemAmount.amount))
                return false;
        }

        return true;
    }

    public bool ContainsItem(ItemSO item)
    {
        return GetItemAmount(item) > 0;
    }

    public bool CanAddItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        if (GetCurrentWeight() + item.weight * count > GetEffectiveMaxWeight())
        {
            return false;
        }

        int remaining = count;

        if (item.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item != item)
                {
                    continue;
                }

                remaining -= Mathf.Max(0, item.maxStack - slot.amount);

                if (remaining <= 0)
                {
                    return true;
                }
            }
        }

        int freeSlots = Mathf.Max(0, maxSlots - slots.Count);

        if (!item.isStackable)
        {
            return remaining <= freeSlots;
        }

        int stackSize = Mathf.Max(1, item.maxStack);
        int requiredSlots = Mathf.CeilToInt(remaining / (float)stackSize);
        return requiredSlots <= freeSlots;
    }

    public static bool CanUseItem(ItemSO item)
    {
        return item != null && (item.isUsable || item.itemType == ItemType.Ammo || IsCharacterRestoreItem(item));
    }

    public bool AddItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        if (!CanAddItem(item, count))
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Inventory is full. Could not add {count}x {item.itemName}.", this);
            }

            return false;
        }

        int remaining = count;

        if (item.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item != item || slot.amount >= item.maxStack)
                {
                    continue;
                }

                int freeSpace = item.maxStack - slot.amount;
                int addAmount = Mathf.Min(freeSpace, remaining);
                slot.amount += addAmount;
                remaining -= addAmount;

                if (remaining <= 0)
                {
                    NotifyItemAdded(item, count);
                    NotifyInventoryChanged();
                    return true;
                }
            }
        }

        while (remaining > 0 && slots.Count < maxSlots)
        {
            int addAmount = item.isStackable
                ? Mathf.Min(remaining, Mathf.Max(1, item.maxStack))
                : 1;

            slots.Add(new InventorySlot(item, addAmount));
            remaining -= addAmount;
        }

        bool addedAnything = remaining < count;

        if (addedAnything)
        {
            NotifyItemAdded(item, count - remaining);
            NotifyInventoryChanged();
        }

        if (remaining > 0 && isViewDebug)
        {
            Debug.LogWarning($"Inventory is full. Could not add {remaining}x {item.itemName}.", this);
        }
        else if (addedAnything && isViewDebug)
        {
            Debug.Log($"Added {item.itemName} to inventory.", this);
        }

        return remaining <= 0;
    }

    public bool RemoveItems(IEnumerable<ItemAmount> items)
    {
        if (!HasItems(items))
            return false;

        foreach (ItemAmount itemAmount in items)
        {
            if (itemAmount == null || itemAmount.amount <= 0)
                continue;

            ItemSO item = ResolveItemAmountItem(itemAmount);
            if (item != null && !RemoveItem(item, itemAmount.amount))
                return false;
        }

        return true;
    }

    public bool RemoveItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        int remaining = count;

        foreach (InventorySlot slot in slots.ToList())
        {
            if (slot.item != item)
            {
                continue;
            }

            int removeAmount = Mathf.Min(slot.amount, remaining);
            slot.amount -= removeAmount;
            remaining -= removeAmount;

            if (slot.amount <= 0)
            {
                slots.Remove(slot);
            }

            if (remaining <= 0)
            {
                NotifyInventoryChanged();

                if (isViewDebug)
                {
                    Debug.Log($"Removed {item.itemName} from inventory.", this);
                }

                return true;
            }
        }

        if (remaining < count)
        {
            NotifyInventoryChanged();
        }

        if (isViewDebug)
        {
            Debug.LogWarning($"Not enough {item.itemName} in inventory.", this);
        }

        return false;
    }

    public bool TryUseItem(ItemSO item)
    {
        return TryUseItem(item, false);
    }

    public bool TryUseItem(ItemSO item, bool forceConsume)
    {
        if (item == null || !CanUseItem(item) || !ContainsItem(item))
        {
            return false;
        }

        if (item.itemType == ItemType.Ammo)
        {
            return TryUseAmmoItem(item);
        }

        if (IsCharacterRestoreItem(item))
        {
            return TryUseCharacterRestoreItem(item);
        }

        if (!item.TryUse())
        {
            return false;
        }

        if (item.isConsumable || forceConsume)
        {
            RemoveItem(item);
            return true;
        }

        NotifyInventoryChanged();
        return true;
    }

    public ItemSO GetUsableItemOfType(ItemType itemType)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null && slot.item.itemType == itemType && CanUseItem(slot.item))
            {
                return slot.item;
            }
        }

        return null;
    }

    public List<InventorySlot> GetUsableSlotsOfType(ItemType itemType)
    {
        List<InventorySlot> result = new();

        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null && slot.item.itemType == itemType && CanUseItem(slot.item))
            {
                result.Add(slot);
            }
        }

        return result;
    }

    public bool DropItem(ItemSO item, Vector3 position, Quaternion rotation, int count = 1)
    {
        if (item == null || count <= 0 || !ContainsItem(item))
        {
            return false;
        }

        if (CoopGameplaySync.TryDropInventoryItem(this, item, count, position, rotation))
            return true;

        Vector3 velocity = CalculateDropVelocity(position);
        Vector3 angularVelocity = UnityEngine.Random.insideUnitSphere * 8f;
        WorldItem droppedItem = WorldItem.Spawn(item, position, rotation, velocity, angularVelocity);
        if (droppedItem == null)
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Could not drop item {item.itemName}.", this);
            }

            return false;
        }

        if (!RemoveItem(item, count))
        {
            Destroy(droppedItem.gameObject);
            return false;
        }

        CoopGameplaySync.NotifyWorldItemDropped(droppedItem, item, count);
        return true;
    }

    private Vector3 CalculateDropVelocity(Vector3 dropPosition)
    {
        Vector3 forward = transform != null ? transform.forward : Vector3.forward;
        Vector3 fromPocket = dropPosition - (transform != null ? transform.position : dropPosition);
        if (fromPocket.sqrMagnitude > 0.001f)
            forward = Vector3.Lerp(forward, fromPocket.normalized, 0.35f).normalized;

        return forward * 4.5f + Vector3.up * 2.2f + UnityEngine.Random.insideUnitSphere * 0.45f;
    }

    public float GetCurrentWeight()
    {
        float totalWeight = 0f;

        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null)
            {
                totalWeight += slot.item.weight * slot.amount;
            }
        }

        return totalWeight;
    }

    public InventorySaveData GetSaveData()
    {
        InventorySaveData data = new InventorySaveData();

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
                continue;

            data.slots.Add(new InventorySlotSaveData
            {
                itemId = !string.IsNullOrWhiteSpace(slot.item.itemID) ? slot.item.itemID : slot.item.name,
                amount = slot.amount
            });
        }

        return data;
    }

    public void LoadFromSaveData(InventorySaveData data, ItemDatabase itemDatabase = null)
    {
        slots.Clear();

        if (data != null && data.slots != null)
        {
            for (int i = 0; i < data.slots.Count; i++)
            {
                InventorySlotSaveData slot = data.slots[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.itemId) || slot.amount <= 0)
                    continue;

                ItemSO item = itemDatabase != null ? itemDatabase.Resolve(slot.itemId) : null;
                if (item == null)
                    item = ItemDatabase.ResolveFromResources(slot.itemId);

                if (item != null)
                    AddItem(item, slot.amount);
            }
        }

        NotifyInventoryChanged();
    }

    public void SortSlots(InventorySortMode sortMode)
    {
        slots.Sort((left, right) => CompareSlots(left, right, sortMode));
        NotifyInventoryChanged();
    }

    public void ShowInventoryDebug()
    {
        if (!isViewDebug)
        {
            return;
        }

        Debug.Log("Inventory content:", this);

        if (slots.Count == 0)
        {
            Debug.Log("Inventory is empty.", this);
            return;
        }

        foreach (InventorySlot slot in slots)
        {
            if (slot.item == null)
            {
                continue;
            }

            Debug.Log(
                $"- {slot.item.itemName} x{slot.amount} " +
                $"(ID: {slot.item.itemID}) " +
                $"(Type: {slot.item.itemType}) " +
                $"(Weight: {slot.item.weight * slot.amount})",
                this);
        }

        Debug.Log($"Total weight: {GetCurrentWeight()}", this);
    }

    private void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }

    private void NotifyItemAdded(ItemSO item, int amount)
    {
        if (item != null && amount > 0)
            ItemAdded?.Invoke(item, amount);
    }

    private static ItemSO ResolveItemAmountItem(ItemAmount itemAmount)
    {
        if (itemAmount == null)
            return null;

        if (itemAmount.item != null)
            return itemAmount.item;

        return ItemDatabase.ResolveFromResources(itemAmount.itemId);
    }

    private float GetEffectiveMaxWeight()
    {
        ResolveCharacterStats();

        if (characterStats != null && characterStats.MaxWeight > 0f)
            return Mathf.Min(maxWeight, characterStats.MaxWeight);

        return maxWeight;
    }

    private static int CompareSlots(InventorySlot left, InventorySlot right, InventorySortMode sortMode)
    {
        bool leftEmpty = left == null || left.IsEmpty();
        bool rightEmpty = right == null || right.IsEmpty();

        if (leftEmpty && rightEmpty)
        {
            return 0;
        }

        if (leftEmpty)
        {
            return 1;
        }

        if (rightEmpty)
        {
            return -1;
        }

        return sortMode switch
        {
            InventorySortMode.Amount => right.amount.CompareTo(left.amount),
            InventorySortMode.Type => CompareByTypeThenName(left.item, right.item),
            _ => string.Compare(GetItemName(left.item), GetItemName(right.item), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static int CompareByTypeThenName(ItemSO left, ItemSO right)
    {
        int typeCompare = left.itemType.CompareTo(right.itemType);
        return typeCompare != 0
            ? typeCompare
            : string.Compare(GetItemName(left), GetItemName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetItemName(ItemSO item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName
            : string.Empty;
    }

    private bool TryUseAmmoItem(ItemSO item)
    {
        ResolveWeaponController();

        if (weaponController == null)
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Could not use ammo item {item.itemName}: no PlayerWeaponController found.", this);
            }

            return false;
        }

        bool addedAmmo = weaponController.TryAddReserveAmmo(
            item.ammoWeaponDefinition,
            item.ammoWeaponID,
            item.ammoAmount);

        if (!addedAmmo)
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Could not add ammo from {item.itemName}. Check target weapon and ammo settings.", this);
            }

            return false;
        }

        RemoveItem(item);
        return true;
    }

    private bool TryUseCharacterRestoreItem(ItemSO item)
    {
        ResolveCharacterStats();

        if (characterStats == null || characterStats.IsDead)
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Could not use {item.itemName}: no living CharacterStats found.", this);
            }

            return false;
        }

        float amount = GetCharacterRestoreAmount(item);

        if (amount <= 0f)
        {
            if (isViewDebug)
            {
                Debug.LogWarning($"Could not use {item.itemName}: restore amount is zero.", this);
            }

            return false;
        }

        switch (item.itemType)
        {
            case ItemType.Drink:
                characterStats.ChangeThirst(amount);
                break;
            case ItemType.Food:
                characterStats.ChangeHunger(amount);
                break;
            case ItemType.Healing:
                characterStats.ChangeHealth(amount);
                break;
            default:
                return false;
        }

        RemoveItem(item);
        return true;
    }

    private static bool IsCharacterRestoreItem(ItemSO item)
    {
        return item != null &&
            (item.itemType == ItemType.Drink ||
             item.itemType == ItemType.Food ||
             item.itemType == ItemType.Healing);
    }

    private static float GetCharacterRestoreAmount(ItemSO item)
    {
        return item.itemType switch
        {
            ItemType.Drink => item.thirstRestoreAmount,
            ItemType.Food => item.hungerRestoreAmount,
            ItemType.Healing => item.healthRestoreAmount,
            _ => 0f
        };
    }

    private void ResolveWeaponController()
    {
        if (weaponController != null)
        {
            return;
        }

        weaponController = GetComponent<PlayerWeaponController>();

        if (weaponController == null)
        {
            weaponController = GetComponentInParent<PlayerWeaponController>();
        }

        if (weaponController == null)
        {
            weaponController = GetComponentInChildren<PlayerWeaponController>(true);
        }
    }

    private void ResolveCharacterStats()
    {
        if (characterStats != null)
        {
            return;
        }

        characterStats = GetComponent<CharacterStats>();

        if (characterStats == null)
        {
            characterStats = GetComponentInParent<CharacterStats>();
        }

        if (characterStats == null)
        {
            characterStats = GetComponentInChildren<CharacterStats>(true);
        }
    }
}
