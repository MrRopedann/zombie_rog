using System.Collections.Generic;
using UnityEngine;

public class LootObjective : ObjectiveBase
{
    private readonly HashSet<PlayerInventory> observedInventories = new();

    public LootObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterLoot(int amount)
    {
        AddProgress(amount);
    }

    protected override void Subscribe()
    {
        SubscribeInventories();
    }

    protected override void Unsubscribe()
    {
        foreach (PlayerInventory inventory in observedInventories)
        {
            if (inventory != null)
                inventory.ItemAdded -= HandleItemAdded;
        }

        observedInventories.Clear();
    }

    private void SubscribeInventories()
    {
        PlayerInventory[] inventories = Object.FindObjectsOfType<PlayerInventory>(true);
        for (int i = 0; i < inventories.Length; i++)
        {
            PlayerInventory inventory = inventories[i];
            if (inventory != null && observedInventories.Add(inventory))
                inventory.ItemAdded += HandleItemAdded;
        }
    }

    private void HandleItemAdded(ItemSO item, int amount)
    {
        if (item == null || amount <= 0 || !MatchesRequiredItem(item))
            return;

        AddProgress(amount);
    }

    private bool MatchesRequiredItem(ItemSO item)
    {
        if (mission == null || string.IsNullOrWhiteSpace(mission.requiredItemId))
            return true;

        string required = mission.requiredItemId.Trim();
        return string.Equals(item.itemID, required, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.name, required, System.StringComparison.OrdinalIgnoreCase);
    }
}
