public class LootObjective : ObjectiveBase
{
    public LootObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterLoot(int amount)
    {
        AddProgress(amount);
    }
}
