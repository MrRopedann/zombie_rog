using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] string gameSceneName = "HuntedDead";
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject characterPanel;
    [SerializeField] GameObject characterPanelPrefab;
    [SerializeField] GameObject coopMenuPanel;
    [SerializeField] GameObject coopMenuPrefab;
    [SerializeField] Transform coopMenuRoot;
    [SerializeField] PlayerCharacterMenuController characterMenuController;

    private void Awake()
    {
        EnsureCharacterMenu();
        BindMenuButtons();
        characterMenuController?.ApplyStartupGate();
    }

    public void OnStartGame()
    {
        if (RequireCharacterBeforeAction(StartGameWithSelectedCharacter, true))
            return;

        StartGameWithSelectedCharacter();
    }

    private void StartGameWithSelectedCharacter()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    public void OnOpenSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnOpenCreateCharacter()
    {
        EnsureCharacterMenu();

        if (characterMenuController != null)
            characterMenuController.ShowCharacterBrowser();
        else if (characterPanel != null)
            characterPanel.SetActive(true);
    }

    public void OnCloseCreateCharacter()
    {
        if (characterPanel != null) characterPanel.SetActive(false);
    }

    public void OnOpenCoopMenu()
    {
        if (RequireCharacterBeforeAction(OpenCoopMenuWithSelectedCharacter, true))
            return;

        OpenCoopMenuWithSelectedCharacter();
    }

    private void OpenCoopMenuWithSelectedCharacter()
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

    private bool RequireCharacterBeforeAction(Action action, bool alwaysShowSelection)
    {
        EnsureCharacterMenu();
        return characterMenuController != null &&
            characterMenuController.RequireCharacterSelection(action, alwaysShowSelection);
    }

    private void EnsureCharacterMenu()
    {
        if (characterMenuController != null)
            return;

        if (characterPanel == null)
            characterPanel = GameObject.Find("CharacterCreationPanel");

        if (characterPanel == null)
        {
            if (characterPanelPrefab == null)
                characterPanelPrefab = Resources.Load<GameObject>("Prefabs/UI/Menu/CharacterCreationPanel");

            if (characterPanelPrefab != null)
            {
                characterPanel = Instantiate(characterPanelPrefab);
                characterPanel.name = "CharacterCreationPanel";
            }
        }

        if (characterPanel == null)
        {
            Debug.LogError("Character creation prefab was not found at Resources/Prefabs/UI/Menu/CharacterCreationPanel.", this);
            return;
        }

        characterMenuController = characterPanel.GetComponent<PlayerCharacterMenuController>();
        if (characterMenuController == null)
            characterMenuController = characterPanel.AddComponent<PlayerCharacterMenuController>();
    }

    private void BindMenuButtons()
    {
        BindButton("Btn_NewGame", OnStartGame);
        BindButton("Btn_NewCoopGame", OnOpenCoopMenu);
        BindButton("Btn_CreateNewCharacter", OnOpenCreateCharacter);
        BindButton("Btn_Settings", OnOpenSettings);
        BindButton("Btn_Quit", OnQuit);
    }

    private void BindButton(string buttonName, UnityEngine.Events.UnityAction action)
    {
        if (string.IsNullOrWhiteSpace(buttonName) || action == null)
            return;

        Button button = FindButton(buttonName);
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private Button FindButton(string buttonName)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == buttonName)
                return buttons[i];
        }

        return null;
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
