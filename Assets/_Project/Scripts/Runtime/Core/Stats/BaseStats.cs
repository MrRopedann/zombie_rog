using System;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

[Serializable]
public class BaseStat
{
    [SerializeField]
    private float baseValue;
    private float modifier = 0f;

    public float BaseValue => baseValue;
    public float Value => baseValue + modifier;

    public float Modifier => modifier;

    public BaseStat(float baseValue = 0f)
    {
        this.baseValue = baseValue;
    }

    public void SetBaseValue(float newBaseValue)
    { 
        baseValue = newBaseValue;
    }

    public void SetModifier(float value)
    { 
        modifier += value;
    }

    public void RemoveModifier(float value)
    {
        modifier -= value;
    }

    public void ClearModifier()
    {
        modifier = 0;
    }

    public void SetProcentModfier(float procent)
    {
        modifier += baseValue * (procent / 100f);
    }
}
