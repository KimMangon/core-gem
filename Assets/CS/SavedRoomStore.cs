using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class SavedBodyState
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
}

[Serializable]
public sealed class SavedPlayerState
{
    public string playerId;
    public string displayName;
    public uint color;
    public float lookPitch;
    public SavedBodyState body;
}

[Serializable]
public sealed class SavedProjectileState
{
    public SavedBodyState body;
    public double remainingLifetime;
}

[Serializable]
public sealed class SavedRoom
{
    public int version = 3;
    public string id;
    public string savedUtc;
    public int stage;
    public double elapsedSeconds;
    public double obstacleTime;
    public SavedBodyState boulder;
    public SavedPlayerState[] players;
    public double nextCannonShotIn;
    public SavedProjectileState[] projectiles;
}

/// <summary>Save slots belong to the host installation that created them.</summary>
public static class SavedRoomStore
{
    private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "SavedRooms");

    public static bool Save(SavedRoom room)
    {
        if (room == null || room.players == null || room.players.Length == 0 ||
            room.boulder == null || room.stage < 1 || room.stage > 7)
            return false;

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (string.IsNullOrEmpty(room.id))
                room.id = Guid.NewGuid().ToString("N");
            room.savedUtc = DateTime.UtcNow.ToString("o");
            string destination = Path.Combine(DirectoryPath, room.id + ".json");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(room, true));
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public static SavedRoom[] LoadAll()
    {
        var rooms = new List<SavedRoom>();
        if (!Directory.Exists(DirectoryPath))
            return rooms.ToArray();
        foreach (string path in Directory.GetFiles(DirectoryPath, "*.json"))
        {
            try
            {
                SavedRoom room = JsonUtility.FromJson<SavedRoom>(File.ReadAllText(path));
                if (room != null && (room.version == 1 || room.version == 2 || room.version == 3) &&
                    room.stage >= 1 && room.stage <= 7 &&
                    room.boulder != null && room.players != null && room.players.Length > 0 &&
                    Array.TrueForAll(room.players, player => player != null &&
                        player.body != null && !string.IsNullOrEmpty(player.playerId)) &&
                    Path.GetFileNameWithoutExtension(path) == room.id)
                    rooms.Add(room);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read saved room {path}: {exception.Message}");
            }
        }
        rooms.Sort((a, b) => string.CompareOrdinal(b.savedUtc, a.savedUtc));
        return rooms.ToArray();
    }

    public static bool Delete(string roomId)
    {
        if (!Guid.TryParseExact(roomId, "N", out _))
            return false;
        try
        {
            string path = Path.Combine(DirectoryPath, roomId + ".json");
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }
}
