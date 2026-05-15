using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterPreviewController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform previewCamera;

    [SerializeField] private float rotationSpeed = 0.3f;

    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 5f;

    private float currentDistance;

    private void Start()
    {
        if (characterRoot != null && previewCamera != null)
        {
            currentDistance = Vector3.Distance(previewCamera.position, characterRoot.position);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (characterRoot == null)
            return;

        characterRoot.localRotation *= Quaternion.Euler(
            0f,
            -eventData.delta.x * rotationSpeed,
            0f
        );
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (previewCamera == null || characterRoot == null)
            return;

        currentDistance -= eventData.scrollDelta.y * zoomSpeed;
        currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

        Vector3 direction = (previewCamera.position - characterRoot.position).normalized;
        previewCamera.position = characterRoot.position + direction * currentDistance;
        previewCamera.LookAt(characterRoot.position + Vector3.up * 1.3f);
    }
}