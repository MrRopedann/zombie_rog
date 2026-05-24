using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Bunker/Bunker Terminal")]
public class BunkerTerminal : MonoBehaviour
{
    [SerializeField] private BunkerManager bunkerManager;
    [SerializeField] private LocationSelectionUI locationSelectionUI;
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool openOnPlayerTrigger = false;

    private bool playerInRange;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            OpenLocationSelection();
    }

    public void OpenLocationSelection()
    {
        ResolveReferences();

        if (locationSelectionUI == null)
        {
            Debug.LogWarning("BunkerTerminal has no LocationSelectionUI.", this);
            return;
        }

        locationSelectionUI.Open(bunkerManager);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerInRange = true;

        if (openOnPlayerTrigger)
            OpenLocationSelection();
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = false;
    }

    private void ResolveReferences()
    {
        if (bunkerManager == null)
            bunkerManager = BunkerManager.Instance ?? FindObjectOfType<BunkerManager>(true);

        if (locationSelectionUI == null)
            locationSelectionUI = FindObjectOfType<LocationSelectionUI>(true);
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null &&
            (other.CompareTag("Player") || other.GetComponentInParent<CharacterStats>() != null);
    }
}
