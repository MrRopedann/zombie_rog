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

    [Header("Action Need Costs")]
    public float sprintHungerCostPerSecond = 0.1f;
    public float sprintThirstCostPerSecond = 0.2f;
    public float jumpHungerCost = 0.35f;
    public float jumpThirstCost = 0.5f;

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

    public event Action OnDeath;
    public event Action OnRevived;
    public event Action<float> OnDamaged;

    public bool IsDead { get; private set; }

    // Производные максимальные значения
    public float MaxHealth { get; private set; }
    public float MaxArmor { get; private set; }
    public float MaxHunger { get; private set; }
    public float MaxThirst { get; private set; }
    public float MaxStamina { get; private set; }
    public float CurrentStaminaLimit { get; private set; }
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
        if (!CoopSessionState.IsCoopSession)
            PlayerCharacterRepository.ApplySelectedTo(this);

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
        if (IsDead)
            return;

        if (currentHealth <= 0f)
        {
            Die();
            return;
        }
        // Пассивный расход голода и жажды
        ConsumeNeeds(hungerDecreaseRate * Time.deltaTime, thirstDecreaseRate * Time.deltaTime);

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

        if (currentStamina >= CurrentStaminaLimit)
            return;

        // бонус от ловкости
        float agilityBonus = 1f + agility.Value * agilityStaminaFactor;

        float regen = staminaRegenRate * agilityBonus * Time.deltaTime;

        currentStamina = Mathf.Clamp(currentStamina + regen, 0f, CurrentStaminaLimit);
        OnStaminaChanged?.Invoke();
    }

    private void HandleCriticalNeedHealthDrain()
    {
        if (currentNeedPenaltyTier != NeedPenaltyTier.Critical || criticalNeedHealthDrainPerSecond <= 0f)
            return;

        ChangeHealth(-criticalNeedHealthDrainPerSecond * Time.deltaTime);
    }

    public void ConsumeSprintNeeds(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        ConsumeNeeds(sprintHungerCostPerSecond * deltaTime, sprintThirstCostPerSecond * deltaTime);
    }

    public void ConsumeJumpNeeds()
    {
        ConsumeNeeds(jumpHungerCost, jumpThirstCost);
    }

    private void ConsumeNeeds(float hungerAmount, float thirstAmount)
    {
        bool spendHunger = hungerAmount > 0f;
        bool spendThirst = thirstAmount > 0f;

        if (!spendHunger && !spendThirst)
            return;

        if (spendHunger)
        {
            currentHunger = Mathf.Clamp(currentHunger - hungerAmount, 0f, MaxHunger);
        }

        if (spendThirst)
        {
            currentThirst = Mathf.Clamp(currentThirst - thirstAmount, 0f, MaxThirst);
        }

        RefreshNeedPenaltyState();

        if (spendHunger)
        {
            OnHungerChanged?.Invoke();
        }

        if (spendThirst)
        {
            OnThirstChanged?.Invoke();
        }
    }

    public bool UseStamina(float amount)
    {
        if (amount <= 0f)
            return true;

        if (currentStamina < amount)
            return false;

        currentStamina -= amount;
        currentStamina = Mathf.Clamp(currentStamina, 0f, CurrentStaminaLimit);

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
        MaxStamina = baseMaxStamina;
        CurrentStaminaLimit = MaxStamina * GetStaminaMultiplierForCurrentNeeds();
        MaxWeight = baseMaxWeight + strength.Value * strengthWeightFactor;

        // Ограничиваем текущие значения новыми максимумами
        currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
        currentHunger = Mathf.Clamp(currentHunger, 0f, MaxHunger);
        currentThirst = Mathf.Clamp(currentThirst, 0f, MaxThirst);
        currentStamina = Mathf.Clamp(currentStamina, 0f, CurrentStaminaLimit);

        OnStatsRecalculated?.Invoke();
        OnHealthChanged?.Invoke();
        OnStaminaChanged?.Invoke();
    }

    // ====================== МЕТОДЫ ИЗМЕНЕНИЯ ======================

    public void ChangeHealth(float amount)
    {
        if (IsDead)
            return;

        CoopGameplaySync.NotifyPlayerHealthChanging(this, amount);

        float previousHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, MaxHealth);
        OnHealthChanged?.Invoke();

        float damageTaken = Mathf.Max(0f, previousHealth - currentHealth);
        if (damageTaken > 0f && currentHealth > 0f)
        {
            OnDamaged?.Invoke(damageTaken);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void ApplyNetworkHealth(float health)
    {
        if (IsDead)
            return;

        currentHealth = Mathf.Clamp(health, 0f, MaxHealth);
        OnHealthChanged?.Invoke();

        if (currentHealth <= 0f)
            Die();
    }

    public void ApplyNetworkState(
        float health,
        float maxHealth,
        float hunger,
        float maxHunger,
        float thirst,
        float maxThirst,
        float stamina,
        float maxStamina,
        int experience,
        int experienceToNextLevel,
        int level,
        bool dead)
    {
        bool wasDead = IsDead;
        float previousHealth = currentHealth;
        float previousHunger = currentHunger;
        float previousThirst = currentThirst;
        float previousStamina = currentStamina;
        int previousLevel = playerLevel;

        MaxHealth = Mathf.Max(1f, maxHealth > 0f ? maxHealth : MaxHealth);
        MaxHunger = Mathf.Max(1f, maxHunger > 0f ? maxHunger : MaxHunger);
        MaxThirst = Mathf.Max(1f, maxThirst > 0f ? maxThirst : MaxThirst);
        MaxStamina = Mathf.Max(1f, maxStamina > 0f ? maxStamina : MaxStamina);
        CurrentStaminaLimit = MaxStamina;

        currentHealth = dead ? 0f : Mathf.Clamp(health, 0f, MaxHealth);
        currentHunger = Mathf.Clamp(hunger, 0f, MaxHunger);
        currentThirst = Mathf.Clamp(thirst, 0f, MaxThirst);
        currentStamina = Mathf.Clamp(stamina, 0f, CurrentStaminaLimit);
        currentExp = Mathf.Max(0, experience);
        expToNextLevel = Mathf.Max(0, experienceToNextLevel);
        playerLevel = Mathf.Max(1, level);
        IsDead = dead || currentHealth <= 0f;

        OnStatsRecalculated?.Invoke();

        if (!Mathf.Approximately(previousHealth, currentHealth))
            OnHealthChanged?.Invoke();

        if (!Mathf.Approximately(previousHunger, currentHunger))
            OnHungerChanged?.Invoke();

        if (!Mathf.Approximately(previousThirst, currentThirst))
            OnThirstChanged?.Invoke();

        if (!Mathf.Approximately(previousStamina, currentStamina))
            OnStaminaChanged?.Invoke();

        if (previousLevel != playerLevel)
            OnLevelChanged?.Invoke();

        if (!wasDead && IsDead)
            OnDeath?.Invoke();
        else if (wasDead && !IsDead)
            OnRevived?.Invoke();
    }

    public void ApplySavedState(
        int savedPlayerID,
        string savedPlayerName,
        int savedLevel,
        int savedExperience,
        int savedExperienceToNextLevel,
        float savedHealth,
        float savedArmor,
        float savedHunger,
        float savedThirst,
        float savedStamina,
        float savedDurabilityBase,
        float savedDurabilityModifier,
        float savedAgilityBase,
        float savedAgilityModifier,
        float savedStrengthBase,
        float savedStrengthModifier,
        bool savedDead)
    {
        bool wasDead = IsDead;

        playerID = Mathf.Max(0, savedPlayerID);
        if (!string.IsNullOrWhiteSpace(savedPlayerName))
            playerName = savedPlayerName.Trim();

        playerLevel = Mathf.Max(1, savedLevel);
        currentExp = Mathf.Max(0, savedExperience);
        expToNextLevel = Mathf.Max(0, savedExperienceToNextLevel);

        RestoreBaseStat(durability, savedDurabilityBase, savedDurabilityModifier);
        RestoreBaseStat(agility, savedAgilityBase, savedAgilityModifier);
        RestoreBaseStat(strength, savedStrengthBase, savedStrengthModifier);

        currentHunger = Mathf.Max(0f, savedHunger);
        currentThirst = Mathf.Max(0f, savedThirst);
        RecalculateAllStats();

        currentHealth = savedDead ? 0f : Mathf.Clamp(savedHealth, 0f, MaxHealth);
        currentArmor = Mathf.Clamp(savedArmor, 0f, MaxArmor);
        currentHunger = Mathf.Clamp(savedHunger, 0f, MaxHunger);
        currentThirst = Mathf.Clamp(savedThirst, 0f, MaxThirst);
        currentStamina = Mathf.Clamp(savedStamina, 0f, CurrentStaminaLimit);
        IsDead = savedDead || currentHealth <= 0f;
        isInitialized = true;

        RefreshNeedPenaltyState(force: true);

        OnStatsRecalculated?.Invoke();
        OnHealthChanged?.Invoke();
        OnHungerChanged?.Invoke();
        OnThirstChanged?.Invoke();
        OnStaminaChanged?.Invoke();
        OnLevelChanged?.Invoke();

        if (!wasDead && IsDead)
            OnDeath?.Invoke();
        else if (wasDead && !IsDead)
            OnRevived?.Invoke();
    }

    private static void RestoreBaseStat(BaseStat stat, float baseValue, float modifier)
    {
        if (stat == null)
            return;

        stat.SetBaseValue(Mathf.Max(0f, baseValue));
        stat.ClearModifier();
        stat.SetModifier(modifier);
    }

    public void Revive(float health)
    {
        if (!IsDead && currentHealth > 0f)
        {
            currentHealth = Mathf.Clamp(health, 1f, MaxHealth);
            OnHealthChanged?.Invoke();
            return;
        }

        IsDead = false;
        currentHealth = Mathf.Clamp(health, 1f, MaxHealth);
        staminaExhaustionLockEndTime = 0f;
        currentStamina = Mathf.Max(currentStamina, Mathf.Min(CurrentStaminaLimit, MaxStamina * 0.35f));

        OnHealthChanged?.Invoke();
        OnStaminaChanged?.Invoke();
        OnRevived?.Invoke();
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;
        currentHealth = 0f;

        OnHealthChanged?.Invoke();
        OnDeath?.Invoke();
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
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, CurrentStaminaLimit);

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
        CharacterProgression progression = GetComponent<CharacterProgression>() ??
            GetComponentInChildren<CharacterProgression>(true) ??
            GetComponentInParent<CharacterProgression>();
        if (progression != null)
        {
            progression.AddExperience(amount);
            return;
        }

        currentExp += Mathf.Max(0, amount);
        bool leveledUp = false;

        while (currentExp >= expToNextLevel && expToNextLevel > 0)
        {
            currentExp -= expToNextLevel;
            playerLevel++;
            expToNextLevel = Mathf.Max(1, Mathf.RoundToInt(expToNextLevel * 1.4f));
            leveledUp = true;
        }

        if (leveledUp)
            RecalculateAllStats();

        NotifyProgressionChanged();
    }

    public void ApplyProgressionState(int level, int experience, int experienceToNextLevel, bool notify = true)
    {
        int previousLevel = playerLevel;

        playerLevel = Mathf.Max(1, level);
        currentExp = Mathf.Max(0, experience);
        expToNextLevel = Mathf.Max(1, experienceToNextLevel);

        if (notify || previousLevel != playerLevel)
            OnLevelChanged?.Invoke();
    }

    public void NotifyProgressionChanged()
    {
        OnLevelChanged?.Invoke();
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
