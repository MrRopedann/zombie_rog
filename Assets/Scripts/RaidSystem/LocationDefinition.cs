using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocationDefinition", menuName = "Raid/Location Definition")]
public class LocationDefinition : ScriptableObject
{
    public string locationId = "location";
    public string displayName = "Location";
    [TextArea(3, 7)] public string description;
    public string sceneName = "City";
    public int difficulty = 1;
    public int recommendedLevel = 1;
    public int baseExperienceReward = 0;
    public bool isUnlockedByDefault = true;
    public List<MissionDefinition> availableMissions = new();

    public string DisplayNameOrId => !string.IsNullOrWhiteSpace(displayName) ? displayName : locationId;
}
