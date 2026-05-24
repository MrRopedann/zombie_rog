public class KillObjective : ObjectiveBase
{
    public KillObjective(MissionDefinition mission) : base(mission)
    {
    }

    protected override void Subscribe()
    {
        ZombieHealth.OnAnyZombieDied += HandleZombieDied;
    }

    protected override void Unsubscribe()
    {
        ZombieHealth.OnAnyZombieDied -= HandleZombieDied;
    }

    private void HandleZombieDied(ZombieHealth zombie)
    {
        AddProgress(1);
    }
}
