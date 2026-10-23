using Unity.Netcode;
using UnityEngine;

/// <summary>Server-simulated ballistic shell; damages only the boulder and stops on scenery.</summary>
[RequireComponent(typeof(NetworkObject), typeof(Rigidbody), typeof(SphereCollider))]
public class MonsterShell : NetworkBehaviour
{
    public static readonly Vector3 Gravity = new Vector3(0f, -20f, 0f);
    [SerializeField, Min(0.05f)] private float sweepRadius = 0.22f;
    [SerializeField, Min(1f)] private float lifetime = 8f;
    private Rigidbody body;
    private BoulderMonster shooter;
    private Vector3 velocity;
    private int damage;
    private float expiresAt;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        GetComponent<SphereCollider>().isTrigger = true;
    }

    public void Launch(BoulderMonster owner, Vector3 initialVelocity, int hitDamage)
    {
        shooter = owner;
        velocity = initialVelocity;
        damage = hitDamage;
    }

    public override void OnNetworkSpawn() => expiresAt = Time.time + lifetime;

    private void FixedUpdate()
    {
        if (!IsServer || !IsSpawned) return;
        if (Time.time >= expiresAt || shooter == null || !shooter.IsSpawned || shooter.IsDead ||
            StageManager.Instance == null || !StageManager.Instance.IsGameStarted ||
            StageManager.Instance.RoomTimerFinished)
        {
            NetworkObject.Despawn(true);
            return;
        }
        float dt = Time.fixedDeltaTime;
        Vector3 step = velocity * dt + Gravity * (0.5f * dt * dt);
        Collider first = null;
        float firstDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.SphereCastAll(body.position, sweepRadius,
            step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.GetComponentInParent<BoulderMonster>() != null ||
                hit.collider.GetComponentInParent<PlayerMovement>() != null ||
                hit.distance >= firstDistance) continue;
            first = hit.collider;
            firstDistance = hit.distance;
        }
        if (first != null)
        {
            first.GetComponentInParent<BoulderHealth>()?.TakeMonsterDamage(shooter, damage);
            NetworkObject.Despawn(true);
            return;
        }
        velocity += Gravity * dt;
        body.MovePosition(body.position + step);
        if (velocity.sqrMagnitude > 0.01f) body.MoveRotation(Quaternion.LookRotation(velocity));
    }

    public static void ClearAll()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;
        foreach (MonsterShell shell in FindObjectsByType<MonsterShell>(FindObjectsSortMode.None))
            if (shell.IsSpawned) shell.NetworkObject.Despawn(true);
    }
}
