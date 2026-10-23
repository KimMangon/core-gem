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
    public int health = 1;
}

[Serializable]
public sealed class SavedMonsterState
{
    public int entryIndex;
    public int variant;
    public int health;
    public Vector3 position;
    public Quaternion rotation;
}

[Serializable]
public sealed class SavedMonsterEncounter
{
    public string encounterId;
    public int[] spawnedCounts;
    public SavedMonsterState[] monsters;
}

[Serializable]
public sealed class SavedRoom
{
    public string recordTrust = "local-unverified";
    public int version = 3;
    public string id;
    public string savedUtc;
    public int stage;
    public double elapsedSeconds;
    public double obstacleTime;
    public SavedBodyState boulder;
    public int boulderHealth = 5;
    public SavedMonsterEncounter[] monsterEncounters;
    public SavedPlayerState[] players;
    public double nextCannonShotIn;
    public SavedProjectileState[] projectiles;
    public SavedCannonControl[] cannonControls;
    public double[] stageClearSeconds;
    public double currentStageElapsedSeconds;
}

[Serializable]
public sealed class SavedCannonControl
{
    public Vector3 position;
    public Quaternion rotation;
    public bool hiddenMount;
    public int health;
}

/// <summary>Save slots belong to the host installation that created them.</summary>
public static class SavedRoomStore
{
    private static string DirectoryPath => Path.Combine(Application.persistentDataPath, "SavedRooms");

    public static bool Save(SavedRoom room)
    {
        if (room == null || room.players == null || room.players.Length == 0 ||
            room.boulder == null || room.stage < 1 || room.stage > 8)
            return false;

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            if (string.IsNullOrEmpty(room.id))
                room.id = Guid.NewGuid().ToString("N");
            if (!Valid(room)) return false;
            room.recordTrust = "local-unverified";
            room.savedUtc = DateTime.UtcNow.ToString("o");
            string destination = Path.Combine(DirectoryPath, room.id + ".json");
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(room, true));
            if (File.Exists(destination) && ReadValid(destination, room.id) != null)
                File.Replace(temporary, destination, destination + ".bak");
            else
            {
                // Preserve the last healthy backup when the primary was damaged.
                if (File.Exists(destination)) File.Move(destination, destination + ".corrupt-" + DateTime.UtcNow.Ticks);
                File.Move(temporary, destination);
            }
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
        var ids = new HashSet<string>();
        foreach (string file in Directory.GetFiles(DirectoryPath, "*.json*"))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".json.bak")) name = name.Substring(0, name.Length - 9);
            else if (name.EndsWith(".json")) name = name.Substring(0, name.Length - 5);
            else continue;
            if (!Guid.TryParseExact(name, "N", out _) || !ids.Add(name)) continue;
            string path = Path.Combine(DirectoryPath, name + ".json");
            try
            {
                SavedRoom room = ReadValid(path, name) ?? ReadValid(path + ".bak", name);
                if (room != null && (room.version == 1 || room.version == 2 || room.version == 3) &&
                    room.stage >= 1 && room.stage <= 8 &&
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

    private static SavedRoom ReadValid(string path, string id)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return null;
            SavedRoom room = JsonUtility.FromJson<SavedRoom>(File.ReadAllText(path));
            return Valid(room) && room.id == id ? room : null;
        }
        catch (Exception) { return null; }
    }

    private static bool PositionValid(Vector3 p) => NetworkInputGuard.Finite(p) &&
        Mathf.Abs(p.x) < 100000f && Mathf.Abs(p.y) < 100000f && Mathf.Abs(p.z) < 100000f;
    private static bool TimeValid(double t) => NetworkInputGuard.Finite(t) && t >= 0 && t < 315360000d;
    private static bool BodyValid(SavedBodyState b) => b != null && PositionValid(b.position) &&
        NetworkInputGuard.ValidRotation(b.rotation) && NetworkInputGuard.Finite(b.velocity) &&
        b.velocity.sqrMagnitude < 1000000f && NetworkInputGuard.Finite(b.angularVelocity) &&
        b.angularVelocity.sqrMagnitude < 1000000f;
    private static bool Valid(SavedRoom r)
    {
        if (r == null || !Guid.TryParseExact(r.id, "N", out _) || r.version < 1 || r.version > 3 ||
            r.stage < 1 || r.stage > 8 || !BodyValid(r.boulder) || r.boulderHealth < 0 || r.boulderHealth > 5 ||
            !TimeValid(r.elapsedSeconds) || !TimeValid(r.obstacleTime) || !TimeValid(r.currentStageElapsedSeconds) ||
            !TimeValid(r.nextCannonShotIn) || r.players == null || r.players.Length < 1 || r.players.Length > 4) return false;
        var ids = new HashSet<string>();
        foreach (var p in r.players)
            if (p == null || string.IsNullOrEmpty(p.playerId) || p.playerId.Length > 64 || !ids.Add(p.playerId) ||
                !BodyValid(p.body) || !NetworkInputGuard.Finite(p.lookPitch) || Mathf.Abs(p.lookPitch) > 90f) return false;
        if (r.stageClearSeconds != null)
        {
            if (r.stageClearSeconds.Length != 8) return false;
            foreach (double t in r.stageClearSeconds) if (t != -1d && !TimeValid(t)) return false;
        }
        if (r.projectiles != null)
        {
            if (r.projectiles.Length > 512) return false;
            foreach (var p in r.projectiles)
                if (p == null || !BodyValid(p.body) || !TimeValid(p.remainingLifetime) || p.health < 0 || p.health > 2) return false;
        }
        if (r.cannonControls != null)
        {
            if (r.cannonControls.Length > 3) return false;
            foreach (var c in r.cannonControls)
                if (c == null || !PositionValid(c.position) || !NetworkInputGuard.ValidRotation(c.rotation) || c.health < 0 || c.health > 3) return false;
        }
        if (r.monsterEncounters != null)
        {
            if (r.monsterEncounters.Length > 128) return false;
            foreach (var e in r.monsterEncounters)
            {
                if (e == null || string.IsNullOrEmpty(e.encounterId) || e.encounterId.Length > 256 ||
                    e.spawnedCounts == null || e.spawnedCounts.Length > 64 || e.monsters == null || e.monsters.Length > 256) return false;
                foreach (int n in e.spawnedCounts) if (n < 0 || n > 256) return false;
                foreach (var m in e.monsters)
                    if (m == null || m.entryIndex < 0 || m.entryIndex >= e.spawnedCounts.Length || m.variant < 0 ||
                        m.variant > 64 || m.health < 0 || m.health > 1000 || !PositionValid(m.position) ||
                        !NetworkInputGuard.ValidRotation(m.rotation)) return false;
            }
        }
        return true;
    }

    public static bool Delete(string roomId)
    {
        if (!Guid.TryParseExact(roomId, "N", out _))
            return false;
        try
        {
            string path = Path.Combine(DirectoryPath, roomId + ".json");
            if (!File.Exists(path) && !File.Exists(path + ".bak"))
                return false;
            File.Delete(path);
            File.Delete(path + ".bak");
            File.Delete(path + ".tmp");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }
}
