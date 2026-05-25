public class ExtractObjective : ObjectiveBase
{
    public ExtractObjective(MissionDefinition mission) : base(mission)
    {
    }

    public void RegisterExtraction()
    {
        AddProgress(TargetCount);
    }

    protected override void Subscribe()
    {
        ExtractionPoint.OnAnyExtractionSucceeded += HandleExtractionSucceeded;
    }

    protected override void Unsubscribe()
    {
        ExtractionPoint.OnAnyExtractionSucceeded -= HandleExtractionSucceeded;
    }

    private void HandleExtractionSucceeded(ExtractionPoint extractionPoint)
    {
        RegisterExtraction();
    }
}
