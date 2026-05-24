using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Buildable Station")]
public class BuildableStation : MonoBehaviour
{
    [SerializeField] private StationDefinition definition;
    [SerializeField] private int level = 1;
    [SerializeField] private bool installed = true;

    public StationDefinition Definition => definition;
    public string StationId => definition != null && !string.IsNullOrWhiteSpace(definition.stationId)
        ? definition.stationId
        : name;
    public int Level => Mathf.Max(1, level);
    public bool Installed => installed;

    public void SetInstalled(bool value)
    {
        installed = value;
        gameObject.SetActive(value);
    }
}
