using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] string gameSceneName = "HuntedDead";
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject characterPanel;
    [SerializeField] GameObject coopMenuPanel;
    [SerializeField] GameObject coopMenuPrefab;
    [SerializeField] Transform coopMenuRoot;


    public void OnStartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnOpenCreateCharacter()
    {
        if (characterPanel != null) characterPanel.SetActive(true);
    }

    public void OnCloseCreateCharacter()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
    }

    public void OnOpenCoopMenu()
    {
        if (coopMenuPanel == null)
        {
            if (coopMenuPrefab == null)
                coopMenuPrefab = Resources.Load<GameObject>("Prefabs/UI/Menu/CoopMenu");

            if (coopMenuPrefab != null)
            {
                Transform parent = coopMenuRoot != null ? coopMenuRoot : transform;
                coopMenuPanel = Instantiate(coopMenuPrefab, parent);
            }
        }

        if (coopMenuPanel != null)
            coopMenuPanel.SetActive(true);
        else
            Debug.LogError("Coop menu prefab is not assigned and Resources/Prefabs/UI/Menu/CoopMenu was not found.", this);
    }

    public void OnCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void Update()
    {
        if (settingsPanel != null && settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            OnCloseSettings();
    }
}
