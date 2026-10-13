using UnityEngine;

/// <summary>Local settings shared by the menu and the player camera.</summary>
public static class GameSettingsPreferences
{
    private const string VolumeKey = "MasterVolume";
    private const string SensitivityKey = "MouseSensitivity";
    private const string FullscreenKey = "Fullscreen";
    private static bool loaded;
    private static float volume;
    private static float sensitivity;
    private static bool fullscreen;

    public static float Volume { get { Load(); return volume; } }
    public static float MouseSensitivity { get { Load(); return sensitivity; } }
    public static bool Fullscreen { get { Load(); return fullscreen; } }

    public static void Load()
    {
        if (!loaded)
        {
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 0.1f), 0.04f, 0.3f);
            fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            loaded = true;
            AudioListener.volume = volume;
            if (PlayerPrefs.HasKey(FullscreenKey))
                Screen.fullScreen = fullscreen;
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

    public static void SetMouseSensitivity(float value)
    {
        Load();
        sensitivity = Mathf.Clamp(value, 0.04f, 0.3f);
        PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
        PlayerPrefs.Save();
    }

    public static void SetFullscreen(bool value)
    {
        Load();
        fullscreen = value;
        Screen.fullScreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
