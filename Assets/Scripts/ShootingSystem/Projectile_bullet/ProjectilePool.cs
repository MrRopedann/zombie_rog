using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Projectile bulletPrefab;
    [SerializeField] private int poolSize = 20;
    [SerializeField] private float maxLifetime = 5f;

    private Queue<Projectile> _pool = new Queue<Projectile>();

    public float MaxLifetime => maxLifetime;

    public void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (Create() == null)
            {
                break;
            }
        }
    }

    private Projectile Create()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"{nameof(ProjectilePool)} on {name} has no bullet prefab.", this);
            return null;
        }

        Projectile bullet = Instantiate(bulletPrefab, transform);
        bullet.gameObject.SetActive(false);
        _pool.Enqueue(bullet);
        return bullet;
    }

    public Projectile Get()
    {
        if(_pool.Count == 0)
        {
            Create();
            Debug.LogWarning("Projectile pool expanded at runtime. Consider increasing the pool size.", this);
        }

        if (_pool.Count == 0)
        {
            return null;
        }

        Projectile bullet = _pool.Dequeue();
        bullet.gameObject.SetActive(true);
        return bullet;

    }

    public void ReturnToPool(Projectile projectile)
    { 
        if (projectile == null)
        {
            return;
        }

        projectile.gameObject.SetActive(false);
        _pool.Enqueue(projectile);
    }
}
