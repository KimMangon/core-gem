using UnityEngine;
using Unity.Netcode;

public class NetworkTestUI : MonoBehaviour
{
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 200, 200)); // 높이 100 → 200으로 수정
        if (GUILayout.Button("Host")) NetworkManager.Singleton.StartHost();
        if (GUILayout.Button("Client")) NetworkManager.Singleton.StartClient();
        if (GUILayout.Button("Server")) NetworkManager.Singleton.StartServer();
        if (GUILayout.Button("Start Round"))
        {
            FindFirstObjectByType<GameManager>().StartRoundServerRpc();
        }
        if (GUILayout.Button("Press"))
        {
            FindFirstObjectByType<GameManager>().PressButtonServerRpc(NetworkManager.Singleton.LocalClientId);
        }
        GUILayout.EndArea();
    }

    void Start() // 추가
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log($"클라이언트 연결됨: {clientId}");
        };
    }
}