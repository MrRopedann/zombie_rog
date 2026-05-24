using UnityEngine;

[DisallowMultipleComponent]
public class CrosshairVisibilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputsController inputsController;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform ownerRoot;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Visibility")]
    [SerializeField] private bool showScreenCrosshair = false;
    [SerializeField] private bool visibleOnlyWhileAiming = true;

    [Header("Laser Dot")]
    [SerializeField] private bool useLaserDot = true;
    [SerializeField] private Color dotColor = new(1f, 0.05f, 0.02f, 0.95f);
    [SerializeField] private Color noHitDotColor = new(1f, 0.35f, 0.05f, 0.65f);
    [SerializeField] private bool showDotAtMaxDistanceWhenNoHit = true;
    [SerializeField] [Min(0.001f)] private float dotScaleByDistance = 0.012f;
    [SerializeField] [Min(0.001f)] private float minDotSize = 0.035f;
    [SerializeField] [Min(0.001f)] private float maxDotSize = 8f;
    [SerializeField] [Min(0f)] private float surfaceOffset = 0.01f;
    [SerializeField] [Min(0.1f)] private float fallbackAimDistance = 1000f;
    [SerializeField] private LayerMask fallbackHitMask = ~0;

    private const int IgnoreRaycastLayer = 2;
    private const int MaxLaserRaycastHits = 64;
    private static readonly RaycastHit[] LaserRaycastHits = new RaycastHit[MaxLaserRaycastHits];

    private GameObject laserDot;
    private MeshRenderer laserDotRenderer;
    private Material laserDotMaterial;

    private void Awake()
    {
        ResolveReferences();
        ApplyVisibility(false);
        EnsureLaserDot();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (inputsController != null)
        {
            inputsController.OnPlayerAimChanged += ApplyVisibility;
        }

        ApplyVisibility(IsVisible());
        UpdateLaserDot();
    }

    private void OnDisable()
    {
        if (inputsController != null)
        {
            inputsController.OnPlayerAimChanged -= ApplyVisibility;
        }

        SetLaserDotVisible(false);
    }

    private void OnDestroy()
    {
        DestroyRuntimeObject(laserDot);
        DestroyRuntimeObject(laserDotMaterial);
    }

    private void Update()
    {
        if (inputsController == null || weaponController == null || playerCamera == null)
        {
            ResolveReferences();

            if (inputsController != null)
            {
                inputsController.OnPlayerAimChanged += ApplyVisibility;
            }
        }

        ApplyVisibility(IsVisible());
        UpdateLaserDot();
    }

    private void ResolveReferences()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (inputsController == null)
        {
            inputsController = FindObjectOfType<InputsController>();
        }

        if (weaponController == null)
        {
            weaponController = FindObjectOfType<PlayerWeaponController>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private bool IsVisible()
    {
        return !visibleOnlyWhileAiming || inputsController != null && inputsController.aim;
    }

    private void ApplyVisibility(bool visible)
    {
        bool screenVisible = showScreenCrosshair && visible;

        if (targetCanvas != null)
        {
            targetCanvas.enabled = screenVisible;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = screenVisible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void UpdateLaserDot()
    {
        if (!useLaserDot || !IsVisible() || playerCamera == null)
        {
            SetLaserDotVisible(false);
            return;
        }

        EnsureLaserDot();

        if (laserDot == null)
        {
            return;
        }

        Ray aimRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        float maxDistance = GetMaxAimDistance();
        bool hasHit = TryGetLaserHit(aimRay, maxDistance, out RaycastHit hit);

        if (!hasHit && !showDotAtMaxDistanceWhenNoHit)
        {
            SetLaserDotVisible(false);
            return;
        }

        Vector3 normal = hasHit && hit.normal.sqrMagnitude > 0.001f
            ? hit.normal.normalized
            : -aimRay.direction.normalized;
        Vector3 position = hasHit
            ? hit.point + normal * surfaceOffset
            : aimRay.origin + aimRay.direction.normalized * maxDistance;
        float distance = Vector3.Distance(playerCamera.transform.position, position);
        float dotSize = Mathf.Clamp(distance * dotScaleByDistance, minDotSize, maxDotSize);

        SetMaterialColor(laserDotMaterial, hasHit ? dotColor : noHitDotColor);
        laserDot.transform.position = position;
        laserDot.transform.localScale = Vector3.one * dotSize;

        SetLaserDotVisible(true);
    }

    private bool TryGetLaserHit(Ray aimRay, float maxDistance, out RaycastHit hit)
    {
        hit = default;

        if (aimRay.direction.sqrMagnitude <= 0.001f || maxDistance <= 0f)
        {
            return false;
        }

        ZombieHitbox.SyncAllActiveHitboxes();
        Physics.SyncTransforms();

        Vector3 direction = aimRay.direction.normalized;
        int hitCount = Physics.RaycastNonAlloc(
            aimRay.origin,
            direction,
            LaserRaycastHits,
            maxDistance,
            GetHitMask(),
            QueryTriggerInteraction.Collide);

        Transform resolvedOwnerRoot = GetOwnerRoot();
        float closestDistance = float.PositiveInfinity;
        bool hasHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit currentHit = LaserRaycastHits[i];

            if (currentHit.collider == null
                || ShooterAimUtility.IsOwnerCollider(currentHit.collider, resolvedOwnerRoot)
                || currentHit.distance >= closestDistance)
            {
                continue;
            }

            if (currentHit.collider.isTrigger
                && ShooterAimUtility.FindDamageable(currentHit.collider) == null)
            {
                continue;
            }

            hit = currentHit;
            closestDistance = currentHit.distance;
            hasHit = true;
        }

        return hasHit;
    }

    private float GetMaxAimDistance()
    {
        Weapon currentWeapon = weaponController != null ? weaponController.CurrentWeapon : null;

        if (currentWeapon != null)
        {
            return Mathf.Max(0.1f, currentWeapon.Range);
        }

        return Mathf.Max(0.1f, fallbackAimDistance);
    }

    private LayerMask GetHitMask()
    {
        Weapon currentWeapon = weaponController != null ? weaponController.CurrentWeapon : null;
        return currentWeapon != null ? currentWeapon.HitMask : fallbackHitMask;
    }

    private Transform GetOwnerRoot()
    {
        if (ownerRoot != null)
        {
            return ownerRoot;
        }

        if (weaponController != null)
        {
            return weaponController.transform.root;
        }

        return inputsController != null ? inputsController.transform.root : null;
    }

    private void EnsureLaserDot()
    {
        if (!useLaserDot || laserDot != null)
        {
            return;
        }

        laserDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        laserDot.name = "Laser Aim Dot";
        laserDot.layer = IgnoreRaycastLayer;
        laserDot.transform.SetParent(null, true);

        Collider dotCollider = laserDot.GetComponent<Collider>();

        if (dotCollider != null)
        {
            DestroyRuntimeObject(dotCollider);
        }

        laserDotRenderer = laserDot.GetComponent<MeshRenderer>();

        if (laserDotRenderer != null)
        {
            laserDotMaterial = CreateLaserDotMaterial();
            laserDotRenderer.sharedMaterial = laserDotMaterial;
            laserDotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laserDotRenderer.receiveShadows = false;
        }

        SetLaserDotVisible(false);
    }

    private Material CreateLaserDotMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        Material material = new(shader);
        material.renderQueue = 3100;
        SetMaterialColor(material, dotColor);

        if (material.HasProperty("_Cull"))
        {
            material.SetInt("_Cull", 0);
        }

        return material;
    }

    private void SetLaserDotVisible(bool visible)
    {
        if (laserDot != null && laserDot.activeSelf != visible)
        {
            laserDot.SetActive(visible);
        }
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
