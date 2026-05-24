using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/UI/Raid/Location Selection UI")]
public class LocationSelectionUI : MonoBehaviour
{
    [SerializeField] private BunkerManager bunkerManager;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text difficultyText;
    [SerializeField] private Text recommendedLevelText;
    [SerializeField] private Button startButton;
    [SerializeField] private Button closeButton;

    private readonly List<Button> locationButtons = new();
    private LocationDefinition selectedLocation;
    private bool cursorPushed;

    private void Awake()
    {
        EnsureEventSystem();
        BuildDefaultUIIfNeeded();
        Close();
    }

    public void Open(BunkerManager manager)
    {
        bunkerManager = manager != null ? manager : bunkerManager;
        if (bunkerManager == null)
            bunkerManager = BunkerManager.Instance ?? FindObjectOfType<BunkerManager>(true);

        RefreshLocationList();
        SetOpen(true);
    }

    public void Close()
    {
        SetOpen(false);
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

    private void RefreshLocationList()
    {
        ClearLocationButtons();

        IReadOnlyList<LocationDefinition> locations = bunkerManager != null ? bunkerManager.Locations : null;
        selectedLocation = null;

        if (locations == null || locations.Count == 0)
        {
            UpdateDetails(null);
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < locations.Count; i++)
        {
            LocationDefinition location = locations[i];
            if (location == null)
                continue;

            bool unlocked = bunkerManager == null || bunkerManager.IsLocationUnlocked(location);
            Button button = CreateLocationButton(location, unlocked, font);
            locationButtons.Add(button);

            if (selectedLocation == null && unlocked)
                selectedLocation = location;
        }

        UpdateDetails(selectedLocation);
    }

    private Button CreateLocationButton(LocationDefinition location, bool unlocked, Font font)
    {
        GameObject buttonObject = new GameObject(location.DisplayNameOrId, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(listRoot, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = unlocked ? new Color(0.13f, 0.15f, 0.16f, 0.96f) : new Color(0.08f, 0.08f, 0.08f, 0.82f);

        Button button = buttonObject.GetComponent<Button>();
        button.interactable = unlocked;
        button.onClick.AddListener(() => SelectLocation(location));

        Text label = CreateText("Label", buttonObject.transform, unlocked ? location.DisplayNameOrId : $"{location.DisplayNameOrId} (locked)", font, 16, TextAnchor.MiddleLeft);
        Stretch(label.rectTransform, new Vector2(12f, 0f), new Vector2(-12f, 0f));

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.minHeight = 38f;
        layout.preferredHeight = 38f;

        return button;
    }

    private void SelectLocation(LocationDefinition location)
    {
        selectedLocation = location;
        UpdateDetails(location);
    }

    private void StartSelectedLocation()
    {
        if (selectedLocation == null)
            return;

        GameFlowManager.Instance.StartRaid(selectedLocation);
    }

    private void UpdateDetails(LocationDefinition location)
    {
        if (titleText != null)
            titleText.text = location != null ? location.DisplayNameOrId : "No location";

        if (descriptionText != null)
            descriptionText.text = location != null ? location.description : "Create a LocationDefinition and assign it to BunkerManager.";

        if (difficultyText != null)
            difficultyText.text = location != null ? $"Difficulty: {location.difficulty}" : "Difficulty: -";

        if (recommendedLevelText != null)
            recommendedLevelText.text = location != null ? $"Recommended level: {location.recommendedLevel}" : "Recommended level: -";

        if (startButton != null)
            startButton.interactable = location != null;
    }

    private void ClearLocationButtons()
    {
        for (int i = 0; i < locationButtons.Count; i++)
        {
            if (locationButtons[i] != null)
                Destroy(locationButtons[i].gameObject);
        }

        locationButtons.Clear();
    }

    private void BuildDefaultUIIfNeeded()
    {
        if (uiRoot != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiRoot = new GameObject("Location Selection UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

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
        panelRect.sizeDelta = new Vector2(920f, 520f);

        titleText = CreateText("Title", panel.transform, "Location", font, 26, TextAnchor.MiddleLeft);
        titleText.fontStyle = FontStyle.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        titleText.rectTransform.sizeDelta = new Vector2(-44f, 42f);

        GameObject listPanel = CreateImage("Location List", panel.transform, new Color(0.08f, 0.085f, 0.09f, 1f));
        listRoot = listPanel.GetComponent<RectTransform>();
        listRoot.anchorMin = new Vector2(0f, 0f);
        listRoot.anchorMax = new Vector2(0f, 1f);
        listRoot.pivot = new Vector2(0f, 0.5f);
        listRoot.anchoredPosition = new Vector2(24f, -20f);
        listRoot.sizeDelta = new Vector2(300f, -112f);
        VerticalLayoutGroup listLayout = listPanel.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(8, 8, 8, 8);
        listLayout.spacing = 8f;
        listLayout.childControlWidth = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandHeight = false;

        RectTransform detailsRoot = new GameObject("Details", typeof(RectTransform)).GetComponent<RectTransform>();
        detailsRoot.transform.SetParent(panel.transform, false);
        detailsRoot.anchorMin = new Vector2(0f, 0f);
        detailsRoot.anchorMax = new Vector2(1f, 1f);
        detailsRoot.offsetMin = new Vector2(350f, 84f);
        detailsRoot.offsetMax = new Vector2(-28f, -82f);

        descriptionText = CreateText("Description", detailsRoot, string.Empty, font, 17, TextAnchor.UpperLeft);
        Stretch(descriptionText.rectTransform);

        difficultyText = CreateText("Difficulty", panel.transform, "Difficulty: -", font, 16, TextAnchor.MiddleLeft);
        difficultyText.rectTransform.anchorMin = new Vector2(0f, 0f);
        difficultyText.rectTransform.anchorMax = new Vector2(1f, 0f);
        difficultyText.rectTransform.anchoredPosition = new Vector2(350f, 72f);
        difficultyText.rectTransform.sizeDelta = new Vector2(-390f, 28f);

        recommendedLevelText = CreateText("Recommended Level", panel.transform, "Recommended level: -", font, 16, TextAnchor.MiddleLeft);
        recommendedLevelText.rectTransform.anchorMin = new Vector2(0f, 0f);
        recommendedLevelText.rectTransform.anchorMax = new Vector2(1f, 0f);
        recommendedLevelText.rectTransform.anchoredPosition = new Vector2(350f, 42f);
        recommendedLevelText.rectTransform.sizeDelta = new Vector2(-390f, 28f);

        startButton = CreateButton("Start Raid", panel.transform, "Start raid", font);
        startButton.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 0f);
        startButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 0f);
        startButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
        startButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-28f, 24f);
        startButton.GetComponent<RectTransform>().sizeDelta = new Vector2(170f, 42f);
        startButton.onClick.AddListener(StartSelectedLocation);

        closeButton = CreateButton("Close", panel.transform, "Close", font);
        closeButton.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
        closeButton.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        closeButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
        closeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-22f, -18f);
        closeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(92f, 34f);
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
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
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
