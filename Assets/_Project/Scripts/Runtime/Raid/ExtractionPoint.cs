using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Raid/Extraction Point")]
public class ExtractionPoint : MonoBehaviour
{
    [SerializeField] private RaidManager raidManager;
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider triggerCollider;
    [SerializeField] private bool activeOnStart = false;

    public bool IsActive { get; private set; }
    public static event Action<ExtractionPoint> OnAnyExtractionSucceeded;

    private void Awake()
    {
        ResolveReferences();
        SetExtractionActive(activeOnStart);
    }

    public void SetExtractionActive(bool active)
    {
        IsActive = active;

        if (visualRoot != null)
            visualRoot.SetActive(active);

        if (triggerCollider != null)
            triggerCollider.enabled = active;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActive || !IsPlayer(other))
            return;

        ResolveReferences();
        OnAnyExtractionSucceeded?.Invoke(this);
        if (raidManager != null)
            raidManager.CompleteRaid(true);
    }

    private void ResolveReferences()
    {
        if (raidManager == null)
            raidManager = RaidManager.Instance ?? FindObjectOfType<RaidManager>(true);

        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        if (visualRoot == null)
            visualRoot = gameObject;
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null &&
            (other.CompareTag("Player") || other.GetComponentInParent<CharacterStats>() != null);
    }
}
