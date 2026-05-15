using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum LootInventorySide
{
    Container,
    Player
}

public class LootContainerSlotView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private LootContainerUIController controller;
    private Image background;
    private Image icon;
    private Text fallbackText;
    private Text amountText;
    private bool isHovered;

    public LootInventorySide Side { get; private set; }
    public int Index { get; private set; }
    public ItemSO Item { get; private set; }
    public int Amount { get; private set; }

    public void Initialize(
        LootContainerUIController owner,
        LootInventorySide side,
        int index,
        Image slotBackground,
        Image slotIcon,
        Text slotFallbackText,
        Text slotAmountText)
    {
        controller = owner;
        Side = side;
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
        Amount = Mathf.Max(0, amount);

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
        if (eventData.button == PointerEventData.InputButton.Left && LootContainerUIController.IsShiftPressed())
        {
            controller?.TransferStack(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshBackground();
        controller?.ShowTooltip(this, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshBackground();
        controller?.HideTooltip(this);
    }

    private void RefreshBackground()
    {
        if (background == null)
        {
            return;
        }

        Color color = Item != null
            ? InventoryUIController.GetRaritySlotColor(Item.rarity, InventoryUISlotKind.Inventory)
            : InventoryUIController.GetEmptySlotColor(InventoryUISlotKind.Inventory);

        if (isHovered)
        {
            color = Color.Lerp(color, Color.white, Item != null ? 0.28f : 0.18f);
            color.a = 1f;
        }

        background.color = color;
    }
}
