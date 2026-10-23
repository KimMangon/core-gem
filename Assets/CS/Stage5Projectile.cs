using Unity.Netcode;
using UnityEngine;

/// <summary>Despawns the summit projectile and gives its boulder hit some weight.</summary>
[RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(SphereCollider))]
public class Stage5Projectile : NetworkBehaviour, IDamageable
{
    [SerializeField] private float maxLifetime = 10f;
    [SerializeField] private float boulderImpactImpulse = 10f;
    [SerializeField] private int activeSection = 5;
    [SerializeField] private int boulderDamage;
    [SerializeField] private float slowSpeedThreshold = 2f;
    [SerializeField] private float slowDespawnSeconds = 3f;
    public int ActiveSection => activeSection;

    private Rigidbody body;
    private double expiresAt;
    private double slowSince = -1d;
    private readonly NetworkVariable<int> health = new(1);
    public int CurrentHealth => health.Value;

    public void RestoreHealth(int savedHealth)
    {
        if (IsServer && IsSpawned) health.Value = 1;
    }

    public void TakeDamage(int amount, ulong attackerClientId)
    {
        if (!IsServer || !IsSpawned || health.Value <= 0 || amount <= 0) return;
        health.Value = Mathf.Max(0, health.Value - amount);
        if (health.Value == 0) NetworkObject.Despawn(true);
    }

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
        {
            health.Value = 1;
            slowSince = -1d;
            expiresAt = NetworkManager.ServerTime.Time + maxLifetime;
        }
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer)
            return;

        StageManager stage = StageManager.Instance;
        double now = NetworkManager.ServerTime.Time;
        if (body.linearVelocity.sqrMagnitude <= slowSpeedThreshold * slowSpeedThreshold)
        {
            if (slowSince < 0d) slowSince = now;
        }
        else slowSince = -1d;
        if (NetworkManager.ServerTime.Time >= expiresAt || transform.position.y < -15f ||
            (slowSince >= 0d && now - slowSince >= slowDespawnSeconds) ||
            (stage != null && (stage.CurrentSection != activeSection || stage.RoomTimerFinished)))
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
        if (boulderDamage > 0)
            other.GetComponent<BoulderHealth>()?.TakeCannonDamage(this, boulderDamage);
        NetworkObject.Despawn();
    }
}
