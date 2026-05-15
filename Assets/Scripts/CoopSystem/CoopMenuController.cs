using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CoopMenuController : MonoBehaviour
{
    private static readonly Color FieldTextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color FieldHintColor = new Color(0.72f, 0.74f, 0.78f, 0.72f);
    private static readonly Color FieldColor = new Color(0.11f, 0.12f, 0.14f, 1f);
    private const float TitleHeight = 44f;
    private const float FieldGroupHeight = 54f;
    private const float FieldInputHeight = 34f;
    private const float FieldLabelHeight = 16f;
    private const float PrimaryButtonHeight = 44f;
    private const float SecondaryButtonHeight = 38f;

    [Header("Panels")]
    public GameObject modePanel;
    public GameObject createRoomPanel;
    public GameObject joinRoomPanel;
    public GameObject lobbyPanel;

    [Header("Create Room")]
    public InputField roomNameInput;
    public InputField playerCountInput;
    public InputField hostPortInput;
    public Dropdown locationDropdown;

    [Header("Join Room")]
    public InputField joinAddressInput;
    public InputField joinPortInput;

    [Header("Lobby")]
    public Text lobbyTitleText;
    public Text roomIdText;
    public Text playerCountText;
    public Text locationText;
    public Text statusText;

    [Header("Buttons")]
    public Button createButton;
    public Button connectButton;
    public Button startButton;
    public Button leaveButton;
    public Button closeButton;
    public Button openCreateButton;
    public Button openJoinButton;
    public Button[] backButtons;

    [Header("Locations")]
    public List<CoopLocationOption> locations = new List<CoopLocationOption>
    {
        new CoopLocationOption
        {
            displayName = "Demo City",
            sceneName = "Demo_City_Universal_RenderPipeline"
        }
    };

    private CoopNetworkSession session;
    private bool cursorGuardActive;

    private void Awake()
    {
        session = CoopNetworkSession.GetOrCreate();
        NormalizeMenuUi();
        BindButtons();
        RefreshLocations();
        ApplyDefaults();
        ShowModeMenu();
    }

    private void OnEnable()
    {
        session = CoopNetworkSession.GetOrCreate();
        NormalizeMenuUi();
        session.StatusChanged += OnStatusChanged;
        session.PlayerCountChanged += OnPlayerCountChanged;
        session.RoomChanged += RefreshLobby;
        ActivateCursorGuard();
        RefreshLobby();
    }

    private void OnDisable()
    {
        if (session == null)
            return;

        session.StatusChanged -= OnStatusChanged;
        session.PlayerCountChanged -= OnPlayerCountChanged;
        session.RoomChanged -= RefreshLobby;
        DeactivateCursorGuard();
    }

    private void Update()
    {
        GameCursorGuard.ApplyUiCursor();

        if (Input.GetKeyDown(KeyCode.Escape))
            BackOrClose();
    }

    public void ShowModeMenu()
    {
        SetPanel(modePanel);
        SetStatus(session != null ? session.LastStatus : string.Empty);
    }

    public void ShowCreateRoom()
    {
        SetPanel(createRoomPanel);
        SetStatus("Настрой комнату и создай лобби.");
    }

    public void ShowJoinRoom()
    {
        SetPanel(joinRoomPanel);
        SetStatus("Введи ID комнаты вида 192.168.0.10:7777 или IP и порт.");
    }

    public void CreateRoom()
    {
        if (session == null)
            session = CoopNetworkSession.GetOrCreate();

        CoopLocationOption location = GetSelectedLocation();
        string roomName = string.IsNullOrWhiteSpace(roomNameInput?.text) ? "Комната" : roomNameInput.text.Trim();
        int maxPlayers = ParseClamped(playerCountInput?.text, 2, 2, 8);
        int port = ParseClamped(hostPortInput?.text, CoopNetworkSession.DefaultPort, 1024, 65535);

        session.StartHost(new CoopRoomSettings(roomName, maxPlayers, location, port));
        SetPanel(lobbyPanel);
        RefreshLobby();
    }

    public void ConnectToRoom()
    {
        if (session == null)
            session = CoopNetworkSession.GetOrCreate();

        string address = joinAddressInput != null ? joinAddressInput.text : string.Empty;
        int port = ParseClamped(joinPortInput?.text, CoopNetworkSession.DefaultPort, 1024, 65535);

        session.Join(address, port);
        SetPanel(lobbyPanel);
        RefreshLobby();
    }

    public void StartGame()
    {
        session?.StartGameAsHost();
        RefreshLobby();
    }

    public void LeaveRoom()
    {
        session?.LeaveRoom();
        ShowModeMenu();
    }

    public void Close()
    {
        DeactivateCursorGuard();
        gameObject.SetActive(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (isActiveAndEnabled)
            GameCursorGuard.ApplyUiCursor();
    }

    public void BackOrClose()
    {
        if (lobbyPanel != null && lobbyPanel.activeSelf)
        {
            LeaveRoom();
            return;
        }

        if ((createRoomPanel != null && createRoomPanel.activeSelf) ||
            (joinRoomPanel != null && joinRoomPanel.activeSelf))
        {
            ShowModeMenu();
            return;
        }

        Close();
    }

    private void BindButtons()
    {
        openCreateButton?.onClick.RemoveListener(ShowCreateRoom);
        openCreateButton?.onClick.AddListener(ShowCreateRoom);

        openJoinButton?.onClick.RemoveListener(ShowJoinRoom);
        openJoinButton?.onClick.AddListener(ShowJoinRoom);

        createButton?.onClick.RemoveListener(CreateRoom);
        createButton?.onClick.AddListener(CreateRoom);

        connectButton?.onClick.RemoveListener(ConnectToRoom);
        connectButton?.onClick.AddListener(ConnectToRoom);

        startButton?.onClick.RemoveListener(StartGame);
        startButton?.onClick.AddListener(StartGame);

        leaveButton?.onClick.RemoveListener(LeaveRoom);
        leaveButton?.onClick.AddListener(LeaveRoom);

        closeButton?.onClick.RemoveListener(BackOrClose);
        closeButton?.onClick.AddListener(BackOrClose);

        if (backButtons == null)
            return;

        foreach (Button backButton in backButtons)
        {
            if (backButton == null)
                continue;

            backButton.onClick.RemoveListener(ShowModeMenu);
            backButton.onClick.AddListener(ShowModeMenu);
        }
    }

    private void RefreshLocations()
    {
        EnsureLocations();

        if (locationDropdown == null)
            return;

        locationDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (CoopLocationOption location in locations)
            options.Add(string.IsNullOrWhiteSpace(location.displayName) ? location.sceneName : location.displayName);

        locationDropdown.AddOptions(options);
        locationDropdown.value = Mathf.Clamp(locationDropdown.value, 0, locations.Count - 1);
        locationDropdown.RefreshShownValue();
    }

    private void ApplyDefaults()
    {
        // Defaults are applied on submit so empty fields can keep their visible hints.
    }

    private void RefreshLobby()
    {
        if (session == null)
            return;

        if (lobbyTitleText != null)
            lobbyTitleText.text = string.IsNullOrWhiteSpace(CoopSessionState.RoomName)
                ? "Кооператив"
                : CoopSessionState.RoomName;

        if (roomIdText != null)
            roomIdText.text = string.IsNullOrWhiteSpace(CoopSessionState.RoomId)
                ? "ID комнаты: -"
                : $"ID комнаты: {CoopSessionState.RoomId}";

        if (playerCountText != null)
            playerCountText.text = $"Игроки: {session.CurrentPlayers}/{session.MaxPlayers}";

        if (locationText != null)
            locationText.text = $"Локация: {CoopSessionState.LocationDisplayName}";

        if (startButton != null)
        {
            startButton.gameObject.SetActive(CoopSessionState.IsHost);
            startButton.interactable = session.CanStartGame;
        }

        SetStatus(session.LastStatus);
    }

    private CoopLocationOption GetSelectedLocation()
    {
        EnsureLocations();

        int index = locationDropdown != null ? locationDropdown.value : 0;
        return locations[Mathf.Clamp(index, 0, locations.Count - 1)];
    }

    private void EnsureLocations()
    {
        if (locations != null && locations.Count > 0)
            return;

        locations = new List<CoopLocationOption>
        {
            new CoopLocationOption
            {
                displayName = "Demo City",
                sceneName = "Demo_City_Universal_RenderPipeline"
            }
        };
    }

    private void SetPanel(GameObject activePanel)
    {
        SetActive(modePanel, activePanel == modePanel);
        SetActive(createRoomPanel, activePanel == createRoomPanel);
        SetActive(joinRoomPanel, activePanel == joinRoomPanel);
        SetActive(lobbyPanel, activePanel == lobbyPanel);
        RebuildLayout(activePanel);
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
            statusText.text = value ?? string.Empty;
    }

    private void OnStatusChanged(string value)
    {
        SetStatus(value);
        RefreshLobby();
    }

    private void OnPlayerCountChanged(int players, int maxPlayers)
    {
        RefreshLobby();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private void NormalizeMenuUi()
    {
        NormalizeStackPanel(modePanel);
        NormalizeStackPanel(createRoomPanel);
        NormalizeStackPanel(joinRoomPanel);
        NormalizeStackPanel(lobbyPanel);

        NormalizeInput(roomNameInput);
        NormalizeInput(playerCountInput);
        NormalizeInput(hostPortInput);
        NormalizeInput(joinAddressInput);
        NormalizeInput(joinPortInput);
        NormalizeDropdown(locationDropdown);

        NormalizeButton(createButton, PrimaryButtonHeight);
        NormalizeButton(connectButton, PrimaryButtonHeight);
        NormalizeButton(startButton, PrimaryButtonHeight);
        NormalizeButton(leaveButton, SecondaryButtonHeight);

        if (backButtons != null)
        {
            foreach (Button button in backButtons)
                NormalizeButton(button, SecondaryButtonHeight);
        }
    }

    private static void NormalizeStackPanel(GameObject panel)
    {
        if (panel == null)
            return;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(24, 24, 12, 8);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        foreach (Transform child in panel.transform)
        {
            if (child.name == "Title")
                SetLayoutHeight(child.gameObject, TitleHeight);
        }
    }

    private static void NormalizeInput(InputField input)
    {
        if (input == null)
            return;

        if (input.targetGraphic is Image image)
            image.color = FieldColor;

        if (input.textComponent == null)
            input.textComponent = FindInputText(input);

        if (input.textComponent != null)
        {
            input.textComponent.enabled = true;
            input.textComponent.gameObject.SetActive(true);
            input.textComponent.color = FieldTextColor;
            input.textComponent.fontSize = 18;
            input.textComponent.alignment = TextAnchor.MiddleLeft;
            input.textComponent.raycastTarget = false;
            ConfigureInputTextRect(input.textComponent.rectTransform);
        }

        if (input.placeholder is Text placeholder)
        {
            placeholder.enabled = true;
            placeholder.color = FieldHintColor;
            placeholder.fontSize = 18;
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.raycastTarget = false;
            ConfigureInputTextRect(placeholder.rectTransform);
        }

        input.customCaretColor = true;
        input.caretColor = FieldTextColor;
        input.selectionColor = new Color(0.55f, 0.72f, 1f, 0.45f);
        input.ForceLabelUpdate();

        SetLayoutHeight(input.gameObject, FieldInputHeight);
        NormalizeFieldGroup(input.transform.parent);
    }

    private static Text FindInputText(InputField input)
    {
        Text placeholder = input.placeholder as Text;
        foreach (Text text in input.GetComponentsInChildren<Text>(true))
        {
            if (text != placeholder)
                return text;
        }

        return null;
    }

    private static void NormalizeDropdown(Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        if (dropdown.targetGraphic is Image image)
            image.color = FieldColor;

        foreach (Text text in dropdown.GetComponentsInChildren<Text>(true))
        {
            text.color = FieldTextColor;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleLeft;
        }

        SetLayoutHeight(dropdown.gameObject, FieldInputHeight);
        NormalizeFieldGroup(dropdown.transform.parent);
    }

    private static void NormalizeFieldGroup(Transform group)
    {
        if (group == null || !group.name.StartsWith("Field_"))
            return;

        VerticalLayoutGroup layout = group.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        SetLayoutHeight(group.gameObject, FieldGroupHeight);

        foreach (Transform child in group)
        {
            if (child.name == "Label")
                SetLayoutHeight(child.gameObject, FieldLabelHeight);
        }
    }

    private static void NormalizeButton(Button button, float height)
    {
        if (button == null)
            return;

        SetLayoutHeight(button.gameObject, height);
    }

    private static void SetLayoutHeight(GameObject target, float height)
    {
        if (target == null)
            return;

        LayoutElement layout = target.GetComponent<LayoutElement>();
        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void ConfigureInputTextRect(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 3f);
        rect.offsetMax = new Vector2(-10f, -3f);
    }

    private static void RebuildLayout(GameObject activePanel)
    {
        if (activePanel == null)
            return;

        RectTransform rect = activePanel.GetComponent<RectTransform>();
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
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

    private static int ParseClamped(string value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, out int result))
            result = fallback;

        return Mathf.Clamp(result, min, max);
    }
}
