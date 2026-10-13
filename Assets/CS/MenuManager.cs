using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject menuPanel;
    public GameObject hostSettingsPanel;
    public InputField joinCodeInput;
    public Text joinCodeDisplayText;
    public Text copyFeedbackText;
    public GameObject joinCodePanel;
    public GameObject logPanel;

    private bool isConnecting = false;

    [Header("Host Settings")]
    public Text timeLimitText;
    public Text livesText;

    private string currentJoinCode;
    private Coroutine copyFeedbackCoroutine;
    private Text colorNameText;
    private UnityEngine.UI.Outline[] colorOutlines;
    private NetworkManager observedNetwork;
    private bool clientSessionStarted;
    private bool returningToMenu;

    async void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        CreateColorPicker();
        CreateSettingsMenu();

        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void CreateSettingsMenu()
    {
        if (menuPanel == null)
            return;

        Canvas canvas = menuPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        GameSettingsMenu settings = canvas.GetComponent<GameSettingsMenu>();
        if (settings == null)
            settings = canvas.gameObject.AddComponent<GameSettingsMenu>();
        PauseManager pause = FindFirstObjectByType<PauseManager>();
        Font menuFont = joinCodeDisplayText != null ? joinCodeDisplayText.font : null;
        settings.Initialize(menuPanel, pause != null ? pause.pausePanel : null, menuFont);
    }

    private void Start()
    {
        observedNetwork = NetworkManager.Singleton;
        if (observedNetwork == null)
            return;

        observedNetwork.OnClientDisconnectCallback += OnClientDisconnected;
        observedNetwork.OnTransportFailure += OnTransportFailure;
    }

    private void OnDestroy()
    {
        if (observedNetwork == null)
            return;

        observedNetwork.OnClientDisconnectCallback -= OnClientDisconnected;
        observedNetwork.OnTransportFailure -= OnTransportFailure;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (observedNetwork == null || clientId != observedNetwork.LocalClientId)
            return;

        ReturnClientToMenu();
    }

    private void OnTransportFailure()
    {
        ReturnClientToMenu();
    }

    private void ReturnClientToMenu()
    {
        if (!clientSessionStarted || returningToMenu || observedNetwork == null ||
            observedNetwork.IsServer || observedNetwork.ShutdownInProgress)
            return;

        returningToMenu = true;
        clientSessionStarted = false;
        StartCoroutine(LoadMainMenuAfterDisconnect());
    }

    private IEnumerator LoadMainMenuAfterDisconnect()
    {
        // Let Netcode finish dispatching the disconnect before unloading its scenes.
        yield return null;

        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsClient && !network.ShutdownInProgress)
            network.Shutdown(true);
        while (network != null && network.ShutdownInProgress)
            yield return null;

        PauseManager.IsPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Stage1", LoadSceneMode.Single);
    }

    private void CreateColorPicker()
    {
        if (menuPanel == null || menuPanel.transform.Find("PlayerColorPicker") != null)
            return;

        var picker = new GameObject("PlayerColorPicker", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        picker.transform.SetParent(menuPanel.transform, false);
        var pickerRect = (RectTransform)picker.transform;
        pickerRect.anchorMin = new Vector2(0.5f, 0f);
        pickerRect.anchorMax = new Vector2(0.5f, 0f);
        pickerRect.pivot = new Vector2(0.5f, 0f);
        pickerRect.anchoredPosition = new Vector2(0f, 16f);
        pickerRect.sizeDelta = new Vector2(372f, 100f);
        var background = picker.GetComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.08f, 0.09f, 0.12f, 0.82f);
        background.raycastTarget = false;

        var titleObject = new GameObject("SelectedColorText", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
        titleObject.transform.SetParent(picker.transform, false);
        var titleRect = (RectTransform)titleObject.transform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 27f);
        titleRect.sizeDelta = new Vector2(340f, 30f);
        colorNameText = titleObject.GetComponent<UnityEngine.UI.Text>();
        colorNameText.font = joinCodeDisplayText != null && joinCodeDisplayText.font != null
            ? joinCodeDisplayText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        colorNameText.fontSize = 22;
        colorNameText.alignment = TextAnchor.MiddleCenter;
        colorNameText.color = Color.white;
        colorNameText.raycastTarget = false;

        colorOutlines = new UnityEngine.UI.Outline[PlayerColorPalette.Colors.Length];
        for (int i = 0; i < PlayerColorPalette.Colors.Length; i++)
        {
            int selected = i;
            var swatch = new GameObject("Color " + PlayerColorPalette.Names[i],
                typeof(RectTransform), typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.Outline));
            swatch.transform.SetParent(picker.transform, false);
            var rect = (RectTransform)swatch.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((i - 3.5f) * 43f, -19f);
            rect.sizeDelta = new Vector2(32f, 32f);
            var image = swatch.GetComponent<UnityEngine.UI.Image>();
            image.color = PlayerColorPalette.Colors[i];
            image.raycastTarget = true;
            var button = swatch.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SelectColor(selected));
            var outline = swatch.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            colorOutlines[i] = outline;
        }

        SelectColor(PlayerColorPalette.SelectedIndex);
    }

    private void SelectColor(int index)
    {
        PlayerColorPalette.SelectedIndex = index;
        colorNameText.text = "PLAYER COLOR  /  " + PlayerColorPalette.Names[index];
        for (int i = 0; i < colorOutlines.Length; i++)
            colorOutlines[i].enabled = i == index;
    }

    public void OnHostClicked()
    {
        menuPanel.SetActive(false);
        hostSettingsPanel.SetActive(true);
        RefreshHostSettingsText();
    }

    public async void OnConfirmHostSettings()
    {
        if (isConnecting) return; // 추가: 이미 처리 중이면 무시
        isConnecting = true; // 추가

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

        NetworkManager.Singleton.StartHost();
        hostSettingsPanel.SetActive(false);

        currentJoinCode = joinCode;
        joinCodeDisplayText.text = $"joinCode : {joinCode}";
        joinCodePanel.SetActive(true);
        logPanel.SetActive(true);

        isConnecting = false; // 추가

    }

    public async void OnJoinClicked()
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCodeInput.text);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.DisconnectTimeoutMS = 8000;
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

        if (!NetworkManager.Singleton.StartClient())
            return;

        clientSessionStarted = true;
        logPanel.SetActive(true);
        menuPanel.SetActive(false);
    }

    public void ChangeTimeLimit(float dir)
    {
        FindAnyObjectByType<GameManager>().AdjustTimeLimit(dir * 0.5f);
        RefreshHostSettingsText();
    }

    public void ChangeLives(int dir)
    {
        FindAnyObjectByType<GameManager>().AdjustStartingLives(dir);
        RefreshHostSettingsText();
    }

    private void RefreshHostSettingsText()
    {
        GameManager gm = FindAnyObjectByType<GameManager>();
        timeLimitText.text = $"{gm.GetTimeLimit():F1}";
        livesText.text = $"{gm.GetStartingLives()}";
    }

    public void CopyJoinCode()
    {
        GUIUtility.systemCopyBuffer = currentJoinCode;

        if (copyFeedbackCoroutine != null) StopCoroutine(copyFeedbackCoroutine);
        copyFeedbackCoroutine = StartCoroutine(ShowCopyFeedback());
    }

    private IEnumerator ShowCopyFeedback()
    {
        copyFeedbackText.text = "Copy!";
        copyFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(1.2f);
        copyFeedbackText.gameObject.SetActive(false);
    }
}
