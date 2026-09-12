using UnityEngine;
using Unity.Netcode;

public class PlayerSpawnLog : NetworkBehaviour
{


    public override void OnNetworkSpawn()
    {
        Debug.Log($"플레이어 스폰됨: OwnerClientId={OwnerClientId}, IsOwner={IsOwner}");
    }


}