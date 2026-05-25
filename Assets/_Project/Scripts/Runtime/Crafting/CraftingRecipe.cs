using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemAmount
{
    public ItemSO item;
    public string itemId;
    [Min(1)] public int amount = 1;

    public string DisplayId => item != null
        ? !string.IsNullOrWhiteSpace(item.itemID) ? item.itemID : item.name
        : itemId;
}

[CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public string recipeId = "recipe";
    public string displayName = "Recipe";
    [TextArea(2, 5)] public string description;
    public Sprite icon;
    public CraftingStationType requiredStationType = CraftingStationType.Workbench;
    [Min(1)] public int requiredStationLevel = 1;
    public List<ItemAmount> ingredients = new();
    public ItemSO resultItem;
    [Min(1)] public int resultAmount = 1;
    [Min(0f)] public float craftTime;
    public bool isUnlockedByDefault = true;

    public string DisplayNameOrId => !string.IsNullOrWhiteSpace(displayName) ? displayName : recipeId;

    private void OnValidate()
    {
        requiredStationLevel = Mathf.Max(1, requiredStationLevel);
        resultAmount = Mathf.Max(1, resultAmount);
        craftTime = Mathf.Max(0f, craftTime);

        for (int i = 0; i < ingredients.Count; i++)
        {
            if (ingredients[i] != null)
                ingredients[i].amount = Mathf.Max(1, ingredients[i].amount);
        }
    }
}
