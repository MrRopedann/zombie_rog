using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CraftingRecipeDatabase", menuName = "Crafting/Recipe Database")]
public class CraftingRecipeDatabase : ScriptableObject
{
    [SerializeField] private List<CraftingRecipe> recipes = new();

    public IReadOnlyList<CraftingRecipe> Recipes => recipes;

    public List<CraftingRecipe> GetUnlockedRecipes(CraftingStationType stationType, int stationLevel)
    {
        List<CraftingRecipe> result = new();
        int level = Mathf.Max(1, stationLevel);

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipe recipe = recipes[i];
            if (recipe == null || !recipe.isUnlockedByDefault)
                continue;

            if (recipe.requiredStationType != CraftingStationType.Any &&
                stationType != CraftingStationType.Any &&
                recipe.requiredStationType != stationType)
                continue;

            if (recipe.requiredStationLevel > level)
                continue;

            result.Add(recipe);
        }

        return result;
    }
}
