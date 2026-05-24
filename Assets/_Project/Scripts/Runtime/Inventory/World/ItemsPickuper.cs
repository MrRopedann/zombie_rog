using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemsPickuper : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField]
    private bool autoPickup = false;
    [SerializeField]
    private PlayerInventory playerInventory;
    [SerializeField]
    private InputsController inputs;

    private bool _canTryPickup = false;
    private WorldItem _pickupWorldItem = null;
    private bool _isPickingUp;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inputs != null)
        {
            inputs.OnUse += HandlePickup;
        }
    }

    private void OnDisable()
    {
        if (inputs != null)
        {
            inputs.OnUse -= HandlePickup;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        WorldItem item = other.GetComponent<WorldItem>() ?? other.GetComponentInParent<WorldItem>();

        if (item != null)
        {
            _pickupWorldItem = item;
            _canTryPickup = true;

            if (autoPickup)
                TryPickupItem();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        WorldItem item = other.GetComponent<WorldItem>() ?? other.GetComponentInParent<WorldItem>();

        if (item != null && item == _pickupWorldItem)
        {
            _pickupWorldItem = null;
            _canTryPickup = false;
        }
    }

    private void HandlePickup()
    {
        TryPickupItem();
    }

    private void TryPickupItem()
    {
        if (_isPickingUp)
            return;

        if (!_canTryPickup || _pickupWorldItem == null)
            return;

        _isPickingUp = true;

        var item = _pickupWorldItem;

        if (item.ItemData == null)
        {
            Debug.LogWarning($"World item {item.name} has no item data and cannot be picked up.", item);
            _isPickingUp = false;
            return;
        }

        if (CoopGameplaySync.TryPickupWorldItem(item, playerInventory))
        {
            _pickupWorldItem = null;
            _canTryPickup = false;
            _isPickingUp = false;
            return;
        }

        if (playerInventory == null || !playerInventory.AddItem(item.ItemData, item.Amount))
        {
            _isPickingUp = false;
            return;
        }

        _pickupWorldItem = null;
        _canTryPickup = false;
        CoopGameplaySync.NotifyWorldItemPickedUp(item);
        item.Pickup();

        _isPickingUp = false;
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
        {
            playerInventory = GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
        }

        if (inputs == null)
        {
            inputs = GetComponent<InputsController>() ?? GetComponentInParent<InputsController>();
        }
    }
}
