using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Crafting/Crafting Station")]
public class CraftingStation : MonoBehaviour
{
    [SerializeField] private CraftingStationType stationType = CraftingStationType.Workbench;
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private CraftingSystem craftingSystem;
    [SerializeField] private CraftingUI craftingUI;
    [SerializeField] private BuildableStation buildableStation;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool openOnPlayerTrigger;

    private bool playerInRange;

    public CraftingStationType StationType => ResolveStationType();
    public int CurrentLevel => ResolveStationLevel();

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            OpenCrafting();
    }

    public void OpenCrafting()
    {
        ResolveReferences();

        if (craftingUI != null)
            craftingUI.Open(craftingSystem, this);
        else
            Debug.LogWarning("CraftingStation has no CraftingUI.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInRange = true;

        if (openOnPlayerTrigger)
            OpenCrafting();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = false;
    }

    private void ResolveReferences()
    {
        if (buildableStation == null)
            buildableStation = GetComponent<BuildableStation>() ?? GetComponentInParent<BuildableStation>();

        if (craftingSystem == null)
            craftingSystem = FindObjectOfType<CraftingSystem>(true);

        if (craftingSystem == null)
            craftingSystem = gameObject.AddComponent<CraftingSystem>();

        if (craftingUI == null)
            craftingUI = FindObjectOfType<CraftingUI>(true);

        if (craftingUI == null)
            craftingUI = gameObject.AddComponent<CraftingUI>();
    }

    private CraftingStationType ResolveStationType()
    {
        if (buildableStation == null)
            return stationType;

        return buildableStation.StationType switch
        {
            StationType.Storage => CraftingStationType.Storage,
            StationType.Workbench => CraftingStationType.Workbench,
            StationType.Medical => CraftingStationType.MedicalStation,
            StationType.WeaponUpgrade => CraftingStationType.WeaponBench,
            StationType.Generator => CraftingStationType.Generator,
            StationType.Radio => CraftingStationType.RadioTerminal,
            _ => stationType
        };
    }

    private int ResolveStationLevel()
    {
        return buildableStation != null
            ? buildableStation.CurrentLevel
            : Mathf.Max(1, currentLevel);
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null &&
            (other.CompareTag("Player") || other.GetComponentInParent<CharacterStats>() != null);
    }
}
