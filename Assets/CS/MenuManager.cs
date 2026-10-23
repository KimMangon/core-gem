using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MenuManager : MonoBehaviour
{
    private const int MaxRoomPlayers = 4;

    public GameObject menuPanel;
    public GameObject hostSettingsPanel;
    public InputField joinCodeInput;
    public Text joinCodeDisplayText;
    public Text copyFeedbackText;
    public GameObject joinCodePanel;
    public GameObject logPanel;

    // Kept for the old scene's serialized button references.
    [Header("Host Settings")]
    public Text timeLimitText;
    public Text livesText;

    private InputField playerNameInput;
    private Text menuErrorText;
    private Image menuErrorBackground;
    private Text lobbyCodeText;
    private Text lobbyStatusText;
    private Text lobbyPlayersText;
    private readonly Text[] lobbyPlayerNames = new Text[4];
    private readonly Text[] lobbyPlayerColors = new Text[4];
    private readonly Image[] lobbyPlayerSwatches = new Image[4];
    private Text lobbyCopyFeedbackText;
    private Text colorNameText;
    private Outline[] colorOutlines;
    private Button startButton;
    private Toggle playTutorialToggle;
    private GameObject controlHintsPanel;
    private Text controlHintsBody;
    private GameObject tutorialGoalIndicator;
    private RectTransform tutorialArrow;
    private string currentJoinCode;
    private Coroutine copyFeedbackCoroutine;
    private Task servicesReady;
    private NetworkManager observedNetwork;
    private bool isConnecting;
    private bool clientSessionStarted;
    private static string pendingRoomError;
    private bool returningToMenu;
    private int connectionGeneration;
    private float nextRosterRefresh;
    private GameObject clearOverlay;
    private GameObject creditsOverlay;
    private Text clearDifficultyText;
    private Text clearTimeText;
    private Text clearGradeText;
    private Text clearAverageText;
    private readonly Text[] stageResultTimes = new Text[8];
    private readonly Text[] stageResultGrades = new Text[8];
    private readonly Text[] stageResultPoints = new Text[8];
    private bool showingClear;
    private AudioSource clearSoundSource;
    private AudioClip clearSound;
    private SavedRoom selectedSavedRoom;
    private SavedRoom[] savedRoomList = Array.Empty<SavedRoom>();
    private GameObject continueOverlay;
    private readonly Button[] savedRoomButtons = new Button[5];
    private readonly Outline[] savedRoomOutlines = new Outline[5];
    private Text savedRoomPageText;
    private Text savedRoomMessage;
    private int savedRoomPage;
    private string selectedSaveId;
    private Button continueSelectedButton;
    private Button deleteSelectedButton;
    private GameObject deleteConfirmOverlay;
    private Button pauseSaveButton;
    private Text pauseSaveFeedback;
    private RoomResourcePreloader resourcePreloader;
    private RoomLoadingScreen roomLoadingScreen;

    private Font MenuFont => joinCodeDisplayText != null && joinCodeDisplayText.font != null
        ? joinCodeDisplayText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    private void Awake()
    {
        if (menuPanel != null) GameUiScaling.Configure(menuPanel.GetComponentInParent<Canvas>(true));
        GameLocalization.Changed += RefreshLocalizedUi;
        GameLocalization.BindAuthoredUi(menuPanel != null ? menuPanel.transform : null);
        GameLocalization.BindAuthoredUi(joinCodePanel != null ? joinCodePanel.transform : null);
        resourcePreloader = GetComponent<RoomResourcePreloader>();
        clearSound = Resources.Load<AudioClip>("Sfx/Win");
        GameObject clearSfxPlayer = new GameObject("ClearSfxPlayer");
        clearSfxPlayer.transform.SetParent(transform, false);
        clearSoundSource = clearSfxPlayer.AddComponent<AudioSource>();
        clearSoundSource.playOnAwake = false;
        clearSoundSource.spatialBlend = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        servicesReady = InitializeServices();
        ConfigureJoinCodeInput();
        CreateNameInput();
        if(!string.IsNullOrEmpty(pendingRoomError) && menuErrorText!=null)
        {
            GameLocalization.ClearText(menuErrorText);menuErrorText.text=pendingRoomError;
            pendingRoomError=null;
        }
        CreateLobby();
        CreateSettingsMenu();
        CreateResultAndCreditsScreens();
        CreateSavedRoomUi();
        CreateTutorialUi();
        CreateMainMenuTitle();
        if(menuPanel!=null)
        {
            var canvas=menuPanel.GetComponentInParent<Canvas>(true);
            if(canvas!=null)
            {
                roomLoadingScreen=gameObject.AddComponent<RoomLoadingScreen>();
                roomLoadingScreen.Initialize(canvas,MenuFont);
            }
        }
        if (menuPanel != null)
            gameObject.AddComponent<PlayerCustomizationMenu>().Initialize(menuPanel, hostSettingsPanel, MenuFont);
        if (joinCodePanel != null)
            joinCodePanel.SetActive(false);
        if (logPanel != null)
            logPanel.SetActive(false);
    }

    private void CreateMainMenuTitle()
    {
        if (menuPanel == null || menuPanel.transform.Find("GameTitle") != null)
            return;

        Texture2D logo = Resources.Load<Texture2D>("UI/GunishmentTitle");
        if (logo != null)
        {
            RectTransform rect = MakeRect("GameTitle", menuPanel.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 350f),
                new Vector2(900f, 900f * logo.height / logo.width));
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture = logo;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        // Move the central buttons and room-code input together, preserving their spacing.
        foreach (Transform child in menuPanel.transform)
        {
            if ((child.GetComponent<Button>() == null && child.GetComponent<InputField>() == null) ||
                !(child is RectTransform rect))
                continue;
            if (rect.anchorMin != new Vector2(0.5f, 0.5f) || rect.anchorMax != rect.anchorMin)
                continue;
            rect.anchoredPosition += Vector2.down * 60f;
        }
    }

    private void RefreshLocalizedUi()
    {
        if (colorNameText != null)
            colorNameText.text = GameLocalization.T("PLAYER COLOR  /  {0}", GameLocalization.T(PlayerColorPalette.Names[PlayerColorPalette.SelectedIndex]));
        if (savedRoomPageText != null) RefreshSavedRooms();
        if (startButton != null) RefreshLobby();
    }

    private static async Task InitializeServices()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void Start()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        observedNetwork = NetworkManager.Singleton;
        if (observedNetwork == null)
            return;
        observedNetwork.OnClientDisconnectCallback += OnClientDisconnected;
        observedNetwork.OnClientConnectedCallback += OnClientConnected;
        observedNetwork.OnTransportFailure += OnTransportFailure;
    }

    private void Update()
    {
        var loadingNetwork=NetworkManager.Singleton;
        bool awaitingClient=clientSessionStarted && !returningToMenu && loadingNetwork!=null &&
            loadingNetwork.IsClient && !loadingNetwork.IsConnectedClient;
        roomLoadingScreen?.SetLoading(isConnecting || awaitingClient,resourcePreloader);
        if(menuErrorBackground!=null)
            menuErrorBackground.enabled=menuErrorText!=null && !string.IsNullOrWhiteSpace(menuErrorText.text);
        clearSoundSource.volume = GameSettingsPreferences.SfxVolume;
        UpdateTutorialUi();
        bool canSave = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost &&
            StageManager.Instance != null && StageManager.Instance.IsGameStarted &&
            !StageManager.Instance.RoomTimerFinished &&
            !StageManager.Instance.IsTutorialStage;
        if (pauseSaveButton != null)
            pauseSaveButton.gameObject.SetActive(canSave);
        StageManager stage = StageManager.Instance;
        if (stage != null && stage.RoomTimerFinished)
        {
            if (!showingClear)
            {
                showingClear = true;
                clearOverlay.SetActive(true);
                if (clearSound != null)
                    clearSoundSource.PlayOneShot(clearSound, 0.8f);
                PauseManager.IsPaused = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            long seconds = (long)Math.Floor(stage.RoomElapsedSeconds);
            int playerCount = Mathf.Max(1, stage.ClearPlayerCount);
            clearDifficultyText.text = GameLocalization.T(playerCount == 1 ? "DIFFICULTY  {0} PLAYER" : "DIFFICULTY  {0} PLAYERS", playerCount);
            clearTimeText.text = GameLocalization.T("TIME") + $"  {seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
            double average=stage.AverageStagePoints;
            string overall=ClearGradeRules.OverallGrade(average);
            clearGradeText.text=GameLocalization.T("RANK  {0}", overall);
            clearGradeText.color=ResultGradeColor(overall);
            clearAverageText.text=GameLocalization.T("AVERAGE  {0} / 5", average.ToString("F2"))+
                (stage.RecordedStageCount<8?GameLocalization.T("  ({0}/8 RECORDED)", stage.RecordedStageCount):"");
            for(int i=0;i<8;i++)
            {
                double duration=stage.GetStageClearSeconds(i+1);
                string grade=ClearGradeRules.StageGrade(duration);
                stageResultTimes[i].text=ClearGradeRules.FormatTime(duration);
                stageResultGrades[i].text=grade;
                stageResultGrades[i].color=ResultGradeColor(grade);
                stageResultPoints[i].text=duration<0d?"--":ClearGradeRules.Points(duration)+" / 5";
            }
            return;
        }
        if (showingClear)
        {
            showingClear = false;
            selectedSavedRoom = null;
            clearOverlay.SetActive(false);
            creditsOverlay.SetActive(false);
            PauseManager.IsPaused = false;
            hostSettingsPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (hostSettingsPanel == null || !hostSettingsPanel.activeSelf)
            return;

        if (StageManager.Instance != null && StageManager.Instance.IsGameStarted)
        {
            hostSettingsPanel.SetActive(false);
            if (joinCodePanel != null)
                joinCodePanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            return;
        }

        if (Time.unscaledTime >= nextRosterRefresh)
        {
            nextRosterRefresh = Time.unscaledTime + 0.25f;
            RefreshLobby();
        }
    }

    private void OnDestroy()
    {
        GameLocalization.Changed -= RefreshLocalizedUi;
        if (observedNetwork == null)
            return;
        observedNetwork.OnClientDisconnectCallback -= OnClientDisconnected;
        observedNetwork.OnClientConnectedCallback -= OnClientConnected;
        observedNetwork.OnTransportFailure -= OnTransportFailure;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (observedNetwork != null && observedNetwork.IsServer &&
            clientId != NetworkManager.ServerClientId &&
            observedNetwork.ConnectedClientsIds.Count > MaxRoomPlayers)
            observedNetwork.DisconnectClient(clientId, "ROOM FULL");
    }

    private static void ApproveRoomConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        NetworkManager network = NetworkManager.Singleton;
        bool approved = request.ClientNetworkId == NetworkManager.ServerClientId ||
            (network != null && network.ConnectedClientsIds.Count < MaxRoomPlayers &&
             (request.Payload == null || request.Payload.Length <= 512));
        response.Approved = approved;
        response.CreatePlayerObject = approved;
        if (!approved)
            response.Reason = "ROOM FULL";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (observedNetwork != null && clientId == observedNetwork.LocalClientId)
        {
            if(!returningToMenu && clientSessionStarted && !observedNetwork.IsServer)
            {
                string reason=observedNetwork.DisconnectReason;
                Debug.LogWarning("Room connection ended. Reason: "+(string.IsNullOrEmpty(reason)?"Server closed or rejected the connection; compare [RoomConfig] logs on both peers.":reason));
                pendingRoomError=reason=="ROOM FULL"?GameLocalization.T("ROOM FULL"):
                    GameLocalization.Korean?"방 연결이 종료됐습니다. 호스트의 방 상태를 확인한 뒤 다시 참가해 주세요.":
                    "CONNECTION CLOSED. CHECK THE HOST ROOM AND TRY AGAIN.";
            }
            ReturnClientToMenu();
        }
    }

    private void OnTransportFailure() => ReturnClientToMenu();

    private void ReturnClientToMenu()
    {
        if (!clientSessionStarted || returningToMenu || observedNetwork == null ||
            observedNetwork.IsServer)
            return;

        returningToMenu = true;
        clientSessionStarted = false;
        StartCoroutine(LoadMainMenuAfterDisconnect());
    }

    private IEnumerator LoadMainMenuAfterDisconnect()
    {
        yield return null;
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsListening && !network.ShutdownInProgress)
            network.Shutdown(true);
        while (network != null && network.ShutdownInProgress)
            yield return null;

        PauseManager.IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Stage1", LoadSceneMode.Single);
    }

    private void CreateSettingsMenu()
    {
        Canvas canvas = menuPanel != null ? menuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return;
        GameSettingsMenu settings = canvas.GetComponent<GameSettingsMenu>();
        if (settings == null)
            settings = canvas.gameObject.AddComponent<GameSettingsMenu>();
        PauseManager pause = FindFirstObjectByType<PauseManager>();
        settings.Initialize(menuPanel, pause != null ? pause.pausePanel : null, MenuFont);
        settings.SetLobbyPanel(hostSettingsPanel);
    }

    private static Color ResultGradeColor(string grade)
    {
        switch(grade)
        {
            case "S": return new Color(1f,.82f,.24f);
            case "A": return new Color(1f,.2f,.2f);
            case "B": return new Color(.5f,.95f,.65f);
            case "C": return new Color(1f,.7f,.4f);
            default: return new Color(.5f,.52f,.55f);
        }
    }

    private void CreateResultAndCreditsScreens()
    {
        Canvas canvas = menuPanel != null ? menuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return;

        Button creditsButton = MakeTemplateButton("MainMenuCreditsButton", menuPanel.transform,
            "CREDITS", Vector2.zero, new Vector2(170f, 60f), OpenCredits);
        RectTransform creditsRect = (RectTransform)creditsButton.transform;
        creditsRect.anchorMin = creditsRect.anchorMax = new Vector2(1f, 0f);
        creditsRect.anchoredPosition = new Vector2(-100f, 55f);
        creditsButton.GetComponentInChildren<Text>(true).fontSize = 24;

        clearOverlay = MakeFullscreenOverlay("ClearOverlay", canvas.transform);
        clearOverlay.GetComponent<Image>().color = new Color(0.015f, 0.02f, 0.03f, 0.25f);
        RectTransform clearCard = MakeCard("ClearCard", clearOverlay.transform, new Vector2(900f, 840f));
        MakeText("ClearTitle", clearCard, "CLEAR", new Vector2(0f, 344f),
            new Vector2(660f, 88f), 64);
        clearDifficultyText = MakeText("ClearDifficulty", clearCard, "DIFFICULTY",
            new Vector2(0f, 278f), new Vector2(650f, 45f), 28);
        clearTimeText = MakeText("ClearTime", clearCard, "TIME",
            new Vector2(0f, 235f), new Vector2(650f, 44f), 27);
        clearGradeText=MakeText("ClearOverallGrade",clearCard,"RANK",new Vector2(0,164),new Vector2(750,104),60);
        clearGradeText.resizeTextForBestFit=false;
        clearGradeText.horizontalOverflow=HorizontalWrapMode.Overflow;
        clearGradeText.verticalOverflow=VerticalWrapMode.Overflow;
        clearAverageText=MakeText("ClearAveragePoints",clearCard,"AVERAGE",new Vector2(0,101),new Vector2(800,38),25);
        MakeText("StageColumn",clearCard,"STAGE",new Vector2(-290,51),new Vector2(150,32),21);
        MakeText("TimeColumn",clearCard,"TIME",new Vector2(-70,51),new Vector2(200,32),21);
        MakeText("GradeColumn",clearCard,"RANK",new Vector2(140,51),new Vector2(120,32),21);
        MakeText("PointsColumn",clearCard,"POINTS",new Vector2(295,51),new Vector2(140,32),21);
        for(int i=0;i<8;i++)
        {
            float y=13-i*33;
            GameLocalization.Bind(MakeText("StageName"+i,clearCard,"",new Vector2(-290,y),new Vector2(160,32),23), "STAGE {0}", i+1);
            stageResultTimes[i]=MakeText("StageTime"+i,clearCard,"--:--:--",new Vector2(-70,y),new Vector2(200,32),23);
            stageResultGrades[i]=MakeText("StageGrade"+i,clearCard,"--",new Vector2(140,y),new Vector2(120,32),25);
            stageResultPoints[i]=MakeText("StagePoints"+i,clearCard,"--",new Vector2(295,y),new Vector2(140,32),23);
        }
        MakeTemplateButton("ClearMainMenuButton", clearCard, "MAIN MENU",
            new Vector2(-195f, -292f), new Vector2(340f, 70f), LeaveRoom);
        MakeTemplateButton("ClearWaitingRoomButton", clearCard, "WAITING ROOM",
            new Vector2(195f, -292f), new Vector2(340f, 70f), ReturnToWaitingRoom);
        MakeTemplateButton("ClearCreditsButton", clearCard, "CREDITS",
            new Vector2(0f, -370f), new Vector2(210f, 62f), OpenCredits);
        clearOverlay.SetActive(false);

        creditsOverlay = MakeFullscreenOverlay("CreditsOverlay", canvas.transform);
        RectTransform creditsCard = MakeCard("CreditsCard", creditsOverlay.transform,
            new Vector2(900f, 740f));
        MakeText("CreditsTitle", creditsCard, "CREDITS", new Vector2(0f, 315f),
            new Vector2(760f, 55f), 43);
        MakeText("CreditsHint", creditsCard, "SOURCE opens the original page",
            new Vector2(0f, 272f), new Vector2(760f, 34f), 19);
        AddCreditRow(creditsCard, 0, "3D ASSETS  Goldmetal - QuarterView 3D Action BE5",
            "https://www.goldmetal.co.kr");
        AddCreditRow(creditsCard, 1, "FONT  Netmarble - netmarbleB",
            "https://company.netmarble.com/company/ci");
        AddMusicCredit(creditsCard, 2, "MENU  Grieg - Anitra's Dance / Musopen Symphony Orchestra",
            "Grieg, Peer Gynt Suite No. 1, Op. 46 - III. Anitra's Dance.ogg");
        AddMusicCredit(creditsCard, 3, "Chopin - Nocturne No. 1, Op. 9 / Vadim Chaimovich",
            "Chopin, Nocturne No. 1 in B Flat Minor, Op. 9.ogg");
        AddMusicCredit(creditsCard, 4, "Chopin - Prelude Op. 28 No. 17",
            "Prelude Op. 28 no. 17.mp3");
        AddMusicCredit(creditsCard, 5, "Bach - Goldberg Variations, Aria",
            "Bach, Goldberg Variations, Aria (Musopen version).ogg");
        AddMusicCredit(creditsCard, 6, "Beethoven - Pathetique Sonata, II. Adagio cantabile",
            "Beethoven, Sonata No. 8 in C Minor Pathetique, Op. 13 - II. Adagio cantabile.ogg");
        AddMusicCredit(creditsCard, 7, "Bach - Prelude BWV 846 / Kimiko Ishizaka",
            "Kimiko Ishizaka - Bach - Well-Tempered Clavier, Book 1 - 01 Prelude No. 1 in C major, BWV 846.ogg");
        AddMusicCredit(creditsCard, 8, "Chopin - Nocturne No. 2, Op. 9",
            "Nocturne Op. 9 no. 2 in E flat major.mp3");
        AddMusicCredit(creditsCard, 9, "Satie - Gymnopedie No. 1 / Teknopazzo",
            "Gymnopedie No. 1..ogg");
        AddCreditRow(creditsCard, 10, "SFX  Goldmetal Studio - Undead Survivor / Win, Melee1",
            "https://www.youtube.com/playlist?list=PLO-mt5Iu5TeZF8xMHqtT_DhAPKmjF6i3x");
        MakeTemplateButton("CreditsCloseButton", creditsCard, "BACK",
            new Vector2(0f, -310f), new Vector2(200f, 58f), CloseCredits);
        creditsOverlay.SetActive(false);
    }

    private static GameObject MakeFullscreenOverlay(string name, Transform parent)
    {
        RectTransform rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.015f, 0.02f, 0.03f, 0.9f);
        return rect.gameObject;
    }

    private static RectTransform MakeCard(string name, Transform parent, Vector2 size)
    {
        RectTransform card = MakeRect(name, parent, new Vector2(0.5f, 0.5f),
            Vector2.zero, size);
        Image image = card.gameObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.13f, 0.16f, 0.98f);
        return card;
    }

    private void AddMusicCredit(Transform parent, int row, string label, string fileName)
    {
        AddCreditRow(parent, row, label,
            "https://commons.wikimedia.org/wiki/File:" + Uri.EscapeDataString(fileName).Replace("%20", "_"));
    }

    private void AddCreditRow(Transform parent, int row, string label, string url)
    {
        float y = 230f - row * 46f;
        MakeText("Credit" + row, parent, label, new Vector2(-65f, y),
            new Vector2(680f, 40f), 19, TextAnchor.MiddleLeft);
        Button source = MakeTemplateButton("CreditSource" + row, parent, "SOURCE",
            new Vector2(365f, y), new Vector2(105f, 38f), () => Application.OpenURL(url));
        Text sourceText = source.GetComponentInChildren<Text>(true);
        sourceText.fontSize = 18;
        sourceText.resizeTextMinSize = 16;
        sourceText.resizeTextMaxSize = 18;
    }

    private void OpenCredits()
    {
        creditsOverlay.SetActive(true);
        creditsOverlay.transform.SetAsLastSibling();
    }

    private void CloseCredits() => creditsOverlay.SetActive(false);

    private void CreateSavedRoomUi()
    {
        Button continueButton = MakeTemplateButton("MainMenuContinueButton", menuPanel.transform,
            "CONTINUE", new Vector2(0f, 240f), new Vector2(200f, 100f), OpenSavedRooms);
        continueButton.GetComponentInChildren<Text>(true).fontSize = 30;

        Canvas canvas = menuPanel.GetComponentInParent<Canvas>();
        continueOverlay = MakeFullscreenOverlay("SavedRoomsOverlay", canvas.transform);
        RectTransform card = MakeCard("SavedRoomsCard", continueOverlay.transform,
            new Vector2(840f, 800f));
        MakeText("SavedRoomsTitle", card, "SAVED ROOMS",
            new Vector2(0f, 300f), new Vector2(720f, 60f), 40);
        savedRoomMessage = MakeText("SavedRoomsMessage", card, "",
            new Vector2(0f, 245f), new Vector2(700f, 36f), 22);
        for (int i = 0; i < savedRoomButtons.Length; i++)
        {
            int row = i;
            savedRoomButtons[i] = MakeTemplateButton("SavedRoom" + i, card, "",
                new Vector2(0f, 175f - i * 88f), new Vector2(690f, 72f),
                () => SelectSavedRoom(row));
            Text rowText = savedRoomButtons[i].GetComponentInChildren<Text>(true);
            rowText.fontSize = 24;
            rowText.resizeTextMinSize = 18;
            rowText.resizeTextMaxSize = 24;
            Outline outline = savedRoomButtons[i].GetComponent<Outline>();
            if (outline == null)
                outline = savedRoomButtons[i].gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(8f, -8f);
            outline.useGraphicAlpha = false;
            outline.enabled = false;
            savedRoomOutlines[i] = outline;
        }
        MakeTemplateButton("SavedRoomsPrevious", card, "PREV",
            new Vector2(-230f, -290f), new Vector2(150f, 60f), () => ChangeSavedRoomPage(-1));
        savedRoomPageText = MakeText("SavedRoomsPage", card, "1 / 1",
            new Vector2(0f, -290f), new Vector2(130f, 50f), 24);
        MakeTemplateButton("SavedRoomsNext", card, "NEXT",
            new Vector2(230f, -290f), new Vector2(150f, 60f), () => ChangeSavedRoomPage(1));
        continueSelectedButton = MakeTemplateButton("ContinueSelectedRoom", card, "CONTINUE",
            new Vector2(-240f, -355f), new Vector2(190f, 60f), ContinueSelectedRoom);
        deleteSelectedButton = MakeTemplateButton("DeleteSelectedRoom", card, "DELETE",
            new Vector2(0f, -355f), new Vector2(190f, 60f), ConfirmDeleteSelectedRoom);
        MakeTemplateButton("SavedRoomsBack", card, "BACK",
            new Vector2(240f, -355f), new Vector2(190f, 60f), CloseSavedRooms);
        continueOverlay.SetActive(false);

        deleteConfirmOverlay = MakeFullscreenOverlay("DeleteSavedRoomOverlay", canvas.transform);
        RectTransform deleteCard = MakeCard("DeleteSavedRoomCard", deleteConfirmOverlay.transform,
            new Vector2(690f, 330f));
        MakeText("DeleteSavedRoomTitle", deleteCard, "DELETE SAVED ROOM?",
            new Vector2(0f, 90f), new Vector2(620f, 60f), 34);
        MakeText("DeleteSavedRoomWarning", deleteCard, "THIS CANNOT BE UNDONE",
            new Vector2(0f, 25f), new Vector2(620f, 45f), 23);
        MakeTemplateButton("DeleteSavedRoomYes", deleteCard, "DELETE",
            new Vector2(-155f, -90f), new Vector2(230f, 75f), DeleteSelectedRoom);
        MakeTemplateButton("DeleteSavedRoomCancel", deleteCard, "CANCEL",
            new Vector2(155f, -90f), new Vector2(230f, 75f),
            () => deleteConfirmOverlay.SetActive(false));
        deleteConfirmOverlay.SetActive(false);

        PauseManager pause = FindFirstObjectByType<PauseManager>();
        if (pause == null || pause.pausePanel == null)
            return;
        pauseSaveButton = MakeTemplateButton("PauseSaveButton", pause.pausePanel.transform,
            "SAVE", Vector2.zero, new Vector2(200f, 100f), SaveRoom);
        foreach (Text label in pause.pausePanel.GetComponentsInChildren<Text>(true))
        {
            if (label.text.Trim().Equals("RESTART", StringComparison.OrdinalIgnoreCase) || label.text.Trim() == "재시작")
                label.text = "MAIN MENU";
            if (label.text.Trim().Equals("MAIN MENU", StringComparison.OrdinalIgnoreCase))
                FitMainMenuLabel(label);
        }
        GameLocalization.BindAuthoredUi(pause.pausePanel.transform);
        pauseSaveFeedback = MakeText("PauseSaveFeedback", pause.pausePanel.transform, "",
            new Vector2(0f, -330f), new Vector2(650f, 40f), 22);
        pauseSaveButton.gameObject.SetActive(false);
    }

    public static void FitMainMenuLabel(Text label)
    {
        Button button = label.GetComponentInParent<Button>(true);
        if (button == null) return;
        Rect buttonRect = ((RectTransform)button.transform).rect;
        float relativeScaleX = Mathf.Abs(label.transform.lossyScale.x / button.transform.lossyScale.x);
        float relativeScaleY = Mathf.Abs(label.transform.lossyScale.y / button.transform.lossyScale.y);
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize = 100;
        float renderedWidth = label.preferredWidth * Mathf.Max(.001f, relativeScaleX);
        float renderedHeight = label.preferredHeight * Mathf.Max(.001f, relativeScaleY);
        float fit = Mathf.Min(buttonRect.width * .75f / Mathf.Max(1f, renderedWidth),
            buttonRect.height * .65f / Mathf.Max(1f, renderedHeight));
        label.fontSize = Mathf.Clamp(Mathf.FloorToInt(100f * fit), 1, 200);
    }

    private void OpenSavedRooms()
    {
        savedRoomList = SavedRoomStore.LoadAll();
        savedRoomPage = 0;
        selectedSaveId = null;
        GameLocalization.Bind(savedRoomMessage, savedRoomList.Length == 0 ? "NO SAVED ROOMS" : "SELECT A ROOM TO CONTINUE");
        RefreshSavedRooms();
        continueOverlay.SetActive(true);
        continueOverlay.transform.SetAsLastSibling();
    }

    private void CloseSavedRooms()
    {
        deleteConfirmOverlay.SetActive(false);
        continueOverlay.SetActive(false);
    }

    private void ChangeSavedRoomPage(int direction)
    {
        int maxPage = Mathf.Max(0, (savedRoomList.Length - 1) / savedRoomButtons.Length);
        savedRoomPage = Mathf.Clamp(savedRoomPage + direction, 0, maxPage);
        selectedSaveId = null;
        GameLocalization.Bind(savedRoomMessage, savedRoomList.Length == 0 ? "NO SAVED ROOMS" : "SELECT A ROOM TO CONTINUE");
        RefreshSavedRooms();
    }

    private void RefreshSavedRooms()
    {
        int offset = savedRoomPage * savedRoomButtons.Length;
        for (int i = 0; i < savedRoomButtons.Length; i++)
        {
            int index = offset + i;
            savedRoomButtons[i].gameObject.SetActive(index < savedRoomList.Length);
            savedRoomOutlines[i].enabled = index < savedRoomList.Length &&
                savedRoomList[index].id == selectedSaveId;
            if (index >= savedRoomList.Length)
                continue;
            SavedRoom room = savedRoomList[index];
            string names = string.Join(", ", Array.ConvertAll(room.players,
                player => player.displayName));
            TimeSpan elapsed = TimeSpan.FromSeconds(Math.Max(0d, room.elapsedSeconds));
            string savedAt = DateTime.TryParse(room.savedUtc, out DateTime date)
                ? date.ToLocalTime().ToString("MM/dd HH:mm") : GameLocalization.T("SAVED ROOM");
            savedRoomButtons[i].GetComponentInChildren<Text>(true).text =
                GameLocalization.T("{0}  /  CHECKPOINT {1}  /  {2}\n{3}", savedAt, room.stage - 1, $"{elapsed.Hours + elapsed.Days * 24:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}", names);
        }
        savedRoomPageText.text = $"{savedRoomPage + 1} / {Mathf.Max(1, (savedRoomList.Length + 4) / 5)}";
        bool hasSelection = !string.IsNullOrEmpty(selectedSaveId);
        continueSelectedButton.interactable = hasSelection;
        deleteSelectedButton.interactable = hasSelection;
    }

    private void SelectSavedRoom(int row)
    {
        int index = savedRoomPage * savedRoomButtons.Length + row;
        if (index < 0 || index >= savedRoomList.Length)
            return;
        selectedSaveId = savedRoomList[index].id;
        GameLocalization.Bind(savedRoomMessage, "ROOM SELECTED");
        RefreshSavedRooms();
    }

    private async void ContinueSelectedRoom()
    {
        string selectedId = selectedSaveId;
        SavedRoom room = Array.Find(savedRoomList, saved => saved.id == selectedId);
        if (room == null)
            return;
        try
        {
            await servicesReady;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            GameLocalization.Bind(savedRoomMessage, "COULD NOT SIGN IN");
            return;
        }
        if (!continueOverlay.activeSelf || selectedSaveId != selectedId)
            return;
        bool hostIsMember = Array.Exists(room.players,
            player => player.playerId == PlayerProfile.Id);
        if (!hostIsMember)
        {
            GameLocalization.Bind(savedRoomMessage, "THIS HOST IS NOT IN THAT ROOM");
            return;
        }
        if (room.players.Length > MaxRoomPlayers)
        {
            GameLocalization.Bind(savedRoomMessage, "ROOM EXCEEDS 4 PLAYERS");
            return;
        }
        CloseSavedRooms();
        HostRoom(room);
    }

    private void ConfirmDeleteSelectedRoom()
    {
        if (string.IsNullOrEmpty(selectedSaveId))
            return;
        deleteConfirmOverlay.SetActive(true);
        deleteConfirmOverlay.transform.SetAsLastSibling();
    }

    private void DeleteSelectedRoom()
    {
        string id = selectedSaveId;
        deleteConfirmOverlay.SetActive(false);
        if (string.IsNullOrEmpty(id) || !SavedRoomStore.Delete(id))
        {
            GameLocalization.Bind(savedRoomMessage, "COULD NOT DELETE ROOM");
            return;
        }

        savedRoomList = SavedRoomStore.LoadAll();
        selectedSaveId = null;
        savedRoomPage = Mathf.Min(savedRoomPage,
            Mathf.Max(0, (savedRoomList.Length - 1) / savedRoomButtons.Length));
        GameLocalization.Bind(savedRoomMessage, savedRoomList.Length == 0 ? "NO SAVED ROOMS" : "ROOM DELETED");
        RefreshSavedRooms();
    }

    private void SaveRoom()
    {
        if (StageManager.Instance != null && StageManager.Instance.TrySaveRoom())
            GameLocalization.Bind(pauseSaveFeedback, "ROOM SAVED");
        else
            GameLocalization.Bind(pauseSaveFeedback, "COULD NOT SAVE ROOM");
    }

    private void ReturnToWaitingRoom()
    {
        StageManager.Instance?.ReturnToWaitingRoom();
    }

    private void CreateTutorialUi()
    {
        Canvas canvas = menuPanel != null ? menuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            return;

        RectTransform hints = MakeRect("ControlHints", canvas.transform,
            new Vector2(0f, 1f), new Vector2(190f, -185f),
            new Vector2(350f, 360f));
        controlHintsPanel = hints.gameObject;
        var background = hints.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);
        background.raycastTarget = false;
        MakeText("ControlHintsTitle", hints, "CONTROLS",
            new Vector2(0f, 150f), new Vector2(315f, 36f), 25);
        controlHintsBody = MakeText("ControlHintsBody", hints, "",
            new Vector2(10f, -20f), new Vector2(305f, 290f), 18,
            TextAnchor.MiddleLeft);

        RectTransform goal = MakeRect("TutorialGoalIndicator", canvas.transform,
            new Vector2(0.5f, 1f), new Vector2(0f, -83f),
            new Vector2(210f, 145f));
        tutorialGoalIndicator = goal.gameObject;
        MakeText("TutorialGoalLabel", goal, "GOAL",
            new Vector2(0f, 45f), new Vector2(180f, 38f), 25);
        tutorialArrow = MakeRect("GoalArrow", goal,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -22f),
            new Vector2(72f, 76f));
        Color yellow = new Color(1f, 0.9f, 0.02f);
        AddArrowPart("ArrowShaft", tutorialArrow,
            new Vector2(0f, -9f), new Vector2(9f, 48f), 0f, yellow);
        AddArrowPart("ArrowLeft", tutorialArrow,
            new Vector2(-14f, 17f), new Vector2(9f, 37f), -45f, yellow);
        AddArrowPart("ArrowRight", tutorialArrow,
            new Vector2(14f, 17f), new Vector2(9f, 37f), 45f, yellow);
        controlHintsPanel.SetActive(false);
        tutorialGoalIndicator.SetActive(false);
    }

    private static void AddArrowPart(string name, Transform parent, Vector2 position,
        Vector2 size, float rotation, Color color)
    {
        RectTransform rect = MakeRect(name, parent,
            new Vector2(0.5f, 0.5f), position, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private void UpdateTutorialUi()
    {
        StageManager stage = StageManager.Instance;
        bool playing = stage != null && stage.IsGameStarted &&
            !stage.RoomTimerFinished && !PauseManager.IsPaused;
        bool tutorial = playing && stage.IsTutorialStage;
        bool showControls = playing && GameSettingsPreferences.ShowControlHints;
        if (controlHintsPanel != null)
            controlHintsPanel.SetActive(showControls);
        if (showControls && controlHintsBody != null)
        {
            string labels =
                GameLocalization.T("FORWARD") + "  " + GameKeyBindings.GetLabel(PlayerAction.MoveForward) + "\n" +
                GameLocalization.T("BACKWARD") + "  " + GameKeyBindings.GetLabel(PlayerAction.MoveBackward) + "\n" +
                GameLocalization.T("MOVE LEFT") + "  " + GameKeyBindings.GetLabel(PlayerAction.MoveLeft) + "\n" +
                GameLocalization.T("MOVE RIGHT") + "  " + GameKeyBindings.GetLabel(PlayerAction.MoveRight) + "\n" +
                GameLocalization.T("MOUSE  LOOK") + "\n" +
                GameLocalization.T("SPRINT") + "  " + GameKeyBindings.GetLabel(PlayerAction.Sprint) + "\n" +
                GameLocalization.T("JUMP") + "  " + GameKeyBindings.GetLabel(PlayerAction.Jump) + "\n" +
                GameLocalization.T("PUSH") + "  " + GameKeyBindings.GetLabel(PlayerAction.Push) + "\n" +
                GameLocalization.T("GESTURE") + "  " + GameKeyBindings.GetLabel(PlayerAction.Gesture) + "\n" +
                GameLocalization.T("KICK") + "  " + GameKeyBindings.GetLabel(PlayerAction.Kick) + "\n" +
                GameLocalization.T("RESPAWN") + "  " + GameKeyBindings.GetLabel(PlayerAction.Respawn) + "\n" +
                GameLocalization.T("RELOAD") + "  " + GameKeyBindings.GetLabel(PlayerAction.Reload);
            if (controlHintsBody.text != labels)
                controlHintsBody.text = labels;
        }
        if (tutorialGoalIndicator != null)
            tutorialGoalIndicator.SetActive(tutorial);
        if (!tutorial || tutorialArrow == null)
            return;

        NetworkManager network = NetworkManager.Singleton;
        Camera view = network != null && network.LocalClient != null &&
            network.LocalClient.PlayerObject != null
            ? network.LocalClient.PlayerObject.GetComponentInChildren<Camera>(true)
            : null;
        if (view == null)
            return;
        Vector3 direction = stage.TutorialGoalPosition - view.transform.position;
        direction.y = 0f;
        Vector3 forward = view.transform.forward;
        forward.y = 0f;
        Vector3 right = view.transform.right;
        right.y = 0f;
        float angle = Mathf.Atan2(Vector3.Dot(direction, right),
            Vector3.Dot(direction, forward)) * Mathf.Rad2Deg;
        tutorialArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor,
        Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private Text MakeText(string name, Transform parent, string value,
        Vector2 position, Vector2 size, int fontSize, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var text = MakeRect(name, parent, new Vector2(0.5f, 0.5f), position, size)
            .gameObject.AddComponent<Text>();
        text.font = MenuFont;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = value;
        GameLocalization.BindKnown(text, value);
        return text;
    }

    private Button MakeTemplateButton(string name, Transform parent, string label,
        Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Transform template = menuPanel.transform.Find("Host Button");
        GameObject go = Instantiate(template.gameObject, parent, false);
        go.name = name;
        go.SetActive(true);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        Text labelText = go.GetComponentInChildren<Text>(true);
        if (labelText != null)
        {
            labelText.text = label;
            RectTransform textRect = labelText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(12f, 6f);
            textRect.offsetMax = new Vector2(-12f, -6f);
            textRect.localScale = Vector3.one;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.fontSize = 32;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 26;
            labelText.resizeTextMaxSize = 32;
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
        }
        GameLocalization.BindKnown(labelText, label);
        return button;
    }

    private void CreateNameInput()
    {
        if (menuPanel == null || menuPanel.transform.Find("PlayerNameCard") != null)
            return;

        var card = MakeRect("PlayerNameCard", menuPanel.transform,
            new Vector2(0f, 0.5f), new Vector2(175f, 0f), new Vector2(310f, 142f));
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
        MakeText("PlayerNameTitle", card, "PLAYER NAME",
            new Vector2(0f, 40f), new Vector2(270f, 34f), 23);

        var fieldRect = MakeRect("PlayerNameInput", card,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -19f), new Vector2(270f, 54f));
        var background = fieldRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.21f, 0.22f, 0.26f, 1f);
        playerNameInput = MakeTextInput(fieldRect, background, PlayerProfile.Name,
            Color.white, 28, "PLAYER NAME");
        playerNameInput.characterLimit = 14;
        playerNameInput.onEndEdit.AddListener(value =>
        {
            PlayerProfile.Name = value;
        });

        var errorPanel=MakeRect("ConnectionMessagePanel",menuPanel.transform,
            new Vector2(.5f,0f),new Vector2(0f,80f),new Vector2(1000f,76f));
        menuErrorBackground=errorPanel.gameObject.AddComponent<Image>();
        menuErrorBackground.color=new Color(.06f,.07f,.09f,.9f);
        menuErrorBackground.raycastTarget=false;menuErrorBackground.enabled=false;
        menuErrorText = MakeText("ConnectionError", errorPanel, "",
            Vector2.zero, new Vector2(960f,66f), 25);
        menuErrorText.horizontalOverflow=HorizontalWrapMode.Wrap;
        menuErrorText.verticalOverflow=VerticalWrapMode.Truncate;
        menuErrorText.color = new Color(1f, 0.67f, 0.35f);
    }

    private void ConfigureJoinCodeInput()
    {
        if (joinCodeInput == null)
            return;
        joinCodeInput.enabled = true;
        joinCodeInput.contentType = InputField.ContentType.Alphanumeric;
        joinCodeInput.characterLimit = 6;
        joinCodeInput.SetTextWithoutNotify(string.Empty);
        if (joinCodeInput.textComponent != null)
            joinCodeInput.textComponent.gameObject.SetActive(true);
        if (joinCodeInput.placeholder != null)
            joinCodeInput.placeholder.gameObject.SetActive(true);
    }

    private InputField MakeTextInput(RectTransform root, Image background,
        string initialText, Color textColor, int fontSize, string placeholderValue)
    {
        RectTransform textRect = MakeRect("InputText", root,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 5f);
        textRect.offsetMax = new Vector2(-12f, -5f);
        Text text = textRect.gameObject.AddComponent<Text>();
        text.font = MenuFont;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        text.raycastTarget = false;

        RectTransform placeholderRect = MakeRect("Placeholder", root,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(12f, 5f);
        placeholderRect.offsetMax = new Vector2(-12f, -5f);
        Text placeholder = placeholderRect.gameObject.AddComponent<Text>();
        placeholder.font = MenuFont;
        placeholder.fontSize = fontSize;
        placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
        placeholder.alignment = TextAnchor.MiddleLeft;
        placeholder.text = placeholderValue;
        GameLocalization.BindKnown(placeholder, placeholderValue);
        placeholder.raycastTarget = false;

        InputField field = root.gameObject.AddComponent<InputField>();
        field.targetGraphic = background;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = InputField.LineType.SingleLine;
        field.SetTextWithoutNotify(initialText ?? string.Empty);
        return field;
    }

    private void SavePlayerName()
    {
        if (playerNameInput != null)
            PlayerProfile.Name = playerNameInput.text;
    }

    private void CreateLobby()
    {
        if (hostSettingsPanel == null || hostSettingsPanel.transform.Find("LobbyRoot") != null)
            return;

        // Retain the scene's existing panel, but replace the obsolete button-game controls.
        foreach (Transform child in hostSettingsPanel.transform)
            child.gameObject.SetActive(false);

        var card = MakeRect("LobbyRoot", hostSettingsPanel.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 810f));
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.12f, 0.13f, 0.16f, 0.97f);
        cardImage.raycastTarget = true;
        MakeText("LobbyTitle", card, "WAITING ROOM",
            new Vector2(0f, 275f), new Vector2(620f, 56f), 38);
        lobbyStatusText = MakeText("LobbyStatus", card, "CONNECTING...",
            new Vector2(0f, 223f), new Vector2(620f, 36f), 22);
        MakeText("RoomCodeLabel", card, "ROOM CODE",
            new Vector2(-190f, 155f), new Vector2(180f, 40f), 23);
        lobbyCodeText = MakeText("RoomCodeValue", card, "------",
            new Vector2(18f, 155f), new Vector2(215f, 50f), 31);
        lobbyCopyFeedbackText = MakeText("CopyFeedback", card, "",
            new Vector2(265f, 106f), new Vector2(120f, 30f), 18);
        CreateLobbyCopyButton(card);

        MakeText("PlayersTitle", card, "PLAYERS",
            new Vector2(0f, 82f), new Vector2(600f, 40f), 28);
        lobbyPlayersText = MakeText("PlayersList", card, "CONNECTING...",
            new Vector2(0f, -100f), new Vector2(570f, 38f), 21, TextAnchor.MiddleLeft);
        lobbyPlayersText.resizeTextForBestFit = true;
        lobbyPlayersText.resizeTextMinSize = 15;
        lobbyPlayersText.resizeTextMaxSize = 21;
        for (int i = 0; i < lobbyPlayerNames.Length; i++)
        {
            var row = MakeRect("PlayerRow" + (i + 1), card,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 42f - i * 34f),
                new Vector2(570f, 30f));
            lobbyPlayerNames[i] = MakeText("PlayerName", row, "",
                new Vector2(-110f, 0f), new Vector2(350f, 30f), 23, TextAnchor.MiddleLeft);
            lobbyPlayerColors[i] = MakeText("PlayerColor", row, "",
                new Vector2(175f, 0f), new Vector2(140f, 30f), 23, TextAnchor.MiddleLeft);
            var swatch = MakeRect("PlayerColorSwatch", row,
                new Vector2(0.5f, 0.5f), new Vector2(270f, 0f), new Vector2(22f, 22f));
            lobbyPlayerSwatches[i] = swatch.gameObject.AddComponent<Image>();
            lobbyPlayerSwatches[i].raycastTarget = false;
            row.gameObject.SetActive(false);
        }
        CreateColorPicker(card);
        var tutorialRow = MakeRect("PlayTutorialRow", card,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -259f),
            new Vector2(390f, 42f));
        playTutorialToggle = tutorialRow.gameObject.AddComponent<Toggle>();
        var box = MakeRect("Checkbox", tutorialRow,
            new Vector2(0.5f, 0.5f), new Vector2(-170f, 0f),
            new Vector2(34f, 34f));
        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = Color.white;
        var mark = MakeRect("Checkmark", box,
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(22f, 22f));
        var markImage = mark.gameObject.AddComponent<Image>();
        markImage.color = new Color(1f, 0.82f, 0.2f);
        playTutorialToggle.targetGraphic = boxImage;
        playTutorialToggle.graphic = markImage;
        MakeText("PlayTutorialLabel", tutorialRow, "PLAY TUTORIAL",
            new Vector2(25f, 0f), new Vector2(315f, 40f), 23,
            TextAnchor.MiddleLeft);
        playTutorialToggle.onValueChanged.AddListener(value =>
            StageManager.Instance?.SetPlayTutorial(value));
        startButton = MakeTemplateButton("LobbyStartButton", card, "START GAME",
            new Vector2(-165f, -348f), new Vector2(285f, 90f), StartGame);
        MakeTemplateButton("LobbyLeaveButton", card, "LEAVE ROOM",
            new Vector2(165f, -348f), new Vector2(285f, 90f), LeaveRoom);
    }

    private void CreateLobbyCopyButton(Transform parent)
    {
        Button original = null;
        if (joinCodePanel != null)
        {
            foreach (Button button in joinCodePanel.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "JoinCodeCopy Button")
                {
                    original = button;
                    break;
                }
            }
        }

        if (original == null)
        {
            MakeTemplateButton("LobbyCopyButton", parent, "COPY",
                new Vector2(255f, 155f), new Vector2(110f, 64f), CopyJoinCode);
            return;
        }

        GameObject copy = Instantiate(original.gameObject, parent, false);
        copy.name = "LobbyCopyButton";
        copy.SetActive(true);
        var rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(255f, 155f);
        rect.sizeDelta = new Vector2(70f, 70f);
    }

    private void CreateColorPicker(Transform parent)
    {
        var picker = MakeRect("PlayerColorPicker", parent,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -175f), new Vector2(500f, 105f));
        picker.gameObject.AddComponent<Image>().color =
            new Color(0.08f, 0.09f, 0.12f, 0.85f);
        colorNameText = MakeText("SelectedColorText", picker, "",
            new Vector2(0f, 27f), new Vector2(470f, 31f), 22);

        colorOutlines = new Outline[PlayerColorPalette.Colors.Length];
        for (int i = 0; i < colorOutlines.Length; i++)
        {
            int selected = i;
            var swatch = MakeRect("Color " + PlayerColorPalette.Names[i], picker,
                new Vector2(0.5f, 0.5f), new Vector2((i - 3.5f) * 53f, -20f),
                new Vector2(39f, 39f));
            var image = swatch.gameObject.AddComponent<Image>();
            image.color = PlayerColorPalette.Colors[i];
            var button = swatch.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectColor(selected));
            var outline = swatch.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            colorOutlines[i] = outline;
        }
        SelectColor(PlayerColorPalette.SelectedIndex);
    }

    private void SelectColor(int index)
    {
        PlayerColorPalette.SelectedIndex = index;
        colorNameText.text = GameLocalization.T("PLAYER COLOR  /  {0}", GameLocalization.T(PlayerColorPalette.Names[index]));
        for (int i = 0; i < colorOutlines.Length; i++)
            colorOutlines[i].enabled = i == index;

        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsClient && network.LocalClient != null &&
            network.LocalClient.PlayerObject != null)
            network.LocalClient.PlayerObject.GetComponent<PlayerMovement>()?.RefreshOwnerProfile();
    }

    private void RefreshLobby()
    {
        NetworkManager network = NetworkManager.Singleton;
        bool connected = network != null && network.IsListening;
        bool rosterReady = selectedSavedRoom == null || SavedRosterReady();
        bool canChooseTutorial = connected && network.IsHost &&
            selectedSavedRoom == null && StageManager.Instance != null &&
            StageManager.Instance.IsSpawned;
        playTutorialToggle.gameObject.SetActive(selectedSavedRoom == null);
        playTutorialToggle.interactable = canChooseTutorial;
        playTutorialToggle.SetIsOnWithoutNotify(StageManager.Instance != null &&
            StageManager.Instance.PlayTutorialSelected);
        startButton.gameObject.SetActive(connected && network.IsHost);
        startButton.interactable = connected && network.IsHost &&
            StageManager.Instance != null && StageManager.Instance.IsSpawned && rosterReady;
        startButton.GetComponentInChildren<Text>(true).text = selectedSavedRoom == null
            ? GameLocalization.T("START GAME") : GameLocalization.T("CONTINUE GAME");
        if (isConnecting)
            lobbyStatusText.text = network != null && network.IsHost
                ? GameLocalization.T("WAITING FOR PLAYERS") : GameLocalization.T("CONNECTING...");
        else
            lobbyStatusText.text = connected
                ? (network.IsHost ? GameLocalization.T("WAITING FOR PLAYERS") : GameLocalization.T("WAITING FOR HOST"))
                : GameLocalization.T("CONNECTING...");

        if (selectedSavedRoom != null)
            lobbyStatusText.text = rosterReady ? GameLocalization.T("ORIGINAL TEAM READY") :
                GameLocalization.T("WAITING FOR ORIGINAL PLAYERS");
        if (resourcePreloader != null && resourcePreloader.IsLoading)
            lobbyStatusText.text = GameLocalization.T("PREPARING RESOURCES  {0}%", Mathf.RoundToInt(resourcePreloader.Progress * 100f));

        var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        if (selectedSavedRoom != null && network != null && network.IsHost)
        {
            var accepted = new System.Collections.Generic.HashSet<string>();
            foreach (PlayerMovement player in players)
            {
                string id = player.PersistentPlayerId;
                if (!player.IsSpawned || string.IsNullOrEmpty(id))
                    continue;
                bool expected = Array.Exists(selectedSavedRoom.players,
                    saved => saved.playerId == id);
                if ((!expected || !accepted.Add(id)) &&
                    player.OwnerClientId != NetworkManager.ServerClientId)
                {
                    network.DisconnectClient(player.OwnerClientId);
                    return;
                }
            }
        }
        for (int i = 0; i < lobbyPlayerNames.Length; i++)
            lobbyPlayerNames[i].transform.parent.gameObject.SetActive(false);
        int count = 0;
        foreach (PlayerMovement player in players)
        {
            if (!player.IsSpawned || count >= lobbyPlayerNames.Length)
                continue;
            string name = player.DisplayName;
            if (string.IsNullOrWhiteSpace(name) || name == "PLAYER")
                name = GameLocalization.T("PLAYER {0}", player.OwnerClientId + 1);
            int colorIndex = 0;
            uint packed = PlayerColorPalette.Pack(player.AvatarColor);
            for (int i = 0; i < PlayerColorPalette.Colors.Length; i++)
            {
                if (packed == PlayerColorPalette.Pack(PlayerColorPalette.Colors[i]))
                {
                    colorIndex = i;
                    break;
                }
            }
            string displayName = (count + 1) + ".  " + name;
            if (player.OwnerClientId == NetworkManager.ServerClientId)
                displayName = GameLocalization.T("{0} (HOST)", displayName);
            lobbyPlayerNames[count].text = displayName;
            lobbyPlayerColors[count].text = GameLocalization.T(PlayerColorPalette.Names[colorIndex]);
            lobbyPlayerSwatches[count].color = player.AvatarColor;
            lobbyPlayerNames[count].transform.parent.gameObject.SetActive(true);
            count++;
        }
        if (selectedSavedRoom == null)
            lobbyPlayersText.text = count == 0 ? GameLocalization.T("WAITING FOR PLAYERS...") : "";
        else
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (SavedPlayerState saved in selectedSavedRoom.players)
            {
                bool present = Array.Exists(players, player => player.IsSpawned &&
                    player.PersistentPlayerId == saved.playerId);
                if (!present)
                    missing.Add(saved.displayName);
            }
            lobbyPlayersText.text = missing.Count == 0 ? "" :
                GameLocalization.T("WAITING: {0}", string.Join(", ", missing));
        }
    }

    private bool SavedRosterReady()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (selectedSavedRoom == null || network == null || !network.IsListening ||
            network.ConnectedClientsIds.Count != selectedSavedRoom.players.Length)
            return false;
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        if (players.Length != selectedSavedRoom.players.Length)
            return false;
        var expected = new System.Collections.Generic.HashSet<string>();
        foreach (SavedPlayerState saved in selectedSavedRoom.players)
            expected.Add(saved.playerId);
        foreach (PlayerMovement player in players)
            if (!player.IsSpawned || !expected.Remove(player.PersistentPlayerId))
                return false;
        return expected.Count == 0;
    }

    public void OnHostClicked() => HostRoom(null);

    private static void PrepareRoomNetwork(NetworkManager network)
    {
        if(network.ShutdownInProgress)
            throw new InvalidOperationException("Previous room is still shutting down. Please try again.");
        // Apply the same handshake configuration before starting either role.
        var config=network.NetworkConfig;
        config.ConnectionApproval=true;
        config.TickRate=30;
        config.ForceSamePrefabs=true;
        config.EnableSceneManagement=true;
        config.EnsureNetworkVariableLengthSafety=false;
    }

    private static void LogRoomConfiguration(NetworkManager network,string role)
    {
        var config=network.NetworkConfig;
        string prefabs=string.Join(",",config.Prefabs.NetworkPrefabOverrideLinks.Keys.OrderBy(key=>key));
        Debug.Log($"[RoomConfig] role={role} version={Application.version} unity={Application.unityVersion} "+
            $"hash={config.GetConfig(false):X16} cached={config.GetConfig():X16} protocol={config.ProtocolVersion} "+
            $"tick={config.TickRate} approval={config.ConnectionApproval} samePrefabs={config.ForceSamePrefabs} "+
            $"scenes={config.EnableSceneManagement} variableSafety={config.EnsureNetworkVariableLengthSafety} rpcHash={config.RpcHashSize} prefabs=[{prefabs}]");
    }

    private async void HostRoom(SavedRoom resume)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (isConnecting || network == null || network.IsListening)
            return;

        SavePlayerName();
        selectedSavedRoom = resume;
        if (menuErrorText != null)
            GameLocalization.ClearText(menuErrorText);
        menuPanel.SetActive(false);
        hostSettingsPanel.SetActive(true);
        currentJoinCode = "";
        lobbyCodeText.text = "------";
        isConnecting = true;
        roomLoadingScreen?.SetLoading(true,resourcePreloader);
        int generation = ++connectionGeneration;

        try
        {
            if (resourcePreloader != null) await resourcePreloader.PrepareAsync();
            if (this == null || generation != connectionGeneration) return;
            await servicesReady;
            if (generation != connectionGeneration)
                return;
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxRoomPlayers - 1);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (generation != connectionGeneration)
                return;

            var transport = network.GetComponent<UnityTransport>();
            PrepareRoomNetwork(network);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveRoomConnection;
            if (!network.StartHost())
                throw new InvalidOperationException("Could not start the room.");
            LogRoomConfiguration(network,"HOST");

            currentJoinCode = code;
            lobbyCodeText.text = code;
        }
        catch (Exception exception)
        {
            if (this == null || generation != connectionGeneration) return;
            Debug.LogException(exception);
            selectedSavedRoom = null;
            hostSettingsPanel.SetActive(false);
            menuPanel.SetActive(true);
            if (menuErrorText != null)
                GameLocalization.Bind(menuErrorText, "COULD NOT CREATE ROOM");
        }
        finally
        {
            isConnecting = false;
        }
    }

    // Keeps the old serialized button callback harmless if that button is re-enabled.
    public void OnConfirmHostSettings() => StartGame();

    public async void OnJoinClicked()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (isConnecting || network == null || network.IsListening)
            return;

        SavePlayerName();
        if (menuErrorText != null)
            GameLocalization.ClearText(menuErrorText);
        string code = joinCodeInput.text.Trim().ToUpperInvariant();
        if (code.Length != 6 || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]{6}$"))
        {
            if (menuErrorText != null)
                GameLocalization.Bind(menuErrorText, "ENTER A 6-CHARACTER ROOM CODE");
            return;
        }

        menuPanel.SetActive(false);
        hostSettingsPanel.SetActive(true);
        currentJoinCode = code;
        lobbyCodeText.text = code;
        isConnecting = true;
        roomLoadingScreen?.SetLoading(true,resourcePreloader);
        int generation = ++connectionGeneration;
        try
        {
            if (resourcePreloader != null) await resourcePreloader.PrepareAsync();
            if (this == null || generation != connectionGeneration) return;
            await servicesReady;
            if (generation != connectionGeneration)
                return;
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            if (generation != connectionGeneration)
                return;

            var transport = network.GetComponent<UnityTransport>();
            PrepareRoomNetwork(network);
            transport.DisconnectTimeoutMS = 8000;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            network.NetworkConfig.ConnectionApproval = true;
            if (!network.StartClient())
                throw new InvalidOperationException("Could not join the room.");
            LogRoomConfiguration(network,"CLIENT");
            clientSessionStarted = true;
        }
        catch (Exception exception)
        {
            if (this == null || generation != connectionGeneration) return;
            Debug.LogException(exception);
            hostSettingsPanel.SetActive(false);
            menuPanel.SetActive(true);
            if (menuErrorText != null)
                GameLocalization.Bind(menuErrorText, "COULD NOT JOIN ROOM");
        }
        finally
        {
            isConnecting = false;
        }
    }

    public void StartGame()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsHost || StageManager.Instance == null)
            return;
        if (selectedSavedRoom != null)
        {
            if (!SavedRosterReady() || !StageManager.Instance.BeginSavedGame(selectedSavedRoom))
                GameLocalization.Bind(lobbyStatusText, "COULD NOT CONTINUE ROOM");
            return;
        }
        StageManager.Instance.BeginGame();
    }

    public void LeaveRoom()
    {
        StageManager.Instance?.SaveElapsedBeforeExit();
        connectionGeneration++;
        selectedSavedRoom = null;
        if (returningToMenu)
            return;
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening)
        {
            isConnecting = false;
            hostSettingsPanel.SetActive(false);
            menuPanel.SetActive(true);
            return;
        }
        returningToMenu = true;
        clientSessionStarted = false;
        StartCoroutine(LoadMainMenuAfterDisconnect());
    }

    public void CopyJoinCode()
    {
        if (string.IsNullOrEmpty(currentJoinCode))
            return;
        GUIUtility.systemCopyBuffer = currentJoinCode;
        if (copyFeedbackCoroutine != null)
            StopCoroutine(copyFeedbackCoroutine);
        copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedback());
    }

    private IEnumerator ShowCopyFeedback()
    {
        if (lobbyCopyFeedbackText != null)
            GameLocalization.Bind(lobbyCopyFeedbackText, "COPIED");
        yield return new WaitForSecondsRealtime(1.2f);
        if (lobbyCopyFeedbackText != null)
            GameLocalization.ClearText(lobbyCopyFeedbackText);
    }

    public void ChangeTimeLimit(float dir)
    {
        GameManager game = FindAnyObjectByType<GameManager>();
        if (game != null)
            game.AdjustTimeLimit(dir * 0.5f);
    }

    public void ChangeLives(int dir)
    {
        GameManager game = FindAnyObjectByType<GameManager>();
        if (game != null)
            game.AdjustStartingLives(dir);
    }
}
