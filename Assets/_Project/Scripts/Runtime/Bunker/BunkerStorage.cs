using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Bunker Storage")]
public class BunkerStorage : MonoBehaviour
{
    [SerializeField] private LootContainer storageContainer;
    [SerializeField] private ItemDatabase itemDatabase;

    public LootContainer StorageContainer
    {
        get
        {
            ResolveContainer();
            return storageContainer;
        }
    }

    public InventorySaveData GetSaveData()
    {
        InventorySaveData data = new InventorySaveData();
        LootContainer container = StorageContainer;
        IReadOnlyList<InventorySlot> slots = container != null ? container.Slots : null;

        if (slots == null)
            return data;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
                continue;

            data.slots.Add(new InventorySlotSaveData
            {
                itemId = GetItemId(slot.item),
                amount = slot.amount
            });
        }

        return data;
    }

    public int GetItemAmount(ItemSO item)
    {
        if (item == null)
            return 0;

        LootContainer container = StorageContainer;
        IReadOnlyList<InventorySlot> slots = container != null ? container.Slots : null;
        if (slots == null)
            return 0;

        int amount = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot != null && slot.item == item)
                amount += slot.amount;
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

    public bool CanAddItem(ItemSO item, int amount = 1)
    {
        LootContainer container = StorageContainer;
        return container != null && container.CanAddItem(item, amount);
    }

    public bool AddItem(ItemSO item, int amount = 1)
    {
        LootContainer container = StorageContainer;
        return container != null && container.AddItem(item, amount);
    }

    public bool RemoveItem(ItemSO item, int amount = 1)
    {
        LootContainer container = StorageContainer;
        return container != null && container.RemoveItem(item, amount);
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

    public void LoadFromSaveData(InventorySaveData data)
    {
        LootContainer container = StorageContainer;
        if (container == null || data == null)
            return;

        container.ClearNetworkState(data.slots != null && data.slots.Count > 0);

        if (data.slots == null)
            return;

        for (int i = 0; i < data.slots.Count; i++)
        {
            InventorySlotSaveData slot = data.slots[i];
            if (slot == null || string.IsNullOrWhiteSpace(slot.itemId) || slot.amount <= 0)
                continue;

            ItemSO item = ResolveItem(slot.itemId);
            if (item != null)
                container.AddNetworkItem(item, slot.amount);
        }
    }

    private void ResolveContainer()
    {
        if (storageContainer != null)
            return;

        storageContainer = GetComponent<LootContainer>() ?? GetComponentInChildren<LootContainer>(true);
    }

    private ItemSO ResolveItem(string itemId)
    {
        if (itemDatabase != null)
        {
            ItemSO item = itemDatabase.Resolve(itemId);
            if (item != null)
                return item;
        }

        return ItemDatabase.ResolveFromResources(itemId);
    }

    private ItemSO ResolveItemAmountItem(ItemAmount itemAmount)
    {
        if (itemAmount == null)
            return null;

        if (itemAmount.item != null)
            return itemAmount.item;

        return ResolveItem(itemAmount.itemId);
    }

    private static string GetItemId(ItemSO item)
    {
        if (item == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(item.itemID) ? item.itemID.Trim() : item.name;
    }
}
