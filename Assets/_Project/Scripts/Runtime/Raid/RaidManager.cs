using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Raid/Raid Manager")]
public class RaidManager : MonoBehaviour
{
    [SerializeField] private LocationDefinition selectedLocation;
    [SerializeField] private MissionDefinition selectedMission;
    [SerializeField] private ObjectiveManager objectiveManager;
    [SerializeField] private RaidStatsTracker statsTracker;
    [SerializeField] private RewardCalculator rewardCalculator;
    [SerializeField] private RaidResultUI resultUI;
    [SerializeField] private bool autoStartOnStart = true;

    private readonly List<string> completedMissionIds = new();
    private bool raidActive;

    private static RaidManager instance;

    public static RaidManager Instance => instance;
    public LocationDefinition SelectedLocation => selectedLocation;
    public MissionDefinition SelectedMission => selectedMission;

    public event Action<LocationDefinition, MissionDefinition> OnRaidStarted;
    public event Action<RaidResult> OnRaidCompleted;
    public event Action OnExtractionActivated;

    private void Awake()
    {
        instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        SubscribeObjectiveEvents();
    }

    private void OnDisable()
    {
        UnsubscribeObjectiveEvents();
    }

    private void Start()
    {
        if (!autoStartOnStart)
            return;

        LocationDefinition location = GameSessionState.SelectedLocation != null
            ? GameSessionState.SelectedLocation
            : selectedLocation;

        MissionDefinition mission = GameSessionState.SelectedMission != null
            ? GameSessionState.SelectedMission
            : selectedMission;

        StartRaid(location, mission);
    }

    public void StartRaid(LocationDefinition location, MissionDefinition mission = null)
    {
        if (raidActive)
            return;

        if (location == null)
            location = LoadDefaultLocation();

        if (location == null)
        {
            Debug.LogWarning("Cannot start raid: no location selected and no default LocationDefinition found in Resources/RuntimeLoadedOnly/Data/Raid.", this);
            return;
        }

        selectedLocation = location;
        selectedMission = mission != null ? mission : ResolveDefaultMission(location);
        completedMissionIds.Clear();
        ResolveReferences();
        SubscribeObjectiveEvents();
        DeactivateExtractionPoints();

        if (statsTracker != null)
            statsTracker.BeginTracking();

        List<MissionDefinition> missions = ResolveMissions(location, selectedMission);
        if (objectiveManager != null)
        {
            objectiveManager.StartObjectives(missions);
            if (objectiveManager.ShouldActivateExtractionImmediately())
                ActivateExtractionPoints();
        }

        raidActive = true;
        GameSessionState.SetMode(GameMode.Raid);
        OnRaidStarted?.Invoke(selectedLocation, selectedMission);

        // TODO: Later mirror this raid start through CoopGameplaySync for co-op sessions.
    }

    public void CompleteRaid(bool extractionSuccess)
    {
        if (!raidActive)
            return;

        raidActive = false;

        if (statsTracker != null)
        {
            statsTracker.SetExtractionSuccess(extractionSuccess);
            statsTracker.StopTracking();
        }

        RaidStatsSnapshot stats = statsTracker != null ? statsTracker.GetSnapshot() : new RaidStatsSnapshot();
        RaidResult result = new RaidResult
        {
            locationId = selectedLocation != null ? selectedLocation.locationId : string.Empty,
            locationName = selectedLocation != null ? selectedLocation.DisplayNameOrId : "Unknown location",
            extractionSuccess = extractionSuccess,
            difficulty = selectedLocation != null ? selectedLocation.difficulty : 0,
            baseExperienceReward = selectedLocation != null ? selectedLocation.baseExperienceReward : 0,
            stats = stats,
            completedMissionIds = new List<string>(completedMissionIds)
        };

        result.experienceEarned = rewardCalculator != null
            ? rewardCalculator.CalculateExperience(selectedLocation, stats)
            : 0;

        GameFlowManager.Instance.CompleteRaid(result);

        if (resultUI != null)
            resultUI.Show(result);

        OnRaidCompleted?.Invoke(result);

        // TODO: Later publish raid completion through CoopGameplaySync and distribute per-player results.
    }

    public void CompleteRaid(RaidResult result)
    {
        if (result == null)
            return;

        raidActive = false;
        GameFlowManager.Instance.CompleteRaid(result);
        OnRaidCompleted?.Invoke(result);
    }

    private void HandleObjectiveCompleted(IObjective objective)
    {
        if (objective == null)
            return;

        if (!string.IsNullOrWhiteSpace(objective.MissionId) && !completedMissionIds.Contains(objective.MissionId))
            completedMissionIds.Add(objective.MissionId);

        if (statsTracker != null)
            statsTracker.RecordObjectiveCompleted(objective.IsRequired);
    }

    private void HandleAllRequiredObjectivesCompleted()
    {
        ActivateExtractionPoints();
    }

    private void ActivateExtractionPoints()
    {
        ExtractionPoint[] points = FindObjectsOfType<ExtractionPoint>(true);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
                points[i].SetExtractionActive(true);
        }

        OnExtractionActivated?.Invoke();

        // TODO: Later sync extraction activation through CoopGameplaySync.
    }

    private void DeactivateExtractionPoints()
    {
        ExtractionPoint[] points = FindObjectsOfType<ExtractionPoint>(true);
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null)
                points[i].SetExtractionActive(false);
        }
    }

    private void ResolveReferences()
    {
        if (objectiveManager == null)
            objectiveManager = GetComponent<ObjectiveManager>() ?? FindObjectOfType<ObjectiveManager>(true);

        if (objectiveManager == null)
            objectiveManager = gameObject.AddComponent<ObjectiveManager>();

        if (statsTracker == null)
            statsTracker = GetComponent<RaidStatsTracker>() ?? gameObject.AddComponent<RaidStatsTracker>();

        if (rewardCalculator == null)
            rewardCalculator = GetComponent<RewardCalculator>() ?? gameObject.AddComponent<RewardCalculator>();

        if (resultUI == null)
            resultUI = FindObjectOfType<RaidResultUI>(true);
    }

    private void SubscribeObjectiveEvents()
    {
        if (objectiveManager == null)
            return;

        objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
        objectiveManager.OnAllRequiredObjectivesCompleted -= HandleAllRequiredObjectivesCompleted;
        objectiveManager.OnObjectiveCompleted += HandleObjectiveCompleted;
        objectiveManager.OnAllRequiredObjectivesCompleted += HandleAllRequiredObjectivesCompleted;
    }

    private void UnsubscribeObjectiveEvents()
    {
        if (objectiveManager == null)
            return;

        objectiveManager.OnObjectiveCompleted -= HandleObjectiveCompleted;
        objectiveManager.OnAllRequiredObjectivesCompleted -= HandleAllRequiredObjectivesCompleted;
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

    private static LocationDefinition LoadDefaultLocation()
    {
        LocationDefinition[] locations = Resources.LoadAll<LocationDefinition>("RuntimeLoadedOnly/Data/Raid");
        if (locations == null || locations.Length == 0)
            return null;

        for (int i = 0; i < locations.Length; i++)
        {
            LocationDefinition location = locations[i];
            if (location != null && location.isUnlockedByDefault)
                return location;
        }

        return locations[0];
    }

    private static List<MissionDefinition> ResolveMissions(LocationDefinition location, MissionDefinition fallbackMission)
    {
        List<MissionDefinition> missions = new List<MissionDefinition>();

        if (location != null && location.availableMissions != null)
        {
            for (int i = 0; i < location.availableMissions.Count; i++)
            {
                MissionDefinition mission = location.availableMissions[i];
                if (mission != null)
                    missions.Add(mission);
            }
        }

        if (missions.Count == 0 && fallbackMission != null)
            missions.Add(fallbackMission);

        return missions;
    }
}
