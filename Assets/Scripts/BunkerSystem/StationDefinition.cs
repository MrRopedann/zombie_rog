using UnityEngine;

[CreateAssetMenu(fileName = "StationDefinition", menuName = "Bunker/Station Definition")]
public class StationDefinition : ScriptableObject
{
    public string stationId = "station";
    public string displayName = "Station";
    [TextArea(2, 5)] public string description;
    public StationType stationType = StationType.Workbench;
    public int requiredBunkerLevel = 1;
    public GameObject stationPrefab;
}
