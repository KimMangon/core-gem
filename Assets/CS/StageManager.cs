using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class StageManager : NetworkBehaviour
{
    [SerializeField] private GameObject stageOneTrack;
    [SerializeField] private GameObject stageOneGround;
    [SerializeField] private GameObject stageOneLight;
    [SerializeField] private FallResetZone fallResetZone;
    [SerializeField] private string stageTwoSceneName = "Stage2";
    [SerializeField] private string stageThreeSceneName = "Stage3";
    [SerializeField] private string stageFourSceneName = "Stage4";
    [SerializeField] private string stageFiveSceneName = "Stage5";
    [SerializeField] private Vector3 stageTwoBoulderStart = new(-16f, -0.5f, 4f);
    [SerializeField] private Vector3 stageTwoPlayerStart = new(-12.5f, -1f, 1f);
    [SerializeField] private Vector3 stageThreeBoulderStart = new(-125f, -0.5f, -12f);
    [SerializeField] private Vector3 stageThreePlayerStart = new(-129f, -1f, -18f);
    [SerializeField] private Vector3 stageFourBoulderStart = new(-25f, 0f, -10f);
    [SerializeField] private Vector3 stageFourPlayerStart = new(-18.5f, 42.7f, -15f);
    [SerializeField] private Vector3 stageFiveBoulderStart = new(150f, 2f, -10f);
    [SerializeField] private Vector3 stageFivePlayerStart = new(144f, 1.1f, -15f);

    private readonly NetworkVariable<int> currentStage = new(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> roomStartedAt = new(
        -1d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<double> roomFinishedAt = new(
        -1d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private bool loadingStageTwo;
    private bool loadingStageThree;
    private bool loadingStageFour;
    private bool loadingStageFive;
    private bool stageFiveCleared;

    public bool RoomTimerStarted => IsSpawned && roomStartedAt.Value >= 0d;
    public bool RoomTimerFinished => RoomTimerStarted && roomFinishedAt.Value >= 0d;
    public double RoomElapsedSeconds
    {
        get
        {
            if (!RoomTimerStarted || NetworkManager == null)
                return 0d;

            double end = RoomTimerFinished
                ? roomFinishedAt.Value
                : NetworkManager.ServerTime.Time;
            return System.Math.Max(0d, end - roomStartedAt.Value);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            stageFiveCleared = false;
            roomFinishedAt.Value = -1d;
            roomStartedAt.Value = NetworkManager.ServerTime.Time;
        }

        currentStage.OnValueChanged += OnStageChanged;
        NetworkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        if (IsServer)
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        ApplyStage(currentStage.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentStage.OnValueChanged -= OnStageChanged;
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        if (NetworkManager != null && NetworkManager.SceneManager != null)
            NetworkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
    }

    private void Update()
    {
        if (!Debug.isDebugBuild || !IsSpawned || Keyboard.current == null ||
            !Keyboard.current.f6Key.wasPressedThisFrame)
            return;

        if (IsServer)
            SkipToNextStage();
        else
            RequestSkipStageRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSkipStageRpc()
    {
        if (!Debug.isDebugBuild)
            return;
        SkipToNextStage();
    }

    private void SkipToNextStage()
    {
        if (currentStage.Value == 1)
            CompleteStageOne();
        else if (currentStage.Value == 2)
            CompleteStageTwo();
        else if (currentStage.Value == 3)
            CompleteStageThree();
        else if (currentStage.Value == 4)
            CompleteStageFour();
        else if (currentStage.Value == 5)
            CompleteStageFive();
    }

    public void CompleteStageOne()
    {
        if (!IsServer || currentStage.Value != 1 || loadingStageTwo)
            return;

        loadingStageTwo = true;
        SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(
            stageTwoSceneName, LoadSceneMode.Additive);
        if (result != SceneEventProgressStatus.Started)
        {
            loadingStageTwo = false;
            Debug.LogError($"Could not load {stageTwoSceneName}: {result}");
        }
    }

    public void CompleteStageTwo()
    {
        if (!IsServer || currentStage.Value != 2 || loadingStageThree)
            return;

        loadingStageThree = true;
        SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(
            stageThreeSceneName, LoadSceneMode.Additive);
        if (result != SceneEventProgressStatus.Started)
        {
            loadingStageThree = false;
            Debug.LogError($"Could not load {stageThreeSceneName}: {result}");
        }
    }

    public void CompleteStageThree()
    {
        if (!IsServer || currentStage.Value != 3 || loadingStageFour)
            return;

        loadingStageFour = true;
        SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(
            stageFourSceneName, LoadSceneMode.Additive);
        if (result != SceneEventProgressStatus.Started)
        {
            loadingStageFour = false;
            Debug.LogError($"Could not load {stageFourSceneName}: {result}");
        }
    }

    public void CompleteStageFour()
    {
        if (!IsServer || currentStage.Value != 4 || loadingStageFive)
            return;

        loadingStageFive = true;
        SceneEventProgressStatus result = NetworkManager.SceneManager.LoadScene(
            stageFiveSceneName, LoadSceneMode.Additive);
        if (result != SceneEventProgressStatus.Started)
        {
            loadingStageFive = false;
            Debug.LogError($"Could not load {stageFiveSceneName}: {result}");
        }
    }

    public void CompleteStageFive()
    {
        if (!IsServer || currentStage.Value != 5 || stageFiveCleared)
            return;

        stageFiveCleared = true;
        roomFinishedAt.Value = NetworkManager.ServerTime.Time;
        long elapsed = (long)System.Math.Floor(RoomElapsedSeconds);
        Debug.Log($"Stage 5 complete. Room time: {elapsed / 3600:00}:{elapsed / 60 % 60:00}:{elapsed % 60:00}.");
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == stageTwoSceneName || sceneName == stageThreeSceneName ||
            sceneName == stageFourSceneName || sceneName == stageFiveSceneName)
            ApplyStage(currentStage.Value);

        if (!IsServer)
            return;

        if (sceneName == stageTwoSceneName && loadingStageTwo)
        {
            currentStage.Value = 2;
            ApplyStage(2);
            MoveTeamToStageTwo();
            loadingStageTwo = false;
        }
        else if (sceneName == stageThreeSceneName && loadingStageThree)
        {
            currentStage.Value = 3;
            ApplyStage(3);
            MoveTeamToStageThree();
            loadingStageThree = false;
        }
        else if (sceneName == stageFourSceneName && loadingStageFour)
        {
            currentStage.Value = 4;
            ApplyStage(4);
            MoveTeamToStageFour();
            loadingStageFour = false;
        }
        else if (sceneName == stageFiveSceneName && loadingStageFive)
        {
            currentStage.Value = 5;
            ApplyStage(5);
            MoveTeamToStageFive();
            loadingStageFive = false;
        }
    }

    private void OnStageChanged(int previous, int current)
    {
        ApplyStage(current);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (currentStage.Value >= 2)
            StartCoroutine(PlaceLatePlayer(clientId));
    }

    private IEnumerator PlaceLatePlayer(ulong clientId)
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
                client.PlayerObject != null && client.PlayerObject.IsSpawned)
            {
                Vector3 position = currentStage.Value == 5
                    ? stageFivePlayerStart + new Vector3((clientId % 4) * 2.5f, 0f, 0f)
                    : currentStage.Value == 4
                    ? stageFourPlayerStart + new Vector3(0f, 0f, (clientId % 4) * 3f)
                    : currentStage.Value == 3
                        ? stageThreePlayerStart + new Vector3((clientId % 4) * 2.5f, 0f, 0f)
                        : stageTwoPlayerStart + new Vector3(0f, 0f, (clientId % 4) * 2.5f);
                Teleport(client.PlayerObject.gameObject, position);
                yield break;
            }
            yield return null;
        }
    }

    private void ApplyStage(int stage)
    {
        if (stageOneTrack != null)
            stageOneTrack.SetActive(stage == 1);
        if (stageOneGround != null)
            stageOneGround.SetActive(stage == 1);
        if (stageOneLight != null)
            stageOneLight.SetActive(stage == 1);

        Scene stageTwo = SceneManager.GetSceneByName(stageTwoSceneName);
        if (stageTwo.IsValid() && stageTwo.isLoaded)
        {
            foreach (GameObject root in stageTwo.GetRootGameObjects())
                root.SetActive(stage == 2);
        }

        Scene stageThree = SceneManager.GetSceneByName(stageThreeSceneName);
        if (stageThree.IsValid() && stageThree.isLoaded)
        {
            foreach (GameObject root in stageThree.GetRootGameObjects())
                root.SetActive(stage == 3);
        }

        Scene stageFour = SceneManager.GetSceneByName(stageFourSceneName);
        if (stageFour.IsValid() && stageFour.isLoaded)
        {
            foreach (GameObject root in stageFour.GetRootGameObjects())
                root.SetActive(stage == 4);
        }

        Scene stageFive = SceneManager.GetSceneByName(stageFiveSceneName);
        if (stageFive.IsValid() && stageFive.isLoaded)
        {
            foreach (GameObject root in stageFive.GetRootGameObjects())
                root.SetActive(stage == 5);
        }

        if (IsServer && stage == 2 && fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageTwoPlayerStart, stageTwoBoulderStart);
        if (IsServer && stage == 3 && fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageThreePlayerStart, stageThreeBoulderStart);
        if (IsServer && stage == 4 && fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageFourPlayerStart, stageFourBoulderStart);
        if (IsServer && stage == 5 && fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageFivePlayerStart, stageFiveBoulderStart);
    }

    private void MoveTeamToStageTwo()
    {
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            float radius = boulder.GetComponent<SphereCollider>().radius * boulder.transform.lossyScale.x;
            stageTwoBoulderStart.y = -2.5f + radius + 0.1f;
            Teleport(boulder.gameObject, stageTwoBoulderStart);
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (left, right) =>
            left.GetComponent<NetworkObject>().OwnerClientId.CompareTo(
                right.GetComponent<NetworkObject>().OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, stageTwoPlayerStart + new Vector3(0f, 0f, i * 2.5f));

        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageTwoPlayerStart, stageTwoBoulderStart);
    }

    private void MoveTeamToStageThree()
    {
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            float radius = boulder.GetComponent<SphereCollider>().radius * boulder.transform.lossyScale.x;
            stageThreeBoulderStart.y = -2.5f + radius + 0.1f;
            Teleport(boulder.gameObject, stageThreeBoulderStart);
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (left, right) =>
            left.GetComponent<NetworkObject>().OwnerClientId.CompareTo(
                right.GetComponent<NetworkObject>().OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, stageThreePlayerStart + new Vector3(i * 2.5f, 0f, 0f));

        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageThreePlayerStart, stageThreeBoulderStart);
    }

    private void MoveTeamToStageFour()
    {
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            float radius = boulder.GetComponent<SphereCollider>().radius * boulder.transform.lossyScale.x;
            stageFourBoulderStart.y = 41.64f + radius + 0.1f;
            Teleport(boulder.gameObject, stageFourBoulderStart);
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (left, right) =>
            left.GetComponent<NetworkObject>().OwnerClientId.CompareTo(
                right.GetComponent<NetworkObject>().OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, stageFourPlayerStart + new Vector3(0f, 0f, i * 3f));

        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageFourPlayerStart, stageFourBoulderStart);
    }

    private void MoveTeamToStageFive()
    {
        BoulderSlopeDrag boulder = FindFirstObjectByType<BoulderSlopeDrag>();
        if (boulder != null)
        {
            float radius = boulder.GetComponent<SphereCollider>().radius * boulder.transform.lossyScale.x;
            stageFiveBoulderStart.y = radius + 0.1f;
            Teleport(boulder.gameObject, stageFiveBoulderStart);
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);
        System.Array.Sort(players, (left, right) =>
            left.GetComponent<NetworkObject>().OwnerClientId.CompareTo(
                right.GetComponent<NetworkObject>().OwnerClientId));
        for (int i = 0; i < players.Length; i++)
            Teleport(players[i].gameObject, stageFivePlayerStart + new Vector3(i * 2.5f, 0f, 0f));

        if (fallResetZone != null)
            fallResetZone.SetRespawnPositions(stageFivePlayerStart, stageFiveBoulderStart);
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
