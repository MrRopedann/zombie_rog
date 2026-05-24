public class InteractObjective : ObjectiveBase
{
    public InteractObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterInteraction()
    {
        AddProgress(1);
    }
}
