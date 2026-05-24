using UnityEngine;

public class CharacterProgression : MonoBehaviour
{
    [Header("Очки характеристик")]
    public int availableStatPoints = 0;

    [Header("Вложенные очки характеристик")]
    public int durabilityPoints = 0;
    public int agilityPoints = 0;
    public int strengthPoints = 0;

    private CharacterStats stats;

    public int CurrentLevel => stats != null ? Mathf.Max(1, stats.playerLevel) : 1;
    public int CurrentExperience => stats != null ? Mathf.Max(0, stats.currentExp) : 0;
    public int ExperienceToNextLevel => GetExperienceToNextLevel();

    private void Awake()
    {
        ResolveStats();
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || !ResolveStats())
            return;

        stats.currentExp += amount;
        bool leveledUp = TryLevelUp();

        if (!leveledUp)
            stats.NotifyProgressionChanged();
    }

    public void GainLevel(int levels = 1)
    {
        if (!ResolveStats())
            return;

        levels = Mathf.Max(1, levels);

        for (int i = 0; i < levels; i++)
        {
            stats.playerLevel++;
            availableStatPoints++;

            // Прогрессия опыта до следующего уровня.
            stats.expToNextLevel = Mathf.Max(1, Mathf.RoundToInt(stats.expToNextLevel * 1.4f));
        }

        stats.RecalculateAllStats();
        stats.NotifyProgressionChanged();
    }

    public bool TryLevelUp()
    {
        if (!ResolveStats())
            return false;

        bool leveledUp = false;

        while (stats.currentExp >= stats.expToNextLevel && stats.expToNextLevel > 0)
        {
            stats.currentExp -= stats.expToNextLevel;
            GainLevel();
            leveledUp = true;
        }

        return leveledUp;
    }

    public int GetExperienceToNextLevel()
    {
        return stats != null ? Mathf.Max(1, stats.expToNextLevel) : 1;
    }

    public int PredictLevelAfterExperience(int amount)
    {
        if (!ResolveStats())
            return 1;

        int predictedLevel = Mathf.Max(1, stats.playerLevel);
        int predictedExperience = Mathf.Max(0, stats.currentExp) + Mathf.Max(0, amount);
        int predictedToNext = Mathf.Max(1, stats.expToNextLevel);

        while (predictedExperience >= predictedToNext)
        {
            predictedExperience -= predictedToNext;
            predictedLevel++;
            predictedToNext = Mathf.Max(1, Mathf.RoundToInt(predictedToNext * 1.4f));
        }

        return predictedLevel;
    }

    public bool SpendStatPoint(StatType statType)
    {
        if (!ResolveStats() || availableStatPoints <= 0)
            return false;

        switch (statType)
        {
            case StatType.Durability:
                durabilityPoints++;
                stats.durability.SetBaseValue(stats.durability.BaseValue + 1f);
                break;
            case StatType.Agility:
                agilityPoints++;
                stats.agility.SetBaseValue(stats.agility.BaseValue + 1f);
                break;
            case StatType.Strength:
                strengthPoints++;
                stats.strength.SetBaseValue(stats.strength.BaseValue + 1f);
                break;
            default:
                return false;
        }

        availableStatPoints--;
        stats.RecalculateAllStats();
        stats.NotifyProgressionChanged();
        return true;
    }

    public int TotalStatPointsSpend()
    {
        return durabilityPoints + agilityPoints + strengthPoints;
    }

    public void ApplySavedProgression(int savedAvailableStatPoints, int savedDurabilityPoints, int savedAgilityPoints, int savedStrengthPoints)
    {
        availableStatPoints = Mathf.Max(0, savedAvailableStatPoints);
        durabilityPoints = Mathf.Max(0, savedDurabilityPoints);
        agilityPoints = Mathf.Max(0, savedAgilityPoints);
        strengthPoints = Mathf.Max(0, savedStrengthPoints);
        stats?.NotifyProgressionChanged();
    }

    private bool ResolveStats()
    {
        if (stats == null)
            stats = GetComponent<CharacterStats>();

        return stats != null;
    }
}
