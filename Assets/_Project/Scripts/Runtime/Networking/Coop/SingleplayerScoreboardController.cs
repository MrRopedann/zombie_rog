using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class SingleplayerScoreboardController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainScene";
    private const float ZombieScanInterval = 0.5f;
    private static SingleplayerScoreboardController instance;

    private readonly List<CoopScoreboardUI.PlayerRowInfo> rowBuffer = new(1);
    private readonly Dictionary<ZombieHealth, ObservedZombie> observedZombies = new();
    private readonly List<ZombieHealth> zombieRemovalBuffer = new();
    private CharacterStats cachedStats;
    private CharacterStats observedStats;
    private float nextZombieScanTime;
    private int zombieKills;
    private float damageDealt;
    private int deaths;

    private sealed class ObservedZombie
    {
        public System.Action<float, Vector3, Vector3> damageHandler;
        public System.Action deathHandler;
        public bool deathCounted;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureActive();
    }

    private static void EnsureActive()
    {
        if (instance != null)
            return;

        GameObject controllerObject = new GameObject("Singleplayer Scoreboard Controller");
        instance = controllerObject.AddComponent<SingleplayerScoreboardController>();
        DontDestroyOnLoad(controllerObject);
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
        UnsubscribeLocalStats();
        ClearObservedZombies();
    }

    private void Update()
    {
        if (CoopSessionState.IsCoopSession)
        {
            cachedStats = null;
            return;
        }

        if (IsMainMenuScene())
        {
            cachedStats = null;
            CoopScoreboardUI.HideExisting();
            return;
        }

        CharacterStats stats = GetLocalPlayerStats();
        if (stats != null)
            TrackSingleplayerStats(stats);

        if (!IsScoreboardKeyHeld())
        {
            CoopScoreboardUI.HideExisting();
            return;
        }

        if (stats == null)
        {
            CoopScoreboardUI.HideExisting();
            return;
        }

        rowBuffer.Clear();
        rowBuffer.Add(BuildRow(stats));
        CoopScoreboardUI.EnsureActive().SetRows(rowBuffer);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedStats = null;
        ResetCounters();
        UnsubscribeLocalStats();
        ClearObservedZombies();
        CoopScoreboardUI.HideExisting();
    }

    private CharacterStats GetLocalPlayerStats()
    {
        if (cachedStats != null && cachedStats.gameObject.scene.IsValid())
            return cachedStats;

        CharacterStats[] stats = FindObjectsOfType<CharacterStats>(true);
        for (int i = 0; i < stats.Length; i++)
        {
            if (stats[i] == null)
                continue;

            if (stats[i].GetComponentInParent<CoopNetworkIdentity>() != null)
                continue;

            cachedStats = stats[i];
            return cachedStats;
        }

        cachedStats = stats.Length > 0 ? stats[0] : null;
        return cachedStats;
    }

    private void TrackSingleplayerStats(CharacterStats stats)
    {
        SubscribeLocalStats(stats);

        if (Time.unscaledTime < nextZombieScanTime)
            return;

        nextZombieScanTime = Time.unscaledTime + ZombieScanInterval;
        RegisterCurrentZombies();
        RemoveMissingZombies();
    }

    private void SubscribeLocalStats(CharacterStats stats)
    {
        if (observedStats == stats)
            return;

        UnsubscribeLocalStats();
        observedStats = stats;

        if (observedStats != null)
            observedStats.OnDeath += HandleLocalDeath;
    }

    private void UnsubscribeLocalStats()
    {
        if (observedStats != null)
            observedStats.OnDeath -= HandleLocalDeath;

        observedStats = null;
    }

    private void RegisterCurrentZombies()
    {
        ZombieHealth[] zombies = FindObjectsOfType<ZombieHealth>(true);
        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieHealth zombie = zombies[i];
            if (zombie == null || observedZombies.ContainsKey(zombie))
                continue;

            ObservedZombie observed = new ObservedZombie();
            observed.damageHandler = (damage, hitPoint, hitNormal) => HandleZombieDamage(damage);
            observed.deathHandler = () => HandleZombieDeath(zombie);

            zombie.OnDamageTaken += observed.damageHandler;
            zombie.OnDeath += observed.deathHandler;
            observed.deathCounted = zombie.IsDead;
            observedZombies.Add(zombie, observed);
        }
    }

    private void RemoveMissingZombies()
    {
        zombieRemovalBuffer.Clear();
        foreach (KeyValuePair<ZombieHealth, ObservedZombie> pair in observedZombies)
        {
            if (pair.Key == null)
                zombieRemovalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < zombieRemovalBuffer.Count; i++)
            observedZombies.Remove(zombieRemovalBuffer[i]);
    }

    private void ClearObservedZombies()
    {
        foreach (KeyValuePair<ZombieHealth, ObservedZombie> pair in observedZombies)
        {
            if (pair.Key == null)
                continue;

            pair.Key.OnDamageTaken -= pair.Value.damageHandler;
            pair.Key.OnDeath -= pair.Value.deathHandler;
        }

        observedZombies.Clear();
        zombieRemovalBuffer.Clear();
    }

    private void HandleZombieDamage(float damage)
    {
        if (CoopSessionState.IsCoopSession)
            return;

        damageDealt += Mathf.Max(0f, damage);
    }

    private void HandleZombieDeath(ZombieHealth zombie)
    {
        if (CoopSessionState.IsCoopSession || zombie == null)
            return;

        if (!observedZombies.TryGetValue(zombie, out ObservedZombie observed) || observed.deathCounted)
            return;

        observed.deathCounted = true;
        zombieKills++;
    }

    private void HandleLocalDeath()
    {
        if (!CoopSessionState.IsCoopSession)
            deaths++;
    }

    private void ResetCounters()
    {
        nextZombieScanTime = 0f;
        zombieKills = 0;
        damageDealt = 0f;
        deaths = 0;
    }

    private CoopScoreboardUI.PlayerRowInfo BuildRow(CharacterStats stats)
    {
        int ownerId = stats.playerID > 0 ? stats.playerID : 1;
        int level = Mathf.Max(1, stats.playerLevel);
        int displayedDeaths = deaths + (stats.IsDead && deaths == 0 ? 1 : 0);
        int score = Mathf.Max(0, Mathf.RoundToInt(zombieKills * 100f + damageDealt * 0.2f + level * 25f - displayedDeaths * 100f));

        return new CoopScoreboardUI.PlayerRowInfo(
            ownerId,
            string.IsNullOrWhiteSpace(stats.playerName) ? "Player" : stats.playerName,
            true,
            ResolveStatus(stats),
            Mathf.CeilToInt(Mathf.Clamp(stats.currentHealth, 0f, Mathf.Max(1f, stats.MaxHealth))),
            Mathf.CeilToInt(Mathf.Max(1f, stats.MaxHealth)),
            Mathf.RoundToInt(Mathf.Clamp(stats.currentHunger, 0f, Mathf.Max(1f, stats.MaxHunger))),
            Mathf.RoundToInt(Mathf.Max(1f, stats.MaxHunger)),
            Mathf.RoundToInt(Mathf.Clamp(stats.currentThirst, 0f, Mathf.Max(1f, stats.MaxThirst))),
            Mathf.RoundToInt(Mathf.Max(1f, stats.MaxThirst)),
            level,
            Mathf.Max(0, stats.currentExp),
            Mathf.Max(0, stats.expToNextLevel),
            zombieKills,
            damageDealt,
            displayedDeaths,
            0,
            0,
            score);
    }

    private static string ResolveStatus(CharacterStats stats)
    {
        if (stats.IsDead || stats.currentHealth <= 0f)
            return "Мертв";

        float healthPercent = stats.MaxHealth > 0f ? stats.currentHealth / stats.MaxHealth : 0f;
        if (healthPercent <= 0.18f)
            return "При смерти";

        if (healthPercent <= 0.35f)
            return "Низкое HP";

        return "Жив";
    }

    private static bool IsMainMenuScene()
    {
        return SceneManager.GetActiveScene().name == MainMenuSceneName;
    }

    private static bool IsScoreboardKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.tabKey.isPressed;
#endif
        return Input.GetKey(KeyCode.Tab);
    }
}
