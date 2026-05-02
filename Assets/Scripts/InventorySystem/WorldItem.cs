using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldItem : MonoBehaviour
{
    [Header("Item Settings")]
    [SerializeField]
    private ItemSO itemData;
    [SerializeField]
    private float destroyDelay = 1.0f;

    [Header("Effects Settings")]
    [SerializeField]
    private ParticleSystem pickupParticles;
    [SerializeField]
    private AudioClip pickupSounds;

    public ItemSO ItemData => itemData;

    private bool _isPickedUp;

    private void Start()
    {
        
    }

    private void PlayPickupEffect()
    {
        if (pickupParticles != null)
        {
            Instantiate(pickupParticles, transform.position, Quaternion.identity).Play();
        }

        if (pickupSounds != null)
        {
            AudioSource.PlayClipAtPoint(pickupSounds, transform.position);
        }
    }

    public void Pickup()
    {
        if (_isPickedUp)
            return;

        _isPickedUp = true;

        // отключаем коллайдер сразу
        var col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        PlayPickupEffect();
        Destroy(gameObject, destroyDelay);
    }

    public void Setup(ItemSO newItemData = null)
    { 
        if(newItemData != null)
            itemData = newItemData;

        if (itemData != null && itemData.worldPrefab == null)
        { 
            // ћожно добавить логику изменени€ меша/матерь€ла здесь
        }
    }


}
