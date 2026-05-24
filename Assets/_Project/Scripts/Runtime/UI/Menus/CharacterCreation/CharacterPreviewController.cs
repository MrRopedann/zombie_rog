using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterPreviewController : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform previewCamera;
    [SerializeField] private RawImage previewImage;

    [SerializeField] private float rotationSpeed = 0.3f;

    [SerializeField] private float zoomSpeed = 0.5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 5f;

    private float currentDistance;
    private RenderTexture runtimePreviewTexture;

    private void Awake()
    {
        ConfigurePreview();
    }

    private void OnEnable()
    {
        ConfigurePreview();
    }

    private void Start()
    {
        ConfigurePreview();
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

    private void ConfigurePreview()
    {
        if (previewImage == null)
            previewImage = GetComponent<RawImage>();

        if (previewCamera == null)
            return;

        Camera camera = previewCamera.GetComponent<Camera>();
        if (camera == null)
            return;

        camera.enabled = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.03f, 0.035f, 0.04f, 1f);

        EnsurePreviewTexture(camera);
        ConfigurePreviewLayer(camera);
        FrameCharacter(camera);
    }

    private void EnsurePreviewTexture(Camera camera)
    {
        if (camera.targetTexture == null)
        {
            runtimePreviewTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32)
            {
                name = "Runtime Character Preview"
            };
            runtimePreviewTexture.Create();
            camera.targetTexture = runtimePreviewTexture;
        }

        if (previewImage != null && previewImage.texture != camera.targetTexture)
            previewImage.texture = camera.targetTexture;
    }

    private void ConfigurePreviewLayer(Camera camera)
    {
        if (characterRoot == null)
            return;

        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer >= 0)
        {
            SetLayerRecursive(characterRoot, previewLayer);
            camera.cullingMask = 1 << previewLayer;
            return;
        }

        camera.cullingMask |= 1 << characterRoot.gameObject.layer;
    }

    private void FrameCharacter(Camera camera)
    {
        if (characterRoot == null)
            return;

        if (!TryGetRendererBounds(characterRoot, out Bounds bounds))
            return;

        Vector3 center = bounds.center;
        float height = Mathf.Max(1.5f, bounds.size.y);
        float width = Mathf.Max(0.75f, Mathf.Max(bounds.size.x, bounds.size.z));
        currentDistance = Mathf.Clamp(Mathf.Max(height * 1.15f, width * 2.2f), minDistance, maxDistance);

        Vector3 lookTarget = center + Vector3.up * (height * 0.08f);
        camera.transform.position = lookTarget + new Vector3(0f, height * 0.08f, -currentDistance);
        camera.transform.LookAt(lookTarget);
        camera.fieldOfView = 38f;
        previewCamera = camera.transform;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void SetLayerRecursive(Transform root, int layer)
    {
        if (root == null)
            return;

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
