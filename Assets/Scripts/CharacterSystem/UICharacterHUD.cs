using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UICharacterHUD : MonoBehaviour
{
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

    private void Start()
    {
        stats = FindObjectOfType<CharacterStats>();
        if (stats != null)
            progression = stats.GetComponent<CharacterProgression>();

        if (stats == null)
        {
            Debug.LogError("CharacterStats не найден на сцене!");
            return;
        }

        SubscribeEvents();
        UpdateAllUI();
    }

    private void SubscribeEvents()
    {
        stats.OnHealthChanged += UpdateHealthUI;
        stats.OnHungerChanged += UpdateHungerUI;
        stats.OnThirstChanged += UpdateThirstUI;
        stats.OnStaminaChanged += UpdateStaminaUI;
        stats.OnStatsRecalculated += UpdateAllUI;
        stats.OnLevelChanged += UpdateLevelUI;
    }

    private void UpdateAllUI()
    {
        UpdateHealthUI();
        UpdateStaminaUI();
        UpdateHungerUI();
        UpdateThirstUI();
        UpdateLevelUI();
    }

    // ====================== ОБНОВЛЕНИЕ КРУГОВ ======================

    private void UpdateHealthUI()
    {
        float percent = stats.HealthPercent;

        if (healthFillImage != null)
            healthFillImage.fillAmount = percent;

        if (healthText != null)
            healthText.text = Mathf.RoundToInt(stats.currentHealth).ToString();

        // === ВРЕМЕННАЯ ОТЛАДКА ===
        Debug.Log($"[HUD] Health: current = {stats.currentHealth:F1} | max = {stats.MaxHealth:F1} | percent = {percent:P0}");
    }

    private void UpdateStaminaUI()
    {
        if (staminaFillImage != null)
            staminaFillImage.fillAmount = stats.StaminaPercent;

        if (staminaText != null)
            staminaText.text = Mathf.RoundToInt(stats.currentStamina).ToString();
    }

    private void UpdateHungerUI()
    {
        if (hungerFillImage != null)
            hungerFillImage.fillAmount = stats.HungerPercent;

        if (hungerText != null)
            hungerText.text = Mathf.RoundToInt(stats.currentHunger).ToString();
    }

    private void UpdateThirstUI()
    {
        if (thirstFillImage != null)
            thirstFillImage.fillAmount = stats.ThirstPercent;

        if (thirstText != null)
            thirstText.text = Mathf.RoundToInt(stats.currentThirst).ToString();
    }

    private void UpdateLevelUI()
    {
        if (levelText != null)
            levelText.text = $"Lv.{stats.playerLevel}";

        if (statPointsText != null && progression != null)
            statPointsText.text = $"Очки: {progression.availableStatPoints}";
    }

    private void OnDestroy()
    {
        if (stats == null) return;

        stats.OnHealthChanged -= UpdateHealthUI;
        stats.OnHungerChanged -= UpdateHungerUI;
        stats.OnThirstChanged -= UpdateThirstUI;
        stats.OnStaminaChanged -= UpdateStaminaUI;
        stats.OnStatsRecalculated -= UpdateAllUI;
        stats.OnLevelChanged -= UpdateLevelUI;
    }
}