using System;
using System.Collections.Generic;

[Serializable]
public class RaidResult
{
    public string locationId;
    public string locationName;
    public bool extractionSuccess;
    public int difficulty;
    public int baseExperienceReward;
    public int experienceEarned;
    public RaidStatsSnapshot stats = new();
    public List<string> completedMissionIds = new();
}
