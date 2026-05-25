public class InteractObjective : ObjectiveBase
{
    public InteractObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterInteraction()
    {
        AddProgress(1);
    }

    protected override void Subscribe()
    {
        ObjectiveInteractable.OnAnyInteracted += HandleInteracted;
    }

    protected override void Unsubscribe()
    {
        ObjectiveInteractable.OnAnyInteracted -= HandleInteracted;
    }

    private void HandleInteracted(ObjectiveInteractable interactable)
    {
        if (interactable == null || !MatchesRequiredInteractable(interactable))
            return;

        RegisterInteraction();
    }

    private bool MatchesRequiredInteractable(ObjectiveInteractable interactable)
    {
        if (mission == null || string.IsNullOrWhiteSpace(mission.targetInteractableId))
            return true;

        return string.Equals(
            interactable.InteractableId,
            mission.targetInteractableId.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }
}
