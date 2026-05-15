using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum LootContainerOwnerType
{
    World,
    Player
}

public class LootContainer : MonoBehaviour
{
    [Serializable]
    public class LootEntry
    {
        public ItemSO item;
        [Range(0f, 1f)] public float spawnChance = 1f;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;

        public int RollAmount()
        {
            return UnityEngine.Random.Range(minAmount, maxAmount + 1);
        }
    }

    [Header("Container")]
    [SerializeField] private string containerName = "Сундук";
    [SerializeField] private string networkId;
    [SerializeField] private Rarity rarity = Rarity.Common;
    [SerializeField, Min(1)] private int maxSlots = 24;
    [SerializeField, Min(0.5f)] private float interactionRange = 3f;
    [SerializeField, Min(0f)] private float firstSearchDelay = 1.5f;
    [SerializeField] private bool createInteractionColliderIfMissing = true;
    [SerializeField, Min(0f)] private float interactionColliderPadding = 0.08f;

    [Header("Owner")]
    [SerializeField] private LootContainerOwnerType ownerType = LootContainerOwnerType.World;
    [SerializeField] private int ownerPlayerID = 0;

    [Header("Loot Table")]
    [SerializeField] private List<LootEntry> lootTable = new();

    [Header("Runtime")]
    [SerializeField] private bool wasSearched;

    private readonly List<InventorySlot> slots = new();
    private bool lootGenerated;

    public event Action InventoryChanged;

    public string ContainerName => string.IsNullOrWhiteSpace(containerName) ? name : containerName;
    public Rarity Rarity => rarity;
    public int MaxSlots => maxSlots;
    public float InteractionRange => interactionRange;
    public float FirstSearchDelay => firstSearchDelay;
    public bool WasSearched => wasSearched;
    public IReadOnlyList<InventorySlot> Slots => slots;
    public string NetworkId => ResolveNetworkId();

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            containerName = name;
        }

        EnsureInteractionCollider();
    }

    private void Awake()
    {
        EnsureInteractionCollider();
    }

    private void OnValidate()
    {
        maxSlots = Mathf.Max(1, maxSlots);
        interactionRange = Mathf.Max(0.5f, interactionRange);
        firstSearchDelay = Mathf.Max(0f, firstSearchDelay);
        interactionColliderPadding = Mathf.Max(0f, interactionColliderPadding);

        foreach (LootEntry entry in lootTable)
        {
            if (entry == null)
            {
                continue;
            }

            entry.minAmount = Mathf.Max(1, entry.minAmount);
            entry.maxAmount = Mathf.Max(entry.minAmount, entry.maxAmount);
        }
    }

    private void EnsureInteractionCollider()
    {
        if (!createInteractionColliderIfMissing || GetComponentInChildren<Collider>() != null)
        {
            return;
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        Bounds localBounds = CalculateLocalRendererBounds();
        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size + Vector3.one * interactionColliderPadding;
    }

    private Bounds CalculateLocalRendererBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        bool hasBounds = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;

            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(min.x, min.y, min.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(min.x, min.y, max.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(min.x, max.y, min.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(min.x, max.y, max.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(max.x, min.y, min.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(max.x, min.y, max.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(max.x, max.y, min.z));
            EncapsulateWorldPoint(ref localBounds, ref hasBounds, new Vector3(max.x, max.y, max.z));
        }

        return hasBounds ? localBounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private void EncapsulateWorldPoint(ref Bounds localBounds, ref bool hasBounds, Vector3 worldPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    public bool CanOpen(CharacterStats playerStats)
    {
        if (ownerType == LootContainerOwnerType.World)
        {
            return true;
        }

        return playerStats != null && playerStats.playerID == ownerPlayerID;
    }

    public bool RequiresSearchHold(CharacterStats playerStats)
    {
        return CanOpen(playerStats) &&
            ownerType == LootContainerOwnerType.World &&
            !wasSearched &&
            firstSearchDelay > 0f;
    }

    public string GetPromptText(CharacterStats playerStats)
    {
        string nameColor = ToHtmlColor(GetRarityTextColor(rarity));
        string searchedColor = wasSearched ? "55DD7A" : "A7A7A7";
        string searchedText = wasSearched ? "обыскивался" : "не обыскивался";

        if (!CanOpen(playerStats))
        {
            return $"<color=#{nameColor}>{ContainerName}</color> <color=#D95B5B>(чужой)</color>";
        }

        return $"<color=#{nameColor}>{ContainerName}</color> <color=#{searchedColor}>({searchedText})</color>";
    }

    public bool OpenFor(PlayerInventory playerInventory)
    {
        CharacterStats stats = playerInventory != null ? playerInventory.GetComponentInParent<CharacterStats>() : null;
        return OpenFor(playerInventory, stats);
    }

    public bool OpenFor(PlayerInventory playerInventory, CharacterStats playerStats)
    {
        if (playerInventory == null || !CanOpen(playerStats))
        {
            return false;
        }

        if (CoopGameplaySync.TryOpenRemoteLootContainer(this, playerInventory, playerStats))
        {
            return true;
        }

        GenerateLootIfNeeded();
        LootContainerUIController.Open(this, playerInventory);
        return true;
    }

    public bool OpenExistingFor(PlayerInventory playerInventory, CharacterStats playerStats)
    {
        if (playerInventory == null || !CanOpen(playerStats))
        {
            return false;
        }

        LootContainerUIController.Open(this, playerInventory);
        return true;
    }

    public InventorySlot GetSlot(int index)
    {
        return index >= 0 && index < slots.Count ? slots[index] : null;
    }

    public bool CanAddItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        int remaining = count;

        if (item.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item != item)
                {
                    continue;
                }

                remaining -= Mathf.Max(0, item.maxStack - slot.amount);

                if (remaining <= 0)
                {
                    return true;
                }
            }
        }

        int freeSlots = Mathf.Max(0, maxSlots - slots.Count);

        if (!item.isStackable)
        {
            return remaining <= freeSlots;
        }

        int stackSize = Mathf.Max(1, item.maxStack);
        int requiredSlots = Mathf.CeilToInt(remaining / (float)stackSize);
        return requiredSlots <= freeSlots;
    }

    public bool AddItem(ItemSO item, int count = 1)
    {
        if (!CanAddItem(item, count))
        {
            return false;
        }

        int remaining = count;

        if (item.isStackable)
        {
            foreach (InventorySlot slot in slots)
            {
                if (slot.item != item || slot.amount >= item.maxStack)
                {
                    continue;
                }

                int freeSpace = item.maxStack - slot.amount;
                int addAmount = Mathf.Min(freeSpace, remaining);
                slot.amount += addAmount;
                remaining -= addAmount;

                if (remaining <= 0)
                {
                    NotifyChanged();
                    return true;
                }
            }
        }

        while (remaining > 0 && slots.Count < maxSlots)
        {
            int addAmount = item.isStackable
                ? Mathf.Min(remaining, Mathf.Max(1, item.maxStack))
                : 1;

            slots.Add(new InventorySlot(item, addAmount));
            remaining -= addAmount;
        }

        NotifyChanged();
        return true;
    }

    public bool RemoveItem(ItemSO item, int count = 1)
    {
        if (item == null || count <= 0)
        {
            return false;
        }

        int remaining = count;

        foreach (InventorySlot slot in slots.ToList())
        {
            if (slot.item != item)
            {
                continue;
            }

            int removeAmount = Mathf.Min(slot.amount, remaining);
            slot.amount -= removeAmount;
            remaining -= removeAmount;

            if (slot.amount <= 0)
            {
                slots.Remove(slot);
            }

            if (remaining <= 0)
            {
                NotifyChanged();
                return true;
            }
        }

        if (remaining < count)
        {
            NotifyChanged();
        }

        return false;
    }

    public void SortSlots(InventorySortMode sortMode)
    {
        slots.Sort((left, right) => CompareSlots(left, right, sortMode));
        NotifyChanged();
    }

    public void GenerateLootForNetworkIfNeeded()
    {
        GenerateLootIfNeeded();
    }

    public void ClearNetworkState(bool searched)
    {
        lootGenerated = searched;
        wasSearched = searched;
        slots.Clear();
        NotifyChanged();
    }

    public void AddNetworkItem(ItemSO item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        AddItem(item, amount);
    }

    public static Color GetRarityTextColor(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Uncommon => new Color(0.38f, 0.92f, 0.48f),
            Rarity.Rare => new Color(0.36f, 0.62f, 1f),
            Rarity.Epic => new Color(0.78f, 0.42f, 1f),
            Rarity.Legendary => new Color(1f, 0.72f, 0.25f),
            _ => new Color(0.92f, 0.92f, 0.92f)
        };
    }

    private void GenerateLootIfNeeded()
    {
        if (lootGenerated)
        {
            return;
        }

        lootGenerated = true;
        wasSearched = true;
        slots.Clear();
        int addedEntries = 0;

        foreach (LootEntry entry in lootTable)
        {
            if (entry == null || entry.item == null || UnityEngine.Random.value > entry.spawnChance)
            {
                continue;
            }

            int amount = entry.RollAmount();

            if (AddItem(entry.item, amount))
            {
                addedEntries++;
            }
            else
            {
                Debug.LogWarning($"LootContainer could not add {amount}x {entry.item.itemName}. Check max slots and stack settings.", this);
            }
        }

        if (addedEntries == 0 && lootTable.Count > 0)
        {
            Debug.LogWarning($"LootContainer generated no loot from {lootTable.Count} loot entries. Check chances and item references.", this);
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        InventoryChanged?.Invoke();
    }

    private string ResolveNetworkId()
    {
        if (!string.IsNullOrWhiteSpace(networkId))
        {
            return networkId.Trim();
        }

        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Runtime";
        return $"{sceneName}/{BuildHierarchyPath(transform)}";
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        string path = $"{target.name}[{target.GetSiblingIndex()}]";
        Transform parent = target.parent;

        while (parent != null)
        {
            path = $"{parent.name}[{parent.GetSiblingIndex()}]/{path}";
            parent = parent.parent;
        }

        return path;
    }

    private static string ToHtmlColor(Color color)
    {
        return ColorUtility.ToHtmlStringRGB(color);
    }

    private static int CompareSlots(InventorySlot left, InventorySlot right, InventorySortMode sortMode)
    {
        bool leftEmpty = left == null || left.IsEmpty();
        bool rightEmpty = right == null || right.IsEmpty();

        if (leftEmpty && rightEmpty)
        {
            return 0;
        }

        if (leftEmpty)
        {
            return 1;
        }

        if (rightEmpty)
        {
            return -1;
        }

        return sortMode switch
        {
            InventorySortMode.Amount => right.amount.CompareTo(left.amount),
            InventorySortMode.Type => CompareByTypeThenName(left.item, right.item),
            _ => string.Compare(GetItemName(left.item), GetItemName(right.item), StringComparison.OrdinalIgnoreCase)
        };
    }

    private static int CompareByTypeThenName(ItemSO left, ItemSO right)
    {
        int typeCompare = left.itemType.CompareTo(right.itemType);
        return typeCompare != 0
            ? typeCompare
            : string.Compare(GetItemName(left), GetItemName(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetItemName(ItemSO item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName
            : string.Empty;
    }
}
