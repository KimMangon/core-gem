using UnityEngine;
using Unity.Services.Authentication;

/// <summary>Local name chosen before joining a room.</summary>
public static class PlayerProfile
{
    private const string NameKey = "PlayerDisplayName";
    private const string IdKey = "PlayerPersistentId";

    public static string Id
    {
        get
        {
            if (AuthenticationService.Instance.IsSignedIn &&
                !string.IsNullOrEmpty(AuthenticationService.Instance.PlayerId))
                return AuthenticationService.Instance.PlayerId;
            string id = PlayerPrefs.GetString(IdKey, string.Empty);
            if (!string.IsNullOrEmpty(id))
                return id;
            id = System.Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(IdKey, id);
            PlayerPrefs.Save();
            return id;
        }
    }

    public static string Name
    {
        get => Normalize(PlayerPrefs.GetString(NameKey, "PLAYER"));
        set
        {
            PlayerPrefs.SetString(NameKey, Normalize(value));
            PlayerPrefs.Save();
        }
    }

    public static string Normalize(string value)
    {
        var clean = new System.Text.StringBuilder(14);
        foreach (char c in value ?? string.Empty) {
            if (char.IsControl(c) || char.IsSurrogate(c) || c == '<' || c == '>' ||
                char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format) continue;
            if (clean.Length >= 14) break;
            clean.Append(c);
        }
        string name = clean.ToString().Trim();
        return string.IsNullOrWhiteSpace(name) ? "PLAYER" : name;
    }
}
