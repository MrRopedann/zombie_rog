using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    [Header("Основные параметры")]
    public int playerID = 0;
    public string playerName = "Ropedann";

    [Header("Уровень и опыт")]
    public int playerLevel = 1;
    public int currentExp = 0;
    public int expToNextLevel = 10;

    [Header("Основные параметры")]
    public int currentHealth = 100;
    public int currentArmor = 100;
    public int currentHungry = 100;
    public int currentThirst = 100;
    public int currentStamina = 100;

    [Header("Максимальное значение (базовые)")]
    public int baseMaxHealth = 100;
    public int baseMaxArmor = 100;
    public int baseMaxHungry = 100;
    public int baseMaxThirst = 100;
    public int baseMaxStamina = 100;
    public float baseMaxWeight = 25f;

    [Header("Коэффициенты расхода")]
    public float hungryFactor = 0.05f;
    public float thirstFactor = 0.1f;
    public float staminaFactor = 0.5f;

    [Header("Основные атрибуты персонажа")]
    public BaseStat durability = new BaseStat(5f);
    public BaseStat agility = new BaseStat(5f);
    public BaseStat strength = new BaseStat(5f);


    [Header("Коэффициенты влияния атрибутов")]
    public float durabilityHealthFactor = 20f;
    public float strengthWeightFactor = 5f;
    public float agilityStaminaFactor = 0.15f;

    [Header("Ссылки на объекты")]
    public GameObject characterPrefab;
    public GameObject weaponSlot;
    public GameObject supportWeaponSlot;

    public float maxHealth { get; private set; }
    public float maxArmor { get; private set; }
    public float maxHungry { get; private set; }
    public float maxThirst { get; private set; }
    public float maxStamina { get; private set; }
    public float maxWeight { get; private set; }

    public float ProcentHealth => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    public float ProcentHungry => maxHungry > 0 ? (float)currentHungry / maxHungry : 0f;
    public float ProcentThirst => maxThirst > 0 ? (float)currentThirst / maxThirst : 0f;
    public float ProcentStamina => maxStamina > 0 ? (float)currentStamina / maxStamina : 0f;

    private void Awake()
    {
        RecalculateAllStats();
    }

    public void RecalculateAllStats()
    {
        maxHealth = baseMaxHealth + durability.Value * durabilityHealthFactor;
        maxArmor = baseMaxArmor;
        maxHungry = baseMaxHungry;
        maxThirst = baseMaxThirst;
        maxStamina = baseMaxStamina;
        maxWeight = baseMaxWeight + strength.Value * strengthWeightFactor;

        currentHealth = Mathf.Clamp(currentHealth, 0, (int)maxHealth);
        currentHungry = Mathf.Clamp(currentHungry, 0, (int)maxHungry);
        currentThirst = Mathf.Clamp(currentThirst, 0, (int)maxThirst);
        currentStamina = Mathf.Clamp(currentStamina, 0, (int)maxStamina);
    }

    public void ChangeHealth(int amount) => currentHealth = Mathf.Clamp(currentHealth + amount, 0, (int)maxHealth);
    public void ChangeHungry(float amount) => currentHungry = Mathf.Clamp((int)(currentHungry + amount), 0, (int)maxHungry);
    public void ChangeThirst(float amount) => currentThirst = Mathf.Clamp((int)(currentThirst + amount), 0, (int)maxThirst);
    public void ChangeStamina(float amount) => currentStamina = Mathf.Clamp((int)(currentStamina + amount), 0, (int)maxStamina);



}
