using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Save System/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [SerializeField] private List<ItemSO> items = new();

    private Dictionary<string, ItemSO> cache;

    public ItemSO Resolve(string itemId)
    {
        EnsureCache();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        return cache.TryGetValue(itemId.Trim(), out ItemSO item) ? item : null;
    }

    public static ItemSO ResolveFromResources(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        string key = itemId.Trim();
        ItemSO[] loadedItems = Resources.LoadAll<ItemSO>(string.Empty);
        for (int i = 0; i < loadedItems.Length; i++)
        {
            ItemSO item = loadedItems[i];
            if (item == null)
                continue;

            if (Matches(item, key))
                return item;
        }

        return null;
    }

    private void EnsureCache()
    {
        if (cache != null)
            return;

        cache = new Dictionary<string, ItemSO>();
        for (int i = 0; i < items.Count; i++)
        {
            Register(items[i]);
        }
    }

    private void Register(ItemSO item)
    {
        if (item == null)
            return;

        RegisterKey(item.itemID, item);
        RegisterKey(item.name, item);
    }

    private void RegisterKey(string key, ItemSO item)
    {
        if (string.IsNullOrWhiteSpace(key) || item == null)
            return;

        cache[key.Trim()] = item;
    }

    private static bool Matches(ItemSO item, string key)
    {
        if (item == null)
            return false;

        return string.Equals(item.itemID, key, System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.name, key, System.StringComparison.OrdinalIgnoreCase);
    }

    private void OnValidate()
    {
        cache = null;
    }
}
