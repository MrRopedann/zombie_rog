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
        ContactPoint contact = collision.contactCount > 0
            ? collision.GetContact(0)
            : default;

        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;

        if (collision.contactCount == 0)
        {
            hitPoint = transform.position;
            hitNormal = -transform.forward;
        }

        BaseDamagable damageable = ShooterAimUtility.FindDamageable(collision.collider);

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

        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        transform.SetPositionAndRotation(spawnPosition, Quaternion.LookRotation(direction));

        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.velocity = direction * speed;

    }


}
