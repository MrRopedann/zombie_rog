using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class EscapeMenuController : MonoBehaviour
{
    private const string MainMenuSceneName = "MainScene";

    private static EscapeMenuController instance;

    private readonly List<InputBlockState> inputStates = new();
    private RectTransform root;
    private Button continueButton;
    private Button saveButton;
    private Button loadButton;
    private Button mainMenuButton;
    private Button quitButton;
    private Font font;
    private bool isOpen;
    private bool cursorGuardActive;
    private bool pausedTimeScale;
    private float previousTimeScale = 1f;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockState;

    private struct InputBlockState
    {
        public InputsController input;
        public bool cursorInputForLook;
        public bool shootingInputBlocked;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureActive();
    }

    private static EscapeMenuController EnsureActive()
    {
        if (instance != null)
            return instance;

        GameObject menuObject = new GameObject("Escape Menu Controller");
        instance = menuObject.AddComponent<EscapeMenuController>();
        DontDestroyOnLoad(menuObject);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        CloseMenu();
    }

    private void OnDestroy()
    {
        CloseMenu();
    }

    private void Update()
    {
        if (IsMainMenuScene())
        {
            CloseMenu();
            return;
        }

        if (isOpen)
        {
            GameCursorGuard.ApplyUiCursor();
            ApplyInputBlock();
        }

        if (!WasEscapePressedThisFrame())
            return;

        if (isOpen)
        {
            CloseMenu();
            return;
        }

        if (GameCursorGuard.IsUiCursorRequested)
            return;

        OpenMenu();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (isOpen)
            GameCursorGuard.ApplyUiCursor();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CloseMenu();
    }

    private void OpenMenu()
    {
        if (root == null)
            BuildUI();

        isOpen = true;
        root.gameObject.SetActive(true);
        previousCursorVisible = Cursor.visible;
        previousCursorLockState = Cursor.lockState;

        ActivateCursorGuard();
        CaptureAndBlockInputs();

        if (!CoopSessionState.IsCoopSession)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            pausedTimeScale = true;
        }
        else
        {
            pausedTimeScale = false;
        }

        RefreshSaveButtons();

        if (continueButton != null)
            continueButton.Select();
    }

    private void CloseMenu()
    {
        if (!isOpen && !cursorGuardActive && inputStates.Count == 0 && !pausedTimeScale)
            return;

        isOpen = false;

        if (root != null)
            root.gameObject.SetActive(false);

        if (pausedTimeScale)
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            pausedTimeScale = false;
        }

        RestoreInputs();
        DeactivateCursorGuard();

        if (!GameCursorGuard.IsUiCursorRequested)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockState;
        }
        else
        {
            GameCursorGuard.ApplyUiCursor();
        }
    }

    private void CaptureAndBlockInputs()
    {
        inputStates.Clear();

        InputsController[] inputs = FindObjectsOfType<InputsController>(true);
        for (int i = 0; i < inputs.Length; i++)
        {
            InputsController input = inputs[i];
            if (input == null)
                continue;

            inputStates.Add(new InputBlockState
            {
                input = input,
                cursorInputForLook = input.cursorInputForLook,
                shootingInputBlocked = input.ShootingInputBlocked
            });
        }

        ApplyInputBlock();
    }

    private void ApplyInputBlock()
    {
        for (int i = 0; i < inputStates.Count; i++)
        {
            InputsController input = inputStates[i].input;
            if (input == null)
                continue;

            input.cursorInputForLook = false;
            input.move = Vector2.zero;
            input.look = Vector2.zero;
            input.jump = false;
            input.sprint = false;
            input.walk = false;
            input.SetShootingInputBlocked(true);
        }
    }

    private void RestoreInputs()
    {
        for (int i = 0; i < inputStates.Count; i++)
        {
            InputBlockState state = inputStates[i];
            if (state.input == null)
                continue;

            state.input.cursorInputForLook = state.cursorInputForLook;
            state.input.SetShootingInputBlocked(state.shootingInputBlocked);
        }

        inputStates.Clear();
    }

    private void ReturnToMainMenu()
    {
        CloseMenu();
        Time.timeScale = 1f;
        CoopSessionState.Clear();
        SceneManager.LoadScene(MainMenuSceneName);
    }

    private void SaveGame()
    {
        GameSaveManager.SaveCurrentGame();
        RefreshSaveButtons();
    }

    private void LoadGame()
    {
        CloseMenu();
        Time.timeScale = 1f;
        GameSaveManager.LoadCurrentGame();
    }

    private void RefreshSaveButtons()
    {
        if (saveButton != null)
            saveButton.interactable = GameSaveManager.CanSaveInCurrentContext;

        if (loadButton != null)
            loadButton.interactable = GameSaveManager.CanSaveInCurrentContext && GameSaveManager.SaveExists;
    }

    private void QuitGame()
    {
        CloseMenu();
        Time.timeScale = 1f;
        Application.Quit();
    }

    private void BuildUI()
    {
        if (root != null)
            return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject("Root", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(canvasObject.transform, false);
        root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image overlay = rootObject.GetComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0.64f);
        overlay.raycastTarget = true;

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panelObject.transform.SetParent(root, false);

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(420f, 540f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.045f, 0.052f, 0.06f, 0.96f);
        panelImage.raycastTarget = true;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(32, 32, 30, 30);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text title = CreateText("Title", panelObject.transform, 34, TextAnchor.MiddleCenter, FontStyle.Bold);
        title.text = "Пауза";
        SetPreferredHeight(title.gameObject, 50f);

        Text subtitle = CreateText("Subtitle", panelObject.transform, 16, TextAnchor.MiddleCenter, FontStyle.Normal);
        subtitle.text = "Меню игры";
        subtitle.color = new Color(0.78f, 0.82f, 0.88f, 1f);
        SetPreferredHeight(subtitle.gameObject, 30f);

        continueButton = CreateButton("Continue", panelObject.transform, "Продолжить");
        saveButton = CreateButton("Save", panelObject.transform, "Сохранить");
        loadButton = CreateButton("Load", panelObject.transform, "Загрузить");
        mainMenuButton = CreateButton("Main Menu", panelObject.transform, "Главное меню");
        quitButton = CreateButton("Quit", panelObject.transform, "Выйти из игры");

        continueButton.onClick.AddListener(CloseMenu);
        saveButton.onClick.AddListener(SaveGame);
        loadButton.onClick.AddListener(LoadGame);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        quitButton.onClick.AddListener(QuitGame);

        RefreshSaveButtons();
        root.gameObject.SetActive(false);
    }

    private Text CreateText(string objectName, Transform parent, int size, TextAnchor alignment, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredHeight = 54f;
        layout.minHeight = 54f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.13f, 0.16f, 0.19f, 0.95f);
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.19f, 0.23f, 0.27f, 1f);
        colors.pressedColor = new Color(0.09f, 0.11f, 0.13f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.08f, 0.09f, 0.1f, 0.75f);
        button.colors = colors;

        Text text = CreateText("Text", buttonObject.transform, 21, TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.text = label;
        return button;
    }

    private static void SetPreferredHeight(GameObject target, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            return;

        layout.preferredHeight = height;
        layout.minHeight = height;
    }

    private static bool IsMainMenuScene()
    {
        return SceneManager.GetActiveScene().name == MainMenuSceneName;
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
            return Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        DontDestroyOnLoad(eventSystemObject);
    }

    private void ActivateCursorGuard()
    {
        if (cursorGuardActive)
        {
            GameCursorGuard.ApplyUiCursor();
            return;
        }

        cursorGuardActive = true;
        GameCursorGuard.PushUiCursor();
    }

    private void DeactivateCursorGuard()
    {
        if (!cursorGuardActive)
            return;

        cursorGuardActive = false;
        GameCursorGuard.PopUiCursor();
    }
}
