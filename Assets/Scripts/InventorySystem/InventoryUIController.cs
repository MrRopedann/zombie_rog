using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public class InventoryUIController : MonoBehaviour
{
    private const int InventorySlotCount = 32;
    private const int HotbarSlotCount = 10;

#if UNITY_EDITOR
    private static readonly Dictionary<UnityEngine.Object, Sprite> editorPreviewIconCache = new();
#endif

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private InputsController inputs;

    private readonly InventoryUISlotView[] inventorySlotViews = new InventoryUISlotView[InventorySlotCount];
    private readonly InventoryUISlotView[] hotbarSlotViews = new InventoryUISlotView[HotbarSlotCount];
    private readonly ItemSO[] hotbarItems = new ItemSO[HotbarSlotCount];

    private GameObject inventoryPanel;
    private RectTransform dragIconRoot;
    private Image dragIcon;
    private Text dragFallbackText;
    private RectTransform contextMenuRoot;
    private RectTransform tooltipRoot;
    private Text tooltipTitleText;
    private Text tooltipDescriptionText;
    private Text tooltipStatsText;
    private Text weightText;
    private InventoryUISlotView hoveredTooltipSlot;
    private int contextMenuOpenedFrame = -1;
    private ItemSO draggedItem;
    private InventoryUISlotView draggedSlot;
    private bool isOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorInputForLook;
    private bool previousShootingInputBlocked;
    private bool cursorGuardActive;

    private void Awake()
    {
        ResolveReferences();
        EnsureEventSystem();
        BuildUI();
        Refresh();
        SetInventoryOpen(false);
    }

    private void Update()
    {
        if (isOpen)
        {
            GameCursorGuard.ApplyUiCursor();
        }

#if UNITY_EDITOR
        if (isOpen && AssetPreview.IsLoadingAssetPreviews())
        {
            Refresh();
        }
#endif

        if (isOpen && tooltipRoot != null && tooltipRoot.gameObject.activeSelf)
        {
            tooltipRoot.position = ClampTooltipPosition(GetPointerPosition());
        }

        if (!isOpen ||
            contextMenuRoot == null ||
            !contextMenuRoot.gameObject.activeSelf ||
            Time.frameCount == contextMenuOpenedFrame ||
            !WasPointerPressedThisFrame())
        {
            return;
        }

        Vector2 pointerPosition = GetPointerPosition();

        if (!RectTransformUtility.RectangleContainsScreenPoint(contextMenuRoot, pointerPosition))
        {
            HideContextMenu();
        }
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inventory != null)
        {
            inventory.InventoryChanged += Refresh;
        }

        if (inputs != null)
        {
            inputs.OnOpenInventory += ToggleInventory;
            inputs.OnHotbarSlot += UseHotbarSlot;
        }
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= Refresh;
        }

        if (inputs != null)
        {
            inputs.OnOpenInventory -= ToggleInventory;
            inputs.OnHotbarSlot -= UseHotbarSlot;
        }

        if (isOpen)
        {
            RestoreCursorState();
        }
    }

    public void BeginDrag(InventoryUISlotView slotView, Vector2 screenPosition)
    {
        ItemSO item = slotView != null ? slotView.Item : null;

        if (item == null)
        {
            return;
        }

        HideContextMenu();
        draggedSlot = slotView;
        draggedItem = item;

        if (dragIconRoot == null)
        {
            return;
        }

        dragIconRoot.gameObject.SetActive(true);
        dragIconRoot.position = screenPosition;
        SetIcon(dragIcon, dragFallbackText, item);
    }

    public void UpdateDragPosition(Vector2 screenPosition)
    {
        if (dragIconRoot != null && dragIconRoot.gameObject.activeSelf)
        {
            dragIconRoot.position = screenPosition;
        }
    }

    public void EndDrag()
    {
        draggedItem = null;
        draggedSlot = null;

        if (dragIconRoot != null)
        {
            dragIconRoot.gameObject.SetActive(false);
        }
    }

    public void DropOnSlot(InventoryUISlotView targetSlot)
    {
        if (targetSlot == null || draggedItem == null)
        {
            return;
        }

        if (targetSlot.SlotKind == InventoryUISlotKind.Hotbar)
        {
            if (TryAssignHotbarItem(targetSlot.Index, draggedItem) &&
                draggedSlot != null &&
                draggedSlot.SlotKind == InventoryUISlotKind.Hotbar &&
                draggedSlot.Index != targetSlot.Index)
            {
                hotbarItems[draggedSlot.Index] = null;
                RefreshHotbar();
            }

            return;
        }

        if (targetSlot.SlotKind == InventoryUISlotKind.Inventory &&
            draggedSlot != null &&
            draggedSlot.SlotKind == InventoryUISlotKind.Hotbar)
        {
            hotbarItems[draggedSlot.Index] = null;
            RefreshHotbar();
        }
    }

    public void OpenContextMenu(InventoryUISlotView slotView, Vector2 screenPosition)
    {
        if (slotView == null || slotView.Item == null || contextMenuRoot == null)
        {
            HideContextMenu();
            return;
        }

        ItemSO item = slotView.Item;
        HideContextMenu();

        if (PlayerInventory.CanUseItem(item))
        {
            AddContextMenuButton("Использовать", () => UseContextItem(item));
        }

        AddContextMenuButton("Выбросить", () => DropContextItem(item));
        AddContextMenuButton("Удалить", () => DeleteContextItem(item));

        LayoutRebuilder.ForceRebuildLayoutImmediate(contextMenuRoot);
        contextMenuRoot.gameObject.SetActive(true);
        contextMenuRoot.position = ClampContextMenuPosition(screenPosition);
        contextMenuOpenedFrame = Time.frameCount;
    }

    public void HideContextMenu()
    {
        if (contextMenuRoot == null)
        {
            return;
        }

        for (int i = contextMenuRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contextMenuRoot.GetChild(i).gameObject);
        }

        contextMenuRoot.gameObject.SetActive(false);
    }

    public void ShowItemTooltip(InventoryUISlotView slotView, Vector2 screenPosition)
    {
        if (slotView == null || slotView.Item == null || tooltipRoot == null)
        {
            HideItemTooltip(slotView);
            return;
        }

        hoveredTooltipSlot = slotView;
        ItemSO item = slotView.Item;

        tooltipTitleText.text = string.IsNullOrWhiteSpace(item.itemName) ? "Предмет" : item.itemName;
        tooltipDescriptionText.text = string.IsNullOrWhiteSpace(item.description) ? "Описание отсутствует." : item.description;
        tooltipStatsText.text = BuildTooltipStats(item);

        tooltipRoot.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
        tooltipRoot.position = ClampTooltipPosition(screenPosition);
    }

    public void HideItemTooltip(InventoryUISlotView slotView)
    {
        if (hoveredTooltipSlot != null && slotView != null && hoveredTooltipSlot != slotView)
        {
            return;
        }

        hoveredTooltipSlot = null;

        if (tooltipRoot != null)
        {
            tooltipRoot.gameObject.SetActive(false);
        }
    }

    public static Sprite GetItemIconSprite(ItemSO item)
    {
        if (item == null)
        {
            return null;
        }

        if (item.icon != null)
        {
            return item.icon;
        }

#if UNITY_EDITOR
        return Application.isPlaying ? null : GetEditorPrefabPreviewSprite(item.worldPrefab);
#else
        return null;
#endif
    }

    private void ToggleInventory()
    {
        SetInventoryOpen(!isOpen);
    }

    private void SetInventoryOpen(bool open)
    {
        if (inventoryPanel == null)
        {
            return;
        }

        if (isOpen == open)
        {
            inventoryPanel.SetActive(open);
            return;
        }

        isOpen = open;
        inventoryPanel.SetActive(open);

        if (open)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockState = Cursor.lockState;
            previousCursorInputForLook = inputs == null || inputs.cursorInputForLook;
            previousShootingInputBlocked = inputs != null && inputs.ShootingInputBlocked;
            ActivateCursorGuard();

            if (inputs != null)
            {
                inputs.cursorInputForLook = false;
                inputs.SetShootingInputBlocked(true);
            }
        }
        else
        {
            RestoreCursorState();
            EndDrag();
            HideContextMenu();
            HideItemTooltip(null);
        }
    }

    private void RestoreCursorState()
    {
        DeactivateCursorGuard();

        if (!GameCursorGuard.IsUiCursorRequested)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
        }
        else
        {
            GameCursorGuard.ApplyUiCursor();
        }

        if (inputs != null)
        {
            inputs.cursorInputForLook = previousCursorInputForLook;
            inputs.SetShootingInputBlocked(previousShootingInputBlocked);
        }
    }

    private void ActivateCursorGuard()
    {
        if (cursorGuardActive)
        {
            GameCursorGuard.ApplyUiCursor();
            return;
        }

        cursorGuardActive = true;
        GameCursorGuard.PushUiCursor();
    }

    private void DeactivateCursorGuard()
    {
        if (!cursorGuardActive)
            return;

        cursorGuardActive = false;
        GameCursorGuard.PopUiCursor();
    }

    private bool TryAssignHotbarItem(int hotbarIndex, ItemSO item)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItems.Length || item == null)
        {
            return false;
        }

        if (!PlayerInventory.CanUseItem(item))
        {
            Debug.Log($"Item cannot be placed on hotbar: {item.itemName}", this);
            return false;
        }

        if (inventory != null && !inventory.ContainsItem(item))
        {
            return false;
        }

        hotbarItems[hotbarIndex] = item;
        RefreshHotbar();
        return true;
    }

    private void UseContextItem(ItemSO item)
    {
        if (inventory == null || item == null)
        {
            return;
        }

        inventory.TryUseItem(item, true);
    }

    private void DropContextItem(ItemSO item)
    {
        if (inventory == null || item == null)
        {
            return;
        }

        inventory.DropItem(item, GetDropPosition(), GetDropRotation());
    }

    private void DeleteContextItem(ItemSO item)
    {
        if (inventory == null || item == null)
        {
            return;
        }

        inventory.RemoveItem(item);
    }

    private Vector3 GetDropPosition()
    {
        Transform dropOrigin = inventory != null ? inventory.transform : transform;
        return dropOrigin.position + dropOrigin.forward * 1.2f + Vector3.up * 0.35f;
    }

    private Quaternion GetDropRotation()
    {
        Transform dropOrigin = inventory != null ? inventory.transform : transform;
        return Quaternion.LookRotation(dropOrigin.forward, Vector3.up);
    }

    private void UseHotbarSlot(int hotbarIndex)
    {
        if (hotbarIndex < 0 || hotbarIndex >= hotbarItems.Length)
        {
            return;
        }

        ItemSO item = hotbarItems[hotbarIndex];

        if (item == null || inventory == null)
        {
            return;
        }

        if (!inventory.TryUseItem(item, true))
        {
            hotbarItems[hotbarIndex] = null;
            RefreshHotbar();
        }
    }

    private void Refresh()
    {
        RefreshInventoryGrid();
        RefreshHotbar();
        RefreshWeightText();

        if (hoveredTooltipSlot != null && hoveredTooltipSlot.Item != null)
        {
            ShowItemTooltip(hoveredTooltipSlot, GetPointerPosition());
        }
        else
        {
            HideItemTooltip(null);
        }
    }

    private void RefreshInventoryGrid()
    {
        for (int i = 0; i < inventorySlotViews.Length; i++)
        {
            InventorySlot slot = inventory != null ? inventory.GetSlot(i) : null;
            inventorySlotViews[i].SetItem(slot != null ? slot.item : null, slot != null ? slot.amount : 0);
        }
    }

    private void RefreshHotbar()
    {
        for (int i = 0; i < hotbarItems.Length; i++)
        {
            ItemSO item = hotbarItems[i];

            if (item != null && (inventory == null || !inventory.ContainsItem(item)))
            {
                item = null;
                hotbarItems[i] = null;
            }

            hotbarSlotViews[i].SetItem(item, item != null && inventory != null ? inventory.GetItemAmount(item) : 0);
        }
    }

    private void ResolveReferences()
    {
        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>() ?? GetComponentInParent<PlayerInventory>();
        }

        if (inputs == null)
        {
            inputs = GetComponent<InputsController>() ?? GetComponentInParent<InputsController>();
        }
    }

    private void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("Inventory UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform.root, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        inventoryPanel = CreatePanel("Inventory Panel", canvasRect, new Color(0.04f, 0.045f, 0.05f, 0.88f));
        RectTransform panelRect = inventoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(650f, 470f);
        panelRect.anchoredPosition = new Vector2(0f, 60f);

        Text title = CreateText("Title", panelRect, "INVENTORY", font, 22, TextAnchor.MiddleLeft);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -16f);
        titleRect.sizeDelta = new Vector2(-32f, 34f);

        CreateCloseButton(panelRect, font);

        weightText = CreateText("Weight", panelRect, string.Empty, font, 15, TextAnchor.LowerLeft);
        weightText.color = new Color(0.76f, 0.81f, 0.86f, 1f);
        RectTransform weightRect = weightText.rectTransform;
        weightRect.anchorMin = new Vector2(0f, 0f);
        weightRect.anchorMax = new Vector2(1f, 0f);
        weightRect.pivot = new Vector2(0.5f, 0f);
        weightRect.anchoredPosition = new Vector2(0f, 6f);
        weightRect.sizeDelta = new Vector2(-48f, 24f);

        GameObject gridObject = new GameObject("Inventory Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(panelRect, false);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.offsetMin = new Vector2(24f, 24f);
        gridRect.offsetMax = new Vector2(-24f, -62f);

        GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.cellSize = new Vector2(68f, 68f);
        grid.spacing = new Vector2(8f, 8f);
        grid.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < inventorySlotViews.Length; i++)
        {
            inventorySlotViews[i] = CreateSlot(gridRect, InventoryUISlotKind.Inventory, i, string.Empty, font);
        }

        GameObject hotbarObject = CreatePanel("Hotbar", canvasRect, new Color(0.04f, 0.045f, 0.05f, 0.7f));
        RectTransform hotbarRect = hotbarObject.GetComponent<RectTransform>();
        hotbarRect.anchorMin = new Vector2(0.5f, 0f);
        hotbarRect.anchorMax = new Vector2(0.5f, 0f);
        hotbarRect.pivot = new Vector2(0.5f, 0f);
        hotbarRect.sizeDelta = new Vector2(780f, 78f);
        hotbarRect.anchoredPosition = new Vector2(0f, 24f);

        HorizontalLayoutGroup hotbarLayout = hotbarObject.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.padding = new RectOffset(10, 10, 7, 7);
        hotbarLayout.spacing = 8f;
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarLayout.childControlWidth = false;
        hotbarLayout.childControlHeight = false;
        hotbarLayout.childForceExpandWidth = false;
        hotbarLayout.childForceExpandHeight = false;

        for (int i = 0; i < hotbarSlotViews.Length; i++)
        {
            string keyLabel = i == 9 ? "0" : (i + 1).ToString();
            hotbarSlotViews[i] = CreateSlot(hotbarRect, InventoryUISlotKind.Hotbar, i, keyLabel, font);
        }

        contextMenuRoot = CreateContextMenu(canvasRect);
        contextMenuRoot.gameObject.SetActive(false);

        tooltipRoot = CreateTooltip(canvasRect, font);
        tooltipRoot.gameObject.SetActive(false);

        dragIconRoot = CreateDragIcon(canvasRect, font);
        dragIconRoot.gameObject.SetActive(false);
    }

    private InventoryUISlotView CreateSlot(RectTransform parent, InventoryUISlotKind kind, int index, string keyLabel, Font font)
    {
        GameObject slotObject = new GameObject($"{kind} Slot {index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slotObject.transform.SetParent(parent, false);

        RectTransform rect = slotObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(68f, 68f);

        Image background = slotObject.GetComponent<Image>();
        background.color = GetEmptySlotColor(kind);
        background.raycastTarget = true;

        Image icon = CreateImage("Icon", rect, Color.white);
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(46f, 46f);
        iconRect.anchoredPosition = Vector2.zero;

        Text fallback = CreateText("Fallback", rect, string.Empty, font, 18, TextAnchor.MiddleCenter);
        fallback.raycastTarget = false;
        fallback.color = new Color(0.78f, 0.83f, 0.88f, 1f);
        StretchToParent(fallback.rectTransform);

        Text amountText = CreateText("Amount", rect, string.Empty, font, 14, TextAnchor.LowerRight);
        amountText.raycastTarget = false;
        amountText.color = Color.white;
        StretchToParent(amountText.rectTransform, new Vector2(4f, 3f), new Vector2(-5f, -3f));

        Text keyText = CreateText("Key", rect, keyLabel, font, 12, TextAnchor.UpperLeft);
        keyText.raycastTarget = false;
        keyText.color = new Color(0.72f, 0.77f, 0.82f, 1f);
        StretchToParent(keyText.rectTransform, new Vector2(5f, 3f), new Vector2(-3f, -3f));

        InventoryUISlotView slotView = slotObject.AddComponent<InventoryUISlotView>();
        slotView.Initialize(this, kind, index, background, icon, fallback, amountText);
        return slotView;
    }

    private void CreateCloseButton(RectTransform parent, Font font)
    {
        GameObject buttonObject = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-16f, -16f);
        rect.sizeDelta = new Vector2(30f, 30f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.15f, 0.16f, 0.18f, 0.95f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.15f, 0.16f, 0.18f, 0.95f);
        colors.highlightedColor = new Color(0.28f, 0.3f, 0.33f, 1f);
        colors.pressedColor = new Color(0.09f, 0.1f, 0.12f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() => SetInventoryOpen(false));

        Text label = CreateText("Label", rect, "X", font, 18, TextAnchor.MiddleCenter);
        label.raycastTarget = false;
        StretchToParent(label.rectTransform);
    }

    private RectTransform CreateTooltip(RectTransform parent, Font font)
    {
        GameObject tooltipObject = CreatePanel("Item Tooltip", parent, new Color(0.035f, 0.038f, 0.043f, 0.97f));
        RectTransform rect = tooltipObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(330f, 0f);

        VerticalLayoutGroup layout = tooltipObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = tooltipObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement tooltipLayout = tooltipObject.AddComponent<LayoutElement>();
        tooltipLayout.preferredWidth = 330f;

        tooltipTitleText = CreateTooltipText("Title", rect, font, 18, FontStyle.Bold, Color.white);
        tooltipDescriptionText = CreateTooltipText("Description", rect, font, 14, FontStyle.Normal, new Color(0.8f, 0.84f, 0.88f, 1f));
        tooltipStatsText = CreateTooltipText("Stats", rect, font, 13, FontStyle.Normal, new Color(0.68f, 0.74f, 0.8f, 1f));

        return rect;
    }

    private static Text CreateTooltipText(string name, RectTransform parent, Font font, int fontSize, FontStyle fontStyle, Color color)
    {
        Text text = CreateText(name, parent, string.Empty, font, fontSize, TextAnchor.UpperLeft);
        text.fontStyle = fontStyle;
        text.color = color;
        text.raycastTarget = false;

        LayoutElement layoutElement = text.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 306f;
        layoutElement.minHeight = fontSize + 4f;

        return text;
    }

    private RectTransform CreateContextMenu(RectTransform parent)
    {
        GameObject menuObject = CreatePanel("Context Menu", parent, new Color(0.055f, 0.06f, 0.065f, 0.96f));
        RectTransform rect = menuObject.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.sizeDelta = new Vector2(180f, 0f);

        VerticalLayoutGroup layout = menuObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = menuObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rect;
    }

    private void AddContextMenuButton(string label, Action action)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(contextMenuRoot, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(172f, 32f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.13f, 0.145f, 1f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.12f, 0.13f, 0.145f, 1f);
        colors.highlightedColor = new Color(0.19f, 0.21f, 0.235f, 1f);
        colors.pressedColor = new Color(0.08f, 0.09f, 0.105f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            HideContextMenu();
            action?.Invoke();
        });

        LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 32f;
        layoutElement.preferredHeight = 32f;

        Text text = CreateText("Label", rect, label, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 15, TextAnchor.MiddleLeft);
        text.raycastTarget = false;
        text.color = Color.white;
        StretchToParent(text.rectTransform, new Vector2(10f, 0f), new Vector2(-8f, 0f));
    }

    private Vector2 ClampContextMenuPosition(Vector2 screenPosition)
    {
        const float margin = 8f;
        Vector2 size = contextMenuRoot.rect.size;
        float x = Mathf.Clamp(screenPosition.x, margin, Mathf.Max(margin, Screen.width - size.x - margin));
        float y = Mathf.Clamp(screenPosition.y, size.y + margin, Mathf.Max(size.y + margin, Screen.height - margin));
        return new Vector2(x, y);
    }

    private Vector2 ClampTooltipPosition(Vector2 screenPosition)
    {
        const float margin = 8f;
        const float offset = 18f;
        Vector2 size = tooltipRoot != null ? tooltipRoot.rect.size : Vector2.zero;
        float x = Mathf.Clamp(screenPosition.x + offset, margin, Mathf.Max(margin, Screen.width - size.x - margin));
        float y = Mathf.Clamp(screenPosition.y - offset, size.y + margin, Mathf.Max(size.y + margin, Screen.height - margin));
        return new Vector2(x, y);
    }

    private void RefreshWeightText()
    {
        if (weightText == null)
        {
            return;
        }

        if (inventory == null)
        {
            weightText.text = "Вес: 0 / 0";
            return;
        }

        weightText.text = $"Вес: {inventory.GetCurrentWeight():0.##} / {inventory.MaxWeight:0.##}";
    }

    private static string BuildTooltipStats(ItemSO item)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Можно использовать: {(PlayerInventory.CanUseItem(item) ? "Да" : "Нет")}");
        builder.AppendLine($"Тип: {GetItemTypeLabel(item.itemType)}");
        builder.AppendLine($"Редкость: {GetRarityLabel(item.rarity)}");
        builder.AppendLine($"Вес: {item.weight:0.##}");

        if (item.itemType == ItemType.Ammo)
        {
            builder.AppendLine($"Оружие: {GetAmmoWeaponLabel(item)}");
            builder.AppendLine($"В коробке: {item.ammoAmount}");
        }

        if (item.itemType == ItemType.Drink)
        {
            builder.AppendLine($"Утоляет жажду: {item.thirstRestoreAmount:0.##}");
        }
        else if (item.itemType == ItemType.Food)
        {
            builder.AppendLine($"Восстанавливает еду: {item.hungerRestoreAmount:0.##}");
        }
        else if (item.itemType == ItemType.Healing)
        {
            builder.AppendLine($"Лечит: {item.healthRestoreAmount:0.##}");
        }

        return builder.ToString().TrimEnd();
    }

    public static Color GetEmptySlotColor(InventoryUISlotKind kind)
    {
        return kind == InventoryUISlotKind.Hotbar
            ? new Color(0.12f, 0.13f, 0.14f, 0.92f)
            : new Color(0.09f, 0.1f, 0.11f, 0.92f);
    }

    public static Color GetRaritySlotColor(Rarity rarity, InventoryUISlotKind kind)
    {
        Color baseColor = rarity switch
        {
            Rarity.Uncommon => new Color(0.08f, 0.22f, 0.14f, 0.96f),
            Rarity.Rare => new Color(0.08f, 0.16f, 0.32f, 0.96f),
            Rarity.Epic => new Color(0.22f, 0.11f, 0.32f, 0.96f),
            Rarity.Legendary => new Color(0.34f, 0.22f, 0.08f, 0.96f),
            _ => new Color(0.14f, 0.15f, 0.16f, 0.94f)
        };

        if (kind == InventoryUISlotKind.Hotbar)
        {
            baseColor = Color.Lerp(baseColor, Color.white, 0.04f);
        }

        return baseColor;
    }

    private static string GetAmmoWeaponLabel(ItemSO item)
    {
        if (item.ammoWeaponDefinition != null)
        {
            if (!string.IsNullOrWhiteSpace(item.ammoWeaponDefinition.DisplayName))
            {
                return item.ammoWeaponDefinition.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(item.ammoWeaponDefinition.WeaponID))
            {
                return item.ammoWeaponDefinition.WeaponID;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ammoWeaponID))
        {
            return item.ammoWeaponID;
        }

        return "Текущее оружие";
    }

    private static string GetItemTypeLabel(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Weapon => "Оружие",
            ItemType.Consumable => "Расходник",
            ItemType.Material => "Материал",
            ItemType.Quest => "Квестовый",
            ItemType.Key => "Ключ",
            ItemType.Armor => "Броня",
            ItemType.Thing => "Вещь",
            ItemType.Ammo => "Патроны",
            ItemType.Drink => "Напиток",
            ItemType.Food => "Еда",
            ItemType.Healing => "Лечение",
            _ => "Обычный"
        };
    }

    private static string GetRarityLabel(Rarity rarity)
    {
        return rarity switch
        {
            Rarity.Uncommon => "Необычный",
            Rarity.Rare => "Редкий",
            Rarity.Epic => "Эпический",
            Rarity.Legendary => "Легендарный",
            _ => "Обычный"
        };
    }

    private static bool WasPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private static Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.mousePosition;
#else
        return Vector2.zero;
#endif
    }

    private static GameObject CreatePanel(string name, RectTransform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return panel;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, RectTransform parent, string value, Font font, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private RectTransform CreateDragIcon(RectTransform parent, Font font)
    {
        GameObject dragObject = new GameObject("Dragged Item", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragObject.transform.SetParent(parent, false);
        RectTransform rect = dragObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(54f, 54f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image background = dragObject.GetComponent<Image>();
        background.color = new Color(0.09f, 0.1f, 0.11f, 0.85f);
        background.raycastTarget = false;

        dragIcon = CreateImage("Icon", rect, Color.white);
        StretchToParent(dragIcon.rectTransform, new Vector2(6f, 6f), new Vector2(-6f, -6f));

        dragFallbackText = CreateText("Fallback", rect, string.Empty, font, 18, TextAnchor.MiddleCenter);
        dragFallbackText.raycastTarget = false;
        StretchToParent(dragFallbackText.rectTransform);

        return rect;
    }

    private static void SetIcon(Image icon, Text fallbackText, ItemSO item)
    {
        if (icon == null || fallbackText == null)
        {
            return;
        }

        Sprite itemIcon = GetItemIconSprite(item);
        bool hasIcon = itemIcon != null;
        icon.enabled = hasIcon;
        icon.sprite = itemIcon;
        fallbackText.text = !hasIcon && item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName.Substring(0, 1).ToUpperInvariant()
            : string.Empty;
    }

#if UNITY_EDITOR
    private static Sprite GetEditorPrefabPreviewSprite(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (editorPreviewIconCache.TryGetValue(prefab, out Sprite cachedSprite) && cachedSprite != null)
        {
            return cachedSprite;
        }

        Texture2D previewTexture = AssetPreview.GetAssetPreview(prefab);

        if (previewTexture == null)
        {
            previewTexture = AssetPreview.GetMiniThumbnail(prefab);
        }

        if (previewTexture == null)
        {
            return null;
        }

        Sprite previewSprite = Sprite.Create(
            previewTexture,
            new Rect(0f, 0f, previewTexture.width, previewTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        previewSprite.name = $"{prefab.name}_PreviewIcon";
        editorPreviewIconCache[prefab] = previewSprite;
        return previewSprite;
    }
#endif

    private static void StretchToParent(RectTransform rect)
    {
        StretchToParent(rect, Vector2.zero, Vector2.zero);
    }

    private static void StretchToParent(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
