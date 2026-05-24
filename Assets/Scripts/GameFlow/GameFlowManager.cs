using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Game Flow/Game Flow Manager")]
public class GameFlowManager : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainScene";
    [SerializeField] private string bunkerSceneName = "Bunker";
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool saveWhenReturningToBunker = true;

    private static GameFlowManager instance;

    public static GameFlowManager Instance => EnsureActive();
    public static bool HasInstance => instance != null;
    public string BunkerSceneName => bunkerSceneName;

    public event Action<GameMode> OnGameModeChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureActive();
    }

    public static GameFlowManager EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject managerObject = new GameObject("Game Flow Manager");
        instance = managerObject.AddComponent<GameFlowManager>();
        DontDestroyOnLoad(managerObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (dontDestroyOnLoad)
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

    public void LoadMainMenu()
    {
        SetMode(GameMode.MainMenu);
        LoadSceneIfNeeded(mainMenuSceneName);
    }

    public void LoadBunker()
    {
        SetMode(GameMode.Bunker);
        LoadSceneIfNeeded(bunkerSceneName);
    }

    public void StartRaid(LocationDefinition location)
    {
        StartRaid(location, ResolveDefaultMission(location));
    }

    public void StartRaid(LocationDefinition location, MissionDefinition mission)
    {
        if (location == null)
        {
            Debug.LogWarning("Cannot start raid: no location selected.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(location.sceneName))
        {
            Debug.LogWarning($"Cannot start raid for '{location.DisplayNameOrId}': sceneName is empty.", this);
            return;
        }

        GameSessionState.SetSelectedRaid(location, mission);
        SetMode(GameMode.RaidLoading);
        SceneManager.LoadScene(location.sceneName);
    }

    public void CompleteRaid(RaidResult result)
    {
        if (result == null)
            return;

        GameSessionState.StoreRaidResult(result);
        SetMode(GameMode.RaidResult);
    }

    public void ReturnToBunker()
    {
        if (saveWhenReturningToBunker)
            GameSaveManager.SaveExtractedRaidReturnAndQueueLoad(bunkerSceneName);

        GameSessionState.ClearRaid();
        LoadBunker();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainMenuSceneName)
        {
            SetMode(GameMode.MainMenu);
            return;
        }

        if (GameSessionState.CurrentMode == GameMode.RaidLoading)
        {
            SetMode(GameMode.Raid);
            EnsureRaidSceneRuntime();
            return;
        }

        if (scene.name == bunkerSceneName)
        {
            SetMode(GameMode.Bunker);
            EnsureBunkerSceneRuntime();
        }
    }

    private void SetMode(GameMode mode)
    {
        GameSessionState.SetMode(mode);
        OnGameModeChanged?.Invoke(mode);
    }

    private void LoadSceneIfNeeded(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (SceneManager.GetActiveScene().name == sceneName)
        {
            if (sceneName == bunkerSceneName)
                EnsureBunkerSceneRuntime();

            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static void EnsureRaidSceneRuntime()
    {
        RaidManager raidManager = FindObjectOfType<RaidManager>(true);
        GameObject runtimeObject = raidManager != null ? raidManager.gameObject : new GameObject("MVP Raid Runtime");

        if (runtimeObject.GetComponent<ObjectiveManager>() == null && FindObjectOfType<ObjectiveManager>(true) == null)
            runtimeObject.AddComponent<ObjectiveManager>();

        if (runtimeObject.GetComponent<RaidStatsTracker>() == null && FindObjectOfType<RaidStatsTracker>(true) == null)
            runtimeObject.AddComponent<RaidStatsTracker>();

        if (runtimeObject.GetComponent<RewardCalculator>() == null && FindObjectOfType<RewardCalculator>(true) == null)
            runtimeObject.AddComponent<RewardCalculator>();

        if (raidManager == null)
            raidManager = runtimeObject.AddComponent<RaidManager>();

        if (FindObjectOfType<RaidObjectiveUI>(true) == null)
            runtimeObject.AddComponent<RaidObjectiveUI>();

        if (FindObjectOfType<RaidResultUI>(true) == null)
            runtimeObject.AddComponent<RaidResultUI>();

        EnsureGameplayHud(runtimeObject);
    }

    private static void EnsureBunkerSceneRuntime()
    {
        BunkerManager bunkerManager = FindObjectOfType<BunkerManager>(true);
        LocationSelectionUI locationSelectionUI = FindObjectOfType<LocationSelectionUI>(true);

        if (bunkerManager != null && locationSelectionUI != null)
            return;

        GameObject runtimeObject = bunkerManager != null ? bunkerManager.gameObject : new GameObject("MVP Bunker Runtime");

        if (bunkerManager == null)
            runtimeObject.AddComponent<BunkerManager>();

        if (locationSelectionUI == null)
            runtimeObject.AddComponent<LocationSelectionUI>();

        EnsureGameplayHud(runtimeObject);
    }

    private static void EnsureGameplayHud(GameObject runtimeObject)
    {
        if (runtimeObject == null)
            return;

        if (FindObjectOfType<UICharacterHUD>(true) == null &&
            FindObjectOfType<CharacterStats>(true) != null &&
            runtimeObject.GetComponent<UICharacterHUD>() == null)
        {
            runtimeObject.AddComponent<UICharacterHUD>().EnableFallbackLayoutWhenEmpty();
        }

        if (FindObjectOfType<UIAmmoHUD>(true) == null &&
            FindObjectOfType<PlayerWeaponController>(true) != null &&
            runtimeObject.GetComponent<UIAmmoHUD>() == null)
        {
            runtimeObject.AddComponent<UIAmmoHUD>();
        }
    }

    private static MissionDefinition ResolveDefaultMission(LocationDefinition location)
    {
        if (location == null || location.availableMissions == null)
            return null;

        for (int i = 0; i < location.availableMissions.Count; i++)
        {
            MissionDefinition mission = location.availableMissions[i];
            if (mission != null && mission.isRequired)
                return mission;
        }

        return location.availableMissions.Count > 0 ? location.availableMissions[0] : null;
    }
}
