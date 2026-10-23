using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerAction
{
    MoveForward,
    MoveBackward,
    MoveLeft,
    MoveRight,
    Sprint,
    Jump,
    Push,
    Gesture,
    Kick,
    Respawn,
    Reload
}

/// <summary>Local, persistent controls shared by the player and settings UI.</summary>
public static class GameKeyBindings
{
    public const int ActionCount = 11;
    private const string PreferencePrefix = "PlayerBinding_";
    private const string RespawnBindingMigrationKey = "RespawnBindingGMigration";
    private static readonly Key[] DefaultKeys =
    {
        Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.Space,
        Key.None, Key.None, Key.F, Key.G, Key.R
    };
    private static readonly int[] DefaultMouseButtons = { 0, 0, 0, 0, 0, 0, 1, 2, 0, 0, 0 };
    private static readonly Key[] keys = new Key[ActionCount];
    private static readonly int[] mouseButtons = new int[ActionCount];
    private static bool loaded;

    public static bool IsHeld(PlayerAction action)
    {
        Load();
        int index = (int)action;
        if (mouseButtons[index] != 0)
            return GetMouseButton(mouseButtons[index])?.isPressed == true;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;
        if (action == PlayerAction.Sprint && keys[index] == Key.LeftShift)
        {
            bool rightShiftAssigned = false;
            for (int i = 0; i < ActionCount; i++)
                if (i != index && mouseButtons[i] == 0 && keys[i] == Key.RightShift)
                    rightShiftAssigned = true;
            return keyboard.leftShiftKey.isPressed ||
                (!rightShiftAssigned && keyboard.rightShiftKey.isPressed);
        }
        return keyboard[keys[index]].isPressed;
    }

    public static bool WasPressedThisFrame(PlayerAction action)
    {
        Load();
        int index = (int)action;
        if (mouseButtons[index] != 0)
            return GetMouseButton(mouseButtons[index])?.wasPressedThisFrame == true;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[keys[index]].wasPressedThisFrame;
    }

    public static string GetLabel(PlayerAction action)
    {
        Load();
        int index = (int)action;
        if (mouseButtons[index] == 1) return GameLocalization.T("MOUSE LEFT");
        if (mouseButtons[index] == 2) return GameLocalization.T("MOUSE RIGHT");
        if (mouseButtons[index] == 3) return GameLocalization.T("MOUSE MIDDLE");
        return keys[index] switch
        {
            Key.LeftShift => GameLocalization.T("SHIFT"),
            Key.RightShift => GameLocalization.T("RIGHT SHIFT"),
            Key.Space => GameLocalization.T("SPACE"),
            Key.UpArrow => GameLocalization.T("UP"),
            Key.DownArrow => GameLocalization.T("DOWN"),
            Key.LeftArrow => GameLocalization.T("LEFT"),
            Key.RightArrow => GameLocalization.T("RIGHT"),
            _ => keys[index].ToString().ToUpperInvariant()
        };
    }

    public static bool TryBind(PlayerAction action, Key key, int mouseButton)
    {
        Load();
        if (mouseButton == 0 && (key == Key.None || key == Key.Escape ||
            key == Key.LeftAlt || key == Key.RightAlt))
            return false;
        if (mouseButton < 0 || mouseButton > 3)
            return false;
        int index = (int)action;
        for (int i = 0; i < ActionCount; i++)
        {
            if (i == index || keys[i] != key || mouseButtons[i] != mouseButton)
                continue;
            keys[i] = keys[index];
            mouseButtons[i] = mouseButtons[index];
            Save((PlayerAction)i);
            break;
        }
        keys[index] = key;
        mouseButtons[index] = mouseButton;
        Save(action);
        PlayerPrefs.Save();
        return true;
    }

    public static void ResetDefaults()
    {
        loaded = true;
        for (int i = 0; i < ActionCount; i++)
        {
            keys[i] = DefaultKeys[i];
            mouseButtons[i] = DefaultMouseButtons[i];
            Save((PlayerAction)i);
        }
        PlayerPrefs.Save();
    }

    private static void Load()
    {
        if (loaded)
            return;
        loaded = true;
        for (int i = 0; i < ActionCount; i++)
        {
            keys[i] = DefaultKeys[i];
            mouseButtons[i] = DefaultMouseButtons[i];
            string saved = PlayerPrefs.GetString(PreferencePrefix + (PlayerAction)i, "");
            if (saved.StartsWith("M:") && int.TryParse(saved.Substring(2), out int button) &&
                button >= 1 && button <= 3)
            {
                keys[i] = Key.None;
                mouseButtons[i] = button;
            }
            else if (saved.StartsWith("K:") &&
                Enum.TryParse(saved.Substring(2), out Key key) &&
                Enum.IsDefined(typeof(Key), key) &&
                key != Key.None && key != Key.Escape &&
                key != Key.LeftAlt && key != Key.RightAlt)
            {
                keys[i] = key;
                mouseButtons[i] = 0;
            }
        }
        // Existing players may have the old default R saved as their respawn key.
        if (PlayerPrefs.GetInt(RespawnBindingMigrationKey, 0) == 0)
        {
            int respawn = (int)PlayerAction.Respawn;
            if (keys[respawn] == Key.R && mouseButtons[respawn] == 0)
            {
                keys[respawn] = Key.G;
                Save(PlayerAction.Respawn);
            }
            PlayerPrefs.SetInt(RespawnBindingMigrationKey, 1);
            PlayerPrefs.Save();
        }
    }

    private static void Save(PlayerAction action)
    {
        int index = (int)action;
        string value = mouseButtons[index] != 0
            ? "M:" + mouseButtons[index] : "K:" + keys[index];
        PlayerPrefs.SetString(PreferencePrefix + action, value);
    }

    private static UnityEngine.InputSystem.Controls.ButtonControl GetMouseButton(int index)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return null;
        return index switch
        {
            1 => mouse.leftButton,
            2 => mouse.rightButton,
            3 => mouse.middleButton,
            _ => null
        };
    }
}
