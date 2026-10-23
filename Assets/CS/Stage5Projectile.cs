using Unity.Netcode;
using UnityEngine;

/// <summary>Despawns the summit projectile and gives its boulder hit some weight.</summary>
[RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(SphereCollider))]
public class Stage5Projectile : NetworkBehaviour
{
    [SerializeField] private float maxLifetime = 10f;
    [SerializeField] private float boulderImpactImpulse = 10f;

    private Rigidbody body;
    private double expiresAt;

    public double RemainingLifetime => IsServer && IsSpawned
        ? System.Math.Max(0d, expiresAt - NetworkManager.ServerTime.Time) : 0d;

    public void RestoreLifetime(double seconds)
    {
        if (IsServer && IsSpawned)
            expiresAt = NetworkManager.ServerTime.Time +
                System.Math.Min(maxLifetime, System.Math.Max(0d, seconds));
    }

    public override void OnNetworkSpawn()
    {
        body = GetComponent<Rigidbody>();
        if (IsServer)
            expiresAt = NetworkManager.ServerTime.Time + maxLifetime;
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer)
            return;

        if (NetworkManager.ServerTime.Time >= expiresAt || transform.position.y < -15f)
            NetworkObject.Despawn();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsSpawned || !IsServer || body == null)
            return;

        Rigidbody other = collision.rigidbody;
        if (other == null || other.GetComponent<BoulderSlopeDrag>() == null)
            return;

        Vector3 direction = body.linearVelocity.sqrMagnitude > 0.01f
            ? body.linearVelocity.normalized
            : -transform.forward;
        other.AddForce(direction * boulderImpactImpulse, ForceMode.Impulse);
        NetworkObject.Despawn();
    }
}
