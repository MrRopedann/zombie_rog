using UnityEngine;

public class RewardCalculator : MonoBehaviour
{
    [SerializeField] private int zombieKillExperience = 10;
    [SerializeField] private float damagePerExperience = 20f;
    [SerializeField] private int requiredObjectiveExperience = 200;
    [SerializeField] private int optionalObjectiveExperience = 75;
    [SerializeField] private int reviveExperience = 100;
    [SerializeField] private int extractionExperience = 100;
    [SerializeField] private float difficultyStepMultiplier = 0.15f;

    public int CalculateExperience(LocationDefinition location, RaidStatsSnapshot stats)
    {
        if (stats == null)
            return 0;

        int rawExperience = 0;
        rawExperience += Mathf.Max(0, stats.kills) * zombieKillExperience;
        rawExperience += Mathf.FloorToInt(Mathf.Max(0f, stats.damageDealt) / Mathf.Max(1f, damagePerExperience));
        rawExperience += Mathf.Max(0, stats.requiredObjectivesCompleted) * requiredObjectiveExperience;
        rawExperience += Mathf.Max(0, stats.optionalObjectivesCompleted) * optionalObjectiveExperience;
        rawExperience += Mathf.Max(0, stats.alliesRevived) * reviveExperience;

        if (stats.extractionSuccess)
        {
            rawExperience += extractionExperience;
            rawExperience += location != null ? Mathf.Max(0, location.baseExperienceReward) : 0;
        }

        int difficulty = location != null ? Mathf.Max(0, location.difficulty) : 0;
        float multiplier = 1f + difficulty * difficultyStepMultiplier;
        return Mathf.Max(0, Mathf.RoundToInt(rawExperience * multiplier));
    }

    public int CalculateExperienceForPlayer(LocationDefinition location, RaidStatsTracker tracker, int playerId)
    {
        RaidStatsSnapshot stats = tracker != null ? tracker.GetSnapshotForPlayer(playerId) : null;
        return CalculateExperience(location, stats);
    }
}
