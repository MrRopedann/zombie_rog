using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Progress;

public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    private bool isViewDebug = false;

    private List<InventorySlot> slots = new();

    public void AddItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
            return;

        if (item.isStackable)
        {
            foreach (var slot in slots)
            {
                if (slot.item == item && slot.amount < item.maxStack)
                {
                    int freeSpace = item.maxStack - slot.amount;
                    int addAmount = Mathf.Min(freeSpace, count);

                    slot.amount += addAmount;
                    count -= addAmount;

                    if (count <= 0)
                        return;
                }
            }
        }

        while (count > 0)
        {
            int addAmount = item.isStackable
                ? Mathf.Min(count, item.maxStack)
                : 1;

            slots.Add(new InventorySlot(item, addAmount));

            count -= addAmount;
        }

        if (isViewDebug)
            Debug.Log($"Предмет {item.itemName} добавлен в инвентарь.");
    }

    public void RemoveItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
            return;

        foreach (var slot in slots.ToList())
        {
            if (slot.item == item)
            {
                // если в слоте хватает предметов
                if (slot.amount >= count)
                {
                    slot.amount -= count;

                    // если стак опустел — удаляем слот
                    if (slot.amount <= 0)
                    {
                        slots.Remove(slot);
                    }

                    if (isViewDebug)
                    {
                        Debug.Log($"Предмет {item.itemName} удален из инвентаря.");
                    }

                    return;
                }
                else
                {
                    // если в слоте меньше, чем нужно удалить
                    count -= slot.amount;
                    slots.Remove(slot);
                }
            }
        }

        if (isViewDebug)
        {
            Debug.LogWarning($"Недостаточно предметов {item.itemName} для удаления.");
        }
    }


    /// <summary>
    /// Получить первый доступный для использования предмет указанного типа
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public ItemSO GetUsableItemOfType(ItemType itemType)
    {
        foreach (var slot in slots)
        {
            if (slot.item != null &&
                slot.item.itemType == itemType &&
                slot.item.isUsable)
            {
                return slot.item;
            }
        }

        return null;
    }

    /// <summary>
    /// Получить предметы доступный для использования предмет указанного типа
    /// </summary>
    /// <param name="itemType"></param>
    /// <returns></returns>
    public List<InventorySlot> GetUsableSlotsOfType(ItemType itemType)
    {
        List<InventorySlot> result = new();

        foreach (var slot in slots)
        {
            if (slot.item != null &&
                slot.item.itemType == itemType &&
                slot.item.isUsable)
            {
                result.Add(slot);
            }
        }

        return result;
    }

    public void DropItem(ItemSO item, Vector3 position, Quaternion rotation, int count = 1)
    {
        if (item == null || count <= 0)
            return;

        foreach (var slot in slots.ToList())
        {
            if (slot.item == item)
            {
                // Сначала пытаемся создать предмет в мире
                bool dropped = item.TryDrop(position, rotation);

                if (!dropped)
                {
                    if (isViewDebug)
                    {
                        Debug.LogWarning($"Не удалось выбросить предмет {item.itemName}");
                    }
                    return;
                }

                // Если предмет успешно выброшен — удаляем из инвентаря
                if (slot.amount >= count)
                {
                    slot.amount -= count;

                    if (slot.amount <= 0)
                    {
                        slots.Remove(slot);
                    }

                    if (isViewDebug)
                    {
                        Debug.Log($"Предмет {item.itemName} выброшен из инвентаря.");
                    }

                    return;
                }
                else
                {
                    count -= slot.amount;
                    slots.Remove(slot);
                }
            }
        }

        if (isViewDebug)
        {
            Debug.LogWarning($"Попытка выбросить предмет {item.itemName}, которого недостаточно в инвентаре.");
        }
    }

    public float GetCurrentWeight()
    {
        float totalWeight = 0f;

        foreach (var slot in slots)
        {
            if (slot.item != null)
            {
                totalWeight += slot.item.weight * slot.amount;
            }
        }

        return totalWeight;
    }

    public void ShowInventoryDebug()
    {
        if (!isViewDebug)
            return;

        Debug.Log("Содержимое инвентаря:");

        if (slots.Count == 0)
        {
            Debug.Log("Инвентарь пуст.");
            return;
        }

        foreach (var slot in slots)
        {
            if (slot.item == null)
                continue;

            Debug.Log(
                $"- {slot.item.itemName} " +
                $"x{slot.amount} " +
                $"(ID: {slot.item.itemID}) " +
                $"(Type: {slot.item.itemType}) " +
                $"(Weight: {slot.item.weight * slot.amount})"
            );
        }

        Debug.Log($"Общий вес: {GetCurrentWeight()}");
    }

}
