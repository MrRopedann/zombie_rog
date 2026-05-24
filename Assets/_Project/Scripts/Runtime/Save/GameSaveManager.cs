using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Save System/Game Save Manager")]
public class GameSaveManager : MonoBehaviour
{
    private const string SaveFileName = "zombie_rog_progress.es3";
    private const string SaveKey = "game_progress";
    private const string MainMenuSceneName = "MainScene";
    private const int CurrentVersion = 1;

    private static GameSaveManager instance;
    private static GameSaveData pendingLoad;

    private readonly Dictionary<string, ItemSO> itemsById = new();
    private readonly Dictionary<string, GameObject> zombiePrefabsByName = new();

    public static bool SaveExists => ES3.KeyExists(SaveKey, SaveFileName);
    public static bool CanSaveInCurrentContext => CanControlPersistence();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureActive();
    }

    public static GameSaveManager EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject managerObject = new GameObject("Game Save Manager");
        instance = managerObject.AddComponent<GameSaveManager>();
        DontDestroyOnLoad(managerObject);
        return instance;
    }

    public static bool SaveCurrentGame()
    {
        return EnsureActive().SaveNow();
    }

    public static bool SaveGame()
    {
        return SaveCurrentGame();
    }

    public static bool LoadCurrentGame()
    {
        return EnsureActive().LoadNow();
    }

    public static bool LoadGame()
    {
        return LoadCurrentGame();
    }

    public static bool HasSave()
    {
        return SaveExists;
    }

    public static void DeleteCurrentSave()
    {
        if (ES3.FileExists(SaveFileName))
            ES3.DeleteFile(SaveFileName);
    }

    public static void DeleteSave()
    {
        DeleteCurrentSave();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        if (IsMainMenuScene())
            return;

        if (Input.GetKeyDown(KeyCode.F5))
            SaveNow();

        if (Input.GetKeyDown(KeyCode.F9))
            LoadNow();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoad != null)
            StartCoroutine(ApplyPendingLoadNextFrame());
    }

    private IEnumerator ApplyPendingLoadNextFrame()
    {
        yield return null;

        GameSaveData data = pendingLoad;
        pendingLoad = null;
        ApplySave(data);
    }

    private bool SaveNow()
    {
        if (!CanControlPersistence())
        {
            Debug.LogWarning("Only singleplayer or the coop host can save the authoritative world state.");
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || activeScene.name == MainMenuSceneName)
        {
            Debug.LogWarning("Game save skipped: no gameplay scene is active.");
            return false;
        }

        GameSaveData data = CaptureSave();
        ES3.Save(SaveKey, data, SaveFileName);
        Debug.Log($"Game saved to Easy Save 3 file '{SaveFileName}' at {data.savedAtUtc}.");
        return true;
    }

    private bool LoadNow()
    {
        if (!CanControlPersistence())
        {
            Debug.LogWarning("Only singleplayer or the coop host can load the authoritative world state.");
            return false;
        }

        if (!SaveExists)
        {
            Debug.LogWarning($"No Easy Save 3 save was found in '{SaveFileName}'.");
            return false;
        }

        GameSaveData data = ES3.Load<GameSaveData>(SaveKey, SaveFileName);
        if (data == null)
        {
            Debug.LogWarning("Easy Save 3 returned an empty game save.");
            return false;
        }

        string targetScene = string.IsNullOrWhiteSpace(data.sceneName)
            ? SceneManager.GetActiveScene().name
            : data.sceneName;

        if (!string.Equals(SceneManager.GetActiveScene().name, targetScene, StringComparison.Ordinal))
        {
            pendingLoad = data;
            Time.timeScale = 1f;
            SceneManager.LoadScene(targetScene);
            return true;
        }

        ApplySave(data);
        return true;
    }

    private GameSaveData CaptureSave()
    {
        GameSaveData data = new GameSaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            sceneName = SceneManager.GetActiveScene().name,
            selectedCharacterId = PlayerCharacterRepository.SelectedCharacterId,
            coopSession = CoopSessionState.IsCoopSession,
            coopHostSave = CoopSessionState.IsHost,
            players = CapturePlayers(),
            authoritativeInventories = CaptureAuthoritativeInventories(),
            lootContainers = CaptureLootContainers(),
            worldItems = CaptureWorldItems(),
            zombies = CaptureZombies(),
            saveableObjects = CaptureSaveableObjects(),
            bunker = CaptureBunker(),
            unlockedLocations = CaptureUnlockedLocations()
        };

        return data;
    }

    private void ApplySave(GameSaveData data)
    {
        if (data == null)
            return;

        LoadItemCache();
        LoadZombiePrefabCache();

        RestoreSaveableObjects(data.saveableObjects);
        RestoreBunker(data.bunker);
        RestorePlayers(data.players);
        RestoreAuthoritativeInventories(data.authoritativeInventories);
        RestoreLootContainers(data.lootContainers);
        RestoreWorldItems(data.worldItems);
        RestoreZombies(data.zombies);

        Debug.Log($"Game save loaded from '{SaveFileName}' (version {data.version}, scene {data.sceneName}).");
    }

    private List<PlayerSaveData> CapturePlayers()
    {
        List<PlayerSaveData> result = new();
        CharacterStats[] statsList = FindObjectsOfType<CharacterStats>(true);

        for (int i = 0; i < statsList.Length; i++)
        {
            CharacterStats stats = statsList[i];
            if (stats == null)
                continue;

            CoopNetworkIdentity identity = stats.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.Kind == CoopNetworkObjectKind.Zombie)
                continue;

            Transform root = ResolvePlayerRoot(stats);
            PlayerInventory inventory = ResolveInventory(stats);
            PlayerWeaponController weapons = ResolveWeaponController(stats);
            CharacterProgression progression = ResolveProgression(stats);

            PlayerSaveData player = new PlayerSaveData
            {
                ownerId = identity != null && identity.OwnerId > 0 ? identity.OwnerId : stats.playerID,
                playerId = stats.playerID,
                playerName = stats.playerName,
                selectedCharacterId = PlayerCharacterRepository.SelectedCharacterId,
                position = root != null ? root.position : stats.transform.position,
                rotation = root != null ? root.rotation : stats.transform.rotation,
                health = stats.currentHealth,
                armor = stats.currentArmor,
                hunger = stats.currentHunger,
                thirst = stats.currentThirst,
                stamina = stats.currentStamina,
                level = stats.playerLevel,
                experience = stats.currentExp,
                experienceToNextLevel = stats.expToNextLevel,
                durabilityBase = stats.durability != null ? stats.durability.BaseValue : 0f,
                durabilityModifier = stats.durability != null ? stats.durability.Modifier : 0f,
                agilityBase = stats.agility != null ? stats.agility.BaseValue : 0f,
                agilityModifier = stats.agility != null ? stats.agility.Modifier : 0f,
                strengthBase = stats.strength != null ? stats.strength.BaseValue : 0f,
                strengthModifier = stats.strength != null ? stats.strength.Modifier : 0f,
                availableStatPoints = progression != null ? progression.availableStatPoints : 0,
                durabilityPoints = progression != null ? progression.durabilityPoints : 0,
                agilityPoints = progression != null ? progression.agilityPoints : 0,
                strengthPoints = progression != null ? progression.strengthPoints : 0,
                dead = stats.IsDead,
                inventory = CaptureItemStacks(inventory != null ? inventory.Slots : null),
                selectedWeaponIndex = weapons != null ? weapons.SelectedWeaponIndex : 0,
                weapons = CaptureWeapons(weapons)
            };

            result.Add(player);
        }

        return result;
    }

    private List<NetworkInventorySaveData> CaptureAuthoritativeInventories()
    {
        Dictionary<int, Dictionary<string, int>> exported = CoopGameplaySync.ExportAuthoritativeInventoryStacks();
        List<NetworkInventorySaveData> result = new();

        if (exported == null)
            return result;

        foreach (KeyValuePair<int, Dictionary<string, int>> pair in exported)
        {
            NetworkInventorySaveData inventory = new NetworkInventorySaveData { ownerId = pair.Key };
            foreach (KeyValuePair<string, int> stack in pair.Value)
            {
                if (!string.IsNullOrWhiteSpace(stack.Key) && stack.Value > 0)
                    inventory.items.Add(new ItemStackSaveData { itemId = stack.Key, amount = stack.Value });
            }

            result.Add(inventory);
        }

        return result;
    }

    private List<LootContainerSaveData> CaptureLootContainers()
    {
        List<LootContainerSaveData> result = new();
        LootContainer[] containers = FindObjectsOfType<LootContainer>(true);

        for (int i = 0; i < containers.Length; i++)
        {
            LootContainer container = containers[i];
            if (container == null)
                continue;

            result.Add(new LootContainerSaveData
            {
                containerId = container.NetworkId,
                wasSearched = container.WasSearched,
                items = CaptureItemStacks(container.Slots)
            });
        }

        return result;
    }

    private List<WorldItemSaveData> CaptureWorldItems()
    {
        List<WorldItemSaveData> result = new();
        WorldItem[] worldItems = FindObjectsOfType<WorldItem>(true);

        for (int i = 0; i < worldItems.Length; i++)
        {
            WorldItem worldItem = worldItems[i];
            if (worldItem == null || worldItem.ItemData == null || worldItem.IsPickedUp)
                continue;

            if (worldItem.RemoteNetworkProxy)
                continue;

            Rigidbody body = worldItem.GetComponent<Rigidbody>();
            result.Add(new WorldItemSaveData
            {
                itemId = GetItemId(worldItem.ItemData),
                amount = worldItem.Amount,
                networkItemId = worldItem.NetworkItemId,
                position = worldItem.transform.position,
                rotation = worldItem.transform.rotation,
                velocity = body != null && !body.isKinematic ? body.velocity : Vector3.zero,
                angularVelocity = body != null && !body.isKinematic ? body.angularVelocity : Vector3.zero
            });
        }

        return result;
    }

    private List<ZombieSaveData> CaptureZombies()
    {
        List<ZombieSaveData> result = new();
        ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>(true);

        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieHealth health = zombies[i];
            if (health == null || health.IsDead)
                continue;

            CoopNetworkIdentity identity = health.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.IsRemoteProxy)
                continue;

            Transform root = ResolveZombieRoot(health, identity);
            result.Add(new ZombieSaveData
            {
                networkId = identity != null ? identity.NetworkId : 0,
                prefabId = identity != null ? identity.PrefabId : 0,
                prefabName = ResolveZombiePrefabName(root != null ? root.gameObject : health.gameObject),
                position = root != null ? root.position : health.transform.position,
                rotation = root != null ? root.rotation : health.transform.rotation,
                health = health.CurrentHealth,
                maxHealth = health.MaxHealth
            });
        }

        return result;
    }

    private List<SaveableObjectData> CaptureSaveableObjects()
    {
        List<SaveableObjectData> result = new();
        SaveableObject[] objects = FindObjectsOfType<SaveableObject>(true);

        for (int i = 0; i < objects.Length; i++)
        {
            SaveableObject saveable = objects[i];
            if (saveable == null)
                continue;

            result.Add(new SaveableObjectData
            {
                saveId = saveable.SaveId,
                active = saveable.gameObject.activeSelf,
                position = saveable.transform.position,
                rotation = saveable.transform.rotation,
                localScale = saveable.transform.localScale
            });
        }

        return result;
    }

    private BunkerSaveData CaptureBunker()
    {
        BunkerManager bunkerManager = FindObjectOfType<BunkerManager>(true);
        return bunkerManager != null ? bunkerManager.GetSaveData() : new BunkerSaveData();
    }

    private List<UnlockedLocationSaveData> CaptureUnlockedLocations()
    {
        BunkerSaveData bunker = CaptureBunker();
        return bunker != null && bunker.unlockedLocations != null
            ? new List<UnlockedLocationSaveData>(bunker.unlockedLocations)
            : new List<UnlockedLocationSaveData>();
    }

    private List<WeaponSaveData> CaptureWeapons(PlayerWeaponController controller)
    {
        List<WeaponSaveData> result = new();
        if (controller == null || controller.Weapons == null)
            return result;

        IReadOnlyList<Weapon> weapons = controller.Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            Weapon weapon = weapons[i];
            if (weapon == null)
                continue;

            result.Add(new WeaponSaveData
            {
                index = i,
                weaponId = weapon.Definition != null && !string.IsNullOrWhiteSpace(weapon.Definition.WeaponID)
                    ? weapon.Definition.WeaponID
                    : weapon.name,
                currentAmmo = weapon.CurrentAmmo,
                reserveAmmo = weapon.ReserveAmmo,
                reloading = weapon.IsReloading
            });
        }

        return result;
    }

    private List<ItemStackSaveData> CaptureItemStacks(IReadOnlyList<InventorySlot> slots)
    {
        List<ItemStackSaveData> result = new();
        if (slots == null)
            return result;

        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.item == null || slot.amount <= 0)
                continue;

            string itemId = GetItemId(slot.item);
            if (string.IsNullOrWhiteSpace(itemId))
                continue;

            result.Add(new ItemStackSaveData { itemId = itemId, amount = slot.amount });
        }

        return result;
    }

    private void RestorePlayers(List<PlayerSaveData> players)
    {
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerSaveData saved = players[i];
            CharacterStats stats = FindPlayerForSave(saved);
            if (stats == null)
                continue;

            Transform root = ResolvePlayerRoot(stats);
            bool localAuthority = IsLocalAuthority(stats);
            TeleportTransform(root != null ? root : stats.transform, saved.position, saved.rotation);

            stats.ApplySavedState(
                saved.playerId,
                saved.playerName,
                saved.level,
                saved.experience,
                saved.experienceToNextLevel,
                saved.health,
                saved.armor,
                saved.hunger,
                saved.thirst,
                saved.stamina,
                saved.durabilityBase,
                saved.durabilityModifier,
                saved.agilityBase,
                saved.agilityModifier,
                saved.strengthBase,
                saved.strengthModifier,
                saved.dead);

            CharacterProgression progression = ResolveProgression(stats);
            if (progression != null)
            {
                progression.ApplySavedProgression(
                    saved.availableStatPoints,
                    saved.durabilityPoints,
                    saved.agilityPoints,
                    saved.strengthPoints);
            }

            if (!saved.dead)
            {
                ThirdPersonController controller = stats.GetComponent<ThirdPersonController>() ??
                    stats.GetComponentInParent<ThirdPersonController>() ??
                    stats.GetComponentInChildren<ThirdPersonController>(true);
                if (controller != null)
                    controller.ReviveFromNetwork(saved.position, saved.rotation, localAuthority);
            }

            PlayerInventory inventory = ResolveInventory(stats);
            if (inventory != null)
                inventory.ReplaceContents(BuildInventorySlots(saved.inventory));

            RestoreWeapons(ResolveWeaponController(stats), saved);
        }
    }

    private void RestoreAuthoritativeInventories(List<NetworkInventorySaveData> inventories)
    {
        if (inventories == null || inventories.Count == 0)
            return;

        Dictionary<int, Dictionary<string, int>> map = new();
        for (int i = 0; i < inventories.Count; i++)
        {
            NetworkInventorySaveData saved = inventories[i];
            if (saved == null || saved.ownerId <= 0)
                continue;

            Dictionary<string, int> stacks = new();
            for (int j = 0; j < saved.items.Count; j++)
            {
                ItemStackSaveData stack = saved.items[j];
                if (stack != null && !string.IsNullOrWhiteSpace(stack.itemId) && stack.amount > 0)
                    stacks[stack.itemId] = stack.amount;
            }

            map[saved.ownerId] = stacks;
        }

        CoopGameplaySync.ImportAuthoritativeInventoryStacks(map);
    }

    private void RestoreLootContainers(List<LootContainerSaveData> containers)
    {
        if (containers == null)
            return;

        Dictionary<string, LootContainer> sceneContainers = new();
        foreach (LootContainer container in FindObjectsOfType<LootContainer>(true))
        {
            if (container != null && !string.IsNullOrWhiteSpace(container.NetworkId))
                sceneContainers[container.NetworkId] = container;
        }

        for (int i = 0; i < containers.Count; i++)
        {
            LootContainerSaveData saved = containers[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.containerId))
                continue;

            if (!sceneContainers.TryGetValue(saved.containerId, out LootContainer container) || container == null)
                continue;

            container.ClearNetworkState(saved.wasSearched);
            for (int j = 0; j < saved.items.Count; j++)
            {
                ItemStackSaveData stack = saved.items[j];
                ItemSO item = stack != null ? ResolveItem(stack.itemId) : null;
                if (item != null && stack.amount > 0)
                    container.AddNetworkItem(item, stack.amount);
            }
        }
    }

    private void RestoreWorldItems(List<WorldItemSaveData> worldItems)
    {
        RemoveExistingWorldItems();

        if (worldItems == null)
            return;

        for (int i = 0; i < worldItems.Count; i++)
        {
            WorldItemSaveData saved = worldItems[i];
            ItemSO item = saved != null ? ResolveItem(saved.itemId) : null;
            if (item == null || saved.amount <= 0)
                continue;

            WorldItem worldItem = WorldItem.Spawn(item, saved.position, saved.rotation, saved.velocity, saved.angularVelocity);
            if (worldItem == null)
                continue;

            worldItem.SetupNetwork(saved.networkItemId, saved.amount, false);

            CoopGameplaySync.NotifyWorldItemDropped(worldItem, item, saved.amount);
        }
    }

    private void RestoreZombies(List<ZombieSaveData> zombies)
    {
        RemoveExistingZombies();

        if (zombies == null)
            return;

        for (int i = 0; i < zombies.Count; i++)
        {
            ZombieSaveData saved = zombies[i];
            if (saved == null || saved.health <= 0f)
                continue;

            GameObject prefab = ResolveZombiePrefab(saved);
            if (prefab == null)
                continue;

            GameObject instanceObject = Instantiate(prefab, saved.position, saved.rotation);
            ZombieHealth health = instanceObject.GetComponentInChildren<ZombieHealth>(true);
            if (health != null)
                health.SetNetworkHealth(saved.health);

            NavMeshAgent agent = instanceObject.GetComponentInChildren<NavMeshAgent>(true);
            if (agent != null && agent.enabled)
                agent.Warp(saved.position);
        }
    }

    private void RestoreSaveableObjects(List<SaveableObjectData> objects)
    {
        if (objects == null)
            return;

        Dictionary<string, SaveableObject> sceneObjects = new();
        foreach (SaveableObject saveable in FindObjectsOfType<SaveableObject>(true))
        {
            if (saveable != null && !string.IsNullOrWhiteSpace(saveable.SaveId))
                sceneObjects[saveable.SaveId] = saveable;
        }

        for (int i = 0; i < objects.Count; i++)
        {
            SaveableObjectData saved = objects[i];
            if (saved == null || string.IsNullOrWhiteSpace(saved.saveId))
                continue;

            if (sceneObjects.TryGetValue(saved.saveId, out SaveableObject saveable) && saveable != null)
                saveable.RestoreState(saved.active, saved.position, saved.rotation, saved.localScale);
        }
    }

    private void RestoreBunker(BunkerSaveData bunker)
    {
        if (bunker == null)
            return;

        BunkerManager bunkerManager = FindObjectOfType<BunkerManager>(true);
        if (bunkerManager != null)
            bunkerManager.LoadFromSaveData(bunker);
    }

    private void RestoreWeapons(PlayerWeaponController controller, PlayerSaveData saved)
    {
        if (controller == null || saved == null || saved.weapons == null || controller.Weapons == null)
            return;

        IReadOnlyList<Weapon> weapons = controller.Weapons;
        for (int i = 0; i < saved.weapons.Count; i++)
        {
            WeaponSaveData weaponData = saved.weapons[i];
            if (weaponData == null || weaponData.index < 0 || weaponData.index >= weapons.Count)
                continue;

            Weapon weapon = weapons[weaponData.index];
            if (weapon != null)
                weapon.ApplyNetworkAmmoState(weaponData.currentAmmo, weaponData.reserveAmmo, weaponData.reloading);
        }

        controller.SetSelectedWeaponIndex(saved.selectedWeaponIndex);
    }

    private List<InventorySlot> BuildInventorySlots(List<ItemStackSaveData> stacks)
    {
        List<InventorySlot> slots = new();
        if (stacks == null)
            return slots;

        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStackSaveData stack = stacks[i];
            ItemSO item = stack != null ? ResolveItem(stack.itemId) : null;
            if (item != null && stack.amount > 0)
                slots.Add(new InventorySlot(item, stack.amount));
        }

        return slots;
    }

    private CharacterStats FindPlayerForSave(PlayerSaveData saved)
    {
        CharacterStats[] statsList = FindObjectsOfType<CharacterStats>(true);
        CharacterStats firstUsable = null;

        for (int i = 0; i < statsList.Length; i++)
        {
            CharacterStats stats = statsList[i];
            if (stats == null)
                continue;

            CoopNetworkIdentity identity = stats.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.Kind == CoopNetworkObjectKind.Zombie)
                continue;

            if (firstUsable == null)
                firstUsable = stats;

            if (saved.ownerId > 0 && identity != null && identity.OwnerId == saved.ownerId)
                return stats;

            if (saved.playerId > 0 && stats.playerID == saved.playerId)
                return stats;
        }

        return firstUsable;
    }

    private void RemoveExistingWorldItems()
    {
        WorldItem[] existing = FindObjectsOfType<WorldItem>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            WorldItem worldItem = existing[i];
            if (worldItem == null || worldItem.RemoteNetworkProxy)
                continue;

            DestroyRuntimeObject(worldItem.gameObject);
        }
    }

    private void RemoveExistingZombies()
    {
        ZombieHealth[] existing = FindObjectsOfType<ZombieHealth>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            ZombieHealth health = existing[i];
            if (health == null)
                continue;

            CoopNetworkIdentity identity = health.GetComponentInParent<CoopNetworkIdentity>();
            if (identity != null && identity.IsRemoteProxy)
                continue;

            Transform root = ResolveZombieRoot(health, identity);
            DestroyRuntimeObject(root != null ? root.gameObject : health.gameObject);
        }
    }

    private void LoadItemCache()
    {
        if (itemsById.Count > 0)
            return;

        RegisterItems(Resources.LoadAll<ItemSO>("RuntimeLoadedOnly/Data"));
        RegisterItems(Resources.LoadAll<ItemSO>("RuntimeLoadedOnly/Data/Item"));
        RegisterItems(Resources.LoadAll<ItemSO>(string.Empty));
    }

    private void RegisterItems(ItemSO[] items)
    {
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemSO item = items[i];
            if (item == null)
                continue;

            RegisterItemKey(item.itemID, item);
            RegisterItemKey(item.name, item);
        }
    }

    private void RegisterItemKey(string key, ItemSO item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null)
            return;

        itemsById[key.Trim()] = item;
    }

    private ItemSO ResolveItem(string itemId)
    {
        LoadItemCache();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return itemsById.TryGetValue(itemId.Trim(), out ItemSO item) ? item : null;
    }

    private void LoadZombiePrefabCache()
    {
        if (zombiePrefabsByName.Count > 0)
            return;

        GameObject[] prefabs = Resources.LoadAll<GameObject>("RuntimeLoadedOnly/Prefabs/Zombie");
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab == null)
                continue;

            zombiePrefabsByName[prefab.name] = prefab;
        }
    }

    private GameObject ResolveZombiePrefab(ZombieSaveData saved)
    {
        LoadZombiePrefabCache();

        if (saved == null)
            return null;

        if (!string.IsNullOrWhiteSpace(saved.prefabName) &&
            zombiePrefabsByName.TryGetValue(saved.prefabName.Trim(), out GameObject byName))
            return byName;

        int index = 0;
        foreach (GameObject prefab in zombiePrefabsByName.Values)
        {
            if (index == saved.prefabId)
                return prefab;

            index++;
        }

        return null;
    }

    private static string GetItemId(ItemSO item)
    {
        if (item == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(item.itemID) ? item.itemID.Trim() : item.name;
    }

    private static PlayerInventory ResolveInventory(CharacterStats stats)
    {
        if (stats == null)
            return null;

        return stats.GetComponent<PlayerInventory>() ??
            stats.GetComponentInChildren<PlayerInventory>(true) ??
            stats.GetComponentInParent<PlayerInventory>();
    }

    private static CharacterProgression ResolveProgression(CharacterStats stats)
    {
        if (stats == null)
            return null;

        return stats.GetComponent<CharacterProgression>() ??
            stats.GetComponentInChildren<CharacterProgression>(true) ??
            stats.GetComponentInParent<CharacterProgression>();
    }

    private static PlayerWeaponController ResolveWeaponController(CharacterStats stats)
    {
        if (stats == null)
            return null;

        return stats.GetComponent<PlayerWeaponController>() ??
            stats.GetComponentInChildren<PlayerWeaponController>(true) ??
            stats.GetComponentInParent<PlayerWeaponController>();
    }

    private static Transform ResolvePlayerRoot(CharacterStats stats)
    {
        if (stats == null)
            return null;

        CharacterController controller = stats.GetComponent<CharacterController>() ??
            stats.GetComponentInParent<CharacterController>() ??
            stats.GetComponentInChildren<CharacterController>(true);

        if (controller != null)
            return controller.transform;

        CoopNetworkIdentity identity = stats.GetComponentInParent<CoopNetworkIdentity>();
        if (identity != null)
            return identity.transform;

        return stats.transform;
    }

    private static Transform ResolveZombieRoot(ZombieHealth health, CoopNetworkIdentity identity)
    {
        if (identity != null)
            return identity.transform;

        ZombieAI ai = health != null ? health.GetComponentInParent<ZombieAI>() : null;
        if (ai != null)
            return ai.transform;

        return health != null ? health.transform : null;
    }

    private static string ResolveZombiePrefabName(GameObject zombieObject)
    {
        if (zombieObject == null)
            return string.Empty;

        return zombieObject.name.Replace("(Clone)", string.Empty).Trim();
    }

    private static void TeleportTransform(Transform target, Vector3 position, Quaternion rotation)
    {
        if (target == null)
            return;

        CharacterController controller = target.GetComponent<CharacterController>();
        bool controllerEnabled = controller != null && controller.enabled;
        if (controllerEnabled)
            controller.enabled = false;

        target.SetPositionAndRotation(position, rotation);

        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (controllerEnabled)
            controller.enabled = true;
    }

    private static bool IsLocalAuthority(CharacterStats stats)
    {
        CoopNetworkIdentity identity = stats != null ? stats.GetComponentInParent<CoopNetworkIdentity>() : null;
        return identity == null || identity.HasLocalAuthority || !identity.IsRemoteProxy;
    }

    private static bool CanControlPersistence()
    {
        return !CoopSessionState.IsCoopSession || CoopSessionState.IsHost;
    }

    private static bool IsMainMenuScene()
    {
        return SceneManager.GetActiveScene().name == MainMenuSceneName;
    }

    private static void DestroyRuntimeObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }
}

[Serializable]
public class GameSaveData
{
    public int version;
    public string savedAtUtc;
    public string sceneName;
    public string selectedCharacterId;
    public bool coopSession;
    public bool coopHostSave;
    public List<PlayerSaveData> players = new();
    public List<NetworkInventorySaveData> authoritativeInventories = new();
    public List<LootContainerSaveData> lootContainers = new();
    public List<WorldItemSaveData> worldItems = new();
    public List<ZombieSaveData> zombies = new();
    public List<SaveableObjectData> saveableObjects = new();
    public BunkerSaveData bunker = new();
    public List<UnlockedLocationSaveData> unlockedLocations = new();
}

[Serializable]
public class PlayerSaveData
{
    public int ownerId;
    public int playerId;
    public string playerName;
    public string selectedCharacterId;
    public Vector3 position;
    public Quaternion rotation;
    public float health;
    public float armor;
    public float hunger;
    public float thirst;
    public float stamina;
    public int level;
    public int experience;
    public int experienceToNextLevel;
    public float durabilityBase;
    public float durabilityModifier;
    public float agilityBase;
    public float agilityModifier;
    public float strengthBase;
    public float strengthModifier;
    public int availableStatPoints;
    public int durabilityPoints;
    public int agilityPoints;
    public int strengthPoints;
    public bool dead;
    public int selectedWeaponIndex;
    public List<ItemStackSaveData> inventory = new();
    public List<WeaponSaveData> weapons = new();
}

[Serializable]
public class InventorySaveData
{
    public List<InventorySlotSaveData> slots = new();
}

[Serializable]
public class InventorySlotSaveData
{
    public string itemId;
    public int amount;
}

[Serializable]
public class BunkerSaveData
{
    public string bunkerId;
    public InventorySaveData storage = new();
    public List<UnlockedLocationSaveData> unlockedLocations = new();
    public List<string> installedStationIds = new();
}

[Serializable]
public class UnlockedLocationSaveData
{
    public string locationId;
    public bool unlocked;
}

[Serializable]
public class NetworkInventorySaveData
{
    public int ownerId;
    public List<ItemStackSaveData> items = new();
}

[Serializable]
public class ItemStackSaveData
{
    public string itemId;
    public int amount;
}

[Serializable]
public class WeaponSaveData
{
    public int index;
    public string weaponId;
    public int currentAmmo;
    public int reserveAmmo;
    public bool reloading;
}

[Serializable]
public class LootContainerSaveData
{
    public string containerId;
    public bool wasSearched;
    public List<ItemStackSaveData> items = new();
}

[Serializable]
public class WorldItemSaveData
{
    public string itemId;
    public int amount;
    public int networkItemId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}

[Serializable]
public class ZombieSaveData
{
    public int networkId;
    public int prefabId;
    public string prefabName;
    public Vector3 position;
    public Quaternion rotation;
    public float health;
    public float maxHealth;
}

[Serializable]
public class SaveableObjectData
{
    public string saveId;
    public bool active;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 localScale;
}
