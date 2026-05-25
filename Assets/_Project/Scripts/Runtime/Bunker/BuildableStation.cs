using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Buildable Station")]
public class BuildableStation : MonoBehaviour
{
    [SerializeField] private StationDefinition definition;
    [SerializeField] private string stationId;
    [SerializeField] private string displayName;
    [SerializeField] private StationType stationType = StationType.Workbench;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int maxLevel = 3;
    [SerializeField] private bool unlocked = true;
    [SerializeField] private bool installed = true;
    [SerializeField] private GameObject worldPrefab;
    [SerializeField] private Transform interactionPoint;
    [SerializeField] private List<CraftingRecipe> upgradeRecipes = new();

    public StationDefinition Definition => definition;
    public string StationId => definition != null && !string.IsNullOrWhiteSpace(definition.stationId)
        ? definition.stationId
        : !string.IsNullOrWhiteSpace(stationId)
            ? stationId.Trim()
            : name;
    public string DisplayName => definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
        ? definition.displayName
        : !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : StationId;
    public StationType StationType => definition != null ? definition.stationType : stationType;
    public int Level => CurrentLevel;
    public int CurrentLevel => Mathf.Clamp(currentLevel, 1, MaxLevel);
    public int MaxLevel => definition != null ? Mathf.Max(1, definition.maxLevel) : Mathf.Max(1, maxLevel);
    public bool Installed => installed;
    public bool IsUnlocked => unlocked || (definition != null && definition.isUnlockedByDefault);
    public GameObject WorldPrefab => worldPrefab != null ? worldPrefab : definition != null ? definition.worldPrefab : null;
    public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
    public IReadOnlyList<CraftingRecipe> UpgradeRecipes => upgradeRecipes.Count > 0
        ? upgradeRecipes
        : definition != null
            ? definition.upgradeRecipes
            : upgradeRecipes;

    public void SetInstalled(bool value)
    {
        installed = value;
        gameObject.SetActive(value && IsUnlocked);
    }

    public void SetUnlocked(bool value)
    {
        unlocked = value;
        gameObject.SetActive(installed && IsUnlocked);
    }

    public void SetLevel(int value)
    {
        currentLevel = Mathf.Clamp(value, 1, MaxLevel);
    }

    public bool TryUpgrade()
    {
        if (CurrentLevel >= MaxLevel)
            return false;

        currentLevel = Mathf.Clamp(CurrentLevel + 1, 1, MaxLevel);
        return true;
    }

    public StationSaveData GetSaveData()
    {
        return new StationSaveData
        {
            stationId = StationId,
            stationType = StationType.ToString(),
            stationLevel = CurrentLevel,
            position = transform.position,
            rotation = transform.eulerAngles,
            isUnlocked = IsUnlocked,
            isBuilt = installed
        };
    }

    public void LoadFromSaveData(StationSaveData data)
    {
        if (data == null)
            return;

        SetLevel(data.stationLevel);
        unlocked = data.isUnlocked;
        installed = data.isBuilt;
        transform.position = data.position;
        transform.eulerAngles = data.rotation;
        gameObject.SetActive(installed && IsUnlocked);
    }

    private void OnValidate()
    {
        currentLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
        maxLevel = Mathf.Max(1, maxLevel);
    }
}
