using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Raid/Objective Manager")]
public class ObjectiveManager : MonoBehaviour
{
    private readonly List<IObjective> objectives = new();
    private bool allRequiredNotified;

    public IReadOnlyList<IObjective> Objectives => objectives;
    public IObjective ActiveObjective { get; private set; }

    public event Action<IObjective> OnObjectiveProgressChanged;
    public event Action<IObjective> OnObjectiveCompleted;
    public event Action OnAllRequiredObjectivesCompleted;

    private void Update()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i] is SurviveObjective surviveObjective)
                surviveObjective.Tick(Time.deltaTime);
        }
    }

    public void StartObjectives(IEnumerable<MissionDefinition> missions)
    {
        ClearObjectives();
        allRequiredNotified = false;

        if (missions == null)
            return;

        foreach (MissionDefinition mission in missions)
        {
            if (mission == null)
                continue;

            IObjective objective = CreateObjective(mission);
            RegisterObjective(objective);
        }

        for (int i = 0; i < objectives.Count; i++)
            objectives[i].Activate();

        ActiveObjective = FindFirstRequiredObjective() ?? (objectives.Count > 0 ? objectives[0] : null);
        OnObjectiveProgressChanged?.Invoke(ActiveObjective);

        if (!allRequiredNotified && AreAllRequiredObjectivesCompleted())
        {
            allRequiredNotified = true;
            OnAllRequiredObjectivesCompleted?.Invoke();
        }
    }

    public void ClearObjectives()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            IObjective objective = objectives[i];
            if (objective == null)
                continue;

            objective.OnProgressChanged -= HandleObjectiveProgressChanged;
            objective.OnCompleted -= HandleObjectiveCompleted;
            objective.Deactivate();
        }

        objectives.Clear();
        ActiveObjective = null;
        allRequiredNotified = false;
    }

    private void RegisterObjective(IObjective objective)
    {
        if (objective == null)
            return;

        objective.OnProgressChanged += HandleObjectiveProgressChanged;
        objective.OnCompleted += HandleObjectiveCompleted;
        objectives.Add(objective);
    }

    private void HandleObjectiveProgressChanged(IObjective objective)
    {
        if (objective != null && objective.IsRequired && objective.State != ObjectiveState.Completed)
            ActiveObjective = objective;

        OnObjectiveProgressChanged?.Invoke(objective);
    }

    private void HandleObjectiveCompleted(IObjective objective)
    {
        OnObjectiveCompleted?.Invoke(objective);

        if (ActiveObjective == objective)
            ActiveObjective = FindFirstRequiredObjective() ?? FindFirstActiveObjective();

        if (!allRequiredNotified && AreAllRequiredObjectivesCompleted())
        {
            allRequiredNotified = true;
            OnAllRequiredObjectivesCompleted?.Invoke();
        }
    }

    private IObjective FindFirstRequiredObjective()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            IObjective objective = objectives[i];
            if (objective != null && objective.IsRequired && objective.State == ObjectiveState.Active)
                return objective;
        }

        return null;
    }

    private IObjective FindFirstActiveObjective()
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            IObjective objective = objectives[i];
            if (objective != null && objective.State == ObjectiveState.Active)
                return objective;
        }

        return null;
    }

    private bool AreAllRequiredObjectivesCompleted()
    {
        bool hasRequired = false;

        for (int i = 0; i < objectives.Count; i++)
        {
            IObjective objective = objectives[i];
            if (objective == null || !objective.IsRequired)
                continue;

            hasRequired = true;
            if (objective.State != ObjectiveState.Completed)
                return false;
        }

        return hasRequired;
    }

    public bool ShouldActivateExtractionImmediately()
    {
        bool hasRequired = false;

        for (int i = 0; i < objectives.Count; i++)
        {
            IObjective objective = objectives[i];
            if (objective == null || !objective.IsRequired)
                continue;

            hasRequired = true;
            if (!(objective is ExtractObjective))
                return false;
        }

        return hasRequired;
    }

    private static IObjective CreateObjective(MissionDefinition mission)
    {
        return mission.missionType switch
        {
            MissionType.KillZombies => new KillObjective(mission),
            MissionType.LootItems => new LootObjective(mission),
            MissionType.Interact => new InteractObjective(mission),
            MissionType.Survive => new SurviveObjective(mission),
            MissionType.Extract => new ExtractObjective(mission),
            _ => new InteractObjective(mission)
        };
    }
}
