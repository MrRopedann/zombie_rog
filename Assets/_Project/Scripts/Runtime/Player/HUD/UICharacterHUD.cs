using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICharacterHUD : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private bool createFallbackIfNoAssignedReferences;

    [Header("Радиальные круги")]
    [SerializeField] private Image healthFillImage;      // Круг здоровья
    [SerializeField] private Image staminaFillImage;     // Круг стамины
    [SerializeField] private Image hungerFillImage;      // Круг голода
    [SerializeField] private Image thirstFillImage;      // Круг жажды

    [Header("Текст по центру кругов")]
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI staminaText;
    [SerializeField] private TextMeshProUGUI hungerText;
    [SerializeField] private TextMeshProUGUI thirstText;

    [Header("Уровень и очки характеристик")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI statPointsText;

    private CharacterStats stats;
    private CharacterProgression progression;
    private bool generatedFallbackHud;
    private bool subscribed;

    private void Awake()
    {
        BuildFallbackUIIfNeeded();
    }

    private void OnEnable()
    {
        BuildFallbackUIIfNeeded();
        TryBindStats();
    }

    private void Start()
    {
        TryBindStats();
    }

    private void Update()
    {
        if (stats == null)
            TryBindStats();
    }

    private void TryBindStats()
    {
        CharacterStats resolvedStats = FindStatsForThisHud();
        if (resolvedStats == null)
            return;

        if (stats == resolvedStats && subscribed)
            return;

        UnsubscribeEvents();

        stats = resolvedStats;
        progression = ResolveProgression(stats);
        SubscribeEvents();
        UpdateAllUI();
    }

    private void SubscribeEvents()
    {
        if (stats == null || subscribed)
            return;

        stats.OnHealthChanged += UpdateHealthUI;
        stats.OnHungerChanged += UpdateHungerUI;
        stats.OnThirstChanged += UpdateThirstUI;
        stats.OnStaminaChanged += UpdateStaminaUI;
        stats.OnStatsRecalculated += UpdateAllUI;
        stats.OnLevelChanged += UpdateLevelUI;

        subscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (stats == null || !subscribed)
            return;

        stats.OnHealthChanged -= UpdateHealthUI;
        stats.OnHungerChanged -= UpdateHungerUI;
        stats.OnThirstChanged -= UpdateThirstUI;
        stats.OnStaminaChanged -= UpdateStaminaUI;
        stats.OnStatsRecalculated -= UpdateAllUI;
        stats.OnLevelChanged -= UpdateLevelUI;

        subscribed = false;
    }

    private void UpdateAllUI()
    {
        if (stats == null)
            return;

        UpdateHealthUI();
        UpdateStaminaUI();
        UpdateHungerUI();
        UpdateThirstUI();
        UpdateLevelUI();
    }

    // ====================== ОБНОВЛЕНИЕ КРУГОВ ======================

    private void UpdateHealthUI()
    {
        if (stats == null)
            return;

        float percent = stats.HealthPercent;

        if (healthFillImage != null)
            healthFillImage.fillAmount = percent;

        if (healthText != null)
            healthText.text = generatedFallbackHud
                ? $"Здоровье: {Mathf.RoundToInt(stats.currentHealth)} / {Mathf.RoundToInt(stats.MaxHealth)}"
                : Mathf.RoundToInt(stats.currentHealth).ToString();
    }

    private void UpdateStaminaUI()
    {
        if (stats == null)
            return;

        if (staminaFillImage != null)
            staminaFillImage.fillAmount = stats.StaminaPercent;

        if (staminaText != null)
            staminaText.text = generatedFallbackHud
                ? $"Стамина: {Mathf.RoundToInt(stats.currentStamina)} / {Mathf.RoundToInt(stats.CurrentStaminaLimit)}"
                : Mathf.RoundToInt(stats.currentStamina).ToString();
    }

    private void UpdateHungerUI()
    {
        if (stats == null)
            return;

        if (hungerFillImage != null)
            hungerFillImage.fillAmount = stats.HungerPercent;

        if (hungerText != null)
            hungerText.text = generatedFallbackHud
                ? $"Голод: {Mathf.RoundToInt(stats.currentHunger)} / {Mathf.RoundToInt(stats.MaxHunger)}"
                : Mathf.RoundToInt(stats.currentHunger).ToString();
    }

    private void UpdateThirstUI()
    {
        if (stats == null)
            return;

        if (thirstFillImage != null)
            thirstFillImage.fillAmount = stats.ThirstPercent;

        if (thirstText != null)
            thirstText.text = generatedFallbackHud
                ? $"Жажда: {Mathf.RoundToInt(stats.currentThirst)} / {Mathf.RoundToInt(stats.MaxThirst)}"
                : Mathf.RoundToInt(stats.currentThirst).ToString();
    }

    private void UpdateLevelUI()
    {
        if (stats == null)
            return;

        if (progression == null)
            progression = ResolveProgression(stats);

        if (levelText != null)
            levelText.text = generatedFallbackHud ? $"Уровень: {stats.playerLevel}" : $"Lv.{stats.playerLevel}";

        if (statPointsText != null)
        {
            int points = progression != null ? progression.availableStatPoints : 0;
            statPointsText.text = generatedFallbackHud ? $"Очки характеристик: {points}" : $"Очки: {points}";
        }
    }

    private CharacterStats FindStatsForThisHud()
    {
        CharacterStats localStats = GetComponentInParent<CharacterStats>();
        if (localStats != null)
            return localStats;

        localStats = GetComponentInChildren<CharacterStats>(true);
        if (localStats != null)
            return localStats;

        return FindPlayerStats();
    }

    private static CharacterStats FindPlayerStats()
    {
        CharacterStats[] allStats = FindObjectsOfType<CharacterStats>(true);
        if (allStats.Length == 0)
            return null;

        for (int i = 0; i < allStats.Length; i++)
        {
            CharacterStats candidate = allStats[i];
            if (candidate != null && candidate.CompareTag("Player"))
                return candidate;
        }

        return allStats[0];
    }

    private static CharacterProgression ResolveProgression(CharacterStats characterStats)
    {
        if (characterStats == null)
            return null;

        return characterStats.GetComponent<CharacterProgression>() ??
            characterStats.GetComponentInChildren<CharacterProgression>(true) ??
            characterStats.GetComponentInParent<CharacterProgression>();
    }

    private void BuildFallbackUIIfNeeded()
    {
        if (!createFallbackIfNoAssignedReferences)
            return;

        if (HasAnyAssignedHudReference())
            return;

        if (uiRoot != null)
            return;

        generatedFallbackHud = true;

        uiRoot = new GameObject("HUD_PlayerStatus", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        uiRoot.transform.SetParent(transform, false);

        Canvas canvas = uiRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;

        CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(uiRoot.transform, false);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(24f, 24f);
        panelRect.sizeDelta = new Vector2(310f, 178f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.04f, 0.045f, 0.78f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        healthText = CreateFallbackText("Health", panel.transform);
        hungerText = CreateFallbackText("Hunger", panel.transform);
        thirstText = CreateFallbackText("Thirst", panel.transform);
        staminaText = CreateFallbackText("Stamina", panel.transform);
        levelText = CreateFallbackText("Level", panel.transform);
        statPointsText = CreateFallbackText("Stat Points", panel.transform);
    }

    public void EnableFallbackLayoutWhenEmpty()
    {
        createFallbackIfNoAssignedReferences = true;
        BuildFallbackUIIfNeeded();
        TryBindStats();
    }

    private bool HasAnyAssignedHudReference()
    {
        return healthFillImage != null ||
            staminaFillImage != null ||
            hungerFillImage != null ||
            thirstFillImage != null ||
            healthText != null ||
            staminaText != null ||
            hungerText != null ||
            thirstText != null ||
            levelText != null ||
            statPointsText != null;
    }

    private static TextMeshProUGUI CreateFallbackText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        LayoutElement layout = textObject.GetComponent<LayoutElement>();
        layout.minHeight = 24f;
        layout.preferredHeight = 24f;

        return text;
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }
}
