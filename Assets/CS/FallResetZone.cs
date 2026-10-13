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

        NetworkObject networkObject = target.GetComponent<NetworkObject>();
        NetworkTransform networkTransform = target.GetComponent<NetworkTransform>();
        if (networkObject == null || !networkObject.IsSpawned || networkTransform == null)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        Vector3 destination = target.GetComponent<BoulderSlopeDrag>() != null
            ? boulderRespawnPosition
            : respawnPosition;
        body.position = destination;
        networkTransform.Teleport(destination, body.rotation, target.transform.localScale);
        body.WakeUp();
    }
}
