using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Bunker Manager")]
public class BunkerManager : MonoBehaviour
{
    [SerializeField] private string bunkerId = "main_bunker";
    [SerializeField] private BunkerStorage storage;
    [SerializeField] private List<LocationDefinition> locations = new();
    [SerializeField] private List<string> unlockedLocationIds = new();
    [SerializeField] private List<BuildableStation> stations = new();

    private static BunkerManager instance;

    public static BunkerManager Instance => instance;
    public IReadOnlyList<LocationDefinition> Locations => locations;
    public event Action OnBunkerStateChanged;

    private void Awake()
    {
        instance = this;
        ResolveReferences();
        UnlockDefaultLocations();
    }

    public bool IsLocationUnlocked(LocationDefinition location)
    {
        if (location == null)
            return false;

        if (location.isUnlockedByDefault)
            return true;

        string id = location.locationId;
        return !string.IsNullOrWhiteSpace(id) && unlockedLocationIds.Contains(id);
    }

    public void UnlockLocation(LocationDefinition location)
    {
        if (location == null || string.IsNullOrWhiteSpace(location.locationId))
            return;

        if (!unlockedLocationIds.Contains(location.locationId))
        {
            unlockedLocationIds.Add(location.locationId);
            OnBunkerStateChanged?.Invoke();
        }
    }

    public LocationDefinition GetFirstUnlockedLocation()
    {
        for (int i = 0; i < locations.Count; i++)
        {
            LocationDefinition location = locations[i];
            if (location != null && IsLocationUnlocked(location))
                return location;
        }

        return null;
    }

    public BunkerSaveData GetSaveData()
    {
        BunkerSaveData data = new BunkerSaveData
        {
            bunkerId = bunkerId,
            storage = storage != null ? storage.GetSaveData() : new InventorySaveData()
        };

        for (int i = 0; i < unlockedLocationIds.Count; i++)
        {
            string locationId = unlockedLocationIds[i];
            if (!string.IsNullOrWhiteSpace(locationId))
                data.unlockedLocations.Add(new UnlockedLocationSaveData { locationId = locationId, unlocked = true });
        }

        for (int i = 0; i < stations.Count; i++)
        {
            BuildableStation station = stations[i];
            if (station != null && station.Installed)
                data.installedStationIds.Add(station.StationId);
        }

        return data;
    }

    public void LoadFromSaveData(BunkerSaveData data)
    {
        if (data == null)
            return;

        if (!string.IsNullOrWhiteSpace(data.bunkerId))
            bunkerId = data.bunkerId;

        unlockedLocationIds.Clear();

        if (data.unlockedLocations != null)
        {
            for (int i = 0; i < data.unlockedLocations.Count; i++)
            {
                UnlockedLocationSaveData location = data.unlockedLocations[i];
                if (location != null && location.unlocked && !string.IsNullOrWhiteSpace(location.locationId))
                    unlockedLocationIds.Add(location.locationId);
            }
        }

        UnlockDefaultLocations();

        if (storage != null)
            storage.LoadFromSaveData(data.storage);

        if (data.installedStationIds != null && data.installedStationIds.Count > 0)
        {
            for (int i = 0; i < stations.Count; i++)
            {
                BuildableStation station = stations[i];
                if (station != null)
                    station.SetInstalled(data.installedStationIds.Contains(station.StationId));
            }
        }

        OnBunkerStateChanged?.Invoke();
    }

    private void ResolveReferences()
    {
        if (storage == null)
            storage = GetComponentInChildren<BunkerStorage>(true);

        if (stations.Count == 0)
            stations.AddRange(GetComponentsInChildren<BuildableStation>(true));
    }

    private void UnlockDefaultLocations()
    {
        for (int i = 0; i < locations.Count; i++)
        {
            LocationDefinition location = locations[i];
            if (location == null || !location.isUnlockedByDefault || string.IsNullOrWhiteSpace(location.locationId))
                continue;

            if (!unlockedLocationIds.Contains(location.locationId))
                unlockedLocationIds.Add(location.locationId);
        }
    }
}
