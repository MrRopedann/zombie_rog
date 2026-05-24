public class ExtractObjective : ObjectiveBase
{
    public ExtractObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterExtraction()
    {
        AddProgress(TargetCount);
    }
}
