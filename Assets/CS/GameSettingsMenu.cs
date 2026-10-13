using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Settings overlay shared by the main menu and the pause screen.</summary>
public class GameSettingsMenu : MonoBehaviour
{
    public static GameSettingsMenu Instance { get; private set; }
    public bool IsOpen => overlay != null && overlay.activeSelf;

    private GameObject mainMenuPanel;
    private GameObject pausePanel;
    private GameObject overlay;
    private Font font;
    private Button buttonTemplate;
    private Image buttonTemplateImage;
    private Text buttonTemplateText;
    private Slider volumeSlider;
    private Slider sensitivitySlider;
    private Text volumeValue;
    private Text sensitivityValue;
    private Text fullscreenValue;
    private bool openedFromPause;

    private static readonly Color PanelColor = new(0.12f, 0.13f, 0.16f, 0.98f);
    private static readonly Color AccentColor = new(1f, 0.82f, 0.2f, 1f);

    private void Awake()
    {
        Instance = this;
        GameSettingsPreferences.Load();
    }

    private void OnDestroy()
    {
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
        if (pausePanel != null)
            CreateButton("OpenSettingsFromPause", pausePanel.transform,
                "SETTINGS", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(200f, 100f), OpenFromPause);

        BuildOverlay();
    }

    public void OpenFromMainMenu()
    {
        openedFromPause = false;
        mainMenuPanel.SetActive(false);
        Open();
    }

    public void OpenFromPause()
    {
        openedFromPause = true;
        pausePanel.SetActive(false);
        Open();
    }

    private void Open()
    {
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

        overlay.SetActive(false);
        if (openedFromPause && pausePanel != null)
            pausePanel.SetActive(true);
        else if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
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
            Vector2.zero, new Vector2(500f, 420f));
        var cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = PanelColor;
        cardImage.raycastTarget = false;

        CreateText("SettingsTitle", card, "SETTINGS", new Vector2(0f, 158f),
            new Vector2(430f, 52f), 38, Color.white);
        var divider = CreateRect("TitleDivider", card, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 115f), new Vector2(410f, 4f));
        divider.gameObject.AddComponent<Image>().color = AccentColor;
        divider.GetComponent<Image>().raycastTarget = false;

        CreateText("VolumeLabel", card, "MASTER VOLUME", new Vector2(-140f, 67f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        volumeValue = CreateText("VolumeValue", card, "100%", new Vector2(205f, 67f),
            new Vector2(62f, 35f), 18, Color.white);
        volumeSlider = CreateSlider("MasterVolumeSlider", card, 67f, 0f, 1f,
            GameSettingsPreferences.Volume, value =>
            {
                GameSettingsPreferences.SetVolume(value);
                volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
            });

        CreateText("SensitivityLabel", card, "MOUSE SPEED", new Vector2(-140f, -4f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        sensitivityValue = CreateText("SensitivityValue", card, "0.10", new Vector2(205f, -4f),
            new Vector2(62f, 35f), 18, Color.white);
        sensitivitySlider = CreateSlider("MouseSensitivitySlider", card, -4f, 0.04f, 0.3f,
            GameSettingsPreferences.MouseSensitivity, value =>
            {
                GameSettingsPreferences.SetMouseSensitivity(value);
                sensitivityValue.text = value.ToString("F2");
            });

        CreateText("FullscreenLabel", card, "FULLSCREEN", new Vector2(-140f, -77f),
            new Vector2(190f, 35f), 20, Color.white, TextAnchor.MiddleLeft);
        var fullscreenButton = CreateButton("FullscreenButton", card, "ON",
            new Vector2(0.5f, 0.5f), new Vector2(95f, -77f),
            new Vector2(190f, 64f), ToggleFullscreen);
        fullscreenValue = fullscreenButton.GetComponentInChildren<Text>();

        CreateButton("SettingsBackButton", card, "BACK",
            new Vector2(0.5f, 0.5f), new Vector2(0f, -160f),
            new Vector2(190f, 64f), Close);
        overlay.SetActive(false);
    }

    private void RefreshValues()
    {
        volumeSlider.SetValueWithoutNotify(GameSettingsPreferences.Volume);
        sensitivitySlider.SetValueWithoutNotify(GameSettingsPreferences.MouseSensitivity);
        volumeValue.text = Mathf.RoundToInt(GameSettingsPreferences.Volume * 100f) + "%";
        sensitivityValue.text = GameSettingsPreferences.MouseSensitivity.ToString("F2");
        fullscreenValue.text = GameSettingsPreferences.Fullscreen ? "ON" : "OFF";
    }

    private void ToggleFullscreen()
    {
        GameSettingsPreferences.SetFullscreen(!GameSettingsPreferences.Fullscreen);
        fullscreenValue.text = GameSettingsPreferences.Fullscreen ? "ON" : "OFF";
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
