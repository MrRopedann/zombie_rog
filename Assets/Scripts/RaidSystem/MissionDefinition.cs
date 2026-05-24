using UnityEngine;

[CreateAssetMenu(fileName = "MissionDefinition", menuName = "Raid/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    public string missionId = "mission";
    public string displayName = "Mission";
    [TextArea(2, 5)] public string description;
    public MissionType missionType = MissionType.KillZombies;
    public int targetCount = 1;
    public int experienceReward = 0;
    public bool isRequired = true;

    public string DisplayNameOrId => !string.IsNullOrWhiteSpace(displayName) ? displayName : missionId;
}
