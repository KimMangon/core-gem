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
        string name = (value ?? string.Empty)
            .Replace("<u>", string.Empty)
            .Replace("</u>", string.Empty)
            .Trim();
        if (name.Length > 14)
            name = name.Substring(0, 14);
        return string.IsNullOrWhiteSpace(name) ? "PLAYER" : name;
    }
}
