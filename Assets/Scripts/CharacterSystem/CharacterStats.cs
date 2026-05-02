using System;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    private enum NeedPenaltyTier
    {
        None,
        Low,
        Severe,
        Critical
    }
    [Header("Идентификация")]
    public int playerID = 0;
    public string playerName = "Ropedann";

    [Header("Уровень и опыт")]
    public int playerLevel = 1;
    public int currentExp = 0;
    public int expToNextLevel = 100;

    [Header("Текущие ресурсы")]
    public float currentHealth = 100f;
    public float currentArmor = 100f;
    public float currentHunger = 100f;
    public float currentThirst = 100f;
    public float currentStamina = 100f;

    [Header("Базовые максимальные значения")]
    public float baseMaxHealth = 100f;
    public float baseMaxArmor = 100f;
    public float baseMaxHunger = 100f;
    public float baseMaxThirst = 100f;
    public float baseMaxStamina = 100f;
    public float baseMaxWeight = 25f;

    [Header("Стамина логика")]
    public float staminaRegenRate = 6f;       // восстановление в секунду
    public float staminaRegenDelay = 1.2f;    // задержка после расхода
    public float sprintCostPerSecond = 18f;   // расход при беге
    public float jumpCost = 10f;              // разовый расход прыжка
    public float staminaExhaustionLockDuration = 1.2f; // блок спринта и прыжка после полного истощения

    [Header("Коэффициенты расхода")]
    public float hungerDecreaseRate = 0.05f;
    public float thirstDecreaseRate = 0.1f;
    public float staminaDecreaseRate = 0.5f;

    [Header("Need Penalties")]
    [Range(0f, 1f)] public float lowNeedThresholdPercent = 0.5f;
    [Range(0f, 1f)] public float severeNeedThresholdPercent = 0.25f;
    [Range(0f, 1f)] public float criticalNeedThresholdPercent = 0.05f;
    [Range(0f, 1f)] public float lowNeedStaminaMultiplier = 0.75f;
    [Range(0f, 1f)] public float severeNeedStaminaMultiplier = 0.5f;
    public float severeNeedRegenDelayMultiplier = 2f;
    public float criticalNeedHealthDrainPerSecond = 2f;

    [Header("Основные атрибуты")]
    public BaseStat durability = new BaseStat(5f);   // Стойкость
    public BaseStat agility = new BaseStat(5f);   // Ловкость
    public BaseStat strength = new BaseStat(5f);   // Сила

    [Header("Коэффициенты влияния атрибутов")]
    public float durabilityHealthFactor = 20f;
    public float strengthWeightFactor = 5f;
    public float agilityStaminaFactor = 0.015f;      // рекомендуется меньшее значение

    [Header("Ссылки")]
    public GameObject characterPrefab;
    public GameObject weaponSlot;
    public GameObject supportWeaponSlot;

    private bool isInitialized = false;
    private float lastStaminaUseTime;
    private float staminaExhaustionLockEndTime;
    private NeedPenaltyTier currentNeedPenaltyTier = NeedPenaltyTier.None;

    // Производные максимальные значения
    public float MaxHealth { get; private set; }
    public float MaxArmor { get; private set; }
    public float MaxHunger { get; private set; }
    public float MaxThirst { get; private set; }
    public float MaxStamina { get; private set; }
    public float MaxWeight { get; private set; }

    // Процентные значения для HUD
    public float HealthPercent => MaxHealth > 0 ? currentHealth / MaxHealth : 0f;
    public float HungerPercent => MaxHunger > 0 ? currentHunger / MaxHunger : 0f;
    public float ThirstPercent => MaxThirst > 0 ? currentThirst / MaxThirst : 0f;
    public float StaminaPercent => MaxStamina > 0 ? currentStamina / MaxStamina : 0f;
    public bool AreStaminaActionsLocked => Time.time < staminaExhaustionLockEndTime;

    // ====================== СОБЫТИЯ ДЛЯ HUD ======================
    public event Action OnHealthChanged;
    public event Action OnHungerChanged;
    public event Action OnThirstChanged;
    public event Action OnStaminaChanged;
    public event Action OnStatsRecalculated;   // когда меняются максимумы
    public event Action OnLevelChanged;

    private void Awake()
    {
        RecalculateAllStats();

        if (!isInitialized)
        {
            SetFullStats();
            isInitialized = true;
        }

        RefreshNeedPenaltyState(force: true);
    }

    private void Update()
    {
        // Пассивный расход голода и жажды
        ChangeHunger(-hungerDecreaseRate * Time.deltaTime);
        ChangeThirst(-thirstDecreaseRate * Time.deltaTime);

        HandleCriticalNeedHealthDrain();
        HandleStaminaRegen();
    }

    private void HandleStaminaRegen()
    {
        if (AreStaminaActionsLocked || IsStaminaRegenBlockedByNeeds())
            return;

        // задержка после использования
        if (Time.time < lastStaminaUseTime + GetEffectiveStaminaRegenDelay())
            return;

        if (currentStamina >= MaxStamina)
            return;

        // бонус от ловкости
        float agilityBonus = 1f + agility.Value * agilityStaminaFactor;

        float regen = staminaRegenRate * agilityBonus * Time.deltaTime;

        currentStamina = Mathf.Clamp(currentStamina + regen, 0f, MaxStamina);
        OnStaminaChanged?.Invoke();
    }

    private void HandleCriticalNeedHealthDrain()
    {
        if (currentNeedPenaltyTier != NeedPenaltyTier.Critical || criticalNeedHealthDrainPerSecond <= 0f)
            return;

        ChangeHealth(-criticalNeedHealthDrainPerSecond * Time.deltaTime);
    }

    public bool UseStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        if (currentStamina < amount)
            return false;

        currentStamina -= amount;
        currentStamina = Mathf.Clamp(currentStamina, 0f, MaxStamina);

        lastStaminaUseTime = Time.time;

        if (currentStamina <= 0f)
        {
            StartStaminaExhaustionLock();
        }

        OnStaminaChanged?.Invoke();

        return true;
    }

    private void StartStaminaExhaustionLock()
    {
        staminaExhaustionLockEndTime = Mathf.Max(staminaExhaustionLockEndTime, Time.time + staminaExhaustionLockDuration);
    }

    private void SetFullStats()
    {
        currentHealth = MaxHealth;
        currentArmor = MaxArmor;
        currentHunger = MaxHunger;
        currentThirst = MaxThirst;
        currentStamina = MaxStamina;
        staminaExhaustionLockEndTime = 0f;

        OnHealthChanged?.Invoke();
        OnHungerChanged?.Invoke();
        OnThirstChanged?.Invoke();
        OnStaminaChanged?.Invoke();
    }

    /// <summary>
    /// Пересчёт всех производных характеристик
    /// </summary>
    public void RecalculateAllStats()
    {
        MaxHealth = baseMaxHealth + durability.Value * durabilityHealthFactor;
        MaxArmor = baseMaxArmor;
        MaxHunger = baseMaxHunger;
        MaxThirst = baseMaxThirst;
        MaxStamina = baseMaxStamina * GetStaminaMultiplierForCurrentNeeds();
        MaxWeight = baseMaxWeight + strength.Value * strengthWeightFactor;

        // Ограничиваем текущие значения новыми максимумами
        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        currentHunger = Mathf.Clamp(currentHunger, 0f, MaxHunger);
        currentThirst = Mathf.Clamp(currentThirst, 0f, MaxThirst);
        currentStamina = Mathf.Clamp(currentStamina, 0f, MaxStamina);

        OnStatsRecalculated?.Invoke();
        OnHealthChanged?.Invoke();
        OnStaminaChanged?.Invoke();
    }

    // ====================== МЕТОДЫ ИЗМЕНЕНИЯ ======================

    public void ChangeHealth(float amount)
    {
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, MaxHealth);
        OnHealthChanged?.Invoke();
    }

    public void ChangeHunger(float amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, MaxHunger);
        RefreshNeedPenaltyState();
        OnHungerChanged?.Invoke();
    }

    public void ChangeThirst(float amount)
    {
        currentThirst = Mathf.Clamp(currentThirst + amount, 0f, MaxThirst);
        RefreshNeedPenaltyState();
        OnThirstChanged?.Invoke();
    }

    public void ChangeStamina(float amount)
    {
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, MaxStamina);

        if (amount < 0f && currentStamina <= 0f)
        {
            lastStaminaUseTime = Time.time;
            StartStaminaExhaustionLock();
        }

        OnStaminaChanged?.Invoke();
    }

    /// <summary>
    /// Добавление опыта с возможным повышением уровня
    /// </summary>
    public void AddExperience(int amount)
    {
        currentExp += amount;

        while (currentExp >= expToNextLevel && expToNextLevel > 0)
        {
            currentExp -= expToNextLevel;
            playerLevel++;
            OnLevelChanged?.Invoke();
            // Здесь можно добавить вызов CharacterProgression.GainLevel()
        }
    }

    private void RefreshNeedPenaltyState(bool force = false)
    {
        NeedPenaltyTier newTier = GetNeedPenaltyTier();

        if (!force && newTier == currentNeedPenaltyTier)
            return;

        currentNeedPenaltyTier = newTier;
        RecalculateAllStats();
    }

    private NeedPenaltyTier GetNeedPenaltyTier()
    {
        float worstNeedPercent = Mathf.Min(HungerPercent, ThirstPercent);

        if (worstNeedPercent <= criticalNeedThresholdPercent)
            return NeedPenaltyTier.Critical;

        if (worstNeedPercent <= severeNeedThresholdPercent)
            return NeedPenaltyTier.Severe;

        if (worstNeedPercent <= lowNeedThresholdPercent)
            return NeedPenaltyTier.Low;

        return NeedPenaltyTier.None;
    }

    private float GetStaminaMultiplierForCurrentNeeds()
    {
        return currentNeedPenaltyTier switch
        {
            NeedPenaltyTier.Low => lowNeedStaminaMultiplier,
            NeedPenaltyTier.Severe => severeNeedStaminaMultiplier,
            NeedPenaltyTier.Critical => severeNeedStaminaMultiplier,
            _ => 1f
        };
    }

    private float GetEffectiveStaminaRegenDelay()
    {
        return currentNeedPenaltyTier switch
        {
            NeedPenaltyTier.Severe => staminaRegenDelay * severeNeedRegenDelayMultiplier,
            NeedPenaltyTier.Critical => staminaRegenDelay * severeNeedRegenDelayMultiplier,
            _ => staminaRegenDelay
        };
    }

    private bool IsStaminaRegenBlockedByNeeds()
    {
        return currentNeedPenaltyTier == NeedPenaltyTier.Critical;
    }
}
