using UnityEngine;

/// <summary>Colors available in the main menu and shared by player avatars.</summary>
public static class PlayerColorPalette
{
    private const string PreferenceKey = "PlayerAvatarColor";

    public static readonly string[] Names =
    {
        "BLUE", "RED", "ORANGE", "YELLOW", "GREEN", "CYAN", "PURPLE", "PINK"
    };

    public static readonly Color32[] Colors =
    {
        new Color32(82, 135, 232, 255),
        new Color32(222, 75, 77, 255),
        new Color32(235, 137, 59, 255),
        new Color32(235, 205, 78, 255),
        new Color32(87, 186, 112, 255),
        new Color32(74, 195, 207, 255),
        new Color32(151, 105, 210, 255),
        new Color32(226, 128, 175, 255)
    };

    public static int SelectedIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, 0), 0, Colors.Length - 1);
        set
        {
            PlayerPrefs.SetInt(PreferenceKey, Mathf.Clamp(value, 0, Colors.Length - 1));
            PlayerPrefs.Save();
        }
    }

    public static uint Pack(Color32 color) =>
        (uint)color.r | ((uint)color.g << 8) | ((uint)color.b << 16) | ((uint)color.a << 24);

    public static Color32 Unpack(uint packed) => new Color32(
        (byte)packed, (byte)(packed >> 8), (byte)(packed >> 16), (byte)(packed >> 24));
}
