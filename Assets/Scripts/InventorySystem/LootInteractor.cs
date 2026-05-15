using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LootInteractor : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float rayDistance = 4f;
    [SerializeField, Min(0f)] private float interactionRangePadding = 0.35f;
    [SerializeField] private LayerMask interactMask = ~0;

    [Header("References")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private CharacterStats characterStats;
    [SerializeField] private InputsController inputs;

    private readonly RaycastHit[] raycastHits = new RaycastHit[24];
    private LootContainer currentTarget;
    private GameObject promptRoot;
    private Text promptText;
    private Image progressBack;
    private Image progressFill;
    private float holdTimer;

    private void Awake()
    {
        ResolveReferences();
        BuildPromptUI();
        SetPromptVisible(false);
    }

    private void Update()
    {
        ResolveReferences();

        if (LootContainerUIController.IsOpen)
        {
            ResetHold();
            SetPromptVisible(false);
            return;
        }

        LootContainer target = FindLookTarget();

        if (target != currentTarget)
        {
            currentTarget = target;
            ResetHold();
        }

        if (currentTarget == null)
        {
            SetPromptVisible(false);
            return;
        }

        UpdatePrompt(currentTarget);
        HandleOpenInput(currentTarget);
    }

    private LootContainer FindLookTarget()
    {
        ResolveCamera();

        if (playerCamera == null)
        {
            return null;
        }

        Vector3 interactorPosition = GetInteractorPosition();
        float cameraOffsetDistance = Vector3.Distance(interactorPosition, playerCamera.transform.position);
        float effectiveRayDistance = Mathf.Max(rayDistance, cameraOffsetDistance + rayDistance);

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int hitCount = Physics.RaycastNonAlloc(ray, raycastHits, effectiveRayDistance, interactMask, QueryTriggerInteraction.Collide);

        if (hitCount <= 0)
        {
            return null;
        }

        Array.Sort(raycastHits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];

            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            LootContainer container = hit.collider.GetComponentInParent<LootContainer>();

            if (container == null)
            {
                if (hit.collider.isTrigger)
                {
                    continue;
                }

                return null;
            }

            float distanceToHit = Vector3.Distance(interactorPosition, hit.point);
            return distanceToHit <= container.InteractionRange + interactionRangePadding ? container : null;
        }

        return null;
    }

    private Vector3 GetInteractorPosition()
    {
        if (characterStats != null)
        {
            return characterStats.transform.position;
        }

        return playerInventory != null ? playerInventory.transform.position : transform.position;
    }

    private bool IsOwnCollider(Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        if (targetCollider.transform == transform || targetCollider.transform.IsChildOf(transform))
        {
            return true;
        }

        if (playerInventory != null && targetCollider.GetComponentInParent<PlayerInventory>() == playerInventory)
        {
            return true;
        }

        return characterStats != null && targetCollider.GetComponentInParent<CharacterStats>() == characterStats;
    }

    private void HandleOpenInput(LootContainer target)
    {
        if (target == null || playerInventory == null || !target.CanOpen(characterStats))
        {
            ResetHold();
            return;
        }

        if (target.RequiresSearchHold(characterStats))
        {
            if (!IsUseHeld())
            {
                ResetHold();
                return;
            }

            holdTimer += Time.deltaTime;
            float delay = Mathf.Max(0.01f, target.FirstSearchDelay);
            SetProgress(holdTimer / delay, true);

            if (holdTimer >= delay)
            {
                OpenTarget(target);
            }

            return;
        }

        ResetHold();

        if (WasUsePressedThisFrame())
        {
            OpenTarget(target);
        }
    }

    private void OpenTarget(LootContainer target)
    {
        if (target.OpenFor(playerInventory, characterStats))
        {
            ResetHold();
            SetPromptVisible(false);
        }
    }

    private void UpdatePrompt(LootContainer target)
    {
        if (promptText != null)
        {
            promptText.text = target.GetPromptText(characterStats);
        }

        SetPromptVisible(true);

        if (target.RequiresSearchHold(characterStats))
        {
            bool showProgress = IsUseHeld() || holdTimer > 0f;
            SetProgress(target.FirstSearchDelay > 0f ? holdTimer / target.FirstSearchDelay : 0f, showProgress);
        }
        else
        {
            SetProgress(0f, false);
        }
    }

    private void ResetHold()
    {
        holdTimer = 0f;
        SetProgress(0f, false);
    }

    private void SetProgress(float progress, bool visible)
    {
        if (progressBack != null)
        {
            progressBack.enabled = visible;
        }

        if (progressFill != null)
        {
            progressFill.enabled = visible;
            progressFill.fillAmount = Mathf.Clamp01(progress);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.activeSelf != visible)
        {
            promptRoot.SetActive(visible);
        }
    }

    private bool IsUseHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (inputs != null && inputs.InputAction != null)
        {
            return inputs.InputAction.Player.Use.IsPressed();
        }

        if (Keyboard.current != null)
        {
            return Keyboard.current.fKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.F);
#else
        return false;
#endif
    }

    private bool WasUsePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (inputs != null && inputs.InputAction != null)
        {
            return inputs.InputAction.Player.Use.WasPressedThisFrame();
        }

        if (Keyboard.current != null)
        {
            return Keyboard.current.fKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F);
#else
        return false;
#endif
    }

    private void ResolveReferences()
    {
        if (playerInventory == null)
        {
            playerInventory = GetComponent<PlayerInventory>();

            if (playerInventory == null)
            {
                playerInventory = GetComponentInParent<PlayerInventory>();
            }
        }

        if (characterStats == null)
        {
            characterStats = GetComponent<CharacterStats>();

            if (characterStats == null)
            {
                characterStats = GetComponentInParent<CharacterStats>();
            }
        }

        if (inputs == null)
        {
            inputs = GetComponent<InputsController>();

            if (inputs == null)
            {
                inputs = GetComponentInParent<InputsController>();
            }
        }

        ResolveCamera();
    }

    private void ResolveCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            return;
        }

        Camera[] cameras = Camera.allCameras;

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null && cameras[i].isActiveAndEnabled)
            {
                playerCamera = cameras[i];
                return;
            }
        }
    }

    private void BuildPromptUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Sprite circleSprite = CreateCircleSprite();

        GameObject canvasObject = new GameObject("Loot Prompt UI", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 46;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        promptRoot = new GameObject("Prompt", typeof(RectTransform));
        promptRoot.transform.SetParent(canvasObject.transform, false);

        RectTransform rootRect = promptRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -92f);
        rootRect.sizeDelta = new Vector2(520f, 86f);

        promptText = CreateText("Label", promptRoot.transform, font, 22, TextAnchor.MiddleCenter);
        promptText.supportRichText = true;
        RectTransform textRect = promptText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0.35f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        progressBack = CreateImage("Open Progress Back", promptRoot.transform, circleSprite, new Color(0f, 0f, 0f, 0.52f));
        RectTransform backRect = progressBack.rectTransform;
        backRect.anchorMin = new Vector2(0.5f, 0f);
        backRect.anchorMax = new Vector2(0.5f, 0f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(0f, 16f);
        backRect.sizeDelta = new Vector2(38f, 38f);

        progressFill = CreateImage("Open Progress Fill", progressBack.transform, circleSprite, new Color(0.92f, 0.92f, 0.86f, 0.95f));
        progressFill.type = Image.Type.Filled;
        progressFill.fillMethod = Image.FillMethod.Radial360;
        progressFill.fillOrigin = 2;
        progressFill.fillClockwise = true;
        RectTransform fillRect = progressFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
    }

    private static Text CreateText(string objectName, Transform parent, Font font, int size, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = (size - 2) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

        public int Compare(RaycastHit left, RaycastHit right)
        {
            return left.distance.CompareTo(right.distance);
        }
    }
}
