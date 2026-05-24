using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("Zombie Rogue/UI/Raid/Raid Result UI")]
public class RaidResultUI : MonoBehaviour
{
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text statsText;
    [SerializeField] private Button continueButton;

    private RaidResult currentResult;
    private bool rewardApplied;
    private bool cursorPushed;

    private void Awake()
    {
        EnsureEventSystem();
        BuildDefaultUIIfNeeded();
        SetOpen(false);
    }

    private void Start()
    {
        if (GameSessionState.CurrentMode == GameMode.RaidResult && GameSessionState.LastRaidResult != null)
            Show(GameSessionState.LastRaidResult);
    }

    public void Show(RaidResult result)
    {
        if (result == null)
            return;

        currentResult = result;
        rewardApplied = false;
        UpdateTexts();
        SetOpen(true);
    }

    private void Continue()
    {
        ApplyRewardIfNeeded();
        SetOpen(false);
        GameFlowManager.Instance.ReturnToBunker();
    }

    private void OnDisable()
    {
        ReleaseCursorIfNeeded();
    }

    private void OnDestroy()
    {
        ReleaseCursorIfNeeded();
    }

    private void ApplyRewardIfNeeded()
    {
        if (rewardApplied || currentResult == null || currentResult.experienceEarned <= 0)
            return;

        CharacterProgression progression = FindObjectOfType<CharacterProgression>(true);
        if (progression != null)
        {
            progression.AddExperience(currentResult.experienceEarned);
            rewardApplied = true;
            return;
        }

        CharacterStats stats = FindPlayerStats();
        if (stats != null)
        {
            stats.AddExperience(currentResult.experienceEarned);
            rewardApplied = true;
            return;
        }

        Debug.LogWarning("RaidResultUI could not find CharacterProgression or CharacterStats to apply raid experience.", this);
    }

    private void UpdateTexts()
    {
        if (currentResult == null)
            return;

        CharacterProgression progression = FindObjectOfType<CharacterProgression>(true);
        CharacterStats playerStats = FindPlayerStats();
        int currentLevel = progression != null
            ? progression.CurrentLevel
            : playerStats != null
                ? Mathf.Max(1, playerStats.playerLevel)
                : 1;
        int predictedLevel = progression != null
            ? progression.PredictLevelAfterExperience(currentResult.experienceEarned)
            : playerStats != null
                ? PredictLevelAfterExperience(playerStats, currentResult.experienceEarned)
                : currentLevel;

        if (titleText != null)
            titleText.text = currentResult.extractionSuccess ? "Эвакуация успешна" : "Рейд провален";

        if (statsText != null)
        {
            RaidStatsSnapshot stats = currentResult.stats ?? new RaidStatsSnapshot();
            statsText.text =
                $"Локация: {currentResult.locationName}\n" +
                $"Убийства: {stats.kills}\n" +
                $"Урон нанесён: {stats.damageDealt:0}\n" +
                $"Урон получен: {stats.damageTaken:0}\n" +
                $"Задачи: {stats.objectivesCompleted}\n" +
                $"Предметы: {stats.itemsLooted}\n" +
                $"Возрождения: {stats.alliesRevived}\n" +
                $"Время: {stats.raidTime:0} с\n" +
                $"Опыт: +{currentResult.experienceEarned}\n" +
                $"Уровень: {currentLevel} -> {predictedLevel}";
        }
    }

    private static CharacterStats FindPlayerStats()
    {
        CharacterStats[] allStats = FindObjectsOfType<CharacterStats>(true);
        if (allStats.Length == 0)
            return null;

        for (int i = 0; i < allStats.Length; i++)
        {
            CharacterStats stats = allStats[i];
            if (stats != null && stats.CompareTag("Player"))
                return stats;
        }

        return allStats[0];
    }

    private static int PredictLevelAfterExperience(CharacterStats stats, int experienceAmount)
    {
        if (stats == null)
            return 1;

        int predictedLevel = Mathf.Max(1, stats.playerLevel);
        int predictedExperience = Mathf.Max(0, stats.currentExp) + Mathf.Max(0, experienceAmount);
        int predictedExperienceToNextLevel = Mathf.Max(1, stats.expToNextLevel);

        while (predictedExperience >= predictedExperienceToNextLevel)
        {
            predictedExperience -= predictedExperienceToNextLevel;
            predictedLevel++;
            predictedExperienceToNextLevel = Mathf.Max(1, Mathf.RoundToInt(predictedExperienceToNextLevel * 1.4f));
        }

        return predictedLevel;
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

    private void BuildDefaultUIIfNeeded()
    {
        if (uiRoot != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiRoot = new GameObject("Raid Result UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shade = CreateImage("Shade", uiRoot.transform, new Color(0.01f, 0.012f, 0.015f, 0.84f));
        Stretch(shade.GetComponent<RectTransform>());

        GameObject panel = CreateImage("Panel", shade.transform, new Color(0.055f, 0.06f, 0.065f, 0.98f));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 520f);

        titleText = CreateText("Title", panel.transform, "Итоги рейда", font, 26, TextAnchor.MiddleCenter);
        titleText.fontStyle = FontStyle.Bold;
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -18f);
        titleText.rectTransform.sizeDelta = new Vector2(-36f, 46f);

        statsText = CreateText("Stats", panel.transform, string.Empty, font, 17, TextAnchor.UpperLeft);
        statsText.rectTransform.anchorMin = new Vector2(0f, 0f);
        statsText.rectTransform.anchorMax = new Vector2(1f, 1f);
        statsText.rectTransform.offsetMin = new Vector2(34f, 88f);
        statsText.rectTransform.offsetMax = new Vector2(-34f, -78f);

        continueButton = CreateButton("Continue", panel.transform, "Продолжить", font);
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 24f);
        buttonRect.sizeDelta = new Vector2(180f, 44f);
        continueButton.onClick.AddListener(Continue);
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
