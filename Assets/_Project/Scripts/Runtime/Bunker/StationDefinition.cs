using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StationDefinition", menuName = "Bunker/Station Definition")]
public class StationDefinition : ScriptableObject
{
    public string stationId = "station";
    public string displayName = "Station";
    [TextArea(2, 5)] public string description;
    public StationType stationType = StationType.Workbench;
    public int requiredBunkerLevel = 1;
    [Min(1)] public int maxLevel = 3;
    public bool isUnlockedByDefault = true;
    public GameObject stationPrefab;
    public GameObject worldPrefab;
    public List<CraftingRecipe> upgradeRecipes = new();
}
