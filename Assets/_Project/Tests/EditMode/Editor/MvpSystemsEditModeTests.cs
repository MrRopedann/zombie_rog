using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MvpSystemsEditModeTests
{
    private readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < createdObjects.Count; i++)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void RewardCalculator_CalculatesExperience()
    {
        RewardCalculator calculator = CreateGameObject("Reward").AddComponent<RewardCalculator>();
        LocationDefinition location = CreateScriptable<LocationDefinition>();
        location.difficulty = 1;
        location.baseExperienceReward = 100;

        RaidStatsSnapshot stats = new RaidStatsSnapshot
        {
            kills = 3,
            requiredObjectivesCompleted = 1,
            extractionSuccess = true
        };

        int experience = calculator.CalculateExperience(location, stats);

        Assert.Greater(experience, 0);
    }

    [Test]
    public void CraftingSystem_FailsWhenResourcesAreMissing()
    {
        CraftingSystem system = CreateGameObject("Crafting").AddComponent<CraftingSystem>();
        ItemSO ingredient = CreateItem("wood", ItemType.Material);
        ItemSO resultItem = CreateItem("bandage", ItemType.Healing);
        CraftingRecipe recipe = CreateRecipe(ingredient, 1, resultItem);

        CraftingResult result = system.Craft(recipe);

        Assert.IsFalse(result.success);
    }

    [Test]
    public void CraftingSystem_RemovesIngredientsAndAddsResult()
    {
        PlayerInventory inventory = CreateGameObject("Player").AddComponent<PlayerInventory>();
        CraftingSystem system = CreateGameObject("Crafting").AddComponent<CraftingSystem>();
        ItemSO ingredient = CreateItem("wood", ItemType.Material);
        ItemSO resultItem = CreateItem("bandage", ItemType.Healing);
        CraftingRecipe recipe = CreateRecipe(ingredient, 2, resultItem);

        Assert.IsTrue(inventory.AddItem(ingredient, 2));

        CraftingResult result = system.Craft(recipe);

        Assert.IsTrue(result.success);
        Assert.AreEqual(0, inventory.GetItemAmount(ingredient));
        Assert.AreEqual(1, inventory.GetItemAmount(resultItem));
    }

    [Test]
    public void CharacterProgression_AddExperience_LevelsUp()
    {
        GameObject player = CreateGameObject("Player");
        CharacterStats stats = player.AddComponent<CharacterStats>();
        CharacterProgression progression = player.AddComponent<CharacterProgression>();
        stats.playerLevel = 1;
        stats.currentExp = 0;
        stats.expToNextLevel = 10;

        progression.AddExperience(15);

        Assert.AreEqual(2, stats.playerLevel);
        Assert.AreEqual(1, progression.availableStatPoints);
    }

    [Test]
    public void InventorySaveLoad_PreservesItemIdAndAmount()
    {
        ItemSO item = CreateItem("test_item", ItemType.Material);
        PlayerInventory source = CreateGameObject("SourceInventory").AddComponent<PlayerInventory>();
        PlayerInventory target = CreateGameObject("TargetInventory").AddComponent<PlayerInventory>();
        ItemDatabase database = CreateScriptable<ItemDatabase>();
        SetDatabaseItems(database, item);

        Assert.IsTrue(source.AddItem(item, 3));
        InventorySaveData saveData = source.GetSaveData();
        target.LoadFromSaveData(saveData, database);

        Assert.AreEqual("test_item", saveData.slots[0].itemId);
        Assert.AreEqual(3, saveData.slots[0].amount);
        Assert.AreEqual(3, target.GetItemAmount(item));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateScriptable<T>() where T : ScriptableObject
    {
        T value = ScriptableObject.CreateInstance<T>();
        createdObjects.Add(value);
        return value;
    }

    private ItemSO CreateItem(string itemId, ItemType itemType)
    {
        ItemSO item = CreateScriptable<ItemSO>();
        item.itemID = itemId;
        item.itemName = itemId;
        item.itemType = itemType;
        item.isStackable = true;
        item.maxStack = 99;
        item.weight = 0f;
        return item;
    }

    private CraftingRecipe CreateRecipe(ItemSO ingredient, int ingredientAmount, ItemSO resultItem)
    {
        CraftingRecipe recipe = CreateScriptable<CraftingRecipe>();
        recipe.recipeId = "test_recipe";
        recipe.displayName = "Test Recipe";
        recipe.requiredStationType = CraftingStationType.Any;
        recipe.ingredients = new List<ItemAmount>
        {
            new ItemAmount { item = ingredient, amount = ingredientAmount }
        };
        recipe.resultItem = resultItem;
        recipe.resultAmount = 1;
        return recipe;
    }

    private static void SetDatabaseItems(ItemDatabase database, ItemSO item)
    {
        FieldInfo field = typeof(ItemDatabase).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(database, new List<ItemSO> { item });
    }
}
