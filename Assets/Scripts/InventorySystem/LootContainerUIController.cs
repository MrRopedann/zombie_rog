using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class LootContainerUIController : MonoBehaviour
{
    private const int PlayerSlotCountFallback = 32;

    private static LootContainerUIController instance;

    private readonly List<LootContainerSlotView> containerSlotViews = new();
    private readonly List<LootContainerSlotView> playerSlotViews = new();

    private LootContainer container;
    private PlayerInventory playerInventory;
    private InputsController inputs;

    private GameObject uiRoot;
    private RectTransform containerGridRoot;
    private RectTransform playerGridRoot;
    private Text containerTitleText;
    private Text playerTitleText;
    private Text weightText;
    private Dropdown containerSortDropdown;
    private Dropdown playerSortDropdown;
    private RectTransform dragIconRoot;
    private Image dragIcon;
    private Text dragFallbackText;
    private RectTransform tooltipRoot;
    private Text tooltipTitleText;
    private Text tooltipDescriptionText;
    private Text tooltipStatsText;
    private RectTransform quantityRoot;
    private Text quantityTitleText;
    private Text quantityInfoText;
    private Slider quantitySlider;
    private InputField quantityInput;

    private LootContainerSlotView draggedSlot;
    private ItemSO draggedItem;
    private LootContainerSlotView hoveredTooltipSlot;
    private LootContainerSlotView quantitySourceSlot;
    private int quantityAmount = 1;
    private bool isOpen;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;
    private bool previousCursorInputForLook;
    private bool previousShootingInputBlocked;
    private bool cursorGuardActive;

    public static bool IsOpen => instance != null && instance.isOpen;

    private void Awake()
    {
        instance = this;
        EnsureEventSystem();
        BuildUI();
        SetOpen(false);
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        GameCursorGuard.ApplyUiCursor();

        if (WasEscapePressedThisFrame())
        {
            Close();
            return;
        }

        Vector2 pointerPosition = GetPointerPosition();

        if (dragIconRoot != null && dragIconRoot.gameObject.activeSelf)
        {
            dragIconRoot.position = pointerPosition;
        }

        if (tooltipRoot != null && tooltipRoot.gameObject.activeSelf)
        {
            tooltipRoot.position = ClampToScreen(pointerPosition + new Vector2(18f, -18f), tooltipRoot);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (isOpen)
        {
            RestoreCursorState();
        }
    }

    public static void Open(LootContainer lootContainer, PlayerInventory inventory)
    {
        if (lootContainer == null || inventory == null)
        {
            return;
        }

        if (instance == null)
        {
            GameObject uiObject = new GameObject("Loot Container UI Controller");
            instance = uiObject.AddComponent<LootContainerUIController>();
        }

        instance.OpenInternal(lootContainer, inventory);
    }

    public void Close()
    {
        SetOpen(false);
        Unsubscribe();
        container = null;
        playerInventory = null;
        inputs = null;
    }

    public void BeginDrag(LootContainerSlotView slotView, Vector2 screenPosition)
    {
        if (slotView == null || slotView.Item == null)
        {
            return;
        }

        HideTooltip(null);
        HideQuantityMenu();

        if (IsControlPressed() && slotView.Amount > 1)
        {
            ShowQuantityMenu(slotView, screenPosition);
            return;
        }

        draggedSlot = slotView;
        draggedItem = slotView.Item;

        if (dragIconRoot == null)
        {
            return;
        }

        dragIconRoot.gameObject.SetActive(true);
        dragIconRoot.position = screenPosition;
        SetIcon(dragIcon, dragFallbackText, draggedItem);
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
        draggedSlot = null;
        draggedItem = null;

        if (dragIconRoot != null)
        {
            dragIconRoot.gameObject.SetActive(false);
        }
    }

    public void DropOnSlot(LootContainerSlotView targetSlot)
    {
        if (targetSlot == null || draggedSlot == null || draggedItem == null)
        {
            return;
        }

        if (targetSlot.Side != draggedSlot.Side)
        {
            Transfer(draggedSlot, draggedSlot.Amount);
        }
    }

    public void TransferStack(LootContainerSlotView sourceSlot)
    {
        if (sourceSlot == null || sourceSlot.Item == null)
        {
            return;
        }

        Transfer(sourceSlot, sourceSlot.Amount);
    }

    public void ShowTooltip(LootContainerSlotView slotView, Vector2 screenPosition)
    {
        if (slotView == null || slotView.Item == null || tooltipRoot == null)
        {
            HideTooltip(slotView);
            return;
        }

        hoveredTooltipSlot = slotView;
        ItemSO item = slotView.Item;

        tooltipTitleText.text = item.itemName;
        tooltipTitleText.color = LootContainer.GetRarityTextColor(item.rarity);
        tooltipDescriptionText.text = string.IsNullOrWhiteSpace(item.description) ? string.Empty : item.description;
        tooltipStatsText.text = BuildTooltipStats(item, slotView.Amount);

        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRoot);
        tooltipRoot.gameObject.SetActive(true);
        tooltipRoot.position = ClampToScreen(screenPosition + new Vector2(18f, -18f), tooltipRoot);
    }

    public void HideTooltip(LootContainerSlotView slotView)
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

    public static bool IsShiftPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
        return false;
#endif
    }

    private static bool IsControlPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#else
        return false;
#endif
    }

    private void OpenInternal(LootContainer lootContainer, PlayerInventory inventory)
    {
        if (isOpen)
        {
            Unsubscribe();
        }

        container = lootContainer;
        playerInventory = inventory;
        inputs = ResolveInputs(inventory);

        Subscribe();
        Refresh();
        SetOpen(true);
    }

    private void Subscribe()
    {
        if (container != null)
        {
            container.InventoryChanged += Refresh;
        }

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged += Refresh;
        }
    }

    private void Unsubscribe()
    {
        if (container != null)
        {
            container.InventoryChanged -= Refresh;
        }

        if (playerInventory != null)
        {
            playerInventory.InventoryChanged -= Refresh;
        }
    }

    private void SetOpen(bool open)
    {
        if (uiRoot == null)
        {
            return;
        }

        if (isOpen == open)
        {
            uiRoot.SetActive(open);
            return;
        }

        isOpen = open;
        uiRoot.SetActive(open);

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
            HideTooltip(null);
            HideQuantityMenu();
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

    private void Refresh()
    {
        if (container == null || playerInventory == null)
        {
            return;
        }

        int playerSlots = Mathf.Max(PlayerSlotCountFallback, playerInventory.MaxSlots);
        EnsureSlotCount(containerSlotViews, containerGridRoot, container.MaxSlots, LootInventorySide.Container);
        EnsureSlotCount(playerSlotViews, playerGridRoot, playerSlots, LootInventorySide.Player);

        if (containerTitleText != null)
        {
            containerTitleText.text = container.ContainerName;
            containerTitleText.color = LootContainer.GetRarityTextColor(container.Rarity);
        }

        if (playerTitleText != null)
        {
            playerTitleText.text = "Инвентарь";
        }

        if (weightText != null)
        {
            weightText.text = $"Вес: {playerInventory.GetCurrentWeight():0.#} / {playerInventory.MaxWeight:0.#}";
        }

        RefreshSlots(containerSlotViews, container.MaxSlots, LootInventorySide.Container);
        RefreshSlots(playerSlotViews, playerSlots, LootInventorySide.Player);
    }

    private void EnsureSlotCount(
        List<LootContainerSlotView> views,
        RectTransform gridRoot,
        int desiredCount,
        LootInventorySide side)
    {
        if (gridRoot == null)
        {
            return;
        }

        while (views.Count < desiredCount)
        {
            views.Add(CreateSlot(gridRoot, side, views.Count));
        }

        for (int i = 0; i < views.Count; i++)
        {
            views[i].gameObject.SetActive(i < desiredCount);
        }
    }

    private void RefreshSlots(List<LootContainerSlotView> views, int slotCount, LootInventorySide side)
    {
        for (int i = 0; i < views.Count; i++)
        {
            if (i >= slotCount)
            {
                continue;
            }

            InventorySlot slot = side == LootInventorySide.Container
                ? container.GetSlot(i)
                : playerInventory.GetSlot(i);

            views[i].SetItem(slot != null ? slot.item : null, slot != null ? slot.amount : 0);
        }
    }

    private bool Transfer(LootContainerSlotView sourceSlot, int amount)
    {
        if (sourceSlot == null || sourceSlot.Item == null || amount <= 0)
        {
            return false;
        }

        ItemSO item = sourceSlot.Item;
        int transferAmount = Mathf.Min(amount, sourceSlot.Amount);
        bool fromContainer = sourceSlot.Side == LootInventorySide.Container;

        if (CoopGameplaySync.TryRequestLootTransfer(container, playerInventory, fromContainer, item, transferAmount))
            return true;

        if (fromContainer)
        {
            if (!playerInventory.CanAddItem(item, transferAmount))
            {
                return false;
            }

            if (!container.RemoveItem(item, transferAmount))
            {
                return false;
            }

            if (!playerInventory.AddItem(item, transferAmount))
            {
                container.AddItem(item, transferAmount);
                return false;
            }
        }
        else
        {
            if (!container.CanAddItem(item, transferAmount))
            {
                return false;
            }

            if (!playerInventory.RemoveItem(item, transferAmount))
            {
                return false;
            }

            if (!container.AddItem(item, transferAmount))
            {
                playerInventory.AddItem(item, transferAmount);
                return false;
            }
        }

        Refresh();
        return true;
    }

    private void TransferAllFromContainer()
    {
        TransferAll(LootInventorySide.Container);
    }

    private void TransferAllFromPlayer()
    {
        TransferAll(LootInventorySide.Player);
    }

    private void TransferAll(LootInventorySide side)
    {
        bool moved;

        do
        {
            moved = false;
            List<LootContainerSlotView> views = side == LootInventorySide.Container ? containerSlotViews : playerSlotViews;

            for (int i = views.Count - 1; i >= 0; i--)
            {
                LootContainerSlotView slot = views[i];

                if (!slot.gameObject.activeSelf || slot.Item == null)
                {
                    continue;
                }

                if (Transfer(slot, slot.Amount))
                {
                    moved = true;
                }
            }
        }
        while (moved);
    }

    private void SortContainer()
    {
        container?.SortSlots(GetSelectedSortMode(containerSortDropdown));
    }

    private void SortPlayer()
    {
        playerInventory?.SortSlots(GetSelectedSortMode(playerSortDropdown));
    }

    private static InventorySortMode GetSelectedSortMode(Dropdown dropdown)
    {
        return dropdown != null && dropdown.value == 1
            ? InventorySortMode.Amount
            : dropdown != null && dropdown.value == 2
                ? InventorySortMode.Type
                : InventorySortMode.Name;
    }

    private void ShowQuantityMenu(LootContainerSlotView sourceSlot, Vector2 screenPosition)
    {
        if (quantityRoot == null || sourceSlot == null || sourceSlot.Item == null)
        {
            return;
        }

        quantitySourceSlot = sourceSlot;
        quantityAmount = Mathf.Clamp(sourceSlot.Amount, 1, sourceSlot.Amount);

        quantityTitleText.text = sourceSlot.Item.itemName;
        quantityInfoText.text = $"Количество: 1 - {sourceSlot.Amount}";
        quantitySlider.minValue = 1f;
        quantitySlider.maxValue = sourceSlot.Amount;
        quantitySlider.wholeNumbers = true;
        quantitySlider.SetValueWithoutNotify(quantityAmount);
        quantityInput.SetTextWithoutNotify(quantityAmount.ToString());

        quantityRoot.gameObject.SetActive(true);
        LayoutRebuilder.ForceRebuildLayoutImmediate(quantityRoot);
        quantityRoot.position = ClampToScreen(screenPosition, quantityRoot);
    }

    private void HideQuantityMenu()
    {
        quantitySourceSlot = null;

        if (quantityRoot != null)
        {
            quantityRoot.gameObject.SetActive(false);
        }
    }

    private void ConfirmQuantityTransfer()
    {
        if (quantitySourceSlot != null)
        {
            Transfer(quantitySourceSlot, quantityAmount);
        }

        HideQuantityMenu();
    }

    private void OnQuantitySliderChanged(float value)
    {
        SetQuantityAmount(Mathf.RoundToInt(value));
    }

    private void OnQuantityInputChanged(string value)
    {
        if (!int.TryParse(value, out int parsed))
        {
            return;
        }

        SetQuantityAmount(parsed);
    }

    private void SetQuantityAmount(int amount)
    {
        if (quantitySourceSlot == null)
        {
            return;
        }

        quantityAmount = Mathf.Clamp(amount, 1, Mathf.Max(1, quantitySourceSlot.Amount));

        if (quantitySlider != null)
        {
            quantitySlider.SetValueWithoutNotify(quantityAmount);
        }

        if (quantityInput != null)
        {
            quantityInput.SetTextWithoutNotify(quantityAmount.ToString());
        }
    }

    private void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        uiRoot = new GameObject("Loot Container UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shade = CreateImageObject("Shade", uiRoot.transform, new Color(0.02f, 0.025f, 0.03f, 0.72f));
        Stretch(shade.GetComponent<RectTransform>());

        GameObject window = new GameObject("Window", typeof(RectTransform));
        window.transform.SetParent(shade.transform, false);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1440f, 560f);

        GameObject layout = new GameObject("Layout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        layout.transform.SetParent(window.transform, false);
        RectTransform layoutRect = layout.GetComponent<RectTransform>();
        Stretch(layoutRect);

        HorizontalLayoutGroup horizontal = layout.GetComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(0, 0, 34, 0);
        horizontal.spacing = 14f;
        horizontal.childControlWidth = true;
        horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = true;
        horizontal.childForceExpandHeight = true;

        GameObject containerPanel = CreateSidePanel(
            "Container Panel",
            layout.transform,
            font,
            "Сундук",
            "Взять всё",
            TransferAllFromContainer,
            SortContainer,
            out containerTitleText,
            out containerSortDropdown,
            out containerGridRoot,
            out _);

        GameObject playerPanel = CreateSidePanel(
            "Player Panel",
            layout.transform,
            font,
            "Инвентарь",
            "Сложить всё",
            TransferAllFromPlayer,
            SortPlayer,
            out playerTitleText,
            out playerSortDropdown,
            out playerGridRoot,
            out weightText);

        containerPanel.name = "Container Panel";
        playerPanel.name = "Player Panel";

        CreateCloseButton(window.transform, font);
        BuildDragIcon(shade.transform, font);
        BuildTooltip(shade.transform, font);
        BuildQuantityMenu(shade.transform, font);
    }

    private GameObject CreateSidePanel(
        string objectName,
        Transform parent,
        Font font,
        string title,
        string transferAllLabel,
        UnityEngine.Events.UnityAction transferAllAction,
        UnityEngine.Events.UnityAction sortAction,
        out Text titleText,
        out Dropdown sortDropdown,
        out RectTransform gridRoot,
        out Text footerText)
    {
        GameObject panel = CreateImageObject(objectName, parent, new Color(0.075f, 0.08f, 0.085f, 0.96f));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.raycastTarget = true;

        LayoutElement layoutElement = panel.AddComponent<LayoutElement>();
        layoutElement.flexibleWidth = 1f;

        VerticalLayoutGroup vertical = panel.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(14, 14, 14, 14);
        vertical.spacing = 8f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        header.transform.SetParent(panel.transform, false);
        LayoutElement headerElement = header.AddComponent<LayoutElement>();
        headerElement.minHeight = 30f;
        headerElement.preferredHeight = 30f;
        headerElement.flexibleHeight = 0f;

        HorizontalLayoutGroup headerLayout = header.GetComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 6f;
        headerLayout.childAlignment = TextAnchor.MiddleCenter;
        headerLayout.childControlWidth = true;
        headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = false;
        headerLayout.childForceExpandHeight = false;

        titleText = CreateText("Title", header.transform, title, font, 20, TextAnchor.MiddleLeft);
        titleText.fontStyle = FontStyle.Bold;
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;
        titleLayout.minHeight = 30f;
        titleLayout.preferredHeight = 30f;

        sortDropdown = CreateSortDropdown("Sort Dropdown", header.transform, font);
        AddSize(sortDropdown.gameObject, 136f, 28f);

        Button sortButton = CreateButton("Sort Button", header.transform, "Сорт", font, new Vector2(56f, 28f));
        sortButton.onClick.AddListener(sortAction);

        Button transferAllButton = CreateButton("Transfer All Button", header.transform, "Все", font, new Vector2(54f, 28f));
        transferAllButton.onClick.AddListener(transferAllAction);

        gridRoot = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        gridRoot.transform.SetParent(panel.transform, false);

        GridLayoutGroup grid = gridRoot.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(66f, 66f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;

        ContentSizeFitter fitter = gridRoot.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LayoutElement gridLayout = gridRoot.gameObject.AddComponent<LayoutElement>();
        gridLayout.flexibleHeight = 1f;
        gridLayout.minHeight = 292f;

        footerText = CreateText("Footer", panel.transform, string.Empty, font, 16, TextAnchor.MiddleLeft);
        LayoutElement footerLayout = footerText.gameObject.AddComponent<LayoutElement>();
        footerLayout.preferredHeight = 26f;

        return panel;
    }

    private LootContainerSlotView CreateSlot(RectTransform gridRoot, LootInventorySide side, int index)
    {
        GameObject slotObject = CreateImageObject($"Slot {index + 1}", gridRoot, InventoryUIController.GetEmptySlotColor(InventoryUISlotKind.Inventory));
        slotObject.GetComponent<Image>().raycastTarget = true;

        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(66f, 66f);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotObject.transform, false);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(7f, 7f);
        iconRect.offsetMax = new Vector2(-7f, -7f);

        Text fallbackText = CreateText("Fallback", slotObject.transform, string.Empty, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 22, TextAnchor.MiddleCenter);
        fallbackText.fontStyle = FontStyle.Bold;
        fallbackText.raycastTarget = false;
        Stretch(fallbackText.rectTransform);

        Text amountText = CreateText("Amount", slotObject.transform, string.Empty, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), 14, TextAnchor.LowerRight);
        amountText.raycastTarget = false;
        RectTransform amountRect = amountText.rectTransform;
        Stretch(amountRect);
        amountRect.offsetMin = new Vector2(4f, 2f);
        amountRect.offsetMax = new Vector2(-5f, -3f);

        LootContainerSlotView view = slotObject.AddComponent<LootContainerSlotView>();
        view.Initialize(this, side, index, slotObject.GetComponent<Image>(), icon, fallbackText, amountText);
        return view;
    }

    private void CreateCloseButton(Transform parent, Font font)
    {
        Button closeButton = CreateButton("Close Button", parent, "X", font, new Vector2(30f, 30f));
        RectTransform rect = closeButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = Vector2.zero;
        closeButton.onClick.AddListener(Close);
    }

    private void BuildDragIcon(Transform parent, Font font)
    {
        dragIconRoot = new GameObject("Drag Icon", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        dragIconRoot.transform.SetParent(parent, false);
        dragIconRoot.sizeDelta = new Vector2(58f, 58f);

        CanvasGroup group = dragIconRoot.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        Image back = dragIconRoot.gameObject.AddComponent<Image>();
        back.color = new Color(0.04f, 0.045f, 0.05f, 0.92f);
        back.raycastTarget = false;

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(dragIconRoot, false);
        dragIcon = iconObject.GetComponent<Image>();
        dragIcon.preserveAspect = true;
        dragIcon.raycastTarget = false;
        Stretch(dragIcon.rectTransform);
        dragIcon.rectTransform.offsetMin = new Vector2(6f, 6f);
        dragIcon.rectTransform.offsetMax = new Vector2(-6f, -6f);

        dragFallbackText = CreateText("Fallback", dragIconRoot, string.Empty, font, 22, TextAnchor.MiddleCenter);
        dragFallbackText.fontStyle = FontStyle.Bold;
        Stretch(dragFallbackText.rectTransform);
        dragIconRoot.gameObject.SetActive(false);
    }

    private void BuildTooltip(Transform parent, Font font)
    {
        tooltipRoot = CreateImageObject("Tooltip", parent, new Color(0.035f, 0.038f, 0.042f, 0.98f)).GetComponent<RectTransform>();
        tooltipRoot.sizeDelta = new Vector2(330f, 10f);
        tooltipRoot.GetComponent<Image>().raycastTarget = false;

        CanvasGroup tooltipGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
        tooltipGroup.blocksRaycasts = false;
        tooltipGroup.interactable = false;

        VerticalLayoutGroup layout = tooltipRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = tooltipRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        tooltipTitleText = CreateText("Title", tooltipRoot, string.Empty, font, 18, TextAnchor.MiddleLeft);
        tooltipTitleText.fontStyle = FontStyle.Bold;
        tooltipDescriptionText = CreateText("Description", tooltipRoot, string.Empty, font, 14, TextAnchor.UpperLeft);
        tooltipStatsText = CreateText("Stats", tooltipRoot, string.Empty, font, 14, TextAnchor.UpperLeft);
        tooltipTitleText.raycastTarget = false;
        tooltipDescriptionText.raycastTarget = false;
        tooltipStatsText.raycastTarget = false;
        tooltipRoot.gameObject.SetActive(false);
    }

    private void BuildQuantityMenu(Transform parent, Font font)
    {
        quantityRoot = CreateImageObject("Quantity Menu", parent, new Color(0.05f, 0.055f, 0.06f, 0.98f)).GetComponent<RectTransform>();
        quantityRoot.sizeDelta = new Vector2(330f, 190f);

        VerticalLayoutGroup layout = quantityRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 9f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        quantityTitleText = CreateText("Title", quantityRoot, string.Empty, font, 18, TextAnchor.MiddleLeft);
        quantityTitleText.fontStyle = FontStyle.Bold;
        AddSize(quantityTitleText.gameObject, 0f, 26f);

        quantityInfoText = CreateText("Info", quantityRoot, string.Empty, font, 14, TextAnchor.MiddleLeft);
        AddSize(quantityInfoText.gameObject, 0f, 22f);

        quantitySlider = CreateSlider("Slider", quantityRoot);
        AddSize(quantitySlider.gameObject, 0f, 28f);
        quantitySlider.onValueChanged.AddListener(OnQuantitySliderChanged);

        quantityInput = CreateInputField("Input", quantityRoot, font);
        AddSize(quantityInput.gameObject, 0f, 32f);
        quantityInput.onEndEdit.AddListener(OnQuantityInputChanged);

        GameObject buttons = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttons.transform.SetParent(quantityRoot, false);
        AddSize(buttons, 0f, 34f);

        HorizontalLayoutGroup buttonsLayout = buttons.GetComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 8f;
        buttonsLayout.childControlWidth = true;
        buttonsLayout.childForceExpandWidth = true;

        Button okButton = CreateButton("OK", buttons.transform, "Перенести", font, new Vector2(0f, 34f));
        okButton.onClick.AddListener(ConfirmQuantityTransfer);
        Button cancelButton = CreateButton("Cancel", buttons.transform, "Отмена", font, new Vector2(0f, 34f));
        cancelButton.onClick.AddListener(HideQuantityMenu);

        quantityRoot.gameObject.SetActive(false);
    }

    private static Dropdown CreateSortDropdown(string objectName, Transform parent, Font font)
    {
        GameObject root = CreateImageObject(objectName, parent, new Color(0.11f, 0.12f, 0.13f, 0.98f));
        Dropdown dropdown = root.AddComponent<Dropdown>();

        Text label = CreateText("Label", root.transform, string.Empty, font, 14, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(10f, 0f);
        label.rectTransform.offsetMax = new Vector2(-28f, 0f);

        Text arrow = CreateText("Arrow", root.transform, "▼", font, 12, TextAnchor.MiddleCenter);
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(1f, 0f);
        arrowRect.anchorMax = new Vector2(1f, 1f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.sizeDelta = new Vector2(26f, 0f);
        arrowRect.anchoredPosition = Vector2.zero;

        RectTransform template = BuildDropdownTemplate(root.transform, font);

        dropdown.captionText = label;
        dropdown.template = template;
        dropdown.itemText = template.GetComponentInChildren<Toggle>(true).GetComponentInChildren<Text>(true);
        dropdown.options.Clear();
        dropdown.options.Add(new Dropdown.OptionData("По названию"));
        dropdown.options.Add(new Dropdown.OptionData("По количеству"));
        dropdown.options.Add(new Dropdown.OptionData("По типу"));
        dropdown.value = 0;
        dropdown.RefreshShownValue();

        return dropdown;
    }

    private static RectTransform BuildDropdownTemplate(Transform parent, Font font)
    {
        GameObject templateObject = CreateImageObject("Template", parent, new Color(0.08f, 0.085f, 0.09f, 0.98f));
        templateObject.SetActive(false);
        RectTransform template = templateObject.GetComponent<RectTransform>();
        template.anchorMin = new Vector2(0f, 0f);
        template.anchorMax = new Vector2(1f, 0f);
        template.pivot = new Vector2(0.5f, 1f);
        template.anchoredPosition = new Vector2(0f, -2f);
        template.sizeDelta = new Vector2(0f, 108f);

        ScrollRect scrollRect = templateObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = CreateImageObject("Viewport", templateObject.transform, new Color(1f, 1f, 1f, 0.05f));
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        Stretch(viewport.GetComponent<RectTransform>());

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 108f);

        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        GameObject item = CreateImageObject("Item", content.transform, new Color(0.12f, 0.13f, 0.14f, 0.95f));
        Toggle toggle = item.AddComponent<Toggle>();
        AddSize(item, 0f, 34f);
        Image itemImage = item.GetComponent<Image>();
        toggle.targetGraphic = itemImage;

        Text itemLabel = CreateText("Item Label", item.transform, string.Empty, font, 14, TextAnchor.MiddleLeft);
        itemLabel.rectTransform.anchorMin = Vector2.zero;
        itemLabel.rectTransform.anchorMax = Vector2.one;
        itemLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
        itemLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        return template;
    }

    private static Slider CreateSlider(string objectName, Transform parent)
    {
        GameObject root = CreateImageObject(objectName, parent, new Color(0.12f, 0.13f, 0.14f, 0.95f));
        Slider slider = root.AddComponent<Slider>();

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(7f, 9f);
        fillAreaRect.offsetMax = new Vector2(-7f, -9f);

        GameObject fillObject = CreateImageObject("Fill", fillArea.transform, new Color(0.55f, 0.72f, 1f, 0.95f));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleObject = CreateImageObject("Handle", root.transform, new Color(0.9f, 0.92f, 0.95f, 1f));
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(16f, 24f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleObject.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static InputField CreateInputField(string objectName, Transform parent, Font font)
    {
        GameObject root = CreateImageObject(objectName, parent, new Color(0.1f, 0.11f, 0.12f, 0.95f));
        InputField input = root.AddComponent<InputField>();

        Text text = CreateText("Text", root.transform, string.Empty, font, 15, TextAnchor.MiddleLeft);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(10f, 0f);
        text.rectTransform.offsetMax = new Vector2(-10f, 0f);

        Text placeholder = CreateText("Placeholder", root.transform, "Количество", font, 15, TextAnchor.MiddleLeft);
        placeholder.color = new Color(1f, 1f, 1f, 0.42f);
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = new Vector2(10f, 0f);
        placeholder.rectTransform.offsetMax = new Vector2(-10f, 0f);

        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = InputField.ContentType.IntegerNumber;
        return input;
    }

    private static Button CreateButton(string objectName, Transform parent, string label, Font font, Vector2 size)
    {
        GameObject root = CreateImageObject(objectName, parent, new Color(0.14f, 0.15f, 0.16f, 0.98f));
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        AddSize(root, size.x, size.y);

        RectTransform rectTransform = root.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(
            size.x > 0f ? size.x : rectTransform.sizeDelta.x,
            size.y > 0f ? size.y : rectTransform.sizeDelta.y);

        Text text = CreateText("Text", root.transform, label, font, label.Length <= 2 ? 23 : 15, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static Text CreateText(string objectName, Transform parent, string value, Font font, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.text = value;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static GameObject CreateImageObject(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return imageObject;
    }

    private static void AddSize(GameObject gameObject, float width, float height)
    {
        LayoutElement layout = gameObject.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout = gameObject.AddComponent<LayoutElement>();
        }

        if (width > 0f)
        {
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        if (height > 0f)
        {
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetIcon(Image image, Text fallbackText, ItemSO item)
    {
        Sprite icon = InventoryUIController.GetItemIconSprite(item);

        if (image != null)
        {
            image.enabled = icon != null;
            image.sprite = icon;
        }

        if (fallbackText != null)
        {
            fallbackText.text = icon == null && item != null && !string.IsNullOrWhiteSpace(item.itemName)
                ? item.itemName.Substring(0, 1).ToUpperInvariant()
                : string.Empty;
        }
    }

    private static InputsController ResolveInputs(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            return null;
        }

        InputsController result = inventory.GetComponent<InputsController>();

        if (result == null)
        {
            result = inventory.GetComponentInParent<InputsController>();
        }

        if (result == null)
        {
            result = inventory.GetComponentInChildren<InputsController>(true);
        }

        return result;
    }

    private static string BuildTooltipStats(ItemSO item, int amount)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Количество: {amount}");
        builder.AppendLine($"Используемый: {(PlayerInventory.CanUseItem(item) ? "да" : "нет")}");
        builder.AppendLine($"Тип: {GetItemTypeLabel(item.itemType)}");
        builder.AppendLine($"Редкость: {GetRarityLabel(item.rarity)}");

        if (item.weight > 0f)
        {
            builder.AppendLine($"Вес: {item.weight * amount:0.##}");
        }

        if (item.itemType == ItemType.Ammo)
        {
            builder.AppendLine($"Оружие: {GetAmmoWeaponLabel(item)}");
            builder.AppendLine($"Патронов в коробке: {item.ammoAmount}");
        }
        else if (item.itemType == ItemType.Drink)
        {
            builder.AppendLine($"Жажда: +{item.thirstRestoreAmount:0.##}");
        }
        else if (item.itemType == ItemType.Food)
        {
            builder.AppendLine($"Еда: +{item.hungerRestoreAmount:0.##}");
        }
        else if (item.itemType == ItemType.Healing)
        {
            builder.AppendLine($"Лечение: +{item.healthRestoreAmount:0.##}");
        }

        return builder.ToString().TrimEnd();
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

        return !string.IsNullOrWhiteSpace(item.ammoWeaponID) ? item.ammoWeaponID : "Текущее оружие";
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

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private static Vector2 ClampToScreen(Vector2 position, RectTransform rect)
    {
        if (rect == null)
        {
            return position;
        }

        Vector2 size = rect.rect.size;
        float x = Mathf.Clamp(position.x, size.x * 0.5f + 8f, Screen.width - size.x * 0.5f - 8f);
        float y = Mathf.Clamp(position.y, size.y * 0.5f + 8f, Screen.height - size.y * 0.5f - 8f);
        return new Vector2(x, y);
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
