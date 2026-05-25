public static class GameSessionState
{
    public static GameMode CurrentMode { get; private set; } = GameMode.MainMenu;
    public static LocationDefinition SelectedLocation { get; private set; }
    public static MissionDefinition SelectedMission { get; private set; }
    public static RaidResult LastRaidResult { get; private set; }

    public static void SetMode(GameMode mode)
    {
        CurrentMode = mode;
    }

    public static void SetSelectedRaid(LocationDefinition location, MissionDefinition mission)
    {
        SelectedLocation = location;
        SelectedMission = mission;
        LastRaidResult = null;
    }

    public static void StoreRaidResult(RaidResult result)
    {
        LastRaidResult = result;
        CurrentMode = GameMode.RaidResult;
    }

    public static void ClearRaid()
    {
        SelectedLocation = null;
        SelectedMission = null;
        LastRaidResult = null;
    }
}
