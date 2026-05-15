using UnityEngine;
using UnityEngine.UI;

public class WorldItem : MonoBehaviour
{
    [Header("Item Settings")]
    [SerializeField] private ItemSO itemData;
    [SerializeField, Min(1)] private int amount = 1;
    [SerializeField] private int networkItemId;
    [SerializeField] private bool remoteNetworkProxy;
    [SerializeField] private float destroyDelay = 1.0f;

    [Header("Physics")]
    [SerializeField] private bool physicalWorldItem = true;
    [SerializeField, Min(0.01f)] private float physicalColliderPadding = 0.04f;

    [Header("Effects Settings")]
    [SerializeField] private ParticleSystem pickupParticles;
    [SerializeField] private AudioClip pickupSounds;

    public ItemSO ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);
    public int NetworkItemId => networkItemId;
    public bool RemoteNetworkProxy => remoteNetworkProxy;

    private bool _isPickedUp;
    private Rigidbody _rigidbody;

    public static WorldItem Spawn(ItemSO item, Vector3 position, Quaternion rotation)
    {
        return Spawn(item, position, rotation, Vector3.zero, Vector3.zero);
    }

    public static WorldItem Spawn(ItemSO item, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
    {
        if (item == null)
        {
            return null;
        }

        position = ResolveSafeSpawnPosition(position);

        if (item.worldPrefab != null && PrefabHasWorldItem(item.worldPrefab))
        {
            GameObject instance = Instantiate(item.worldPrefab, position, rotation);
            WorldItem worldItem = instance.GetComponent<WorldItem>() ??
                instance.GetComponentInChildren<WorldItem>(true);
            PreparePickup(worldItem, item);
            worldItem?.ApplyLaunchVelocity(velocity, angularVelocity);
            return worldItem;
        }

        GameObject root = new GameObject($"WorldItem_{GetSafeName(item)}");
        root.transform.SetPositionAndRotation(position, rotation);

        WorldItem generatedItem = root.AddComponent<WorldItem>();
        CreateVisual(root.transform, item);
        PreparePickup(generatedItem, item);
        generatedItem.ApplyLaunchVelocity(velocity, angularVelocity);

        return generatedItem;
    }

    public void Setup(ItemSO newItemData = null)
    {
        if (newItemData != null)
        {
            itemData = newItemData;
        }
    }

    public void SetupNetwork(int newNetworkItemId, int newAmount, bool isRemoteProxy)
    {
        networkItemId = newNetworkItemId;
        amount = Mathf.Max(1, newAmount);
        remoteNetworkProxy = isRemoteProxy;
    }

    public void ApplyLaunchVelocity(Vector3 velocity, Vector3 angularVelocity)
    {
        EnsurePhysicalBody();

        if (_rigidbody == null)
            return;

        _rigidbody.velocity = velocity;
        _rigidbody.angularVelocity = angularVelocity;
    }

    public void Pickup()
    {
        if (_isPickedUp)
        {
            return;
        }

        _isPickedUp = true;

        foreach (Collider itemCollider in GetComponentsInChildren<Collider>())
        {
            itemCollider.enabled = false;
        }

        PlayPickupEffect();
        Destroy(gameObject, destroyDelay);
    }

    private static void PreparePickup(WorldItem worldItem, ItemSO item)
    {
        if (worldItem == null || item == null)
        {
            return;
        }

        worldItem.Setup(item);
        worldItem.EnsurePhysicalBody();
        EnsureTriggerCollider(worldItem.transform, item.pickupRadius);
        EnsureHint(worldItem.gameObject, item);
    }

    private static bool PrefabHasWorldItem(GameObject prefab)
    {
        return prefab.GetComponent<WorldItem>() != null ||
            prefab.GetComponentInChildren<WorldItem>(true) != null;
    }

    private static void CreateVisual(Transform parent, ItemSO item)
    {
        GameObject visual;

        if (item.worldPrefab != null)
        {
            visual = Instantiate(item.worldPrefab, parent);
            visual.name = $"{GetSafeName(item)}_Visual";
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = $"{GetSafeName(item)}_FallbackVisual";
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * 0.35f;
        }

        visual.transform.localPosition = item.worldVisualOffset;
        visual.transform.localRotation = Quaternion.Euler(item.worldVisualRotation);
        visual.transform.localScale = item.worldVisualScale == Vector3.zero ? Vector3.one : item.worldVisualScale;
        DisableVisualPhysics(visual);
    }

    private static void DisableVisualPhysics(GameObject visual)
    {
        foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>())
        {
            visualCollider.enabled = false;
        }

        foreach (Rigidbody visualRigidbody in visual.GetComponentsInChildren<Rigidbody>())
        {
            visualRigidbody.isKinematic = true;
            visualRigidbody.useGravity = false;
        }
    }

    private void EnsurePhysicalBody()
    {
        if (!physicalWorldItem)
            return;

        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        _rigidbody.mass = Mathf.Max(0.05f, itemData != null ? itemData.weight : 1f);
        _rigidbody.drag = 0.05f;
        _rigidbody.angularDrag = 0.05f;
        _rigidbody.useGravity = true;
        _rigidbody.isKinematic = false;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (GetComponent<BoxCollider>() == null && GetComponent<CapsuleCollider>() == null && GetComponent<MeshCollider>() == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            Bounds bounds = CalculateLocalRendererBounds(transform);
            boxCollider.center = bounds.center;
            boxCollider.size = Vector3.Max(bounds.size + Vector3.one * physicalColliderPadding, Vector3.one * 0.12f);
            boxCollider.isTrigger = false;
        }
    }

    private static void EnsureTriggerCollider(Transform target, float radius)
    {
        Transform triggerTransform = FindChildRecursive(target, "PickupTrigger");
        GameObject triggerObject;

        if (triggerTransform == null)
        {
            triggerObject = new GameObject("PickupTrigger");
            triggerObject.transform.SetParent(target, false);
            triggerObject.transform.localPosition = Vector3.zero;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
        }
        else
        {
            triggerObject = triggerTransform.gameObject;
        }

        SphereCollider pickupCollider = triggerObject.GetComponent<SphereCollider>();

        if (pickupCollider == null)
        {
            pickupCollider = triggerObject.AddComponent<SphereCollider>();
        }

        pickupCollider.isTrigger = true;
        pickupCollider.radius = Mathf.Max(0.1f, radius);
    }

    private static Vector3 ResolveSafeSpawnPosition(Vector3 position)
    {
        Vector3 rayOrigin = position + Vector3.up * 1.5f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (position.y < hit.point.y + 0.12f)
                position.y = hit.point.y + 0.12f;
        }

        return position;
    }

    private static Bounds CalculateLocalRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * 0.35f);

        bool hasBounds = false;
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Bounds bounds = renderer.bounds;
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, bounds.min);
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, bounds.max);
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.min.x, bounds.min.y, bounds.max.z));
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.min.x, bounds.max.y, bounds.min.z));
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.max.x, bounds.min.y, bounds.min.z));
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.min.x, bounds.max.y, bounds.max.z));
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.max.x, bounds.min.y, bounds.max.z));
            EncapsulateWorldPoint(root, ref localBounds, ref hasBounds, new Vector3(bounds.max.x, bounds.max.y, bounds.min.z));
        }

        return hasBounds ? localBounds : new Bounds(Vector3.zero, Vector3.one * 0.35f);
    }

    private static void EncapsulateWorldPoint(Transform root, ref Bounds localBounds, ref bool hasBounds, Vector3 worldPoint)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            localBounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        localBounds.Encapsulate(localPoint);
    }

    private static void EnsureHint(GameObject target, ItemSO item)
    {
        HintInteractionItem hintInteraction = target.GetComponent<HintInteractionItem>();

        if (hintInteraction == null)
        {
            hintInteraction = target.AddComponent<HintInteractionItem>();
        }

        GameObject hintUI = item.createRuntimeHint ? EnsureRuntimeHint(target.transform, item) : null;
        hintInteraction.ConfigureRuntime(hintUI, item.pickupRadius);
    }

    private static GameObject EnsureRuntimeHint(Transform parent, ItemSO item)
    {
        Transform existingHint = FindChildRecursive(parent, "Hint");

        if (existingHint != null)
        {
            return existingHint.gameObject;
        }

        GameObject hintObject = new GameObject("Hint", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        hintObject.transform.SetParent(parent, false);

        RectTransform hintRect = hintObject.GetComponent<RectTransform>();
        hintRect.localPosition = Vector3.up * 1.15f;
        hintRect.localRotation = Quaternion.identity;
        hintRect.localScale = Vector3.one * 0.01f;
        hintRect.sizeDelta = new Vector2(180f, 48f);

        Canvas canvas = hintObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(hintRect, false);

        Text text = textObject.GetComponent<Text>();
        text.text = string.IsNullOrWhiteSpace(item.pickupHintText) ? "E" : item.pickupHintText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        return hintObject;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static string GetSafeName(ItemSO item)
    {
        return string.IsNullOrWhiteSpace(item.itemID)
            ? item.name
            : item.itemID;
    }

    private void PlayPickupEffect()
    {
        if (pickupParticles != null)
        {
            Instantiate(pickupParticles, transform.position, Quaternion.identity).Play();
        }

        if (pickupSounds != null)
        {
            AudioSource.PlayClipAtPoint(pickupSounds, transform.position);
        }
    }
}
