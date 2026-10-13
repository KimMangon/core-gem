using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    [SerializeField] private float timeLimit = 3f;
    [SerializeField] private int startingLives = 3;
    [SerializeField] private Transform tableTransform;

    private List<ulong> turnOrder = new List<ulong>();
    private int currentTurnIndex = 0;
    private float pressStartTime;
    private bool timerRunning = false;
    private float roundElapsedTime = 0f;
    private Dictionary<ulong, int> lives = new Dictionary<ulong, int>();
    private TableSpotManager tableSpotManager;
    private int localLives = -1;

    public int LocalLives => localLives;

    public override void OnNetworkSpawn()
    {
        if (IsServer) tableSpotManager = FindAnyObjectByType<TableSpotManager>();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartRoundServerRpc()
    {
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!lives.ContainsKey(id))
                lives[id] = startingLives;

            SetLocalLivesRpc(id, lives[id], RpcTarget.Single(id, RpcTargetUse.Temp));
        }
        StartNewRound();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void SetLocalLivesRpc(ulong playerId, int value, RpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == playerId)
            localLives = value;
    }

    private void StartNewRound()
    {
        turnOrder = new List<ulong>();
        foreach (var kvp in lives)
            if (kvp.Value > 0) turnOrder.Add(kvp.Key);

        for (int i = 0; i < turnOrder.Count; i++)
        {
            int rand = Random.Range(i, turnOrder.Count);
            (turnOrder[i], turnOrder[rand]) = (turnOrder[rand], turnOrder[i]);
        }
        currentTurnIndex = 0;
        roundElapsedTime = 0f;
        Debug.Log($"[서버] 새 라운드 시작, 순서: {string.Join(",", turnOrder)}");
        FindAnyObjectByType<LogManager>().BroadcastLog($"New round started — Player {turnOrder[currentTurnIndex]}'s turn");
        MoveTableToCurrentPlayer();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PressButtonServerRpc(ulong senderId)
    {
        if (turnOrder[currentTurnIndex] != senderId)
        {
            Debug.Log($"[서버] {senderId}의 차례가 아님, 무시");
            return;
        }

        if (!timerRunning)
        {
            pressStartTime = Time.time;
            timerRunning = true;
            Debug.Log($"[서버] {senderId} 타이머 시작");
        }
        else
        {
            float segment = Time.time - pressStartTime;
            roundElapsedTime += segment;
            timerRunning = false;
            Debug.Log($"[서버] {senderId} 정지, 이번 구간={segment:F2}초, 누적={roundElapsedTime:F2}초");

            if (roundElapsedTime > timeLimit)
            {
                lives[senderId]--;
                SetLocalLivesRpc(senderId, lives[senderId], RpcTarget.Single(senderId, RpcTargetUse.Temp));
                Debug.Log($"[서버] {senderId} 초과! 남은 목숨: {lives[senderId]}");
                FindAnyObjectByType<LogManager>().BroadcastLog($"Player {senderId} lost a life (remaining: {lives[senderId]})");
                if (lives[senderId] <= 0)
                {
                    Debug.Log($"[서버] {senderId} 탈락");
                    FindAnyObjectByType<LogManager>().BroadcastLog($"Player {senderId} eliminated");
                }
                    

                int aliveCount = 0;
                ulong lastAlive = 0;
                foreach (var kvp in lives)
                    if (kvp.Value > 0) { aliveCount++; lastAlive = kvp.Key; }

                if (aliveCount <= 1)
                {
                    Debug.Log($"[서버] 게임 종료! 승자: {lastAlive}");
                    FindAnyObjectByType<LogManager>().BroadcastLog($"Game over! Winner: Player {lastAlive}");
                }
                    
                else
                    StartNewRound();
            }
            else
            {
                currentTurnIndex = (currentTurnIndex + 1) % turnOrder.Count;
                Debug.Log($"[서버] 다음 차례: {turnOrder[currentTurnIndex]}");
                FindAnyObjectByType<LogManager>().BroadcastLog($"Player {turnOrder[currentTurnIndex]}'s turn");
                MoveTableToCurrentPlayer();
            }
        }
    }

    private void MoveTableToCurrentPlayer()
    {
        if (tableSpotManager == null || turnOrder.Count == 0) return;
        ulong currentPlayer = turnOrder[currentTurnIndex];
        Transform spot = tableSpotManager.spots[currentPlayer];
        tableTransform.position = spot.position;
        tableTransform.rotation = spot.rotation;
    }

    public void AdjustTimeLimit(float delta)
    {
        timeLimit = Mathf.Clamp(timeLimit + delta, 1f, 10f);
    }

    public void AdjustStartingLives(int delta)
    {
        startingLives = Mathf.Clamp(startingLives + delta, 1, 9);
    }

    public float GetTimeLimit() => timeLimit;
    public int GetStartingLives() => startingLives;
}
