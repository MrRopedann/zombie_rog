using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/UI/Bunker/Station Upgrade UI")]
public class StationUpgradeUI : MonoBehaviour
{
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text requirementText;
    [SerializeField] private Text messageText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private StationUpgradeSystem upgradeSystem;

    private BuildableStation station;
    private bool cursorPushed;

    private void Awake()
    {
        EnsureEventSystem();
        BuildDefaultUIIfNeeded();
        SetOpen(false);
    }

    public void Open(BuildableStation station)
    {
        this.station = station;
        if (upgradeSystem == null)
            upgradeSystem = FindObjectOfType<StationUpgradeSystem>(true);

        Refresh();
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
    }

    private void OnDisable()
    {
        if (cursorPushed)
        {
            GameCursorGuard.PopUiCursor();
            cursorPushed = false;
        }
    }

    private void Upgrade()
    {
        if (upgradeSystem == null || station == null)
            return;

        CraftingResult result = upgradeSystem.Upgrade(station);
        if (messageText != null)
            messageText.text = result != null ? result.message : "Upgrade failed.";

        Refresh();
    }

    private void Refresh()
    {
        if (station == null)
            return;

        if (titleText != null)
            titleText.text = $"{station.DisplayName} level {station.CurrentLevel}/{station.MaxLevel}";

        CraftingRecipe recipe = upgradeSystem != null ? upgradeSystem.GetNextUpgradeRecipe(station) : null;
        if (requirementText != null)
            requirementText.text = recipe != null
                ? $"Upgrade recipe: {recipe.DisplayNameOrId}"
                : station.CurrentLevel >= station.MaxLevel
                    ? "Max level reached."
                    : "No upgrade recipe configured.";

        if (upgradeButton != null)
            upgradeButton.interactable = station.CurrentLevel < station.MaxLevel;
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
            GameCursorGuard.PopUiCursor();
            cursorPushed = false;
        }
    }

    private void BuildDefaultUIIfNeeded()
    {
        if (uiRoot != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiRoot = new GameObject("Station Upgrade UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 83;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject panel = CreateImage("Panel", uiRoot.transform, new Color(0.055f, 0.06f, 0.065f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(460f, 260f);

        titleText = CreateText("Title", panel.transform, "Station", font, 22, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.offsetMin = new Vector2(24f, -62f);
        titleText.rectTransform.offsetMax = new Vector2(-24f, -18f);

        requirementText = CreateText("Requirements", panel.transform, string.Empty, font, 16, TextAnchor.UpperLeft);
        requirementText.rectTransform.anchorMin = new Vector2(0f, 0f);
        requirementText.rectTransform.anchorMax = new Vector2(1f, 1f);
        requirementText.rectTransform.offsetMin = new Vector2(30f, 92f);
        requirementText.rectTransform.offsetMax = new Vector2(-30f, -78f);

        messageText = CreateText("Message", panel.transform, string.Empty, font, 14, TextAnchor.UpperLeft);
        messageText.color = new Color(1f, 0.76f, 0.52f);
        messageText.rectTransform.anchorMin = new Vector2(0f, 0f);
        messageText.rectTransform.anchorMax = new Vector2(1f, 0f);
        messageText.rectTransform.offsetMin = new Vector2(30f, 56f);
        messageText.rectTransform.offsetMax = new Vector2(-30f, 88f);

        upgradeButton = CreateButton("Upgrade", panel.transform, "Upgrade", font);
        RectTransform upgradeRect = upgradeButton.GetComponent<RectTransform>();
        upgradeRect.anchorMin = new Vector2(1f, 0f);
        upgradeRect.anchorMax = new Vector2(1f, 0f);
        upgradeRect.pivot = new Vector2(1f, 0f);
        upgradeRect.anchoredPosition = new Vector2(-30f, 24f);
        upgradeRect.sizeDelta = new Vector2(130f, 40f);
        upgradeButton.onClick.AddListener(Upgrade);

        closeButton = CreateButton("Close", panel.transform, "Close", font);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0f, 0f);
        closeRect.anchorMax = new Vector2(0f, 0f);
        closeRect.anchoredPosition = new Vector2(30f, 24f);
        closeRect.sizeDelta = new Vector2(110f, 40f);
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
