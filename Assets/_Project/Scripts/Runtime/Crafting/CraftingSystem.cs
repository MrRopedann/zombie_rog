using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Crafting/Crafting System")]
public class CraftingSystem : MonoBehaviour
{
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private BunkerStorage bunkerStorage;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private CraftingRecipeDatabase recipeDatabase;
    [SerializeField] private bool addResultToPlayerInventory = true;

    private readonly List<CraftingRecipe> runtimeLoadedRecipes = new();

    public CraftingRecipeDatabase RecipeDatabase => recipeDatabase;

    private void Awake()
    {
        ResolveReferences();
    }

    public List<CraftingRecipe> GetAvailableRecipes(CraftingStationType stationType, int stationLevel)
    {
        if (recipeDatabase != null)
            return recipeDatabase.GetUnlockedRecipes(stationType, stationLevel);

        LoadRuntimeRecipesIfNeeded();

        List<CraftingRecipe> result = new();
        int level = Mathf.Max(1, stationLevel);
        for (int i = 0; i < runtimeLoadedRecipes.Count; i++)
        {
            CraftingRecipe recipe = runtimeLoadedRecipes[i];
            if (recipe == null || !recipe.isUnlockedByDefault)
                continue;

            if (recipe.requiredStationType != CraftingStationType.Any &&
                stationType != CraftingStationType.Any &&
                recipe.requiredStationType != stationType)
                continue;

            if (recipe.requiredStationLevel <= level)
                result.Add(recipe);
        }

        return result;
    }

    public CraftingResult Craft(CraftingRecipe recipe, CraftingStation station = null)
    {
        ResolveReferences();

        if (!CanCraft(recipe, station, out string reason))
        {
            Debug.LogWarning(reason, this);
            return CraftingResult.Failure(recipe, reason);
        }

        if (!ConsumeIngredients(recipe, out reason))
        {
            Debug.LogWarning(reason, this);
            return CraftingResult.Failure(recipe, reason);
        }

        if (!TryAddResult(recipe.resultItem, recipe.resultAmount))
        {
            reason = $"Cannot add crafting result '{GetItemName(recipe.resultItem)}'. Inventory and bunker storage are full.";
            Debug.LogWarning(reason, this);
            return CraftingResult.Failure(recipe, reason);
        }

        string message = $"Crafted {recipe.resultAmount}x {GetItemName(recipe.resultItem)}.";
        Debug.Log(message, this);
        return CraftingResult.Success(recipe, recipe.resultItem, recipe.resultAmount, message);
    }

    public bool CanCraft(CraftingRecipe recipe, CraftingStation station, out string reason)
    {
        reason = string.Empty;

        if (recipe == null)
        {
            reason = "Cannot craft: recipe is missing.";
            return false;
        }

        if (recipe.resultItem == null || recipe.resultAmount <= 0)
        {
            reason = $"Cannot craft '{recipe.DisplayNameOrId}': result item is not configured.";
            return false;
        }

        if (!MeetsStationRequirement(recipe, station, out reason))
            return false;

        if (!HasIngredients(recipe, out reason))
            return false;

        if (!CanAddResult(recipe.resultItem, recipe.resultAmount))
        {
            reason = $"Cannot craft '{recipe.DisplayNameOrId}': no room for result.";
            return false;
        }

        return true;
    }

    public bool HasIngredients(CraftingRecipe recipe, out string reason)
    {
        reason = string.Empty;

        Dictionary<ItemSO, int> requirements = BuildRequirements(recipe, out reason);
        if (requirements == null)
            return false;

        foreach (KeyValuePair<ItemSO, int> requirement in requirements)
        {
            int available = GetAvailableAmount(requirement.Key);
            if (available < requirement.Value)
            {
                reason = $"Cannot craft '{recipe.DisplayNameOrId}': missing {requirement.Value - available}x {GetItemName(requirement.Key)}.";
                return false;
            }
        }

        return true;
    }

    public int GetAvailableAmount(ItemAmount itemAmount)
    {
        ItemSO item = ResolveItem(itemAmount);
        return item != null ? GetAvailableAmount(item) : 0;
    }

    public bool ConsumeIngredients(CraftingRecipe recipe, out string reason)
    {
        reason = string.Empty;

        if (!HasIngredients(recipe, out reason))
            return false;

        Dictionary<ItemSO, int> requirements = BuildRequirements(recipe, out reason);
        if (requirements == null)
            return false;

        foreach (KeyValuePair<ItemSO, int> requirement in requirements)
        {
            ItemSO item = requirement.Key;
            int remaining = requirement.Value;

            if (playerInventory != null)
            {
                int fromPlayer = Mathf.Min(playerInventory.GetItemAmount(item), remaining);
                if (fromPlayer > 0 && !playerInventory.RemoveItem(item, fromPlayer))
                {
                    reason = $"Cannot remove {fromPlayer}x {GetItemName(item)} from player inventory.";
                    return false;
                }

                remaining -= fromPlayer;
            }

            if (remaining > 0 && bunkerStorage != null)
            {
                int fromStorage = Mathf.Min(bunkerStorage.GetItemAmount(item), remaining);
                if (fromStorage > 0 && !bunkerStorage.RemoveItem(item, fromStorage))
                {
                    reason = $"Cannot remove {fromStorage}x {GetItemName(item)} from bunker storage.";
                    return false;
                }

                remaining -= fromStorage;
            }

            if (remaining > 0)
            {
                reason = $"Cannot remove enough {GetItemName(item)}.";
                return false;
            }
        }

        return true;
    }

    private bool MeetsStationRequirement(CraftingRecipe recipe, CraftingStation station, out string reason)
    {
        reason = string.Empty;

        if (recipe.requiredStationType == CraftingStationType.Any)
            return true;

        if (station == null)
        {
            reason = $"Cannot craft '{recipe.DisplayNameOrId}': requires {recipe.requiredStationType}.";
            return false;
        }

        if (station.StationType != recipe.requiredStationType)
        {
            reason = $"Cannot craft '{recipe.DisplayNameOrId}': wrong station type.";
            return false;
        }

        if (station.CurrentLevel < recipe.requiredStationLevel)
        {
            reason = $"Cannot craft '{recipe.DisplayNameOrId}': station level {recipe.requiredStationLevel} required.";
            return false;
        }

        return true;
    }

    private Dictionary<ItemSO, int> BuildRequirements(CraftingRecipe recipe, out string reason)
    {
        reason = string.Empty;
        Dictionary<ItemSO, int> requirements = new();

        if (recipe == null)
        {
            reason = "Cannot craft: recipe is missing.";
            return null;
        }

        if (recipe.ingredients == null)
            return requirements;

        for (int i = 0; i < recipe.ingredients.Count; i++)
        {
            ItemAmount ingredient = recipe.ingredients[i];
            if (ingredient == null || ingredient.amount <= 0)
                continue;

            ItemSO item = ResolveItem(ingredient);
            if (item == null)
            {
                reason = $"Cannot craft '{recipe.DisplayNameOrId}': ingredient '{ingredient.DisplayId}' was not found.";
                return null;
            }

            if (!requirements.ContainsKey(item))
                requirements[item] = 0;

            requirements[item] += ingredient.amount;
        }

        return requirements;
    }

    private ItemSO ResolveItem(ItemAmount itemAmount)
    {
        if (itemAmount == null)
            return null;

        if (itemAmount.item != null)
            return itemAmount.item;

        if (itemDatabase != null)
        {
            ItemSO item = itemDatabase.Resolve(itemAmount.itemId);
            if (item != null)
                return item;
        }

        return ItemDatabase.ResolveFromResources(itemAmount.itemId);
    }

    private int GetAvailableAmount(ItemSO item)
    {
        int amount = 0;

        if (playerInventory != null)
            amount += playerInventory.GetItemAmount(item);

        if (bunkerStorage != null)
            amount += bunkerStorage.GetItemAmount(item);

        return amount;
    }

    private bool CanAddResult(ItemSO item, int amount)
    {
        if (item == null || amount <= 0)
            return false;

        if (addResultToPlayerInventory && playerInventory != null && playerInventory.CanAddItem(item, amount))
            return true;

        return bunkerStorage != null && bunkerStorage.CanAddItem(item, amount);
    }

    private bool TryAddResult(ItemSO item, int amount)
    {
        if (addResultToPlayerInventory && playerInventory != null && playerInventory.CanAddItem(item, amount))
            return playerInventory.AddItem(item, amount);

        if (bunkerStorage != null && bunkerStorage.CanAddItem(item, amount))
            return bunkerStorage.AddItem(item, amount);

        return false;
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
            playerInventory = FindObjectOfType<PlayerInventory>(true);

        if (bunkerStorage == null)
            bunkerStorage = BunkerManager.Instance != null
                ? BunkerManager.Instance.GetComponentInChildren<BunkerStorage>(true)
                : FindObjectOfType<BunkerStorage>(true);
    }

    private void LoadRuntimeRecipesIfNeeded()
    {
        if (runtimeLoadedRecipes.Count > 0)
            return;

        CraftingRecipe[] recipes = Resources.LoadAll<CraftingRecipe>("RuntimeLoadedOnly/Data/Crafting");
        for (int i = 0; i < recipes.Length; i++)
        {
            if (recipes[i] != null && !runtimeLoadedRecipes.Contains(recipes[i]))
                runtimeLoadedRecipes.Add(recipes[i]);
        }
    }

    private static string GetItemName(ItemSO item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName
            : item != null
                ? item.name
                : "item";
    }
}
