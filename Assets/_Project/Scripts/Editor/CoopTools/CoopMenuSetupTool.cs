using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CoopMenuSetupTool
{
    private const string MainScenePath = "Assets/_Project/Scenes/Main/MainScene.unity";
    private const string CoopMenuPrefabPath = "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu/CoopMenu.prefab";
    private const string ButtonTemplatePath = "Assets/_Project/Prefabs/UI/Menus/Btn_Template.prefab";
    private const string AutoConfigureRequestPath = "Library/CodexConfigureCoopMenu.request";
    private const string AutoConfigureResultPath = "Library/CodexConfigureCoopMenu.result";

    private static readonly Color OverlayColor = new Color(0.02f, 0.025f, 0.03f, 0.78f);
    private static readonly Color WindowColor = new Color(0.07f, 0.075f, 0.085f, 0.96f);
    private static readonly Color FieldColor = new Color(0.11f, 0.12f, 0.14f, 1f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.18f, 0.21f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color MutedTextColor = new Color(0.72f, 0.74f, 0.78f, 1f);
    private const float FieldGroupHeight = 54f;
    private const float FieldLabelHeight = 16f;
    private const float FieldInputHeight = 34f;

    [MenuItem("Tools/Zombie Rogue/Coop/Configure Main Menu")]
    public static void ConfigureMainMenu()
    {
        GameObject prefab = CreateOrUpdateCoopMenuPrefab();
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (canvas == null)
            canvas = CreateCanvas();

        MenuController menuController = canvas.GetComponent<MenuController>();
        if (menuController == null)
            menuController = canvas.gameObject.AddComponent<MenuController>();

        SerializedObject serializedMenu = new SerializedObject(menuController);
        serializedMenu.FindProperty("coopMenuPrefab").objectReferenceValue = prefab;
        serializedMenu.FindProperty("coopMenuRoot").objectReferenceValue = canvas.transform;
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        Button coopButton = FindOrCreateCoopButton();
        WireCoopButton(coopButton, menuController);
        EnsureNetcodeBootstrapOverride();
        EnsureEventSystem();

        EditorUtility.SetDirty(menuController);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Coop menu prefab and MainScene button were configured.");
    }

    [InitializeOnLoadMethod]
    private static void ConfigureFromRequestOnLoad()
    {
        if (!File.Exists(AutoConfigureRequestPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoConfigureRequestPath))
                return;

            try
            {
                File.Delete(AutoConfigureRequestPath);
                ConfigureMainMenu();
                File.WriteAllText(AutoConfigureResultPath, $"OK {DateTime.Now:O}");
            }
            catch (Exception exception)
            {
                File.WriteAllText(AutoConfigureResultPath, exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    private static GameObject CreateOrUpdateCoopMenuPrefab()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(CoopMenuPrefabPath) ?? "Assets/_Project/Resources/RuntimeLoadedOnly/Prefabs/UI/Menu");

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        DefaultControls.Resources resources = GetDefaultUiResources();

        GameObject root = new GameObject("CoopMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CoopMenuController));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        root.GetComponent<Image>().color = OverlayColor;

        GameObject window = CreatePanel("Window", root.transform, WindowColor);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(640f, 620f);
        windowRect.anchoredPosition = Vector2.zero;

        GameObject closeButtonObject = CreateButton("Btn_Close", window.transform, "X", font, resources, new Vector2(34f, 34f));
        RectTransform closeRect = closeButtonObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-14f, -14f);

        Text statusText = CreateText("Txt_Status", window.transform, "Готово", 18, MutedTextColor, TextAnchor.MiddleCenter);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.pivot = new Vector2(0.5f, 0f);
        statusRect.anchoredPosition = new Vector2(0f, 18f);
        statusRect.sizeDelta = new Vector2(-64f, 34f);

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.layer = root.layer;
        content.transform.SetParent(window.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(56f, 68f);
        contentRect.offsetMax = new Vector2(-56f, -58f);

        GameObject modePanel = CreateStackPanel("ModePanel", content.transform);
        CreateTitle(modePanel.transform, "Кооператив", font);
        Button openCreateButton = CreateButton("Btn_OpenCreate", modePanel.transform, "Создать комнату", font, resources, new Vector2(0f, 56f)).GetComponent<Button>();
        Button openJoinButton = CreateButton("Btn_OpenJoin", modePanel.transform, "Подключиться", font, resources, new Vector2(0f, 56f)).GetComponent<Button>();
        Button modeBackButton = CreateButton("Btn_ModeBack", modePanel.transform, "Назад", font, resources, new Vector2(0f, 48f)).GetComponent<Button>();

        GameObject createPanel = CreateStackPanel("CreateRoomPanel", content.transform);
        CreateTitle(createPanel.transform, "Создание комнаты", font);
        InputField roomNameInput = CreateLabeledInput("Field_RoomName", createPanel.transform, "Название комнаты", "Input_RoomName", "Например: Комната друзей", string.Empty, font, resources);
        InputField playerCountInput = CreateLabeledInput("Field_PlayerCount", createPanel.transform, "Количество игроков", "Input_PlayerCount", "От 2 до 8", string.Empty, font, resources);
        Dropdown locationDropdown = CreateLabeledDropdown("Field_Location", createPanel.transform, "Локация", "Dropdown_Location", font, resources, "Demo City");
        InputField hostPortInput = CreateLabeledInput("Field_HostPort", createPanel.transform, "Порт комнаты", "Input_HostPort", "7777", string.Empty, font, resources);
        Button createButton = CreateButton("Btn_CreateRoom", createPanel.transform, "Создать", font, resources, new Vector2(0f, 44f)).GetComponent<Button>();
        Button createBackButton = CreateButton("Btn_CreateBack", createPanel.transform, "Назад", font, resources, new Vector2(0f, 38f)).GetComponent<Button>();

        GameObject joinPanel = CreateStackPanel("JoinRoomPanel", content.transform);
        CreateTitle(joinPanel.transform, "Подключение", font);
        InputField joinAddressInput = CreateLabeledInput("Field_JoinAddress", joinPanel.transform, "ID комнаты или IP", "Input_JoinAddress", "Например: 127.0.0.1:7777", string.Empty, font, resources);
        InputField joinPortInput = CreateLabeledInput("Field_JoinPort", joinPanel.transform, "Порт", "Input_JoinPort", "Если IP введен без порта", string.Empty, font, resources);
        Button connectButton = CreateButton("Btn_Connect", joinPanel.transform, "Подключиться", font, resources, new Vector2(0f, 44f)).GetComponent<Button>();
        Button joinBackButton = CreateButton("Btn_JoinBack", joinPanel.transform, "Назад", font, resources, new Vector2(0f, 38f)).GetComponent<Button>();

        GameObject lobbyPanel = CreateStackPanel("LobbyPanel", content.transform);
        Text lobbyTitle = CreateTitle(lobbyPanel.transform, "Комната", font);
        Text roomIdText = CreateTextLine(lobbyPanel.transform, "ID комнаты: -", font);
        Text playerCountText = CreateTextLine(lobbyPanel.transform, "Игроки: 1/2", font);
        Text locationText = CreateTextLine(lobbyPanel.transform, "Локация: Demo City", font);
        Button startButton = CreateButton("Btn_StartGame", lobbyPanel.transform, "Запустить", font, resources, new Vector2(0f, 44f)).GetComponent<Button>();
        Button leaveButton = CreateButton("Btn_LeaveRoom", lobbyPanel.transform, "Покинуть", font, resources, new Vector2(0f, 38f)).GetComponent<Button>();

        CoopMenuController controller = root.GetComponent<CoopMenuController>();
        controller.modePanel = modePanel;
        controller.createRoomPanel = createPanel;
        controller.joinRoomPanel = joinPanel;
        controller.lobbyPanel = lobbyPanel;
        controller.roomNameInput = roomNameInput;
        controller.playerCountInput = playerCountInput;
        controller.hostPortInput = hostPortInput;
        controller.locationDropdown = locationDropdown;
        controller.joinAddressInput = joinAddressInput;
        controller.joinPortInput = joinPortInput;
        controller.lobbyTitleText = lobbyTitle;
        controller.roomIdText = roomIdText;
        controller.playerCountText = playerCountText;
        controller.locationText = locationText;
        controller.statusText = statusText;
        controller.createButton = createButton;
        controller.connectButton = connectButton;
        controller.startButton = startButton;
        controller.leaveButton = leaveButton;
        controller.closeButton = closeButtonObject.GetComponent<Button>();
        controller.openCreateButton = openCreateButton;
        controller.openJoinButton = openJoinButton;
        controller.backButtons = new[] { modeBackButton, createBackButton, joinBackButton };

        createPanel.SetActive(false);
        joinPanel.SetActive(false);
        lobbyPanel.SetActive(false);

        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, CoopMenuPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return savedPrefab;
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MenuController));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static Button FindOrCreateCoopButton()
    {
        GameObject existing = GameObject.Find("Btn_NewCoopGame");
        if (existing != null && existing.TryGetComponent(out Button existingButton))
            return existingButton;

        Transform parent = FindMenuButtonsParent();
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonTemplatePath);
        GameObject buttonObject;

        if (template != null)
        {
            buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(template);
            buttonObject.transform.SetParent(parent, false);
        }
        else
        {
            buttonObject = CreateButton("Btn_NewCoopGame", parent, "Кооператив", Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), GetDefaultUiResources(), new Vector2(0f, 64f));
        }

        buttonObject.name = "Btn_NewCoopGame";
        SetButtonLabel(buttonObject, "Кооператив");
        return buttonObject.GetComponent<Button>();
    }

    private static Transform FindMenuButtonsParent()
    {
        GameObject menuPanel = GameObject.Find("MenuPanel");
        if (menuPanel != null)
        {
            Transform vbox = FindChildRecursive(menuPanel.transform, "VBox");
            if (vbox != null)
                return vbox;

            return menuPanel.transform;
        }

        Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    private static void WireCoopButton(Button button, MenuController menuController)
    {
        if (button == null || menuController == null)
            return;

        button.onClick.RemoveAllListeners();
        UnityEventTools.AddPersistentListener(button.onClick, menuController.OnOpenCoopMenu);
        SetButtonLabel(button.gameObject, "Кооператив");
        EditorUtility.SetDirty(button);
    }

    private static void EnsureNetcodeBootstrapOverride()
    {
        OverrideAutomaticNetcodeBootstrap bootstrapOverride = UnityEngine.Object.FindObjectOfType<OverrideAutomaticNetcodeBootstrap>();
        if (bootstrapOverride == null)
        {
            GameObject root = new GameObject("NetcodeBootstrapOverride");
            bootstrapOverride = root.AddComponent<OverrideAutomaticNetcodeBootstrap>();
        }

        bootstrapOverride.ForceAutomaticBootstrapInScene = NetCodeConfig.AutomaticBootstrapSetting.DisableAutomaticBootstrap;
        EditorUtility.SetDirty(bootstrapOverride);
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        return panel;
    }

    private static GameObject CreateStackPanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        panel.layer = LayerMask.NameToLayer("UI");
        panel.transform.SetParent(parent, false);
        Stretch(panel.GetComponent<RectTransform>());

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 12, 8);
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return panel;
    }

    private static Text CreateTitle(Transform parent, string text, Font font)
    {
        Text title = CreateText("Title", parent, text, 30, TextColor, TextAnchor.MiddleCenter);
        title.font = font;
        LayoutElement layout = title.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 44f;
        return title;
    }

    private static Text CreateTextLine(Transform parent, string text, Font font)
    {
        Text line = CreateText("Text", parent, text, 20, MutedTextColor, TextAnchor.MiddleLeft);
        line.font = font;
        LayoutElement layout = line.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;
        return line;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateButton(string name, Transform parent, string label, Font font, DefaultControls.Resources resources, Vector2 size)
    {
        GameObject buttonObject = DefaultControls.CreateButton(resources);
        buttonObject.name = name;
        buttonObject.layer = LayerMask.NameToLayer("UI");
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        SetButtonLabel(buttonObject, label);
        Text text = buttonObject.GetComponentInChildren<Text>(true);
        if (text != null)
        {
            text.font = font;
            text.fontSize = 22;
            text.color = TextColor;
        }

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredHeight = size.y;
        layout.minHeight = size.y;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        return buttonObject;
    }

    private static InputField CreateLabeledInput(string groupName, Transform parent, string label, string inputName, string placeholder, string value, Font font, DefaultControls.Resources resources)
    {
        Transform group = CreateFieldGroup(groupName, parent, font, label, FieldGroupHeight);
        return CreateInput(inputName, group, placeholder, value, font, resources);
    }

    private static Dropdown CreateLabeledDropdown(string groupName, Transform parent, string label, string dropdownName, Font font, DefaultControls.Resources resources, string option)
    {
        Transform group = CreateFieldGroup(groupName, parent, font, label, FieldGroupHeight);
        return CreateDropdown(dropdownName, group, font, resources, option);
    }

    private static Transform CreateFieldGroup(string name, Transform parent, Font font, string label, float preferredHeight)
    {
        GameObject groupObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        groupObject.layer = LayerMask.NameToLayer("UI");
        groupObject.transform.SetParent(parent, false);

        VerticalLayoutGroup layout = groupObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LayoutElement groupLayout = groupObject.GetComponent<LayoutElement>();
        groupLayout.preferredHeight = preferredHeight;
        groupLayout.minHeight = preferredHeight;

        Text labelText = CreateText("Label", groupObject.transform, label, 16, MutedTextColor, TextAnchor.MiddleLeft);
        labelText.font = font;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredHeight = FieldLabelHeight;
        labelLayout.minHeight = FieldLabelHeight;

        return groupObject.transform;
    }

    private static InputField CreateInput(string name, Transform parent, string placeholder, string value, Font font, DefaultControls.Resources resources)
    {
        GameObject inputObject = DefaultControls.CreateInputField(resources);
        inputObject.name = name;
        inputObject.layer = LayerMask.NameToLayer("UI");
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<Image>().color = FieldColor;

        InputField input = inputObject.GetComponent<InputField>();
        input.text = value;
        input.textComponent.font = font;
        input.textComponent.fontSize = 18;
        input.textComponent.color = TextColor;
        input.textComponent.alignment = TextAnchor.MiddleLeft;
        input.textComponent.raycastTarget = false;
        ConfigureInputTextRect(input.textComponent.rectTransform);

        Text placeholderText = input.placeholder as Text;
        if (placeholderText != null)
        {
            placeholderText.text = placeholder;
            placeholderText.font = font;
            placeholderText.fontSize = 18;
            placeholderText.color = new Color(MutedTextColor.r, MutedTextColor.g, MutedTextColor.b, 0.72f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.raycastTarget = false;
            ConfigureInputTextRect(placeholderText.rectTransform);
        }

        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.preferredHeight = FieldInputHeight;
        layout.minHeight = FieldInputHeight;
        return input;
    }

    private static void ConfigureInputTextRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 3f);
        rect.offsetMax = new Vector2(-10f, -3f);
    }

    private static Dropdown CreateDropdown(string name, Transform parent, Font font, DefaultControls.Resources resources, string option)
    {
        GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
        dropdownObject.name = name;
        dropdownObject.layer = LayerMask.NameToLayer("UI");
        dropdownObject.transform.SetParent(parent, false);
        dropdownObject.GetComponent<Image>().color = FieldColor;

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { option });

        Text[] texts = dropdownObject.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            text.font = font;
            text.fontSize = 18;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleLeft;
        }

        LayoutElement layout = dropdownObject.AddComponent<LayoutElement>();
        layout.preferredHeight = FieldInputHeight;
        layout.minHeight = FieldInputHeight;
        return dropdown;
    }

    private static void SetButtonLabel(GameObject buttonObject, string label)
    {
        TMP_Text tmp = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        Text text = buttonObject.GetComponentInChildren<Text>(true);
        if (text != null)
            text.text = label;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static DefaultControls.Resources GetDefaultUiResources()
    {
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }
}
