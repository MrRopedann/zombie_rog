using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    private float _maxLifetime;
    private float _damage;

    private Rigidbody _rigidbody;

    private ProjectilePool _pool;

    private float _timeElapsed = 0f;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        CountdownLifeTime();
    }

    private void OnCollisionEnter(Collision collision)
    {
        ContactPoint contact = collision.contacts[0];

        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        BaseDamagable damageable = collision.collider.GetComponent<BaseDamagable>();

        if (damageable == null)
        {
            damageable = collision.collider.GetComponentInParent<BaseDamagable>();
            if (damageable == null)
            {
                damageable = collision.collider.GetComponentInChildren<BaseDamagable>();
            }
        }

        if(damageable != null)
        {
            damageable.TakeDamage(_damage, hitPoint, hitNormal);
        }

        if(_pool != null)
        {
            _pool.ReturnToPool(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CountdownLifeTime()
    { 
        _timeElapsed += Time.deltaTime;

        if(_timeElapsed >= _maxLifetime)
        {
            if (_pool != null)
            {
                _pool.ReturnToPool(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    public void Startup(float damage, Vector3 spawnPosition, Vector3 direction, float speed, ProjectilePool pool, float maxLifetime)
    { 
        _damage = damage;
        _pool = pool;
        _maxLifetime = maxLifetime;
        _timeElapsed = 0;

        _rigidbody.velocity  = Vector3.zero;

        _rigidbody.AddForce(direction * speed, ForceMode.Impulse);

    }


}
