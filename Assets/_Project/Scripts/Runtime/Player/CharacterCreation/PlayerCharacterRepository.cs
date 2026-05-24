using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PlayerCharacterRepository
{
    [Serializable]
    private class CharacterProfileStore
    {
        public int version = 1;
        public string selectedCharacterId;
        public List<PlayerCharacterProfile> characters = new List<PlayerCharacterProfile>();
    }

    private const string StoreFileName = "player_characters.json";
    private static CharacterProfileStore store;
    private static bool isLoaded;

    public static IReadOnlyList<PlayerCharacterProfile> Characters
    {
        get
        {
            EnsureLoaded();
            return store.characters;
        }
    }

    public static bool HasCharacters
    {
        get
        {
            EnsureLoaded();
            return store.characters.Count > 0;
        }
    }

    public static string SelectedCharacterId
    {
        get
        {
            EnsureLoaded();
            return store.selectedCharacterId;
        }
    }

    public static PlayerCharacterProfile SelectedCharacter
    {
        get
        {
            EnsureLoaded();
            return FindById(store.selectedCharacterId);
        }
    }

    public static string StorePath => Path.Combine(Application.persistentDataPath, StoreFileName);

    public static void Reload()
    {
        isLoaded = false;
        EnsureLoaded();
    }

    public static PlayerCharacterProfile CreateCharacter(
        string characterName,
        PlayerCharacterGender gender,
        int modelIndex,
        string modelId,
        PlayerCharacterClass characterClass)
    {
        EnsureLoaded();

        PlayerCharacterProfile profile = PlayerCharacterProfile.Create(
            characterName,
            gender,
            modelIndex,
            modelId,
            characterClass);

        store.characters.Add(profile);
        store.selectedCharacterId = profile.characterId;
        Save();
        return profile;
    }

    public static bool SelectCharacter(string characterId)
    {
        EnsureLoaded();

        PlayerCharacterProfile profile = FindById(characterId);
        if (profile == null)
            return false;

        store.selectedCharacterId = profile.characterId;
        profile.lastPlayedUtc = DateTime.UtcNow.ToString("O");
        Save();
        return true;
    }

    public static bool TryEnsureSelected(out PlayerCharacterProfile profile)
    {
        EnsureLoaded();

        profile = SelectedCharacter;
        if (profile != null)
            return true;

        if (store.characters.Count == 0)
            return false;

        profile = store.characters[0];
        store.selectedCharacterId = profile.characterId;
        profile.lastPlayedUtc = DateTime.UtcNow.ToString("O");
        Save();
        return true;
    }

    public static void ApplySelectedTo(CharacterStats stats)
    {
        if (stats == null)
            return;

        if (!TryEnsureSelected(out PlayerCharacterProfile profile))
            return;

        stats.playerName = profile.DisplayName;

        Transform visualRoot = stats.characterPrefab != null ? stats.characterPrefab.transform : stats.transform;
        CharacterSkinSelector.ApplyProfileToCharacterRoot(visualRoot, profile);
    }

    public static PlayerCharacterProfile FindById(string characterId)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        for (int i = 0; i < store.characters.Count; i++)
        {
            PlayerCharacterProfile profile = store.characters[i];
            if (profile != null && profile.characterId == characterId)
                return profile;
        }

        return null;
    }

    private static void EnsureLoaded()
    {
        if (isLoaded)
            return;

        isLoaded = true;
        store = new CharacterProfileStore();

        string path = StorePath;
        if (!File.Exists(path))
            return;

        try
        {
            string json = File.ReadAllText(path);
            CharacterProfileStore loaded = JsonUtility.FromJson<CharacterProfileStore>(json);
            if (loaded != null)
                store = loaded;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load player characters from '{path}': {exception.Message}");
            store = new CharacterProfileStore();
        }

        if (store.characters == null)
            store.characters = new List<PlayerCharacterProfile>();

        RemoveBrokenProfiles();
    }

    private static void RemoveBrokenProfiles()
    {
        bool changed = false;

        for (int i = store.characters.Count - 1; i >= 0; i--)
        {
            PlayerCharacterProfile profile = store.characters[i];
            if (profile == null)
            {
                store.characters.RemoveAt(i);
                changed = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(profile.characterId))
            {
                profile.characterId = Guid.NewGuid().ToString("N");
                changed = true;
            }

            string sanitizedName = PlayerCharacterProfile.SanitizeName(profile.characterName);
            if (profile.characterName != sanitizedName)
            {
                profile.characterName = sanitizedName;
                changed = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(store.selectedCharacterId) && FindById(store.selectedCharacterId) == null)
        {
            store.selectedCharacterId = null;
            changed = true;
        }

        if (changed)
            Save();
    }

    private static void Save()
    {
        EnsureLoaded();

        string path = StorePath;
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(store, true);
        File.WriteAllText(path, json);
    }
}
