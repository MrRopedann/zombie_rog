using System;
using UnityEngine;

public abstract class ObjectiveBase : IObjective
{
    protected readonly MissionDefinition mission;
    private ObjectiveState state = ObjectiveState.Inactive;
    private int currentCount;

    public string MissionId => mission != null ? mission.missionId : string.Empty;
    public string DisplayName => mission != null ? mission.DisplayNameOrId : "Objective";
    public string Description => mission != null ? mission.description : string.Empty;
    public bool IsRequired => mission == null || (mission.isRequired && !mission.optionalObjective);
    public ObjectiveState State => state;
    public int CurrentCount => currentCount;
    public virtual int TargetCount => mission != null ? Mathf.Max(1, mission.targetCount) : 1;

    public event Action<IObjective> OnProgressChanged;
    public event Action<IObjective> OnCompleted;

    protected ObjectiveBase(MissionDefinition mission)
    {
        this.mission = mission;
    }

    public virtual void Activate()
    {
        if (state == ObjectiveState.Completed)
            return;

        state = ObjectiveState.Active;
        Subscribe();
        RaiseProgressChanged();
    }

    public virtual void Deactivate()
    {
        Unsubscribe();

        if (state == ObjectiveState.Active)
            state = ObjectiveState.Inactive;
    }

    public virtual void Fail()
    {
        Unsubscribe();
        state = ObjectiveState.Failed;
        RaiseProgressChanged();
    }

    protected void AddProgress(int amount)
    {
        if (state != ObjectiveState.Active || amount <= 0)
            return;

        currentCount = Mathf.Clamp(currentCount + amount, 0, TargetCount);
        RaiseProgressChanged();

        if (currentCount >= TargetCount)
            Complete();
    }

    protected void Complete()
    {
        if (state == ObjectiveState.Completed)
            return;

        currentCount = TargetCount;
        state = ObjectiveState.Completed;
        Unsubscribe();
        RaiseProgressChanged();
        OnCompleted?.Invoke(this);
    }

    protected virtual void Subscribe()
    {
    }

    protected virtual void Unsubscribe()
    {
    }

    protected void RaiseProgressChanged()
    {
        OnProgressChanged?.Invoke(this);
    }
}
