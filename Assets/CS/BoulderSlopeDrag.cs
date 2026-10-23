using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Keeps the boulder controllable on level ground while letting gravity
/// accelerate it naturally on a slope.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BoulderSlopeDrag : NetworkBehaviour
{
    [SerializeField] private float flatLinearDamping = 1.25f;
    [SerializeField] private float flatAngularDamping = 0.8f;
    [SerializeField] private float slopeLinearDamping = 0.15f;
    [SerializeField] private float slopeAngularDamping = 0.08f;
    [SerializeField] private float minimumSlopeAngle = 5f;
    [SerializeField] private float baseDiameter = 2.4f;
    [SerializeField] private float sizePerPlayer = 1.1f;
    [SerializeField] private float soloSizeMultiplier = 1.6f;

    private Rigidbody body;
    private SphereCollider sphere;
    private int lastPlayerCount = -1;
    private bool trackingStageFour;
    private float lastStageFourSupportedHeight;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        sphere = GetComponent<SphereCollider>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetDamping(flatLinearDamping, flatAngularDamping);
            UpdateSizeForPlayers();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

        UpdateSizeForPlayers();

        float radius = sphere.radius * Mathf.Max(
            transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        bool grounded = Physics.Raycast(
            transform.position, Vector3.down, out RaycastHit ground,
            radius + 0.5f, ~0, QueryTriggerInteraction.Ignore);
        StageManager stage = StageManager.Instance;
        bool guardActive = stage != null && stage.IsGameStarted &&
            stage.CurrentSection == 4 && !stage.RoomTimerFinished;
        if (guardActive)
        {
            if (!trackingStageFour)
                lastStageFourSupportedHeight = body.position.y;
            // The normal spiral continuously supports the stone. A free fall of
            // ten metres skips a tier and must reset before it reaches the exit.
            if (lastStageFourSupportedHeight - body.position.y > 10f)
            {
                BoulderHealth health = GetComponent<BoulderHealth>();
                stage.RespawnBoulder(health);
                health?.ResetAfterFall();
                MonsterSpawnZone.ResetAllForRetry();
                lastStageFourSupportedHeight = body.position.y;
                trackingStageFour = false;
                return;
            }
            if (grounded) lastStageFourSupportedHeight = body.position.y;
        }
        trackingStageFour = guardActive;
        bool onSlope = !grounded || Vector3.Angle(ground.normal, Vector3.up) >= minimumSlopeAngle;

        SetDamping(
            onSlope ? slopeLinearDamping : flatLinearDamping,
            onSlope ? slopeAngularDamping : flatAngularDamping);
    }

    private void SetDamping(float linear, float angular)
    {
        body.linearDamping = linear;
        body.angularDamping = angular;
    }

    private void UpdateSizeForPlayers()
    {
        int playerCount = Mathf.Clamp(NetworkManager.ConnectedClientsIds.Count, 1, 2);
        if (playerCount == lastPlayerCount)
            return;

        lastPlayerCount = playerCount;
        float oldRadius = sphere.radius * transform.lossyScale.x;
        float diameter = baseDiameter * (playerCount == 1
            ? soloSizeMultiplier
            : sizePerPlayer * playerCount);
        transform.localScale = Vector3.one * diameter;
        float newRadius = sphere.radius * transform.lossyScale.x;

        // Keep the bottom of the sphere near the ground when its size changes.
        body.position += Vector3.up * (newRadius - oldRadius);
        Physics.SyncTransforms();
        body.WakeUp();
    }
}
