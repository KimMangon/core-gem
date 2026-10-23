using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StageManager : NetworkBehaviour
{
    public static StageManager Instance { get; private set; }

    [SerializeField] private GameObject stageOneTrack;
    [SerializeField] private GameObject stageOneGround;
    [SerializeField] private GameObject stageOneLight;
    [SerializeField] private GameObject fullCourseSections;
    [SerializeField] private FallResetZone fallResetZone;
    [SerializeField] private string tutorialSceneName = "Tutorial";

    private readonly NetworkVariable<int> currentStage = new(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> gameStarted = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> playTutorial = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> roomStartedAt = new(
        -1d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> roomFinishedAt = new(
        -1d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> savedElapsedSeconds = new(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> clearPlayerCount = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> obstacleTimeOffset = new(
        0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool loadingTutorial;
    private bool loadingCourse;
    private bool courseReady;
    private bool courseCleared;
    private SavedRoom pendingResume;
    private string activeSaveId;
    private TutorialCourse tutorialCourse;
    private float nextAutomaticSaveAt;
    private const float AutomaticSaveInterval = 3f;
    private NetworkList<double> completedStageSeconds;
    private readonly NetworkVariable<double> sectionStartedElapsed = new(0d);
    public double CurrentStageElapsedSeconds => IsTutorialStage ? 0d :
        System.Math.Max(0d, RoomElapsedSeconds-sectionStartedElapsed.Value);
    public double GetStageClearSeconds(int section) => section>=1 && section<=completedStageSeconds.Count
        ? completedStageSeconds[section-1] : -1d;
    public int RecordedStageCount
    {
        get { int count=0;foreach(double time in completedStageSeconds)if(time>=0d)count++;return count; }
    }
    public double AverageStagePoints
    {
        get
        {
            int count=0,total=0;
            foreach(double time in completedStageSeconds)if(time>=0d){count++;total+=ClearGradeRules.Points(time);}
            return count>0?total/(double)count:0d;
        }
    }
    private void ResetStageTimes()
    {
        completedStageSeconds.Clear();
        for(int i=0;i<8;i++)completedStageSeconds.Add(-1d);
        sectionStartedElapsed.Value=0d;
    }
    private void RecordStageTime(int section)
    {
        if(section<1||section>8||completedStageSeconds.Count<8)return;
        if(completedStageSeconds[section-1]<0d)completedStageSeconds[section-1]=CurrentStageElapsedSeconds;
        sectionStartedElapsed.Value=RoomElapsedSeconds;
    }
    private double[] CaptureStageTimes()
    {
        double[] result=new double[completedStageSeconds.Count];
        for(int i=0;i<result.Length;i++)result[i]=completedStageSeconds[i];
        return result;
    }

    // Offsets applied to the authored scenes when the first six sections were joined.
    private static readonly Vector3[] SectionOffsets =
    {
        Vector3.zero, Vector3.zero,
        new(40f, 21.833215f, 42.671013f),
        new(145f, 66.009238f, 54.671013f),
        new(62.97f, 21.833215f, 182.671013f),
        new(-103.03f, 21.323215f, 195.691013f),
        new(-220.03f, 60.451741f, 354.691013f),
        Vector3.zero, Vector3.zero
    };

    public static Vector3 GetSectionOffset(int section) =>
        section >= 1 && section <= 8 ? SectionOffsets[section] : Vector3.zero;

    private static readonly Vector3[] CheckpointBoulderPositions =
    {
        Vector3.zero,
        new(0f, 5f, 4f),
        new(24f, 21f, 36.6f),
        new(20f, 65.5f, 32.671013f),
        new(38f, 65.5f, 162.671013f),
        new(46.97f, 23f, 190.691013f),
        new(49.97f, 62.5f, 335.691013f),
        new(49.97f, 71f, 456.5f),
        new(63.97f, 81.5f, 658f)
    };

    private static readonly Vector3[] CheckpointPlayerPositions =
    {
        Vector3.zero,
        new(0f, 5f, -2f),
        new(24f, 20f, 33.6f),
        new(20f, 64.5f, 29.671013f),
        new(38f, 64.5f, 159.671013f),
        new(46.97f, 21.7f, 187.691013f),
        new(49.97f, 61.5f, 332.691013f),
        new(49.97f, 69.4f, 452.5f),
        new(63.97f, 80f, 655f)
    };

    private static readonly Vector3[] SectionBoulderStarts =
    {
        Vector3.zero,
        new(0f, 5f, 4f),
        new(24f, 21f, 46.671013f),
        new(20f, 65.5f, 42.671013f),
        new(38f, 65.5f, 172.671013f),
        new(46.97f, 23f, 193.691013f),
        new(49.97f, 62.5f, 341.691013f),
        new(49.97f, 71f, 460f),
        new(63.97f, 81.5f, 658f)
    };

    private static readonly Vector3[] SectionPlayerStarts =
    {
        Vector3.zero,
        new(0f, 5f, -2f),
        new(24f, 20f, 43.671013f),
        new(20f, 64.5f, 39.671013f),
        new(38f, 64.5f, 169.671013f),
        new(46.97f, 22f, 190.691013f),
        new(49.97f, 61.5f, 338.691013f),
        new(49.97f, 69.4f, 456f),
        new(63.97f, 80f, 655f)
    };

    public bool IsGameStarted => IsSpawned && gameStarted.Value;
    public int CurrentSection => IsSpawned ? currentStage.Value : 1;
    public bool IsTutorialStage => IsGameStarted && currentStage.Value == 0;
    public bool PlayTutorialSelected => IsSpawned && playTutorial.Value;
    public Vector3 TutorialGoalPosition => tutorialCourse != null
        ? tutorialCourse.GoalPosition : Vector3.zero;
    public bool RoomTimerStarted => IsSpawned && roomStartedAt.Value >= 0d;
    public bool RoomTimerFinished => RoomTimerStarted && roomFinishedAt.Value >= 0d;
    public int ClearPlayerCount => IsSpawned ? clearPlayerCount.Value : 0;
    public double ObstacleTime => NetworkManager == null
        ? Time.fixedTimeAsDouble : NetworkManager.ServerTime.Time + obstacleTimeOffset.Value;
    public double RoomElapsedSeconds
    {
        get
        {
            if (!RoomTimerStarted || NetworkManager == null)
                return 0d;

            double end = RoomTimerFinished
                ? roomFinishedAt.Value
                : NetworkManager.ServerTime.Time;
            return savedElapsedSeconds.Value + System.Math.Max(0d, end - roomStartedAt.Value);
        }
    }

    private void Awake()
    {
        completedStageSeconds = new NetworkList<double>();
        Application.wantsToQuit += SaveBeforeApplicationQuit;
        Instance = this;
        if (fullCourseSections == null)
            fullCourseSections = GameObject.Find("FullCourseSections");
        if (GetComponent<GameplayMusicPlayer>() == null)
            gameObject.AddComponent<GameplayMusicPlayer>();
        if (GetComponent<SkyCloudBackdrop>() == null)
            gameObject.AddComponent<SkyCloudBackdrop>();
    }

    public override void OnDestroy()
    {
        Application.wantsToQuit -= SaveBeforeApplicationQuit;
        if (Instance == this)
            Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ResetStageTimes();
            courseReady = false;
            loadingCourse = false;
            courseCleared = false;
            roomFinishedAt.Value = -1d;
            roomStartedAt.Value = -1d;
            savedElapsedSeconds.Value = 0d;
            clearPlayerCount.Value = 0;
            obstacleTimeOffset.Value = 0d;
            gameStarted.Value = false;
            playTutorial.Value = false;
        }

        currentStage.OnValueChanged += OnStageChanged;
        NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        ApplyStage(currentStage.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentStage.OnValueChanged -= OnStageChanged;
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    private void Update()
    {
        if (IsServer && IsGameStarted && !IsTutorialStage && !RoomTimerFinished &&
            Time.unscaledTime >= nextAutomaticSaveAt)
        {
            nextAutomaticSaveAt = Time.unscaledTime + AutomaticSaveInterval;
            SaveElapsedBeforeExit();
        }
    }

    public void BeginGame()
    {
        if (!IsServer || gameStarted.Value || loadingTutorial || loadingCourse)
            return;

        bool tutorial = playTutorial.Value;
        ResetStageTimes();
        obstacleTimeOffset.Value = 0d;
        clearPlayerCount.Value = NetworkManager.ConnectedClientsIds.Count;
        if (tutorial)
        {
            loadingTutorial = true;
            SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(
                tutorialSceneName, LoadSceneMode.Additive);
            if (result != SceneEventProgressStatus.Started)
            {
                loadingTutorial = false;
                Debug.LogError($"Could not load {tutorialSceneName}: {result}");
            }
            return;
        }
        PrepareCourse();
    }

    public void SetPlayTutorial(bool enabled)
    {
        if (IsServer && !gameStarted.Value)
            playTutorial.Value = enabled;
    }

    public void RespawnPlayer(PlayerMovement player)
    {
        if (!IsServer || !IsGameStarted || RoomTimerFinished || player == null ||
            !player.IsSpawned || fallResetZone == null)
            return;

        fallResetZone.RespawnPlayer(player);
    }

    public void CompleteTutorial()
    {
        if (!IsServer || !IsTutorialStage)
            return;
        TutorialCombatLesson lesson = FindFirstObjectByType<TutorialCombatLesson>();
        if (lesson != null && !lesson.CanFinish) return;
        MonsterSpawnZone.CompleteActiveEncounters();
        currentStage.Value = 1;
        ApplyStage(1);
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            Teleport(boulder.gameObject, new Vector3(0f, 5f, 4f));
            BoulderHealth health = boulder.GetComponent<BoulderHealth>();
            if (health != null) health.RestoreHealth(health.MaximumHealth);
        }
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject,
                new Vector3((i - (players.Length - 1) * 0.5f) * 2.5f, 5f, -2f));
        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(new Vector3(0f, 5f, -2f), new Vector3(0f, 5f, 4f));
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        if (tutorialScene.IsValid() && tutorialScene.isLoaded)
        {
            SceneEventProgressStatus result = NetworkManager.SceneManager.UnloadScene(tutorialScene);
            if (result == SceneEventProgressStatus.Started)
                StartCoroutine(PrepareCourseAfterTutorial());
            else
                Debug.LogError($"Could not unload {tutorialSceneName}: {result}");
        }
        else
            PrepareCourse();
    }

    public void RespawnBoulder(BoulderHealth boulder)
    {
        if (!IsServer || !IsGameStarted || RoomTimerFinished || boulder == null) return;
        if (fallResetZone != null)
            fallResetZone.RespawnBoulder(boulder.GetComponent<BoulderSlopeDrag>());
    }

    private IEnumerator PrepareCourseAfterTutorial()
    {
        while (IsServer)
        {
            Scene scene = SceneManager.GetSceneByName(tutorialSceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                break;
            yield return null;
        }
        if (IsServer)
        {
            yield return null;
            PrepareCourse();
        }
    }

    private void MoveTeamToTutorial()
    {
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (tutorialCourse == null)
            return;
        Vector3 boulderStart = tutorialCourse.BoulderSpawn;
        if (boulder != null)
        {
            SphereCollider sphere = boulder.GetComponent<SphereCollider>();
            if (sphere != null)
                boulderStart.y += sphere.radius * boulder.transform.lossyScale.x + 0.1f;
            Teleport(boulder.gameObject, boulderStart);
        }
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, tutorialCourse.PlayerSpawn +
                new Vector3((i - (players.Length - 1) * 0.5f) * 2.5f, 0f, 0f));
        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(tutorialCourse.PlayerSpawn, boulderStart);
    }

    public bool TrySaveRoom()
    {
        if (!IsServer || !IsGameStarted || loadingTutorial || currentStage.Value == 0 ||
            RoomTimerFinished || pendingResume != null ||
            loadingCourse || !courseReady ||
            NetworkManager.ConnectedClientsIds.Count != clearPlayerCount.Value)
            return false;

        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder == null)
            return false;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        if (players.Length != NetworkManager.ConnectedClientsIds.Count)
            return false;

        var savedPlayers = new SavedPlayerState[players.Length];
        var identities = new HashSet<string>();
        for (int i = 0; i < players.Length; i++)
        {
            string id = players[i].PersistentPlayerId;
            if (!players[i].IsSpawned || string.IsNullOrEmpty(id) || !identities.Add(id))
                return false;
            savedPlayers[i] = new SavedPlayerState
            {
                playerId = id,
                displayName = players[i].DisplayName,
                color = PlayerColorPalette.Pack(players[i].AvatarColor),
                lookPitch = players[i].GetComponent<PlayerEyes>()?.CurrentPitch ?? 0f,
                body = CaptureBody(players[i].gameObject)
            };
        }

        var room = new SavedRoom
        {
            id = activeSaveId,
            stage = currentStage.Value,
            elapsedSeconds = RoomElapsedSeconds,
            obstacleTime = ObstacleTime,
            boulder = CaptureBody(boulder.gameObject),
            boulderHealth = boulder.GetComponent<BoulderHealth>()?.CurrentHealth ?? 5,
            monsterEncounters = MonsterSpawnZone.CaptureAll(),
            cannonControls = Stage8CannonControls.CaptureCurrent(),
            stageClearSeconds = CaptureStageTimes(),
            currentStageElapsedSeconds = CurrentStageElapsedSeconds,
            players = savedPlayers
        };
        if (currentStage.Value == 5)
        {
            Stage5BallLauncher launcher = System.Array.Find(FindObjectsByType<Stage5BallLauncher>(FindObjectsSortMode.None), cannon => cannon.ActiveSection == 5);
            room.nextCannonShotIn = launcher != null ? launcher.SecondsUntilNextShot : 2d;
            Stage5Projectile[] flying = FindObjectsByType<Stage5Projectile>(FindObjectsSortMode.None);
            room.projectiles = new SavedProjectileState[flying.Length];
            for (int i = 0; i < flying.Length; i++)
                room.projectiles[i] = new SavedProjectileState
                {
                    body = CaptureBody(flying[i].gameObject),
                    health = flying[i].CurrentHealth,
                    remainingLifetime = flying[i].RemainingLifetime
                };
        }
        if (!SavedRoomStore.Save(room))
            return false;
        activeSaveId = room.id;
        nextAutomaticSaveAt = Time.unscaledTime + AutomaticSaveInterval;
        return true;
    }

    public void SaveElapsedBeforeExit()
    {
        if (!IsServer || !IsGameStarted || IsTutorialStage || RoomTimerFinished) return;
        if (TrySaveRoom()) return;
        SavedRoom room = System.Array.Find(SavedRoomStore.LoadAll(), saved => saved.id == activeSaveId);
        if (room == null)
        {
            TrySaveRoom();
            return;
        }
        // A departing teammate may already be despawned. Keep the last complete
        // roster snapshot in that case, while preserving the latest elapsed time.
        room.elapsedSeconds = RoomElapsedSeconds;
        if(room.stage==currentStage.Value)
        {
            room.currentStageElapsedSeconds=CurrentStageElapsedSeconds;
            room.stageClearSeconds=CaptureStageTimes();
        }
        SavedRoomStore.Save(room);
    }

    private void OnApplicationQuit() => SaveElapsedBeforeExit();
    private bool SaveBeforeApplicationQuit()
    {
        SaveElapsedBeforeExit();
        return true;
    }
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveElapsedBeforeExit();
    }
    private void OnClientDisconnected(ulong clientId) => SaveElapsedBeforeExit();

    public bool BeginSavedGame(SavedRoom room)
    {
        if (!IsServer || gameStarted.Value || pendingResume != null || room == null ||
            room.stage < 1 || room.stage > 8 || room.players == null ||
            room.players.Length != NetworkManager.ConnectedClientsIds.Count)
            return false;

        var expected = new HashSet<string>();
        foreach (SavedPlayerState player in room.players)
            if (player == null || string.IsNullOrEmpty(player.playerId) ||
                !expected.Add(player.playerId))
                return false;
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            if (player.IsSpawned && expected.Remove(player.PersistentPlayerId))
                continue;
            else
                return false;
        if (expected.Count != 0)
            return false;

        UpgradeSavedRoomPositions(room);
        pendingResume = room;
        return PrepareCourse();
    }

    private bool PrepareCourse()
    {
        if (loadingCourse || fullCourseSections == null)
        {
            if (fullCourseSections == null)
                Debug.LogError("Stage1 scene has no FullCourseSections object.");
            return false;
        }
        loadingCourse = true;
        loadingCourse = false;
        courseReady = true;
        if (pendingResume != null)
            FinishResume();
        else
        {
            ApplyStage(currentStage.Value);
            savedElapsedSeconds.Value = 0d;
            roomStartedAt.Value = NetworkManager.ServerTime.Time;
            gameStarted.Value = true;
        }
        return true;
    }

    private static void UpgradeSavedRoomPositions(SavedRoom room)
    {
        if (room.version >= 3)
            return;
        UpgradeSavedBody(room.boulder, room.version, room.stage);
        foreach (SavedPlayerState player in room.players)
            UpgradeSavedBody(player.body, room.version, room.stage);
        if (room.projectiles != null)
            foreach (SavedProjectileState projectile in room.projectiles)
                if (projectile != null && projectile.body != null)
                    UpgradeSavedBody(projectile.body, room.version, room.stage);
        room.version = 3;
    }

    private static void UpgradeSavedBody(SavedBodyState body, int version, int stage)
    {
        if (body == null)
            return;
        if (version == 1)
            body.position += SectionOffsets[stage];
        else if (stage == 6 || (stage == 5 && body.position.z >= 188f))
            body.position += Vector3.up * 2f;
    }

    private static SavedBodyState CaptureBody(GameObject target)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        return new SavedBodyState
        {
            position = target.transform.position,
            rotation = target.transform.rotation,
            velocity = body != null ? body.linearVelocity : Vector3.zero,
            angularVelocity = body != null ? body.angularVelocity : Vector3.zero
        };
    }

    private void FinishResume()
    {
        if (!IsServer || pendingResume == null)
            return;
        SavedRoom room = pendingResume;
        currentStage.Value = room.stage;
        ApplyStage(room.stage);

        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            // A checkpoint advances the section before the boulder has crossed the
            // connecting path. Resume at that section's safe reset position instead
            // of restoring the previous section's finish line (and saved momentum).
            var checkpointBody = new SavedBodyState
            {
                position = CheckpointBoulderPositions[room.stage],
                rotation = room.boulder != null ? room.boulder.rotation : Quaternion.identity,
                velocity = Vector3.zero,
                angularVelocity = Vector3.zero
            };
            RestoreBody(boulder.gameObject, checkpointBody);
            boulder.GetComponent<BoulderHealth>()?.RestoreHealth(room.boulderHealth);
        }
        foreach (PlayerMovement player in FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
        {
            foreach (SavedPlayerState savedPlayer in room.players)
            {
                if (player.PersistentPlayerId != savedPlayer.playerId)
                    continue;
                RestoreBody(player.gameObject, savedPlayer.body);
                player.RestoreOwnerView(savedPlayer.body.rotation.eulerAngles.y,
                    savedPlayer.lookPitch, savedPlayer.color);
                break;
            }
        }

        activeSaveId = room.id;
        clearPlayerCount.Value = room.players.Length;
        obstacleTimeOffset.Value = room.obstacleTime - NetworkManager.ServerTime.Time;
        if (room.stage == 5)
        {
            Stage5BallLauncher launcher = System.Array.Find(FindObjectsByType<Stage5BallLauncher>(FindObjectsSortMode.None), cannon => cannon.ActiveSection == 5);
            if (launcher != null)
            {
                launcher.RestoreShotDelay(room.nextCannonShotIn);
                if (room.projectiles != null)
                    foreach (SavedProjectileState projectile in room.projectiles)
                        launcher.RestoreProjectile(projectile);
            }
        }
        savedElapsedSeconds.Value = System.Math.Max(0d, room.elapsedSeconds);
        roomStartedAt.Value = NetworkManager.ServerTime.Time;
        ResetStageTimes();
        if(room.stageClearSeconds!=null)
            for(int i=0;i<System.Math.Min(8,room.stageClearSeconds.Length);i++)
                if(i<room.stage-1 && room.stageClearSeconds[i]>=0d)completedStageSeconds[i]=room.stageClearSeconds[i];
        sectionStartedElapsed.Value = System.Math.Max(0d,room.elapsedSeconds-
            System.Math.Max(0d,room.currentStageElapsedSeconds));
        roomFinishedAt.Value = -1d;
        gameStarted.Value = true;
        MonsterSpawnZone.RestoreAll(room.monsterEncounters);
        Stage8CannonControls.RestoreCurrent(room.cannonControls);
        pendingResume = null;
    }

    private static void RestoreBody(GameObject target, SavedBodyState saved)
    {
        if (target == null || saved == null)
            return;
        Rigidbody body = target.GetComponent<Rigidbody>();
        NetworkTransform networkTransform = target.GetComponent<NetworkTransform>();
        if (body == null || networkTransform == null)
            return;
        body.position = saved.position;
        body.rotation = saved.rotation;
        networkTransform.Teleport(saved.position, saved.rotation, target.transform.localScale);
        body.linearVelocity = saved.velocity;
        body.angularVelocity = saved.angularVelocity;
        body.WakeUp();
    }

    private void SkipToNextStage()
    {
        if (currentStage.Value == 0)
        {
            CompleteTutorial();
            return;
        }
        if (currentStage.Value == 8)
        {
            CompleteStageEight();
            return;
        }
        if (!courseReady || currentStage.Value < 1 || currentStage.Value > 7)
            return;

        int nextSection = currentStage.Value + 1;
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
            Teleport(boulder.gameObject, SectionBoulderStarts[nextSection]);
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, SectionPlayerStarts[nextSection] +
                new Vector3((i - (players.Length - 1) * 0.5f) * 2.5f, 0f, 0f));
        ReachCheckpoint(currentStage.Value);
    }

    public void CompleteStageOne()
    {
        ReachCheckpoint(1);
    }

    public void CompleteStageTwo()
    {
        ReachCheckpoint(2);
    }

    public void CompleteStageThree()
    {
        ReachCheckpoint(3);
    }

    public void CompleteStageFour()
    {
        ReachCheckpoint(4);
    }

    public void CompleteStageFive()
    {
        ReachCheckpoint(5);
    }

    public void CompleteStageSix()
    {
        ReachCheckpoint(6);
    }

    private void ReachCheckpoint(int completedSection)
    {
        if (!IsServer || !IsGameStarted || !courseReady || RoomTimerFinished ||
            currentStage.Value != completedSection)
            return;

        RecordStageTime(completedSection);
        MonsterSpawnZone.CompleteActiveEncounters();
        currentStage.Value = completedSection + 1;
        ApplyStage(currentStage.Value);
        SaveReachedCheckpoint();
    }

    public void CompleteStageSeven()
    {
        ReachCheckpoint(7);
    }

    public void CompleteStageEight()
    {
        if (!IsServer || !IsGameStarted || !courseReady ||
            currentStage.Value != 8 || courseCleared)
            return;

        RecordStageTime(8);
        MonsterSpawnZone.CompleteActiveEncounters();
        courseCleared = true;
        roomFinishedAt.Value = NetworkManager.ServerTime.Time;
        if (!string.IsNullOrEmpty(activeSaveId))
        {
            if (SavedRoomStore.Delete(activeSaveId)) activeSaveId = null;
            else Debug.LogWarning("Could not remove the completed room save: " + activeSaveId);
        }
        long elapsed = (long)System.Math.Floor(RoomElapsedSeconds);
        Debug.Log($"Stage 8 complete. Room time: {elapsed / 3600:00}:{elapsed / 60 % 60:00}:{elapsed % 60:00}.");
    }

    public void ReturnToWaitingRoom()
    {
        if (!IsSpawned || !RoomTimerFinished)
            return;
        if (IsServer)
            ResetRoomAfterClear();
        else
            RequestWaitingRoomRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestWaitingRoomRpc()
    {
        if (RoomTimerFinished)
            ResetRoomAfterClear();
    }

    private void ResetRoomAfterClear()
    {
        if (!IsServer || !RoomTimerFinished)
            return;

        currentStage.Value = 1;
        ApplyStage(1);
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
            Teleport(boulder.gameObject, new Vector3(0f, 5f, 4f));
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, new Vector3((i - (players.Length - 1) * 0.5f) * 2.5f, 5f, -2f));
        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(new Vector3(0f, 5f, -2f), new Vector3(0f, 5f, 4f));

        courseCleared = false;
        ResetStageTimes();
        clearPlayerCount.Value = 0;
        roomStartedAt.Value = -1d;
        savedElapsedSeconds.Value = 0d;
        roomFinishedAt.Value = -1d;
        gameStarted.Value = false;
        obstacleTimeOffset.Value = 0d;
        activeSaveId = null;
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == tutorialSceneName && !loadingTutorial)
            ApplyStage(currentStage.Value);

        if (!IsServer)
            return;

        if (sceneName == tutorialSceneName && loadingTutorial)
        {
            tutorialCourse = FindFirstObjectByType<TutorialCourse>();
            if (tutorialCourse == null)
            {
                loadingTutorial = false;
                Debug.LogError("Tutorial scene has no TutorialCourse component.");
                return;
            }
            currentStage.Value = 0;
            ApplyStage(0);
            MoveTeamToTutorial();
            roomStartedAt.Value = -1d;
            gameStarted.Value = true;
            loadingTutorial = false;
            return;
        }

    }

    private void SaveReachedCheckpoint()
    {
        // Save the shared boulder and team at the finish line, without moving them.
        if (TrySaveRoom())
            Debug.Log($"Checkpoint {currentStage.Value - 1} saved.");
        else
            Debug.LogWarning($"Could not save checkpoint after section {currentStage.Value - 1}.");
    }

    private void OnStageChanged(int previous, int current)
    {
        ApplyStage(current);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsServer && gameStarted.Value && !RoomTimerFinished)
            clearPlayerCount.Value = System.Math.Max(clearPlayerCount.Value,
                NetworkManager.ConnectedClientsIds.Count);
        if (currentStage.Value == 0 || currentStage.Value >= 2)
            StartCoroutine(PlaceLatePlayer(clientId));
    }

    private IEnumerator PlaceLatePlayer(ulong clientId)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null && client.PlayerObject.IsSpawned)
            {
                Vector3 position = currentStage.Value == 0
                    ? (tutorialCourse != null ? tutorialCourse.PlayerSpawn : Vector3.zero) +
                        new Vector3((clientId % 4) * 2.5f, 0f, 0f)
                    : CheckpointPlayerPositions[Mathf.Clamp(currentStage.Value, 1, 8)] +
                        new Vector3((clientId % 4) * 2.5f, 0f, 0f);
                Teleport(client.PlayerObject.gameObject, position);
                yield break;
            }
            yield return null;
        }
    }

    private void ApplyStage(int stage)
    {
        Scene tutorialScene = SceneManager.GetSceneByName(tutorialSceneName);
        if (tutorialScene.IsValid() && tutorialScene.isLoaded)
        {
            foreach (GameObject root in tutorialScene.GetRootGameObjects())
                root.SetActive(stage == 0);
        }
        if (stageOneTrack != null)
            stageOneTrack.SetActive(stage != 0);
        if (stageOneGround != null)
            stageOneGround.SetActive(stage != 0);
        if (stageOneLight != null)
            stageOneLight.SetActive(stage != 0);

        if (fullCourseSections != null)
            fullCourseSections.SetActive(stage != 0);

        if (IsServer && stage >= 1 && stage <= 8 && fallResetZone != null)
            fallResetZone.SetRespawnPositions(
                CheckpointPlayerPositions[stage], CheckpointBoulderPositions[stage]);
    }

    private static void Teleport(GameObject target, Vector3 position)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        NetworkTransform networkTransform = target.GetComponent<NetworkTransform>();
        if (body == null || networkTransform == null)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = position;
        networkTransform.Teleport(position, body.rotation, target.transform.localScale);
        body.WakeUp();
    }
}
