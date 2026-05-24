using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Collider))]
public class TargetDummy : MonoBehaviour, IDamageable
{
    [Header("Stats")]
    public float maxHealth = 100f;

    [Header("VFX / SFX")]
    public GameObject impactVFX;        // мелкие всплески при попадании
    public AudioClip hitSfx;
    public GameObject damagePopupPrefab; // 3D TextMeshPro
    public Transform popupSpawnPoint;

    float health;
    AudioSource audioSrc;

    private void Awake()
    {
        health = maxHealth;
        audioSrc = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (!popupSpawnPoint)
        {
            // Если не указан, ставим над головой
            popupSpawnPoint = new GameObject("PopupSpawnPoint").transform;
            popupSpawnPoint.SetParent(transform, false);
            popupSpawnPoint.localPosition = Vector3.up * 2f; // над головой
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Не убиваем цель, здоровье бесконечно
        SpawnImpact(hitPoint, hitNormal);
        ShowDamage(amount);
        PlaySound(hitSfx);
    }

    void SpawnImpact(Vector3 point, Vector3 normal)
    {
        if (impactVFX)
        {
            var fx = Instantiate(impactVFX, point, Quaternion.LookRotation(normal));
            Destroy(fx, 3f);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSrc == null) return;
        audioSrc.PlayOneShot(clip);
    }

    void ShowDamage(float amount)
    {
        if (!damagePopupPrefab || !popupSpawnPoint) return;

        // Создаём текст
        var popup = Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity);
        var textMesh = popup.GetComponent<TextMeshPro>();
        if (textMesh != null) textMesh.text = amount.ToString("F0");

        // Поворачиваем к камере
        if (Camera.main != null)
            popup.transform.LookAt(Camera.main.transform);

        // Немного корректируем чтобы не было зеркально
        popup.transform.Rotate(0f, 180f, 0f);

        // Анимация полёта вверх + исчезновение
        popup.AddComponent<DamagePopup>();
    }
}

public class DamagePopup : MonoBehaviour
{
    public float duration = 1f;
    public float floatSpeed = 1f;

    private float timer;

    void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= duration) Destroy(gameObject);
    }
}
