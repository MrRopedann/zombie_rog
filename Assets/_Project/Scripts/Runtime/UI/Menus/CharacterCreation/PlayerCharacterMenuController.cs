using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerCharacterMenuController : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.075f, 0.085f, 0.88f);
    private static readonly Color FieldColor = new Color(0.11f, 0.12f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.93f, 0.94f, 0.97f, 1f);
    private static readonly Color MutedTextColor = new Color(0.68f, 0.71f, 0.76f, 1f);
    private static readonly Color AccentColor = new Color(0.52f, 0.29f, 0.95f, 1f);
    private static readonly Color SelectedColor = new Color(0.22f, 0.28f, 0.34f, 1f);
    private static readonly Color ButtonColor = new Color(0.15f, 0.18f, 0.22f, 1f);

    [Header("Optional Scene References")]
    [SerializeField] private CharacterSkinSelector skinSelector;
    [SerializeField] private RectTransform generatedUiRoot;

    private GameObject selectionView;
    private GameObject creationView;
    private Transform characterListRoot;
    private Text selectionTitleText;
    private Text selectionHintText;
    private Text creationHintText;
    private Text feedbackText;
    private Text currentSkinText;
    private Text classButtonText;
    private InputField nameInput;
    private RectTransform rootRect;
    private RectTransform windowRect;
    private RectTransform mainPanelRect;
    private RectTransform previewRect;
    private RectTransform settingsPanelRect;
    private RectTransform generatedHostRect;
    private Button confirmSelectionButton;
    private Button createCharacterButton;
    private Button backToSelectionButton;
    private Button closeButton;
    private Button maleButton;
    private Button femaleButton;
    private Button previousSkinButton;
    private Button nextSkinButton;

    private readonly List<Button> profileButtons = new List<Button>();
    private Font uiFont;
    private Action pendingReadyAction;
    private string selectedCharacterId;
    private PlayerCharacterClass selectedClass = PlayerCharacterClass.Survivor;
    private bool initialized;
    private bool characterRequired;
    private bool cursorGuardActive;

    private Font UiFont
    {
        get
        {
            if (uiFont == null)
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return uiFont;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoGateMainScene()
    {
        if (SceneManager.GetActiveScene().name != "MainScene")
            return;

        PlayerCharacterMenuController controller = FindSceneController();
        if (controller != null)
            controller.ApplyStartupGate();
    }

    public static PlayerCharacterMenuController FindSceneController()
    {
        PlayerCharacterMenuController existing = FindObjectOfType<PlayerCharacterMenuController>(true);
        if (existing != null)
            return existing;

        GameObject panel = FindSceneObjectByName("CharacterCreationPanel");
        if (panel == null)
        {
            GameObject prefab = Resources.Load<GameObject>("RuntimeLoadedOnly/Prefabs/UI/Menu/CharacterCreationPanel");
            if (prefab == null)
                return null;

            panel = Instantiate(prefab);
            panel.name = "CharacterCreationPanel";
        }

        return panel.GetComponent<PlayerCharacterMenuController>() ?? panel.AddComponent<PlayerCharacterMenuController>();
    }

    public void ApplyStartupGate()
    {
        InitializeIfNeeded();

        if (!PlayerCharacterRepository.HasCharacters)
        {
            ShowCreation(required: true);
            return;
        }

        if (!PlayerCharacterRepository.TryEnsureSelected(out _))
            ShowSelection(required: true);
        else
            Hide();
    }

    public bool RequireCharacterSelection(Action onReady, bool alwaysShowSelection)
    {
        InitializeIfNeeded();

        if (!PlayerCharacterRepository.HasCharacters)
        {
            pendingReadyAction = onReady;
            ShowCreation(required: true);
            return true;
        }

        if (alwaysShowSelection || !PlayerCharacterRepository.TryEnsureSelected(out _))
        {
            pendingReadyAction = onReady;
            ShowSelection(required: true);
            return true;
        }

        return false;
    }

    public void ShowCharacterBrowser()
    {
        InitializeIfNeeded();
        pendingReadyAction = null;

        if (PlayerCharacterRepository.HasCharacters)
            ShowSelection(required: false);
        else
            ShowCreation(required: true);
    }

    private void Awake()
    {
        InitializeIfNeeded();
    }

    private void OnEnable()
    {
        if (!initialized)
            return;

        ActivateCursorGuard();
        RefreshCurrentSkinText();
    }

    private void OnDisable()
    {
        DeactivateCursorGuard();
    }

    private void Update()
    {
        if (!isActiveAndEnabled)
            return;

        GameCursorGuard.ApplyUiCursor();
        RefreshCurrentSkinText();

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseRequested();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;

        if (skinSelector == null)
            skinSelector = GetComponentInChildren<CharacterSkinSelector>(true);

        NormalizeRootLayout();
        NormalizeCharacterPanelLayout();
        BuildGeneratedUi();
        BindExistingCloseButton();
    }

    private void NormalizeRootLayout()
    {
        rootRect = transform as RectTransform;
        if (rootRect != null)
        {
            Stretch(rootRect);
            rootRect.localScale = Vector3.one;
            rootRect.localRotation = Quaternion.identity;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform window = transform.Find("Window");
        windowRect = window != null ? window.GetComponent<RectTransform>() : null;
        if (windowRect != null)
        {
            Stretch(windowRect);
            windowRect.localScale = Vector3.one;
        }
    }

    private void NormalizeCharacterPanelLayout()
    {
        Transform mainPanel = transform.Find("Window/Panel");
        Transform preview = mainPanel != null ? mainPanel.Find("RawImage") : null;
        Transform settingsPanel = mainPanel != null ? mainPanel.Find("Panel") : null;
        Transform generatedHost = settingsPanel != null ? settingsPanel.Find("BG") : null;

        mainPanelRect = mainPanel != null ? mainPanel.GetComponent<RectTransform>() : null;
        previewRect = preview != null ? preview.GetComponent<RectTransform>() : null;
        settingsPanelRect = settingsPanel != null ? settingsPanel.GetComponent<RectTransform>() : null;
        generatedHostRect = generatedHost != null ? generatedHost.GetComponent<RectTransform>() : null;

        if (mainPanelRect != null)
        {
            Stretch(mainPanelRect);

            HorizontalLayoutGroup layout = mainPanelRect.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = mainPanelRect.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.padding = new RectOffset(24, 24, 22, 22);
            layout.spacing = 18f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
        }

        if (previewRect != null)
        {
            previewRect.localScale = Vector3.one;
            SetLayoutSize(previewRect.gameObject, 500f, 680f, 0f);
            HidePreviewButtons(previewRect);
        }

        if (settingsPanelRect != null)
        {
            SetLayoutSize(settingsPanelRect.gameObject, 520f, 680f, 0f);
            settingsPanelRect.localScale = Vector3.one;
        }

        if (generatedHostRect != null)
        {
            Stretch(generatedHostRect);
            generatedHostRect.localScale = Vector3.one;
        }
    }

    private void HidePreviewButtons(RectTransform previewRoot)
    {
        if (previewRoot == null)
            return;

        SetPreviewButtonVisible(previewRoot, "Btn_Male", false);
        SetPreviewButtonVisible(previewRoot, "Btn_Female", false);
        SetPreviewButtonVisible(previewRoot, "Btn_PreviousSkin", false);
        SetPreviewButtonVisible(previewRoot, "Btn_NextSkin", false);
    }

    private static void SetPreviewButtonVisible(RectTransform parent, string buttonName, bool visible)
    {
        Transform child = parent != null ? parent.Find(buttonName) : null;
        if (child != null)
            child.gameObject.SetActive(visible);
    }

    private void BuildGeneratedUi()
    {
        Transform parent = ResolveGeneratedParent();
        if (parent == null)
            return;

        Transform oldRoot = parent.Find("CharacterProfileGeneratedUI");
        if (oldRoot != null)
            Destroy(oldRoot.gameObject);

        GameObject rootObject = CreateUiObject("CharacterProfileGeneratedUI", parent);
        generatedUiRoot = rootObject.GetComponent<RectTransform>();
        DockGeneratedRoot(generatedUiRoot);

        Image background = rootObject.AddComponent<Image>();
        background.color = PanelColor;

        VerticalLayoutGroup rootLayout = rootObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(18, 18, 14, 14);
        rootLayout.spacing = 4f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = false;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        selectionView = CreateStackPanel("SelectionView", generatedUiRoot, false);
        creationView = CreateStackPanel("CreationView", generatedUiRoot, false);

        BuildSelectionView(selectionView.transform);
        BuildCreationView(creationView.transform);
    }

    private void BuildSelectionView(Transform parent)
    {
        selectionTitleText = CreateText("Title", parent, "Выбор персонажа", 22, TextAnchor.MiddleLeft, TextColor);
        selectionHintText = CreateText("Hint", parent, string.Empty, 13, TextAnchor.UpperLeft, MutedTextColor);

        GameObject listViewport = CreatePanel("CharacterListViewport", parent, new Color(0f, 0f, 0f, 0.08f));
        SetLayoutHeight(listViewport, 214f);

        RectTransform viewportRect = listViewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = new Vector2(0f, 0f);
        viewportRect.anchorMax = new Vector2(1f, 1f);

        Mask mask = listViewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject listContent = CreateUiObject("Content", viewportRect);
        characterListRoot = listContent.transform;
        RectTransform contentRect = listContent.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup listLayout = listContent.AddComponent<VerticalLayoutGroup>();
        listLayout.padding = new RectOffset(0, 0, 0, 0);
        listLayout.spacing = 6f;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = false;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listContent.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = listViewport.AddComponent<ScrollRect>();
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        confirmSelectionButton = CreateButton("ConfirmCharacter", parent, "Выбрать", ConfirmSelectedCharacter, 36f);
        CreateButton("NewCharacter", parent, "Новый персонаж", () => ShowCreation(required: false), 32f);
        CreateButton("Close", parent, "Закрыть", CloseRequested, 32f);
    }

    private void BuildCreationView(Transform parent)
    {
        CreateText("Title", parent, "Создание персонажа", 20, TextAnchor.MiddleLeft, TextColor);
        creationHintText = CreateText(
            "Hint",
            parent,
            "Выбери имя, пол, скин и класс.",
            13,
            TextAnchor.UpperLeft,
            MutedTextColor);
        SetLayoutHeight(creationHintText.gameObject, 22f);

        CreateLabel(parent, "Имя персонажа");
        nameInput = CreateInput("CharacterNameInput", parent, "Введите имя");

        CreateLabel(parent, "Пол");
        GameObject genderRow = CreateHorizontalPanel("GenderRow", parent, 8f);
        maleButton = CreateButton("GenderMale", genderRow.transform, "Мужской", SelectMaleFromUi, 32f);
        femaleButton = CreateButton("GenderFemale", genderRow.transform, "Женский", SelectFemaleFromUi, 32f);

        CreateLabel(parent, "Скин");
        GameObject skinRow = CreateHorizontalPanel("SkinRow", parent, 8f);
        previousSkinButton = CreateButton("PreviousSkin", skinRow.transform, "Назад", PreviousSkinFromUi, 32f);
        nextSkinButton = CreateButton("NextSkin", skinRow.transform, "Далее", NextSkinFromUi, 32f);

        CreateLabel(parent, "Класс персонажа");
        Button classButton = CreateButton("ClassButton", parent, string.Empty, CycleClass, 34f);
        classButtonText = classButton.GetComponentInChildren<Text>(true);
        RefreshClassText();

        currentSkinText = CreateText("SkinInfo", parent, string.Empty, 13, TextAnchor.MiddleLeft, MutedTextColor);
        SetLayoutHeight(currentSkinText.gameObject, 20f);

        feedbackText = CreateText("Feedback", parent, string.Empty, 13, TextAnchor.MiddleLeft, new Color(1f, 0.68f, 0.36f, 1f));
        SetLayoutHeight(feedbackText.gameObject, 20f);

        createCharacterButton = CreateButton("CreateCharacter", parent, "Создать персонажа", CreateCharacter, 38f);
        backToSelectionButton = CreateButton("BackToSelection", parent, "Назад к выбору", () => ShowSelection(required: false), 32f);
        RefreshSkinControls();
    }

    private void ShowSelection(bool required)
    {
        characterRequired = required;
        gameObject.SetActive(true);
        ActivateCursorGuard();
        RefreshCloseButtonState();

        SetActive(selectionView, true);
        SetActive(creationView, false);
        RefreshSelectionList();
    }

    private void ShowCreation(bool required)
    {
        characterRequired = required;
        gameObject.SetActive(true);
        ActivateCursorGuard();
        RefreshCloseButtonState();

        SetActive(selectionView, false);
        SetActive(creationView, true);

        if (nameInput != null && string.IsNullOrWhiteSpace(nameInput.text))
            nameInput.text = string.Empty;

        if (feedbackText != null)
            feedbackText.text = string.Empty;

        if (backToSelectionButton != null)
            backToSelectionButton.gameObject.SetActive(PlayerCharacterRepository.HasCharacters);

        RefreshCloseButtonState();
        RefreshSkinControls();
    }

    private void SelectMaleFromUi()
    {
        if (skinSelector != null)
            skinSelector.SelectMale();

        RefreshSkinControls();
    }

    private void SelectFemaleFromUi()
    {
        if (skinSelector != null)
            skinSelector.SelectFemale();

        RefreshSkinControls();
    }

    private void PreviousSkinFromUi()
    {
        if (skinSelector != null)
            skinSelector.PreviousSkin();

        RefreshSkinControls();
    }

    private void NextSkinFromUi()
    {
        if (skinSelector != null)
            skinSelector.NextSkin();

        RefreshSkinControls();
    }

    private void RefreshSelectionList()
    {
        PlayerCharacterRepository.TryEnsureSelected(out PlayerCharacterProfile selected);
        selectedCharacterId = selected != null ? selected.characterId : PlayerCharacterRepository.SelectedCharacterId;

        if (selectionTitleText != null)
            selectionTitleText.text = "Выбор персонажа";

        if (selectionHintText != null)
            selectionHintText.text = pendingReadyAction == null
                ? "Выбранный персонаж будет использоваться в одиночной игре и кооперативе."
                : "Выбери персонажа перед запуском игры.";

        for (int i = characterListRoot.childCount - 1; i >= 0; i--)
            Destroy(characterListRoot.GetChild(i).gameObject);

        profileButtons.Clear();

        IReadOnlyList<PlayerCharacterProfile> profiles = PlayerCharacterRepository.Characters;
        for (int i = 0; i < profiles.Count; i++)
        {
            PlayerCharacterProfile profile = profiles[i];
            if (profile == null)
                continue;

            Button button = CreateButton(
                $"Profile_{profile.characterId}",
                characterListRoot,
                FormatProfile(profile),
                () => SelectProfile(profile.characterId),
                48f);

            profileButtons.Add(button);
        }

        RefreshProfileButtonColors();
        if (confirmSelectionButton != null)
            confirmSelectionButton.interactable = profiles.Count > 0;
    }

    private void SelectProfile(string characterId)
    {
        selectedCharacterId = characterId;
        PlayerCharacterRepository.SelectCharacter(characterId);
        RefreshProfileButtonColors();
    }

    private void ConfirmSelectedCharacter()
    {
        if (string.IsNullOrWhiteSpace(selectedCharacterId) && !PlayerCharacterRepository.TryEnsureSelected(out PlayerCharacterProfile ensured))
            return;

        if (!string.IsNullOrWhiteSpace(selectedCharacterId))
            PlayerCharacterRepository.SelectCharacter(selectedCharacterId);

        CompleteCharacterReady();
    }

    private void CreateCharacter()
    {
        string characterName = nameInput != null ? nameInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(characterName))
        {
            if (feedbackText != null)
                feedbackText.text = "Введите имя персонажа.";

            return;
        }

        PlayerCharacterGender gender = skinSelector != null ? skinSelector.CurrentGender : PlayerCharacterGender.Male;
        int modelIndex = skinSelector != null ? skinSelector.CurrentSkinIndex : 0;
        string modelId = skinSelector != null ? skinSelector.CurrentModelId : $"skin_{modelIndex}";

        PlayerCharacterProfile profile = PlayerCharacterRepository.CreateCharacter(
            characterName,
            gender,
            modelIndex,
            modelId,
            selectedClass);

        selectedCharacterId = profile.characterId;

        if (pendingReadyAction != null)
        {
            CompleteCharacterReady();
            return;
        }

        ShowSelection(required: false);
    }

    private void CompleteCharacterReady()
    {
        Action readyAction = pendingReadyAction;
        pendingReadyAction = null;
        characterRequired = false;
        Hide();
        readyAction?.Invoke();
    }

    private void CloseRequested()
    {
        if (characterRequired && !PlayerCharacterRepository.HasCharacters)
        {
            if (feedbackText != null)
                feedbackText.text = "Сначала создайте персонажа.";

            return;
        }

        pendingReadyAction = null;
        Hide();
    }

    private void Hide()
    {
        DeactivateCursorGuard();
        gameObject.SetActive(false);
    }

    private void CycleClass()
    {
        int next = ((int)selectedClass + 1) % Enum.GetValues(typeof(PlayerCharacterClass)).Length;
        selectedClass = (PlayerCharacterClass)next;
        RefreshClassText();
    }

    private void RefreshClassText()
    {
        if (classButtonText != null)
            classButtonText.text = $"Класс: {GetClassLabel(selectedClass)}";
    }

    private void RefreshCurrentSkinText()
    {
        if (currentSkinText == null)
            return;

        PlayerCharacterGender gender = skinSelector != null ? skinSelector.CurrentGender : PlayerCharacterGender.Male;
        int skinIndex = skinSelector != null ? skinSelector.CurrentSkinIndex : 0;
        currentSkinText.text = $"Пол: {GetGenderLabel(gender)} | Модель: {skinIndex + 1}";
    }

    private void RefreshSkinControls()
    {
        RefreshCurrentSkinText();

        PlayerCharacterGender gender = skinSelector != null ? skinSelector.CurrentGender : PlayerCharacterGender.Male;
        SetButtonColor(maleButton, gender == PlayerCharacterGender.Male ? SelectedColor : ButtonColor);
        SetButtonColor(femaleButton, gender == PlayerCharacterGender.Female ? SelectedColor : ButtonColor);
        SetButtonColor(previousSkinButton, ButtonColor);
        SetButtonColor(nextSkinButton, ButtonColor);
    }

    private void RefreshProfileButtonColors()
    {
        for (int i = 0; i < profileButtons.Count; i++)
        {
            Button button = profileButtons[i];
            if (button == null)
                continue;

            Image image = button.GetComponent<Image>();
            if (image == null)
                continue;

            string profileId = ExtractProfileId(button.name);
            image.color = PlayerCharacterRepository.SelectedCharacter != null &&
                profileId == PlayerCharacterRepository.SelectedCharacter.characterId
                    ? SelectedColor
                    : ButtonColor;
        }
    }

    private Transform ResolveGeneratedParent()
    {
        if (generatedUiRoot != null)
            return generatedUiRoot.parent;

        Transform candidate = transform.Find("Window/Panel/Panel/BG");
        if (candidate != null)
            return candidate;

        candidate = transform.Find("Window/Panel/Panel");
        if (candidate != null)
            return candidate;

        candidate = transform.Find("Window/Panel");
        if (candidate != null)
            return candidate;

        return transform;
    }

    private void BindExistingCloseButton()
    {
        if (closeButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == "ClosePanel")
                {
                    closeButton = buttons[i];
                    break;
                }
            }
        }

        if (closeButton == null)
            return;

        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        if (closeRect != null)
        {
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-18f, -18f);
            closeRect.sizeDelta = new Vector2(34f, 34f);
            closeRect.localScale = Vector3.one;
        }

        closeButton.onClick.RemoveListener(CloseRequested);
        closeButton.onClick.AddListener(CloseRequested);
        RefreshCloseButtonState();
    }

    private void RefreshCloseButtonState()
    {
        if (closeButton != null)
            closeButton.interactable = !(characterRequired && !PlayerCharacterRepository.HasCharacters);
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

    private string FormatProfile(PlayerCharacterProfile profile)
    {
        return $"{profile.DisplayName} | {GetClassLabel(profile.characterClass)} | {GetGenderLabel(profile.gender)} | ID {profile.ShortId}";
    }

    private static string ExtractProfileId(string objectName)
    {
        return string.IsNullOrWhiteSpace(objectName) || !objectName.StartsWith("Profile_")
            ? string.Empty
            : objectName.Substring("Profile_".Length);
    }

    private static string GetGenderLabel(PlayerCharacterGender gender)
    {
        return gender == PlayerCharacterGender.Female ? "Женский" : "Мужской";
    }

    private static string GetClassLabel(PlayerCharacterClass characterClass)
    {
        switch (characterClass)
        {
            case PlayerCharacterClass.Soldier:
                return "Солдат";
            case PlayerCharacterClass.Medic:
                return "Медик";
            case PlayerCharacterClass.Scout:
                return "Разведчик";
            case PlayerCharacterClass.Engineer:
                return "Инженер";
            default:
                return "Выживший";
        }
    }

    private static GameObject FindSceneObjectByName(string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate == null || candidate.name != objectName || candidate.scene != activeScene)
                continue;

            return candidate;
        }

        return null;
    }

    private GameObject CreateStackPanel(string objectName, Transform parent, bool active)
    {
        GameObject panel = CreateUiObject(objectName, parent);
        LayoutElement panelLayout = panel.AddComponent<LayoutElement>();
        panelLayout.flexibleHeight = 0f;

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.SetActive(active);
        return panel;
    }

    private GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = CreateUiObject(objectName, parent);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        return panel;
    }

    private GameObject CreateHorizontalPanel(string objectName, Transform parent, float spacing)
    {
        GameObject panel = CreateUiObject(objectName, parent);
        SetLayoutHeight(panel, 32f);

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return panel;
    }

    private Text CreateLabel(Transform parent, string text)
    {
        Text label = CreateText($"Label_{text}", parent, text, 14, TextAnchor.MiddleLeft, MutedTextColor);
        SetLayoutHeight(label.gameObject, 16f);
        return label;
    }

    private Text CreateText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = UiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        SetLayoutHeight(textObject, Mathf.Max(18f, fontSize + 6f));
        return text;
    }

    private InputField CreateInput(string objectName, Transform parent, string placeholder)
    {
        GameObject inputObject = CreateUiObject(objectName, parent);
        Image image = inputObject.AddComponent<Image>();
        image.color = FieldColor;

        InputField input = inputObject.AddComponent<InputField>();
        input.targetGraphic = image;
        input.customCaretColor = true;
        input.caretColor = TextColor;
        input.selectionColor = new Color(0.55f, 0.72f, 1f, 0.45f);
        input.characterLimit = 24;

        Text text = CreateChildText("Text", inputObject.transform, string.Empty, 15, TextAnchor.MiddleLeft, TextColor);
        Text placeholderText = CreateChildText("Placeholder", inputObject.transform, placeholder, 15, TextAnchor.MiddleLeft, MutedTextColor);
        placeholderText.fontStyle = FontStyle.Italic;

        input.textComponent = text;
        input.placeholder = placeholderText;

        SetLayoutHeight(inputObject, 30f);
        return input;
    }

    private Button CreateButton(string objectName, Transform parent, string label, Action onClick, float height)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => onClick?.Invoke());

        Text text = CreateChildText("Text", buttonObject.transform, label, 14, TextAnchor.MiddleCenter, TextColor);
        text.fontStyle = FontStyle.Bold;

        SetLayoutHeight(buttonObject, height);
        return button;
    }

    private Text CreateChildText(string objectName, Transform parent, string value, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = CreateUiObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        Stretch(rect);
        rect.offsetMin = new Vector2(10f, 3f);
        rect.offsetMax = new Vector2(-10f, -3f);

        Text text = textObject.AddComponent<Text>();
        text.font = UiFont;
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetLayoutHeight(GameObject target, float height)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void SetLayoutSize(GameObject target, float width, float height, float flexibleWidth)
    {
        if (target == null)
            return;

        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.minWidth = width;
        layout.preferredWidth = width;

        layout.minHeight = height;
        layout.preferredHeight = height;

        layout.flexibleWidth = flexibleWidth;
        layout.flexibleHeight = 0f;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = color;
    }

    private static void DockGeneratedRoot(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
