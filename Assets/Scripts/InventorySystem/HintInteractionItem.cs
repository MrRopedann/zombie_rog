using System.Collections;
using UnityEngine;

public class HintInteractionItem : MonoBehaviour
{
    [Header("Hint Interaction Item Setting")]
    [SerializeField] protected float interectionRange = 2f;
    [SerializeField] protected GameObject hintInterectionUI;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 0.3f;

    protected SphereCollider _interectionTrigger;

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;

    public void ConfigureRuntime(GameObject hintUI, float interactionRange)
    {
        if (hintUI != null)
        {
            hintInterectionUI = hintUI;
        }

        interectionRange = Mathf.Max(0.1f, interactionRange);
    }

    private void Start()
    {
        ResolveHintUI();

        if (hintInterectionUI != null)
        {
            _canvasGroup = hintInterectionUI.GetComponent<CanvasGroup>();

            if (_canvasGroup == null)
            {
                _canvasGroup = hintInterectionUI.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            hintInterectionUI.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"Hint Interaction UI не назначен на {name}. Подсказка взаимодействия будет отключена.", this);
        }

        _interectionTrigger = GetComponent<SphereCollider>();

        if (_interectionTrigger != null)
        {
            _interectionTrigger.radius = interectionRange;
            _interectionTrigger.isTrigger = true;
        }
        else
        {
            Debug.LogError("Interaction Trigger не найден на объекте.");
        }
    }

    private void ResolveHintUI()
    {
        if (hintInterectionUI != null)
        {
            return;
        }

        Transform hintTransform = FindChildRecursive(transform, "Hint");

        if (hintTransform != null)
        {
            hintInterectionUI = hintTransform.gameObject;
            return;
        }

        Canvas canvas = GetComponentInChildren<Canvas>(true);

        if (canvas != null && canvas.gameObject != gameObject)
        {
            hintInterectionUI = canvas.gameObject;
        }
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

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ShowHint();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HideHint();
        }
    }

    private void ShowHint()
    {
        if (hintInterectionUI == null || _canvasGroup == null) return;

        hintInterectionUI.SetActive(true);

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeCanvasGroup(1f));
    }

    private void HideHint()
    {
        if (hintInterectionUI == null || _canvasGroup == null) return;

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _fadeCoroutine = StartCoroutine(FadeOutAndDisable());
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        float startAlpha = _canvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeOutAndDisable()
    {
        yield return FadeCanvasGroup(0f);

        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        hintInterectionUI.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interectionRange);
    }
}
