using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Station Upgrade System")]
public class StationUpgradeSystem : MonoBehaviour
{
    [SerializeField] private CraftingSystem craftingSystem;
    [SerializeField] private bool saveAfterUpgrade = true;

    private void Awake()
    {
        ResolveReferences();
    }

    public CraftingResult Upgrade(BuildableStation station)
    {
        ResolveReferences();

        if (station == null)
            return CraftingResult.Failure(null, "Station is missing.");

        if (station.CurrentLevel >= station.MaxLevel)
            return CraftingResult.Failure(null, $"{station.DisplayName} is already at max level.");

        CraftingRecipe recipe = GetNextUpgradeRecipe(station);
        if (recipe != null)
        {
            if (craftingSystem == null)
                return CraftingResult.Failure(recipe, "Crafting system is missing.");

            if (!craftingSystem.HasIngredients(recipe, out string reason))
                return CraftingResult.Failure(recipe, reason);

            if (!craftingSystem.ConsumeIngredients(recipe, out reason))
                return CraftingResult.Failure(recipe, reason);
        }

        if (!station.TryUpgrade())
            return CraftingResult.Failure(recipe, $"{station.DisplayName} cannot be upgraded.");

        if (saveAfterUpgrade && GameSaveManager.CanSaveInCurrentContext)
            GameSaveManager.SaveCurrentGame();

        return CraftingResult.Success(recipe, null, 0, $"{station.DisplayName} upgraded to level {station.CurrentLevel}.");
    }

    public CraftingRecipe GetNextUpgradeRecipe(BuildableStation station)
    {
        if (station == null || station.UpgradeRecipes == null)
            return null;

        int recipeIndex = Mathf.Max(0, station.CurrentLevel - 1);
        return recipeIndex < station.UpgradeRecipes.Count ? station.UpgradeRecipes[recipeIndex] : null;
    }

    private void ResolveReferences()
    {
        if (craftingSystem == null)
            craftingSystem = FindObjectOfType<CraftingSystem>(true);
    }
}
