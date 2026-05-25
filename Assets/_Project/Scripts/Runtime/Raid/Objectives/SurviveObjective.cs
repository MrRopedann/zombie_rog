using UnityEngine;

public class SurviveObjective : ObjectiveBase
{
    private float elapsedSeconds;
    private int reportedSeconds;

    public SurviveObjective(MissionDefinition mission) : base(mission)
    {
    }

    public override int TargetCount => mission != null
        ? Mathf.Max(1, Mathf.RoundToInt(mission.surviveSeconds))
        : 1;

    public void Tick(float deltaTime)
    {
        if (State != ObjectiveState.Active || deltaTime <= 0f)
            return;

        elapsedSeconds += deltaTime;
        int wholeSeconds = Mathf.FloorToInt(elapsedSeconds);
        int deltaSeconds = wholeSeconds - reportedSeconds;
        if (deltaSeconds <= 0)
            return;

        reportedSeconds = wholeSeconds;
        AddProgress(deltaSeconds);
    }
}
