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

    private void Start()
    {
        if (hintInterectionUI != null)
        {
            _canvasGroup = hintInterectionUI.GetComponent<CanvasGroup>();

            // Если CanvasGroup отсутствует — добавляем автоматически
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
            Debug.LogError("Hint Interaction UI не назначен.");
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