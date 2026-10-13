using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class LogManager : NetworkBehaviour
{
    public Text logText;
    public ScrollRect scrollRect;
    private const int maxLines = 13;
    private Queue<string> lines = new Queue<string>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        SendHistoryRpc(string.Join("\n", lines), RpcTarget.Single(clientId, RpcTargetUse.Temp)); // 추가: 접속한 사람에게만 지금까지 로그 전달
        BroadcastLog($"Player {clientId} connected");
    }

    public void BroadcastLog(string message)
    {
        AddLogClientRpc(message);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AddLogClientRpc(string message)
    {
        lines.Enqueue(message);
        if (lines.Count > maxLines) lines.Dequeue();
        logText.text = string.Join("\n", lines);

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    [Rpc(SendTo.SpecifiedInParams)] // 추가: 특정 클라이언트 한 명에게만 보내는 RPC
    private void SendHistoryRpc(string history, RpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(history)) return;

        foreach (var line in history.Split('\n'))
            lines.Enqueue(line);
        while (lines.Count > maxLines) lines.Dequeue();

        logText.text = string.Join("\n", lines);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

}