using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Shows the simple avatar to other players while keeping the owner's view clear.
/// The single visible arm follows the already-networked hand transform.
/// </summary>
public class PlayerVisual : NetworkBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Transform shoulder;
    [SerializeField] private Transform upperArm;
    [SerializeField] private Transform lowerArm;
    [SerializeField] private Transform elbow;
    [SerializeField] private Transform hand;
    private MeshRenderer[] handRenderers;
    private PlayerArm playerArm;

    public override void OnNetworkSpawn()
    {
        if (visualRoot != null)
            visualRoot.SetActive(!IsOwner);
        if (hand != null)
            handRenderers = hand.GetComponentsInChildren<MeshRenderer>(true);
        playerArm = GetComponent<PlayerArm>();
    }

    private void LateUpdate()
    {
        if (IsSpawned && handRenderers != null && playerArm != null)
        {
            bool showHand = IsOwner || playerArm.IsPushing || playerArm.IsGesturing;
            foreach (var renderer in handRenderers)
                if (renderer != null) renderer.enabled = showHand;
        }
        if (!IsSpawned || IsOwner || shoulder == null || upperArm == null ||
            lowerArm == null || elbow == null || hand == null)
            return;

        Vector3 span = hand.position - shoulder.position;
        if (span.sqrMagnitude < 0.0001f)
            return;

        Vector3 elbowPosition = shoulder.position + span * 0.52f - transform.forward * 0.055f;
        elbow.position = elbowPosition;
        PlaceRod(upperArm, shoulder.position, elbowPosition, 0.048f);
        PlaceRod(lowerArm, elbowPosition, hand.position, 0.043f);
    }

    private static void PlaceRod(Transform rod, Vector3 start, Vector3 end, float radius)
    {
        Vector3 direction = end - start;
        rod.position = (start + end) * 0.5f;
        rod.rotation = Quaternion.FromToRotation(Vector3.up, direction);
        rod.localScale = new Vector3(radius, direction.magnitude * 0.5f, radius);
    }
}
