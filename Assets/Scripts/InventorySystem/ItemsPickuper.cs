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


    private void OnEnable()
    {
        inputs.OnUse += HandlePickup;
        inputs.OnOpenInventory += HandleOpenInventory;
    }

    private void OnDisable()
    {
        inputs.OnUse -= HandlePickup;
        inputs.OnOpenInventory -= HandleOpenInventory;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out WorldItem item))
        {
            _pickupWorldItem = item;
            _canTryPickup = true;

            if (autoPickup)
                TryPickupItem();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out WorldItem item) &&
            item == _pickupWorldItem)
        {
            _pickupWorldItem = null;
            _canTryPickup = false;
        }
    }

    private void HandlePickup()
    {
        TryPickupItem();
    }

    private void HandleOpenInventory()
    {
        playerInventory.ShowInventoryDebug();
    }

    private void TryPickupItem()
    {
        if (_isPickingUp)
            return;

        if (!_canTryPickup || _pickupWorldItem == null)
            return;

        _isPickingUp = true;

        var item = _pickupWorldItem;

        _pickupWorldItem = null;
        _canTryPickup = false;

        playerInventory.AddItem(item.ItemData);
        item.Pickup();

        _isPickingUp = false;
    }
}
