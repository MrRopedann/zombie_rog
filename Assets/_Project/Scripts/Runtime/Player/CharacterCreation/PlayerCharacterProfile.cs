using System;
using UnityEngine;

public enum PlayerCharacterGender
{
    Male = 0,
    Female = 1
}

public enum PlayerCharacterClass
{
    Survivor = 0,
    Soldier = 1,
    Medic = 2,
    Scout = 3,
    Engineer = 4
}

[Serializable]
public class PlayerCharacterProfile
{
    public string characterId;
    public string characterName;
    public PlayerCharacterGender gender;
    public int modelIndex;
    public string modelId;
    public PlayerCharacterClass characterClass;
    public string createdUtc;
    public string lastPlayedUtc;

    public string DisplayName => string.IsNullOrWhiteSpace(characterName) ? "Выживший" : characterName;

    public string ShortId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return "--------";

            return characterId.Length <= 8 ? characterId : characterId.Substring(0, 8);
        }
    }

    public static PlayerCharacterProfile Create(
        string characterName,
        PlayerCharacterGender gender,
        int modelIndex,
        string modelId,
        PlayerCharacterClass characterClass)
    {
        string now = DateTime.UtcNow.ToString("O");

        return new PlayerCharacterProfile
        {
            characterId = Guid.NewGuid().ToString("N"),
            characterName = SanitizeName(characterName),
            gender = gender,
            modelIndex = Mathf.Max(0, modelIndex),
            modelId = string.IsNullOrWhiteSpace(modelId) ? $"skin_{Mathf.Max(0, modelIndex)}" : modelId,
            characterClass = characterClass,
            createdUtc = now,
            lastPlayedUtc = now
        };
    }

    public static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Выживший";

        value = value.Trim();
        return value.Length > 24 ? value.Substring(0, 24) : value;
    }
}
