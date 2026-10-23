using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;

/// <summary>Settings overlay shared by the main menu and the pause screen.</summary>
public class GameSettingsMenu : MonoBehaviour
{
    public static GameSettingsMenu Instance { get; private set; }
    public bool IsOpen => overlay != null && overlay.activeSelf;
    public bool IsWaitingForBinding => waitingForBinding.HasValue;

    private GameObject mainMenuPanel;
    private GameObject pausePanel;
    private GameObject lobbyPanel;
    private GameObject returnPanel;
    private GameObject overlay;
    private GameObject settingsCard;
    private GameObject keyBindingsPanel;
    private readonly Text[] bindingValues = new Text[GameKeyBindings.ActionCount];
    private Text bindingStatus;
    private PlayerAction? waitingForBinding;
    private int captureStartFrame;
    private Font font;
    private Button buttonTemplate;
    private Image buttonTemplateImage;
    private Text buttonTemplateText;
    private Slider volumeSlider;
    private Slider bgmVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider sensitivitySlider;
    private Toggle controlHintsToggle;
    private Text volumeValue;
    private Text bgmVolumeValue;
    private Text sfxVolumeValue;
    private Text sensitivityValue;
    private Text fullscreenValue;
    private Text resolutionValue;
    private Text languageValue;
    private readonly List<Vector2Int> availableResolutions = new();

    private static readonly Color PanelColor = new(0.12f, 0.13f, 0.16f, 0.98f);
    private static readonly Color AccentColor = new(1f, 0.82f, 0.2f, 1f);

    private void Awake()
    {
        Instance = this;
        GameSettingsPreferences.Load();
        GameLocalization.Changed += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        GameLocalization.Changed -= OnLanguageChanged;
        if (Instance == this)
            Instance = null;
    }

    public void Initialize(GameObject mainMenu, GameObject pause, Font menuFont)
    {
        if (overlay != null)
            return;

        mainMenuPanel = mainMenu;
        pausePanel = pause;
        font = menuFont != null ? menuFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var hostButton = mainMenuPanel.transform.Find("Host Button");
        if (hostButton != null)
        {
            buttonTemplate = hostButton.GetComponent<Button>();
            buttonTemplateImage = hostButton.GetComponent<Image>();
            buttonTemplateText = hostButton.GetComponentInChildren<Text>();
        }

        CreateButton("OpenSettingsFromMainMenu", mainMenuPanel.transform,
            "SETTINGS", new Vector2(1f, 1f), new Vector2(-110f, -60f),
            new Vector2(200f, 100f), OpenFromMainMenu);
        CreateButton("QuitFromMainMenu", mainMenuPanel.transform,
            "QUIT", new Vector2(0.5f, 0.5f), new Vector2(0f, -240f),
            new Vector2(200f, 100f), QuitGame);
        if (pausePanel != null)
            CreateButton("OpenSettingsFromPause", pausePanel.transform,
                "SETTINGS", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(200f, 100f), OpenFromPause);

        BuildOverlay();
    }

    public void SetLobbyPanel(GameObject lobby) => lobbyPanel = lobby;

    public void OpenFromMainMenu()
    {
        returnPanel = mainMenuPanel;
        mainMenuPanel.SetActive(false);
        Open();
    }

    public void OpenFromPause()
    {
        returnPanel = pausePanel;
        pausePanel.SetActive(false);
        Open();
    }

    public void OpenFromLobby()
    {
        if (lobbyPanel == null)
            return;
        returnPanel = lobbyPanel;
        lobbyPanel.SetActive(false);
        Open();
    }

    private void Open()
    {
        waitingForBinding = null;
        if (settingsCard != null)
            settingsCard.SetActive(true);
        if (keyBindingsPanel != null)
            keyBindingsPanel.SetActive(false);
        RefreshValues();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        if (keyBindingsPanel != null && keyBindingsPanel.activeSelf)
        {
            CloseKeyBindings();
            return;
        }

        overlay.SetActive(false);
        if (returnPanel != null)
            returnPanel.SetActive(true);
    }

    private void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void BuildOverlay()
    {
        var overlayRect = CreateRect("GameSettingsOverlay", transform, Vector2.zero,
            Vector2.zero, Vector2.zero);
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        overlay = overlayRect.gameObject;
        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.025f, 0.04f, 0.82f);
        dim.raycastTarget = true;

        var card = CreateRect("SettingsCard", overlayRect, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(500f, 800f));
        settingsCard = card.gameObject;
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = PanelColor;
        cardImage.raycastTarget = false;

        CreateText("SettingsTitle", card, "SETTINGS", new Vector2(0f, 330f),
            new Vector2(430f, 52f), 38, Color.white);
        var divider = CreateRect("TitleDivider", card, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 289f), new Vector2(410f, 4f));
        divider.gameObject.AddComponent<Image>().color = AccentColor;
        divider.GetComponent<Image>().raycastTarget = false;

        CreateText("LanguageLabel", card, "LANGUAGE", new Vector2(-140f, 245f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        var languageButton = CreateButton("LanguageButton", card, "한국어 / English",
            new Vector2(0.5f, 0.5f), new Vector2(95f, 245f), new Vector2(220f, 56f),
            () => GameLocalization.SetLanguage(!GameLocalization.Korean));
        languageValue = languageButton.GetComponentInChildren<Text>();
        languageValue.rectTransform.localScale = Vector3.one;
        languageValue.fontSize = 24;
        GameLocalization.Style(languageValue);

        CreateText("VolumeLabel", card, "MASTER VOLUME", new Vector2(-140f, 150f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        volumeValue = CreateText("VolumeValue", card, "100%", new Vector2(205f, 150f),
            new Vector2(62f, 35f), 18, Color.white);
        volumeSlider = CreateSlider("MasterVolumeSlider", card, 150f, 0f, 1f,
            GameSettingsPreferences.Volume, value =>
            {
                GameSettingsPreferences.SetVolume(value);
                volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
            });

        CreateText("BgmVolumeLabel", card, "BGM VOLUME", new Vector2(-140f, 93f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        bgmVolumeValue = CreateText("BgmVolumeValue", card, "100%", new Vector2(205f, 93f),
            new Vector2(62f, 35f), 18, Color.white);
        bgmVolumeSlider = CreateSlider("BgmVolumeSlider", card, 93f, 0f, 1f,
            GameSettingsPreferences.BgmVolume, value =>
            {
                GameSettingsPreferences.SetBgmVolume(value);
                bgmVolumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
            });

        CreateText("SfxVolumeLabel", card, "SFX VOLUME", new Vector2(-140f, 36f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        sfxVolumeValue = CreateText("SfxVolumeValue", card, "100%", new Vector2(205f, 36f),
            new Vector2(62f, 35f), 18, Color.white);
        sfxVolumeSlider = CreateSlider("SfxVolumeSlider", card, 36f, 0f, 1f,
            GameSettingsPreferences.SfxVolume, value =>
            {
                GameSettingsPreferences.SetSfxVolume(value);
                sfxVolumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
            });

        CreateText("SensitivityLabel", card, "MOUSE SPEED", new Vector2(-140f, -21f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        sensitivityValue = CreateText("SensitivityValue", card, "0.10", new Vector2(205f, -21f),
            new Vector2(62f, 35f), 18, Color.white);
        sensitivitySlider = CreateSlider("MouseSensitivitySlider", card, -21f,
            GameSettingsPreferences.MinMouseSensitivity, GameSettingsPreferences.MaxMouseSensitivity,
            GameSettingsPreferences.MouseSensitivity, value =>
            {
                GameSettingsPreferences.SetMouseSensitivity(value);
                sensitivityValue.text = value.ToString("F2");
            });

        CreateText("FullscreenLabel", card, "FULLSCREEN", new Vector2(-140f, -84f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        var fullscreenButton = CreateButton("FullscreenButton", card, "ON",
            new Vector2(0.5f, 0.5f), new Vector2(95f, -84f),
            new Vector2(190f, 64f), ToggleFullscreen);
        fullscreenValue = fullscreenButton.GetComponentInChildren<Text>();

        CreateText("ResolutionLabel", card, "RESOLUTION", new Vector2(-140f, -150f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        CreateButton("PreviousResolutionButton", card, "<",
            new Vector2(0.5f, 0.5f), new Vector2(10f, -150f),
            new Vector2(55f, 56f), () => CycleResolution(-1));
        resolutionValue = CreateText("ResolutionValue", card, string.Empty,
            new Vector2(99f, -150f), new Vector2(122f, 35f), 20, Color.white);
        CreateButton("NextResolutionButton", card, ">",
            new Vector2(0.5f, 0.5f), new Vector2(192f, -150f),
            new Vector2(55f, 56f), () => CycleResolution(1));

        CreateText("ControlHintsLabel", card, "CONTROL HINTS",
            new Vector2(-140f, -215f), new Vector2(190f, 35f), 20,
            Color.white, TextAnchor.MiddleLeft);
        var toggleRect = CreateRect("ControlHintsToggle", card,
            new Vector2(0.5f, 0.5f), new Vector2(95f, -215f),
            new Vector2(190f, 50f));
        controlHintsToggle = toggleRect.gameObject.AddComponent<Toggle>();
        var box = CreateRect("Checkbox", toggleRect, new Vector2(0.5f, 0.5f),
            new Vector2(-62f, 0f), new Vector2(35f, 35f));
        var boxImage = box.gameObject.AddComponent<Image>();
        boxImage.color = Color.white;
        var mark = CreateRect("Checkmark", box, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(22f, 22f));
        var markImage = mark.gameObject.AddComponent<Image>();
        markImage.color = AccentColor;
        var valueText = CreateText("ControlHintsValue", toggleRect, "ON",
            new Vector2(18f, 0f), new Vector2(100f, 35f), 20, Color.white);
        controlHintsToggle.targetGraphic = boxImage;
        controlHintsToggle.graphic = markImage;
        controlHintsToggle.SetIsOnWithoutNotify(GameSettingsPreferences.ShowControlHints);
        controlHintsToggle.onValueChanged.AddListener(value =>
        {
            GameSettingsPreferences.SetShowControlHints(value);
            GameLocalization.Bind(valueText, value ? "ON" : "OFF");
        });

        Button keySettingsButton = CreateButton("OpenKeyBindingsButton", card, "KEY SETTINGS",
            new Vector2(0.5f, 0.5f), new Vector2(0f, -280f),
            new Vector2(240f, 56f), OpenKeyBindings);
        Text keySettingsLabel = keySettingsButton.GetComponentInChildren<Text>();
        keySettingsLabel.rectTransform.localScale = Vector3.one;
        keySettingsLabel.fontSize = 25;
        CreateButton("SettingsBackButton", card, "BACK",
            new Vector2(0.5f, 0.5f), new Vector2(0f, -345f),
            new Vector2(190f, 64f), Close);
        BuildKeyBindingsPanel(overlayRect);
        overlay.SetActive(false);
    }

    private void BuildKeyBindingsPanel(Transform parent)
    {
        var card = CreateRect("KeyBindingsCard", parent, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(900f, 640f));
        keyBindingsPanel = card.gameObject;
        var background = card.gameObject.AddComponent<Image>();
        background.color = PanelColor;
        background.raycastTarget = true;
        CreateText("KeyBindingsTitle", card, "KEY SETTINGS",
            new Vector2(0f, 265f), new Vector2(820f, 52f), 36, Color.white);
        var divider = CreateRect("KeyBindingsDivider", card,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 220f),
            new Vector2(820f, 4f));
        var dividerImage = divider.gameObject.AddComponent<Image>();
        dividerImage.color = AccentColor;
        dividerImage.raycastTarget = false;

        string[] names =
        {
            "MOVE FORWARD", "MOVE BACK", "MOVE LEFT", "MOVE RIGHT",
            "SPRINT", "JUMP", "PUSH", "GESTURE", "KICK", "RESPAWN", "RELOAD"
        };
        for (int i = 0; i < GameKeyBindings.ActionCount; i++)
        {
            int column = i / 6;
            int row = i % 6;
            float y = 180f - row * 62f;
            float labelX = column == 0 ? -325f : 90f;
            float buttonX = column == 0 ? -110f : 305f;
            PlayerAction action = (PlayerAction)i;
            CreateText(action + "Label", card, names[i],
                new Vector2(labelX, y), new Vector2(190f, 42f), 24,
                Color.white, TextAnchor.MiddleLeft);
            Button button = CreateButton(action + "Binding", card, "",
                new Vector2(0.5f, 0.5f), new Vector2(buttonX, y),
                new Vector2(215f, 55f), () => BeginKeyCapture(action));
            bindingValues[i] = button.GetComponentInChildren<Text>();
            bindingValues[i].rectTransform.localScale = Vector3.one;
            bindingValues[i].fontSize = i >= 6 ? 21 : 24;
            bindingValues[i].resizeTextForBestFit = true;
            bindingValues[i].resizeTextMinSize = 18;
            bindingValues[i].resizeTextMaxSize = i >= 6 ? 21 : 24;
        }
        bindingStatus = CreateText("BindingStatus", card,
            "CLICK A CONTROL TO CHANGE IT", new Vector2(0f, -195f),
            new Vector2(820f, 38f), 24, Color.white);
        Button resetButton = CreateButton("ResetBindings", card, "RESET DEFAULTS",
            new Vector2(0.5f, 0.5f), new Vector2(-155f, -270f),
            new Vector2(270f, 66f), ResetBindings);
        Text resetLabel = resetButton.GetComponentInChildren<Text>();
        resetLabel.rectTransform.localScale = Vector3.one;
        resetLabel.fontSize = 25;
        CreateButton("BackFromBindings", card, "BACK",
            new Vector2(0.5f, 0.5f), new Vector2(155f, -270f),
            new Vector2(200f, 66f), CloseKeyBindings);
        RefreshBindingLabels();
        keyBindingsPanel.SetActive(false);
    }

    private void OpenKeyBindings()
    {
        settingsCard.SetActive(false);
        keyBindingsPanel.SetActive(true);
        keyBindingsPanel.transform.SetAsLastSibling();
        RefreshBindingLabels();
        GameLocalization.Bind(bindingStatus, "CLICK A CONTROL TO CHANGE IT");
    }

    private void CloseKeyBindings()
    {
        waitingForBinding = null;
        keyBindingsPanel.SetActive(false);
        settingsCard.SetActive(true);
    }

    private void BeginKeyCapture(PlayerAction action)
    {
        waitingForBinding = action;
        captureStartFrame = Time.frameCount;
        GameLocalization.Bind(bindingStatus, "PRESS A KEY OR MOUSE BUTTON  /  ESC CANCELS");
        bindingValues[(int)action].text = GameLocalization.T("PRESS KEY");
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void CancelKeyCapture()
    {
        if (!waitingForBinding.HasValue)
            return;
        waitingForBinding = null;
        GameLocalization.Bind(bindingStatus, "KEY CHANGE CANCELLED");
        RefreshBindingLabels();
    }

    private void Update()
    {
        if (!IsOpen || !waitingForBinding.HasValue ||
            Time.frameCount <= captureStartFrame)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame || key.keyCode == Key.Escape)
                    continue;
                CommitKeyCapture(key.keyCode, 0);
                return;
            }
        }
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        if (mouse.leftButton.wasPressedThisFrame)
            CommitKeyCapture(Key.None, 1);
        else if (mouse.rightButton.wasPressedThisFrame)
            CommitKeyCapture(Key.None, 2);
        else if (mouse.middleButton.wasPressedThisFrame)
            CommitKeyCapture(Key.None, 3);
    }

    private void CommitKeyCapture(Key key, int mouseButton)
    {
        PlayerAction action = waitingForBinding.Value;
        if (!GameKeyBindings.TryBind(action, key, mouseButton))
        {
            GameLocalization.Bind(bindingStatus, "THAT KEY IS RESERVED  /  PRESS ANOTHER");
            return;
        }
        waitingForBinding = null;
        RefreshBindingLabels();
        GameLocalization.Bind(bindingStatus, "CONTROL UPDATED");
    }

    private void ResetBindings()
    {
        waitingForBinding = null;
        GameKeyBindings.ResetDefaults();
        RefreshBindingLabels();
        GameLocalization.Bind(bindingStatus, "DEFAULT CONTROLS RESTORED");
    }

    private void RefreshBindingLabels()
    {
        for (int i = 0; i < bindingValues.Length; i++)
            if (bindingValues[i] != null)
                bindingValues[i].text = waitingForBinding == (PlayerAction)i ? GameLocalization.T("PRESS KEY") : GameKeyBindings.GetLabel((PlayerAction)i);
    }

    private void OnLanguageChanged()
    {
        if (overlay == null) return;
        RefreshValues();
        RefreshBindingLabels();
    }

    private void RefreshValues()
    {
        if (languageValue != null) languageValue.text = GameLocalization.Korean ? "한국어" : "English";
        volumeSlider.SetValueWithoutNotify(GameSettingsPreferences.Volume);
        bgmVolumeSlider.SetValueWithoutNotify(GameSettingsPreferences.BgmVolume);
        sfxVolumeSlider.SetValueWithoutNotify(GameSettingsPreferences.SfxVolume);
        sensitivitySlider.SetValueWithoutNotify(GameSettingsPreferences.MouseSensitivity);
        volumeValue.text = Mathf.RoundToInt(GameSettingsPreferences.Volume * 100f) + "%";
        bgmVolumeValue.text = Mathf.RoundToInt(GameSettingsPreferences.BgmVolume * 100f) + "%";
        sfxVolumeValue.text = Mathf.RoundToInt(GameSettingsPreferences.SfxVolume * 100f) + "%";
        sensitivityValue.text = GameSettingsPreferences.MouseSensitivity.ToString("F2");
        GameLocalization.Bind(fullscreenValue, GameSettingsPreferences.Fullscreen ? "ON" : "OFF");
        controlHintsToggle.SetIsOnWithoutNotify(GameSettingsPreferences.ShowControlHints);
        GameLocalization.Bind(controlHintsToggle.GetComponentInChildren<Text>(),
            GameSettingsPreferences.ShowControlHints ? "ON" : "OFF");
        RefreshResolutionOptions();
        resolutionValue.text = GameSettingsPreferences.ResolutionWidth + "x" +
            GameSettingsPreferences.ResolutionHeight;
    }

    private void ToggleFullscreen()
    {
        GameSettingsPreferences.SetFullscreen(!GameSettingsPreferences.Fullscreen);
        fullscreenValue.text = GameSettingsPreferences.Fullscreen ? "ON" : "OFF";
    }

    private void RefreshResolutionOptions()
    {
        availableResolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            if (resolution.width < 800 || resolution.height < 600)
                continue;

            var size = new Vector2Int(resolution.width, resolution.height);
            if (!availableResolutions.Contains(size))
                availableResolutions.Add(size);
        }

        var selected = new Vector2Int(GameSettingsPreferences.ResolutionWidth,
            GameSettingsPreferences.ResolutionHeight);
        if (!availableResolutions.Contains(selected))
            availableResolutions.Add(selected);

        availableResolutions.Sort((a, b) =>
        {
            int areaOrder = ((long)a.x * a.y).CompareTo((long)b.x * b.y);
            return areaOrder != 0 ? areaOrder : a.x.CompareTo(b.x);
        });
    }

    private void CycleResolution(int direction)
    {
        RefreshResolutionOptions();
        if (availableResolutions.Count <= 1)
            return;

        var selected = new Vector2Int(GameSettingsPreferences.ResolutionWidth,
            GameSettingsPreferences.ResolutionHeight);
        int index = availableResolutions.IndexOf(selected);
        index = (index + direction + availableResolutions.Count) % availableResolutions.Count;
        Vector2Int next = availableResolutions[index];
        GameSettingsPreferences.SetResolution(next.x, next.y);
        resolutionValue.text = next.x + "x" + next.y;
    }

    private Slider CreateSlider(string name, Transform parent, float y, float minimum,
        float maximum, float value, Action<float> onChanged)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f),
            new Vector2(68f, y), new Vector2(205f, 28f));
        var slider = rect.gameObject.AddComponent<Slider>();
        var track = CreateRect("Track", rect, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(205f, 11f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0.25f, 0.27f, 0.31f, 1f);
        trackImage.raycastTarget = true;
        var fillArea = CreateRect("FillArea", rect, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(205f, 11f));
        var fill = CreateRect("Fill", fillArea, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(205f, 11f));
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = AccentColor;
        fillImage.raycastTarget = false;
        var handleArea = CreateRect("HandleArea", rect, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(205f, 28f));
        var handle = CreateRect("Handle", handleArea, new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(22f, 28f));
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(changed => onChanged(changed));
        return slider;
    }

    private Button CreateButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        var rect = CreateRect(name, parent, anchor, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        if (buttonTemplateImage != null)
        {
            image.sprite = buttonTemplateImage.sprite;
            image.type = buttonTemplateImage.type;
            image.color = buttonTemplateImage.color;
            image.material = buttonTemplateImage.material;
            image.preserveAspect = buttonTemplateImage.preserveAspect;
        }
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (buttonTemplate != null)
        {
            button.transition = buttonTemplate.transition;
            button.colors = buttonTemplate.colors;
            button.spriteState = buttonTemplate.spriteState;
        }
        button.onClick.AddListener(click);
        var labelText = CreateText(name + "Text", rect, label, Vector2.zero, size,
            buttonTemplateText != null ? buttonTemplateText.fontSize : 35,
            buttonTemplateText != null ? buttonTemplateText.color : Color.black);
        if (buttonTemplateText != null)
        {
            labelText.font = buttonTemplateText.font;
            labelText.fontStyle = buttonTemplateText.fontStyle;
            labelText.horizontalOverflow = buttonTemplateText.horizontalOverflow;
            labelText.verticalOverflow = buttonTemplateText.verticalOverflow;
            labelText.rectTransform.localScale = buttonTemplateText.rectTransform.localScale;
        }
        GameLocalization.BindKnown(labelText, label);
        return button;
    }

    private Text CreateText(string name, Transform parent, string content,
        Vector2 position, Vector2 size, int fontSize, Color color,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), position, size);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Normal;
        text.text = content;
        GameLocalization.BindKnown(text, content);
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor,
        Vector2 position, Vector2 size)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        child.layer = 5;
        child.transform.SetParent(parent, false);
        var rect = (RectTransform)child.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
