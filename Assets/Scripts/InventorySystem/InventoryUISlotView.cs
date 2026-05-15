using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum InventoryUISlotKind
{
    Inventory,
    Hotbar
}

public class InventoryUISlotView : UnityEngine.MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private InventoryUIController controller;
    private Image background;
    private Image icon;
    private Text fallbackText;
    private Text amountText;
    private bool isHovered;

    public InventoryUISlotKind SlotKind { get; private set; }
    public int Index { get; private set; }
    public ItemSO Item { get; private set; }

    public void Initialize(
        InventoryUIController owner,
        InventoryUISlotKind kind,
        int index,
        Image slotBackground,
        Image slotIcon,
        Text slotFallbackText,
        Text slotAmountText)
    {
        controller = owner;
        SlotKind = kind;
        Index = index;
        background = slotBackground;
        icon = slotIcon;
        fallbackText = slotFallbackText;
        amountText = slotAmountText;
        SetItem(null, 0);
    }

    public void SetItem(ItemSO item, int amount)
    {
        Item = item;

        bool hasItem = item != null;
        Sprite itemIcon = InventoryUIController.GetItemIconSprite(item);
        bool hasIcon = itemIcon != null;

        if (icon != null)
        {
            icon.enabled = hasIcon;
            icon.sprite = itemIcon;
        }

        if (fallbackText != null)
        {
            fallbackText.text = hasItem && !hasIcon && !string.IsNullOrWhiteSpace(item.itemName)
                ? item.itemName.Substring(0, 1).ToUpperInvariant()
                : string.Empty;
        }

        if (amountText != null)
        {
            amountText.text = hasItem && amount > 1 ? amount.ToString() : string.Empty;
        }

        RefreshBackground();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        controller?.BeginDrag(this, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        controller?.UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        controller?.EndDrag();
    }

    public void OnDrop(PointerEventData eventData)
    {
        controller?.DropOnSlot(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            controller?.OpenContextMenu(this, eventData.position);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            controller?.HideContextMenu();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshBackground();
        controller?.ShowItemTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshBackground();
        controller?.HideItemTooltip(this);
    }

    private void RefreshBackground()
    {
        if (background == null)
        {
            return;
        }

        Color color = Item != null
            ? InventoryUIController.GetRaritySlotColor(Item.rarity, SlotKind)
            : InventoryUIController.GetEmptySlotColor(SlotKind);

        if (isHovered)
        {
            color = Color.Lerp(color, Color.white, Item != null ? 0.28f : 0.18f);
            color.a = 1f;
        }

        background.color = color;
    }
}
