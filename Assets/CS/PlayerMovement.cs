using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner-submitted movement input is simulated by the server so player collisions
/// can interact consistently with future server-authoritative physics objects.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveAcceleration = 80f;
    [SerializeField] private float maxHorizontalSpeed = 5f;
    [SerializeField] private float sprintMultiplier = 1.5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float boulderImpactThreshold = 4f;
    [SerializeField] private float boulderKnockbackMaxSpeed = 2.4f;
    [SerializeField] private float boulderKnockbackDuration = 0.3f;
    [SerializeField] private float boulderKnockbackDeceleration = 8f;

    private readonly NetworkVariable<Vector2> requestedMove = new(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> requestedYaw = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> requestedSprint = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<uint> avatarColor = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private Rigidbody body;
    private CapsuleCollider capsule;
    private PlayerLook playerLook;
    private bool jumpRequested;
    private float nextJumpTime;
    private float boulderKnockbackUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerLook = GetComponent<PlayerLook>();
    }

    public override void OnNetworkSpawn()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();

        // The server owns physics simulation. Other peers display the replicated
        // NetworkTransform without running a second copy of the physics body.
        body.isKinematic = !IsServer;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.linearDamping = 0.8f;

        avatarColor.OnValueChanged += OnAvatarColorChanged;
        if (IsOwner)
            avatarColor.Value = PlayerColorPalette.Pack(
                PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex]);
        ApplyAvatarColor(avatarColor.Value);
    }

    public override void OnNetworkDespawn()
    {
        avatarColor.OnValueChanged -= OnAvatarColorChanged;
    }

    private void OnAvatarColorChanged(uint previous, uint current)
    {
        ApplyAvatarColor(current);
    }

    private void ApplyAvatarColor(uint packed)
    {
        if (packed == 0)
            return;

        Color color = PlayerColorPalette.Unpack(packed);
        var properties = new MaterialPropertyBlock();
        PlayerEyes eyes = GetComponent<PlayerEyes>();
        foreach (MeshRenderer mesh in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (eyes != null && eyes.ContainsEye(mesh.transform))
                continue;
            mesh.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            mesh.SetPropertyBlock(properties);
        }
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null)
            return;

        Keyboard keyboard = Keyboard.current;
        Vector2 input = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

        if (input.sqrMagnitude > 1f)
            input.Normalize();
        if (PauseManager.IsPaused)
            input = Vector2.zero;

        // The current camera yaw defines forward/back/left/right for each player.
        float lookYaw = playerLook != null ? playerLook.Yaw : transform.eulerAngles.y;
        Quaternion yawRotation = Quaternion.Euler(0f, lookYaw, 0f);
        Vector3 worldDirection = yawRotation * new Vector3(input.x, 0f, input.y);

        requestedMove.Value = new Vector2(worldDirection.x, worldDirection.z);
        requestedYaw.Value = lookYaw;
        bool sprintHeld = !PauseManager.IsPaused &&
            (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        if (requestedSprint.Value != sprintHeld)
            requestedSprint.Value = sprintHeld;

        if (keyboard.spaceKey.wasPressedThisFrame)
            RequestJumpServerRpc();
    }

    [ServerRpc]
    private void RequestJumpServerRpc()
    {
        jumpRequested = true;
    }

    private void FixedUpdate()
    {
        if (!IsServer || body == null)
            return;

        if (jumpRequested)
        {
            jumpRequested = false;
            if (Time.time >= nextJumpTime && IsGrounded())
            {
                Vector3 velocityBeforeJump = body.linearVelocity;
                if (velocityBeforeJump.y < 0f)
                    body.linearVelocity = new Vector3(
                        velocityBeforeJump.x, 0f, velocityBeforeJump.z);

                float jumpSpeed = Mathf.Sqrt(2f * Physics.gravity.magnitude * jumpHeight);
                body.AddForce(Vector3.up * jumpSpeed, ForceMode.VelocityChange);
                nextJumpTime = Time.time + 0.2f;
            }
        }

        body.MoveRotation(Quaternion.Euler(0f, requestedYaw.Value, 0f));

        Vector3 direction = new Vector3(requestedMove.Value.x, 0f, requestedMove.Value.y);
        if (direction.sqrMagnitude > 1f)
            direction.Normalize();

        float speedMultiplier = requestedSprint.Value ? sprintMultiplier : 1f;
        if (direction.sqrMagnitude > 0f)
            body.AddForce(direction * (moveAcceleration * speedMultiplier), ForceMode.Acceleration);

        Vector3 velocity = body.linearVelocity;
        if (direction.sqrMagnitude == 0f && IsGrounded())
        {
            // Keep a brief shove from a strong boulder impact, but retain the
            // immediate stop for ordinary movement when the keys are released.
            Vector3 horizontal = Time.time < boulderKnockbackUntil
                ? Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z),
                    Vector3.zero, boulderKnockbackDeceleration * Time.fixedDeltaTime)
                : Vector3.zero;
            body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
            return;
        }

        Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float speedLimit = maxHorizontalSpeed * speedMultiplier;
        if (Time.time < boulderKnockbackUntil)
            speedLimit += boulderKnockbackMaxSpeed;
        if (horizontalVelocity.sqrMagnitude > speedLimit * speedLimit)
        {
            horizontalVelocity = horizontalVelocity.normalized * speedLimit;
            body.linearVelocity = new Vector3(horizontalVelocity.x, velocity.y, horizontalVelocity.z);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyBoulderKnockback(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryApplyBoulderKnockback(collision);
    }

    private void TryApplyBoulderKnockback(Collision collision)
    {
        if (!IsServer || body == null || Time.time < boulderKnockbackUntil)
            return;

        Rigidbody otherBody = collision.rigidbody;
        if (otherBody == null || otherBody.GetComponent<BoulderSlopeDrag>() == null)
            return;

        float impact = collision.impulse.magnitude;
        if (impact < boulderImpactThreshold)
            return;

        Vector3 away = body.position - otherBody.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f && collision.contactCount > 0)
        {
            away = collision.GetContact(0).normal;
            away.y = 0f;
        }
        if (away.sqrMagnitude < 0.01f)
            away = transform.right;
        away.Normalize();

        float shoveSpeed = Mathf.Clamp(
            0.7f + (impact - boulderImpactThreshold) / Mathf.Max(body.mass, 0.01f) * 0.15f,
            0.7f, boulderKnockbackMaxSpeed);
        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float alongAway = Vector3.Dot(horizontal, away);
        horizontal += away * (Mathf.Max(alongAway, shoveSpeed) - alongAway);
        body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        body.WakeUp();
        boulderKnockbackUntil = Time.time + boulderKnockbackDuration;
    }

    private bool IsGrounded()
    {
        Vector3 origin = transform.TransformPoint(capsule.center);
        float halfHeight = capsule.height * transform.lossyScale.y * 0.5f;
        return Physics.Raycast(origin, Vector3.down, out RaycastHit ground,
                   halfHeight + 0.25f, ~0, QueryTriggerInteraction.Ignore)
               && ground.collider.attachedRigidbody != body
               && Vector3.Dot(ground.normal, Vector3.up) > 0.5f;
    }
}
