using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class RaidStatsSnapshot
{
    public int kills;
    public float damageDealt;
    public float damageTaken;
    public int itemsLooted;
    public int objectivesCompleted;
    public int requiredObjectivesCompleted;
    public int optionalObjectivesCompleted;
    public int alliesRevived;
    public float raidTime;
    public bool extractionSuccess;

    public RaidStatsSnapshot Clone()
    {
        return (RaidStatsSnapshot)MemberwiseClone();
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Raid/Raid Stats Tracker")]
public class RaidStatsTracker : MonoBehaviour
{
    private const int LocalSingleplayerId = 0;
    private const float PlayerScanInterval = 0.5f;

    private readonly RaidStatsSnapshot aggregateStats = new();
    private readonly Dictionary<int, RaidStatsSnapshot> statsByPlayerId = new();
    private readonly HashSet<CharacterStats> observedStats = new();
    private readonly HashSet<PlayerInventory> observedInventories = new();

    private bool tracking;
    private float startedAt;
    private float nextPlayerScanTime;

    public RaidStatsSnapshot CurrentStats => aggregateStats;

    private void OnEnable()
    {
        ZombieHealth.OnAnyZombieDied += HandleZombieDied;
        ZombieHealth.OnAnyZombieDamaged += HandleZombieDamaged;
    }

    private void OnDisable()
    {
        ZombieHealth.OnAnyZombieDied -= HandleZombieDied;
        ZombieHealth.OnAnyZombieDamaged -= HandleZombieDamaged;
        UnsubscribePlayers();
    }

    private void Update()
    {
        if (!tracking)
            return;

        aggregateStats.raidTime = Time.time - startedAt;

        if (Time.unscaledTime >= nextPlayerScanTime)
        {
            nextPlayerScanTime = Time.unscaledTime + PlayerScanInterval;
            SubscribeCurrentPlayers();
        }
    }

    public void BeginTracking()
    {
        ResetStats();
        tracking = true;
        startedAt = Time.time;
        SubscribeCurrentPlayers();
    }

    public void StopTracking()
    {
        tracking = false;
        aggregateStats.raidTime = Time.time - startedAt;
    }

    public void RecordObjectiveCompleted(bool required)
    {
        aggregateStats.objectivesCompleted++;

        if (required)
            aggregateStats.requiredObjectivesCompleted++;
        else
            aggregateStats.optionalObjectivesCompleted++;

        RaidStatsSnapshot playerStats = GetOrCreatePlayerStats(LocalSingleplayerId);
        playerStats.objectivesCompleted++;
        if (required)
            playerStats.requiredObjectivesCompleted++;
        else
            playerStats.optionalObjectivesCompleted++;
    }

    public void RecordAllyRevived(int playerId = LocalSingleplayerId)
    {
        aggregateStats.alliesRevived++;
        GetOrCreatePlayerStats(playerId).alliesRevived++;
    }

    public void SetExtractionSuccess(bool success)
    {
        aggregateStats.extractionSuccess = success;
        GetOrCreatePlayerStats(LocalSingleplayerId).extractionSuccess = success;
    }

    public RaidStatsSnapshot GetSnapshot()
    {
        aggregateStats.raidTime = tracking ? Time.time - startedAt : aggregateStats.raidTime;
        return aggregateStats.Clone();
    }

    public RaidStatsSnapshot GetSnapshotForPlayer(int playerId)
    {
        return statsByPlayerId.TryGetValue(playerId, out RaidStatsSnapshot stats)
            ? stats.Clone()
            : new RaidStatsSnapshot();
    }

    private void ResetStats()
    {
        aggregateStats.kills = 0;
        aggregateStats.damageDealt = 0f;
        aggregateStats.damageTaken = 0f;
        aggregateStats.itemsLooted = 0;
        aggregateStats.objectivesCompleted = 0;
        aggregateStats.requiredObjectivesCompleted = 0;
        aggregateStats.optionalObjectivesCompleted = 0;
        aggregateStats.alliesRevived = 0;
        aggregateStats.raidTime = 0f;
        aggregateStats.extractionSuccess = false;
        statsByPlayerId.Clear();
    }

    private void SubscribeCurrentPlayers()
    {
        CharacterStats[] statsList = FindObjectsOfType<CharacterStats>(true);
        for (int i = 0; i < statsList.Length; i++)
        {
            CharacterStats stats = statsList[i];
            if (stats == null || observedStats.Contains(stats))
                continue;

            observedStats.Add(stats);
            stats.OnDamaged += HandlePlayerDamaged;

            PlayerInventory inventory = stats.GetComponent<PlayerInventory>() ??
                stats.GetComponentInChildren<PlayerInventory>(true) ??
                stats.GetComponentInParent<PlayerInventory>();

            if (inventory != null && observedInventories.Add(inventory))
                inventory.ItemAdded += HandlePlayerItemAdded;
        }

        PlayerInventory[] inventories = FindObjectsOfType<PlayerInventory>(true);
        for (int i = 0; i < inventories.Length; i++)
        {
            PlayerInventory inventory = inventories[i];
            if (inventory != null && observedInventories.Add(inventory))
                inventory.ItemAdded += HandlePlayerItemAdded;
        }
    }

    private void UnsubscribePlayers()
    {
        foreach (CharacterStats stats in observedStats)
        {
            if (stats != null)
                stats.OnDamaged -= HandlePlayerDamaged;
        }

        foreach (PlayerInventory inventory in observedInventories)
        {
            if (inventory != null)
                inventory.ItemAdded -= HandlePlayerItemAdded;
        }

        observedStats.Clear();
        observedInventories.Clear();
    }

    private void HandleZombieDied(ZombieHealth zombie)
    {
        if (!tracking)
            return;

        aggregateStats.kills++;
        GetOrCreatePlayerStats(LocalSingleplayerId).kills++;
    }

    private void HandleZombieDamaged(ZombieHealth zombie, float damage)
    {
        if (!tracking || damage <= 0f)
            return;

        aggregateStats.damageDealt += damage;
        GetOrCreatePlayerStats(LocalSingleplayerId).damageDealt += damage;
    }

    private void HandlePlayerDamaged(float amount)
    {
        if (!tracking || amount <= 0f)
            return;

        aggregateStats.damageTaken += amount;
        GetOrCreatePlayerStats(LocalSingleplayerId).damageTaken += amount;
    }

    private void HandlePlayerItemAdded(ItemSO item, int amount)
    {
        if (!tracking || item == null || amount <= 0)
            return;

        aggregateStats.itemsLooted += amount;
        GetOrCreatePlayerStats(LocalSingleplayerId).itemsLooted += amount;
    }

    private RaidStatsSnapshot GetOrCreatePlayerStats(int playerId)
    {
        if (!statsByPlayerId.TryGetValue(playerId, out RaidStatsSnapshot stats))
        {
            stats = new RaidStatsSnapshot();
            statsByPlayerId[playerId] = stats;
        }

        return stats;
    }
}
