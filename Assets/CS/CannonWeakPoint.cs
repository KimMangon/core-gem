using UnityEngine;
using Unity.Netcode;

/// <summary>Gun target; health and cannon state are owned by the shared controller.</summary>
public class CannonWeakPoint : MonoBehaviour, IDamageable
{
    [SerializeField] private int cannonIndex;
    [SerializeField] private float maximumHorizontalRange = 15f;
    public bool CanBeHitBy(ulong attackerClientId)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer ||
            !network.ConnectedClients.TryGetValue(attackerClientId, out NetworkClient client) ||
            client.PlayerObject == null) return false;
        BoulderHealth boulder = BoulderHealth.Instance;
        if (boulder == null || !boulder.IsSpawned || boulder.IsRecovering) return false;
        Vector3 difference = boulder.transform.position - transform.position;
        return difference.x * difference.x + difference.z * difference.z <=
            maximumHorizontalRange * maximumHorizontalRange;
    }
    public void TakeDamage(int amount, ulong attackerClientId)
    {
        if (!CanBeHitBy(attackerClientId)) return;
        GetComponentInParent<Stage8CannonControls>()?.Hit(cannonIndex, amount);
    }
}
