using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnLog : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        Debug.Log($"플레이어 스폰됨: OwnerClientId={OwnerClientId}, IsOwner={IsOwner}");

        playerCamera.enabled = IsOwner; // SetActive 대신 Camera 컴포넌트만 껐다 켬
        AudioListener listener = playerCamera.GetComponent<AudioListener>();
        if (listener != null) listener.enabled = IsOwner;

        if (IsOwner && Camera.main != null)
            Camera.main.gameObject.SetActive(false);

        if (IsServer)
        {
            SeatManager seatManager = FindFirstObjectByType<SeatManager>();
            if (seatManager != null && OwnerClientId < (ulong)seatManager.seats.Length)
            {
                Transform seat = seatManager.seats[OwnerClientId];
                transform.position = seat.position + new Vector3(0, 1.5f, 0); // 수정: Y좌표 +1.5
                transform.rotation = seat.rotation;
                Debug.Log($"[서버] {OwnerClientId}번 플레이어를 좌석 {OwnerClientId}에 배치");
            }
        }
    }


}