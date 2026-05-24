using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/UI/Raid/Raid Objective UI")]
public class RaidObjectiveUI : MonoBehaviour
{
    [SerializeField] private ObjectiveManager objectiveManager;
    [SerializeField] private RaidManager raidManager;
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Text objectiveText;

    private void Awake()
    {
        BuildDefaultUIIfNeeded();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        Refresh(objectiveManager != null ? objectiveManager.ActiveObjective : null);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveProgressChanged -= Refresh;
            objectiveManager.OnObjectiveCompleted -= Refresh;
            objectiveManager.OnObjectiveProgressChanged += Refresh;
            objectiveManager.OnObjectiveCompleted += Refresh;
        }

        if (raidManager != null)
        {
            raidManager.OnExtractionActivated -= HandleExtractionActivated;
            raidManager.OnExtractionActivated += HandleExtractionActivated;
        }
    }

    private void Unsubscribe()
    {
        if (objectiveManager != null)
        {
            objectiveManager.OnObjectiveProgressChanged -= Refresh;
            objectiveManager.OnObjectiveCompleted -= Refresh;
        }

        if (raidManager != null)
            raidManager.OnExtractionActivated -= HandleExtractionActivated;
    }

    private void Refresh(IObjective objective)
    {
        if (objectiveText == null)
            return;

        IObjective displayObjective = objective ?? (objectiveManager != null ? objectiveManager.ActiveObjective : null);
        if (displayObjective == null)
        {
            objectiveText.text = string.Empty;
            return;
        }

        objectiveText.text = $"{displayObjective.DisplayName}: {displayObjective.CurrentCount}/{displayObjective.TargetCount}";
    }

    private void HandleExtractionActivated()
    {
        if (objectiveText != null)
            objectiveText.text = "Extraction active";
    }

    private void ResolveReferences()
    {
        if (objectiveManager == null)
            objectiveManager = FindObjectOfType<ObjectiveManager>(true);

        if (raidManager == null)
            raidManager = RaidManager.Instance ?? FindObjectOfType<RaidManager>(true);
    }

    private void BuildDefaultUIIfNeeded()
    {
        if (uiRoot != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiRoot = new GameObject("Raid Objective UI", typeof(Canvas), typeof(CanvasScaler));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Objective Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(uiRoot.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -28f);
        panelRect.sizeDelta = new Vector2(520f, 48f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.035f, 0.04f, 0.045f, 0.78f);

        objectiveText = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        objectiveText.transform.SetParent(panel.transform, false);
        objectiveText.font = font;
        objectiveText.fontSize = 18;
        objectiveText.alignment = TextAnchor.MiddleCenter;
        objectiveText.color = Color.white;
        objectiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
        objectiveText.verticalOverflow = VerticalWrapMode.Truncate;
        objectiveText.rectTransform.anchorMin = Vector2.zero;
        objectiveText.rectTransform.anchorMax = Vector2.one;
        objectiveText.rectTransform.offsetMin = new Vector2(12f, 0f);
        objectiveText.rectTransform.offsetMax = new Vector2(-12f, 0f);
    }
}
