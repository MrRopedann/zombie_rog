public class CraftingResult
{
    public bool success;
    public string message;
    public CraftingRecipe recipe;
    public ItemSO resultItem;
    public int resultAmount;

    public static CraftingResult Success(CraftingRecipe recipe, ItemSO resultItem, int resultAmount, string message)
    {
        return new CraftingResult
        {
            success = true,
            recipe = recipe,
            resultItem = resultItem,
            resultAmount = resultAmount,
            message = message
        };
    }

    public static CraftingResult Failure(CraftingRecipe recipe, string message)
    {
        return new CraftingResult
        {
            success = false,
            recipe = recipe,
            message = message
        };
    }
}
