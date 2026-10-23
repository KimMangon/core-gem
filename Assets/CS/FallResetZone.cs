using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Returns fallen networked players and the shared boulder to the course.
/// Place this on a large, invisible trigger below the level.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class FallResetZone : MonoBehaviour
{
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 5f, 4f);
    private Vector3 boulderRespawnPosition = new Vector3(0f, 5f, 4f);

    public void SetRespawnPositions(Vector3 playerPosition, Vector3 boulderPosition)
    {
        respawnPosition = playerPosition;
        boulderRespawnPosition = boulderPosition;
    }

    private void OnTriggerEnter(Collider other) => ResetIfFallen(other);

    private void OnTriggerStay(Collider other) => ResetIfFallen(other);

    public void RespawnPlayer(PlayerMovement player)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || player == null)
            return;

        ResetNetworkBody(player.GetComponent<Rigidbody>(), respawnPosition);
    }

    public void RespawnBoulder(BoulderSlopeDrag boulder)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || boulder == null) return;
        ResetNetworkBody(boulder.GetComponent<Rigidbody>(), boulderRespawnPosition);
    }

    private void ResetIfFallen(Collider other)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer)
            return;

        Rigidbody body = other.attachedRigidbody;
        if (body == null || body.isKinematic)
            return;

        GameObject target = body.gameObject;
        if (target.GetComponent<PlayerMovement>() == null &&
            target.GetComponent<BoulderSlopeDrag>() == null)
            return;

        bool isBoulder = target.GetComponent<BoulderSlopeDrag>() != null;
        Vector3 destination = isBoulder
            ? boulderRespawnPosition
            : respawnPosition;
        if (ResetNetworkBody(body, destination) && isBoulder)
        {
            target.GetComponent<BoulderHealth>()?.ResetAfterFall();
            MonsterSpawnZone.ResetAllForRetry();
        }
    }

    private static bool ResetNetworkBody(Rigidbody body, Vector3 destination)
    {
        if (body == null || body.isKinematic)
            return false;

        GameObject target = body.gameObject;
        NetworkObject networkObject = target.GetComponent<NetworkObject>();
        NetworkTransform networkTransform = target.GetComponent<NetworkTransform>();
        if (networkObject == null || !networkObject.IsSpawned || networkTransform == null)
            return false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = destination;
        networkTransform.Teleport(destination, body.rotation, target.transform.localScale);
        body.WakeUp();
        return true;
    }
}
