using UnityEngine;

/// <summary>Local settings shared by the menu and the player camera.</summary>
public static class GameSettingsPreferences
{
    public const float MinMouseSensitivity = 0.02f;
    public const float MaxMouseSensitivity = 0.6f;
    private const string VolumeKey = "MasterVolume";
    private const string BgmVolumeKey = "BgmVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private const string SensitivityKey = "MouseSensitivity";
    private const string FullscreenKey = "Fullscreen";
    private const string ResolutionWidthKey = "ResolutionWidth";
    private const string ResolutionHeightKey = "ResolutionHeight";
    private const string ControlHintsKey = "ShowControlHints";
    private static bool loaded;
    private static float volume;
    private static float bgmVolume;
    private static float sfxVolume;
    private static float sensitivity;
    private static bool fullscreen;
    private static int resolutionWidth;
    private static int resolutionHeight;
    private static bool showControlHints;

    public static float Volume { get { Load(); return volume; } }
    public static float BgmVolume { get { Load(); return bgmVolume; } }
    public static float SfxVolume { get { Load(); return sfxVolume; } }
    public static float MouseSensitivity { get { Load(); return sensitivity; } }
    public static bool Fullscreen { get { Load(); return fullscreen; } }
    public static int ResolutionWidth { get { Load(); return resolutionWidth; } }
    public static int ResolutionHeight { get { Load(); return resolutionHeight; } }
    public static bool ShowControlHints { get { Load(); return showControlHints; } }

    public static void Load()
    {
        if (!loaded)
        {
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.1f),
                MinMouseSensitivity, MaxMouseSensitivity);
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            Resolution nativeResolution = Screen.currentResolution;
            resolutionWidth = PlayerPrefs.GetInt(ResolutionWidthKey,
                nativeResolution.width > 0 ? nativeResolution.width : Screen.width);
            resolutionHeight = PlayerPrefs.GetInt(ResolutionHeightKey,
                nativeResolution.height > 0 ? nativeResolution.height : Screen.height);
            resolutionWidth = Mathf.Max(1, resolutionWidth);
            resolutionHeight = Mathf.Max(1, resolutionHeight);
            showControlHints = PlayerPrefs.GetInt(ControlHintsKey, 1) != 0;
            loaded = true;
            AudioListener.volume = volume;
            if (PlayerPrefs.HasKey(FullscreenKey) ||
                (PlayerPrefs.HasKey(ResolutionWidthKey) && PlayerPrefs.HasKey(ResolutionHeightKey)))
                ApplyDisplay();
        }
    }

    public static void SetVolume(float value)
    {
        Load();
        volume = Mathf.Clamp01(value);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
        PlayerPrefs.Save();
    }

    public static void SetBgmVolume(float value)
    {
        Load();
        bgmVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
        PlayerPrefs.Save();
    }

    public static void SetSfxVolume(float value)
    {
        Load();
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.Save();
    }

    public static void SetMouseSensitivity(float value)
    {
        Load();
        sensitivity = Mathf.Clamp(value, MinMouseSensitivity, MaxMouseSensitivity);
        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
    }

    public static void SetFullscreen(bool value)
    {
        Load();
        fullscreen = value;
        ApplyDisplay();
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetResolution(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        Load();
        resolutionWidth = width;
        resolutionHeight = height;
        ApplyDisplay();
        PlayerPrefs.SetInt(ResolutionWidthKey, width);
        PlayerPrefs.SetInt(ResolutionHeightKey, height);
        PlayerPrefs.Save();
    }

    public static void SetShowControlHints(bool value)
    {
        Load();
        showControlHints = value;
        PlayerPrefs.SetInt(ControlHintsKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void ApplyDisplay()
    {
        Screen.SetResolution(resolutionWidth, resolutionHeight,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
}
