using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(IShooter))]
public class Weapon : MonoBehaviour
{
    [Header("Common Settings")]
    [SerializeField] protected float damage = 10f;
    [SerializeField] protected float fireRate = 0.2f;
    [SerializeField] protected bool automatic = true;

    private float _nextFireTime;

    private IShooter _shooter;

    public float Damage => damage;
    public bool Automatic => automatic;

    private void Awake()
    {
        _shooter = GetComponent<IShooter>();
    }

    public bool TryShoot()
    { 
        if (Time.time < _nextFireTime || _shooter == null)
        {
            return false;
        }

        _nextFireTime = Time.time + fireRate;
        _shooter.Shoot();
        return true;
    }

}
