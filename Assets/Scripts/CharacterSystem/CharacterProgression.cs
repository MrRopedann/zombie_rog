using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterProgression : MonoBehaviour
{
    [Header("Очки харрактеристик")]
    public int availableStatPoints = 0;

    [Header("Вложенные очки харрактеристик")]
    public int durabilityPoints = 0;
    public int agilityPoints = 0;
    public int strengthPoints = 0;

    private CharacterStats stats;

    private void Awake()
    {
        stats = GetComponent<CharacterStats>();
    }

    /// <summary>
    /// Получить новый уровень
    /// </summary>
    public void GainLevel(int levels = 1)
    {
        stats.playerLevel += levels;
        availableStatPoints += 1;

        //Погрессия опыта до следуюего уровня
        stats.expToNextLevel = Mathf.RoundToInt(stats.expToNextLevel * 1.4f);

    }

    public bool SpendStatPoint(StatType statType)
    {
        if (availableStatPoints <= 0) return false;

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
        return true;
    }

    public int TotalStatPointsSpend()
    {
       return durabilityPoints + agilityPoints + strengthPoints;
    }


}
