using TMPro;
using UnityEngine;

public class ZombieDamageNumberPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] [Min(0.05f)] private float lifetime = 0.9f;
    [SerializeField] [Min(0f)] private float floatSpeed = 1.25f;
    [SerializeField] private bool faceCamera = true;

    private float timer;
    private Color startColor = Color.white;
    private Camera targetCamera;

    public static ZombieDamageNumberPopup CreateDefault(Vector3 position, Quaternion rotation, float fontSize)
    {
        GameObject popupObject = new GameObject("Zombie Damage Number");
        popupObject.transform.SetPositionAndRotation(position, rotation);

        TextMeshPro textMesh = popupObject.AddComponent<TextMeshPro>();
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = Mathf.Max(0.1f, fontSize);
        textMesh.enableWordWrapping = false;
        textMesh.raycastTarget = false;

        ZombieDamageNumberPopup popup = popupObject.AddComponent<ZombieDamageNumberPopup>();
        popup.text = textMesh;
        return popup;
    }

    public void Initialize(float damage, Color color, float newLifetime, float newFloatSpeed, string format)
    {
        ResolveText();

        lifetime = Mathf.Max(0.05f, newLifetime);
        floatSpeed = Mathf.Max(0f, newFloatSpeed);
        timer = 0f;
        startColor = color;
        targetCamera = Camera.main;

        if (text != null)
        {
            text.text = damage.ToString(string.IsNullOrWhiteSpace(format) ? "0" : format);
            text.color = startColor;
        }

        FaceCamera();
    }

    private void Awake()
    {
        ResolveText();
    }

    private void LateUpdate()
    {
        timer += Time.deltaTime;
        transform.position += Vector3.up * (floatSpeed * Time.deltaTime);

        FaceCamera();
        UpdateFade();

        if (timer >= lifetime)
            Destroy(gameObject);
    }

    private void ResolveText()
    {
        if (text == null)
            text = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>(true);
    }

    private void FaceCamera()
    {
        if (!faceCamera)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 direction = transform.position - targetCamera.transform.position;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void UpdateFade()
    {
        if (text == null)
            return;

        float fade = 1f - Mathf.Clamp01(timer / lifetime);
        Color color = startColor;
        color.a *= fade;
        text.color = color;
    }
}
