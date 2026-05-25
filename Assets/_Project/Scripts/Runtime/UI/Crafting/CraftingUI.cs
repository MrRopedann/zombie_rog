using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/UI/Crafting/Crafting UI")]
public class CraftingUI : MonoBehaviour
{
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform recipeListRoot;
    [SerializeField] private RectTransform ingredientListRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text resultText;
    [SerializeField] private Text errorText;
    [SerializeField] private Button craftButton;
    [SerializeField] private Button closeButton;

    private readonly List<CraftingRecipeSlotUI> recipeSlots = new();
    private readonly List<CraftingIngredientRowUI> ingredientRows = new();
    private CraftingSystem craftingSystem;
    private CraftingStation station;
    private CraftingRecipe selectedRecipe;
    private bool cursorPushed;

    private void Awake()
    {
        EnsureEventSystem();
        BuildDefaultUIIfNeeded();
        SetOpen(false);
    }

    public void Open(CraftingSystem system, CraftingStation station)
    {
        craftingSystem = system != null ? system : FindObjectOfType<CraftingSystem>(true);
        this.station = station;

        RefreshRecipes();
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void OnDisable()
    {
        ReleaseCursorIfNeeded();
    }

    private void OnDestroy()
    {
        ReleaseCursorIfNeeded();
    }

    private void RefreshRecipes()
    {
        ClearRecipes();
        ClearIngredients();

        if (craftingSystem == null)
        {
            SelectRecipe(null);
            ShowMessage("Crafting system is missing.");
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CraftingStationType stationType = station != null ? station.StationType : CraftingStationType.Any;
        int stationLevel = station != null ? station.CurrentLevel : 1;
        List<CraftingRecipe> recipes = craftingSystem.GetAvailableRecipes(stationType, stationLevel);

        for (int i = 0; i < recipes.Count; i++)
        {
            CraftingRecipe recipe = recipes[i];
            if (recipe == null)
                continue;

            GameObject row = CreateImage(recipe.DisplayNameOrId, recipeListRoot, new Color(0.13f, 0.15f, 0.16f, 0.96f));
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 36f;
            layout.preferredHeight = 36f;

            CraftingRecipeSlotUI slot = row.AddComponent<CraftingRecipeSlotUI>();
            slot.Bind(recipe, font, SelectRecipe);
            recipeSlots.Add(slot);

            if (selectedRecipe == null)
                selectedRecipe = recipe;
        }

        SelectRecipe(selectedRecipe);
    }

    private void SelectRecipe(CraftingRecipe recipe)
    {
        selectedRecipe = recipe;
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        ClearIngredients();

        if (selectedRecipe == null)
        {
            if (titleText != null)
                titleText.text = "No recipes";
            if (descriptionText != null)
                descriptionText.text = string.Empty;
            if (resultText != null)
                resultText.text = string.Empty;
            if (craftButton != null)
                craftButton.interactable = false;
            return;
        }

        if (titleText != null)
            titleText.text = selectedRecipe.DisplayNameOrId;

        if (descriptionText != null)
            descriptionText.text = selectedRecipe.description;

        if (resultText != null)
            resultText.text = selectedRecipe.resultItem != null
                ? $"Result: {selectedRecipe.resultAmount}x {GetItemName(selectedRecipe.resultItem)}"
                : "Result: not configured";

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (selectedRecipe.ingredients != null)
        {
            for (int i = 0; i < selectedRecipe.ingredients.Count; i++)
            {
                ItemAmount ingredient = selectedRecipe.ingredients[i];
                if (ingredient == null)
                    continue;

                GameObject row = new GameObject("Ingredient", typeof(RectTransform));
                row.transform.SetParent(ingredientListRoot, false);
                LayoutElement layout = row.AddComponent<LayoutElement>();
                layout.minHeight = 24f;
                layout.preferredHeight = 24f;

                CraftingIngredientRowUI ingredientRow = row.AddComponent<CraftingIngredientRowUI>();
                int available = craftingSystem != null ? craftingSystem.GetAvailableAmount(ingredient) : 0;
                ingredientRow.Bind(ingredient, available, font);
                ingredientRows.Add(ingredientRow);
            }
        }

        bool canCraft = craftingSystem != null && craftingSystem.CanCraft(selectedRecipe, station, out string reason);
        if (craftButton != null)
            craftButton.interactable = canCraft;

        ShowMessage(canCraft ? string.Empty : reason);
    }

    private void CraftSelected()
    {
        if (craftingSystem == null || selectedRecipe == null)
            return;

        CraftingResult result = craftingSystem.Craft(selectedRecipe, station);
        ShowMessage(result != null ? result.message : "Crafting failed.");
        RefreshDetails();
    }

    private void ShowMessage(string message)
    {
        if (errorText != null)
            errorText.text = message ?? string.Empty;
    }

    private void SetOpen(bool open)
    {
        if (uiRoot != null)
            uiRoot.SetActive(open);

        if (open && !cursorPushed)
        {
            GameCursorGuard.PushUiCursor();
            cursorPushed = true;
        }
        else if (!open && cursorPushed)
        {
            ReleaseCursorIfNeeded();
        }
    }

    private void ReleaseCursorIfNeeded()
    {
        if (!cursorPushed)
            return;

        GameCursorGuard.PopUiCursor();
        cursorPushed = false;
    }

    private void ClearRecipes()
    {
        for (int i = 0; i < recipeSlots.Count; i++)
        {
            if (recipeSlots[i] != null)
                Destroy(recipeSlots[i].gameObject);
        }

        recipeSlots.Clear();
        selectedRecipe = null;
    }

    private void ClearIngredients()
    {
        for (int i = 0; i < ingredientRows.Count; i++)
        {
            if (ingredientRows[i] != null)
                Destroy(ingredientRows[i].gameObject);
        }

        ingredientRows.Clear();
    }

    private void BuildDefaultUIIfNeeded()
    {
        if (uiRoot != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiRoot = new GameObject("Crafting UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 82;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shade = CreateImage("Shade", uiRoot.transform, new Color(0.015f, 0.018f, 0.02f, 0.82f));
        Stretch(shade.GetComponent<RectTransform>());

        GameObject panel = CreateImage("Panel", shade.transform, new Color(0.055f, 0.06f, 0.065f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(860f, 520f);

        GameObject listPanel = CreateImage("Recipe List", panel.transform, new Color(0.08f, 0.085f, 0.09f, 1f));
        recipeListRoot = listPanel.GetComponent<RectTransform>();
        recipeListRoot.anchorMin = new Vector2(0f, 0f);
        recipeListRoot.anchorMax = new Vector2(0f, 1f);
        recipeListRoot.pivot = new Vector2(0f, 0.5f);
        recipeListRoot.anchoredPosition = new Vector2(24f, 0f);
        recipeListRoot.sizeDelta = new Vector2(280f, -56f);
        VerticalLayoutGroup recipeLayout = listPanel.AddComponent<VerticalLayoutGroup>();
        recipeLayout.padding = new RectOffset(8, 8, 8, 8);
        recipeLayout.spacing = 8f;
        recipeLayout.childControlWidth = true;
        recipeLayout.childForceExpandWidth = true;
        recipeLayout.childControlHeight = true;
        recipeLayout.childForceExpandHeight = false;

        titleText = CreateText("Title", panel.transform, "Crafting", font, 26, TextAnchor.MiddleLeft);
        titleText.fontStyle = FontStyle.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(328f, -70f);
        titleText.rectTransform.offsetMax = new Vector2(-124f, -20f);

        descriptionText = CreateText("Description", panel.transform, string.Empty, font, 16, TextAnchor.UpperLeft);
        descriptionText.rectTransform.anchorMin = new Vector2(0f, 1f);
        descriptionText.rectTransform.anchorMax = new Vector2(1f, 1f);
        descriptionText.rectTransform.offsetMin = new Vector2(328f, -170f);
        descriptionText.rectTransform.offsetMax = new Vector2(-36f, -80f);

        resultText = CreateText("Result", panel.transform, string.Empty, font, 16, TextAnchor.MiddleLeft);
        resultText.rectTransform.anchorMin = new Vector2(0f, 0f);
        resultText.rectTransform.anchorMax = new Vector2(1f, 0f);
        resultText.rectTransform.offsetMin = new Vector2(328f, 158f);
        resultText.rectTransform.offsetMax = new Vector2(-36f, 190f);

        GameObject ingredientsPanel = CreateImage("Ingredients", panel.transform, new Color(0.075f, 0.08f, 0.085f, 1f));
        ingredientListRoot = ingredientsPanel.GetComponent<RectTransform>();
        ingredientListRoot.anchorMin = new Vector2(0f, 0f);
        ingredientListRoot.anchorMax = new Vector2(1f, 0f);
        ingredientListRoot.offsetMin = new Vector2(328f, 92f);
        ingredientListRoot.offsetMax = new Vector2(-36f, 154f);
        VerticalLayoutGroup ingredientLayout = ingredientsPanel.AddComponent<VerticalLayoutGroup>();
        ingredientLayout.padding = new RectOffset(8, 8, 8, 8);
        ingredientLayout.spacing = 4f;
        ingredientLayout.childControlWidth = true;
        ingredientLayout.childForceExpandWidth = true;
        ingredientLayout.childControlHeight = true;
        ingredientLayout.childForceExpandHeight = false;

        errorText = CreateText("Message", panel.transform, string.Empty, font, 14, TextAnchor.UpperLeft);
        errorText.color = new Color(1f, 0.76f, 0.52f);
        errorText.rectTransform.anchorMin = new Vector2(0f, 0f);
        errorText.rectTransform.anchorMax = new Vector2(1f, 0f);
        errorText.rectTransform.offsetMin = new Vector2(328f, 24f);
        errorText.rectTransform.offsetMax = new Vector2(-236f, 82f);

        craftButton = CreateButton("Craft", panel.transform, "Create", font);
        RectTransform craftRect = craftButton.GetComponent<RectTransform>();
        craftRect.anchorMin = new Vector2(1f, 0f);
        craftRect.anchorMax = new Vector2(1f, 0f);
        craftRect.pivot = new Vector2(1f, 0f);
        craftRect.anchoredPosition = new Vector2(-36f, 28f);
        craftRect.sizeDelta = new Vector2(150f, 42f);
        craftButton.onClick.AddListener(CraftSelected);

        closeButton = CreateButton("Close", panel.transform, "Close", font);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-22f, -18f);
        closeRect.sizeDelta = new Vector2(92f, 34f);
        closeButton.onClick.AddListener(Close);
    }

    private static Button CreateButton(string name, Transform parent, string label, Font font)
    {
        GameObject buttonObject = CreateImage(name, parent, new Color(0.16f, 0.18f, 0.19f, 1f));
        Button button = buttonObject.AddComponent<Button>();
        Text text = CreateText("Text", buttonObject.transform, label, font, 16, TextAnchor.MiddleCenter);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
        return button;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        imageObject.GetComponent<Image>().color = color;
        return imageObject;
    }

    private static Text CreateText(string name, Transform parent, string value, Font font, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static string GetItemName(ItemSO item)
    {
        return item != null && !string.IsNullOrWhiteSpace(item.itemName)
            ? item.itemName
            : item != null ? item.name : "item";
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }
}
