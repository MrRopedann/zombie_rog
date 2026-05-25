using System;

public interface IObjective
{
    string MissionId { get; }
    string DisplayName { get; }
    string Description { get; }
    bool IsRequired { get; }
    ObjectiveState State { get; }
    int CurrentCount { get; }
    int TargetCount { get; }

    event Action<IObjective> OnProgressChanged;
    event Action<IObjective> OnCompleted;

    void Activate();
    void Deactivate();
    void Fail();
}
