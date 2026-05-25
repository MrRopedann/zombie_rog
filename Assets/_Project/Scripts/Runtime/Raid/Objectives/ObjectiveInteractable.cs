using System;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/Raid/Objective Interactable")]
public class ObjectiveInteractable : MonoBehaviour
{
    [SerializeField] private string interactableId = "objective_interactable";
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private bool requirePlayerInTrigger = true;

    private bool playerInRange;

    public string InteractableId => string.IsNullOrWhiteSpace(interactableId) ? name : interactableId.Trim();

    public static event Action<ObjectiveInteractable> OnAnyInteracted;

    private void Update()
    {
        if (requirePlayerInTrigger && !playerInRange)
            return;

        if (Input.GetKeyDown(interactKey))
            Interact();
    }

    public void Interact()
    {
        OnAnyInteracted?.Invoke(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
            playerInRange = false;
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null &&
            (other.CompareTag("Player") || other.GetComponentInParent<CharacterStats>() != null);
    }
}
