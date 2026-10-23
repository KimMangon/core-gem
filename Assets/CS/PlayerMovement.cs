using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Collections;
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
    private const float BoulderContactAccelerationMultiplier = 0.25f;
    private const float KickCooldown = 0.8f;
    private const float KickDuration = 0.38f;
    private const float KickPlayerSpeed = 4.6f;
    private const float KickBoulderImpulse = 11f;

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

    private readonly NetworkVariable<FixedString64Bytes> playerName = new(
        new FixedString64Bytes("PLAYER"),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<FixedString64Bytes> persistentPlayerId = new(
        new FixedString64Bytes(),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private Rigidbody body;
    private CapsuleCollider capsule;
    private PlayerLook playerLook;
    private float boulderContactUntil = -1f;
    private readonly Collider[] kickHits = new Collider[16];
    private Transform kickLegPivot;
    private AudioSource kickSoundSource;
    private AudioClip kickSound;
    private float kickAnimationStarted = -10f;
    private float nextKickInputTime;
    private float nextKickServerTime;

    public string DisplayName => playerName.Value.ToString();
    public string PersistentPlayerId => persistentPlayerId.Value.ToString();
    public Color32 AvatarColor => PlayerColorPalette.Unpack(avatarColor.Value);
    private bool jumpRequested;
    private float nextJumpTime;
    private float boulderKnockbackUntil;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        playerLook = GetComponent<PlayerLook>();
        CreateKickLeg();
        kickSound = Resources.Load<AudioClip>("Sfx/Melee1");
        GameObject kickAudioObject = new GameObject("KickSfxPlayer");
        kickAudioObject.transform.SetParent(transform, false);
        kickSoundSource = kickAudioObject.AddComponent<AudioSource>();
        kickSoundSource.playOnAwake = false;
        kickSoundSource.spatialBlend = 0.8f;
        kickSoundSource.minDistance = 2f;
        kickSoundSource.maxDistance = 16f;
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
            RefreshOwnerProfile();
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

    public void RefreshOwnerProfile()
    {
        if (!IsOwner || !IsSpawned)
            return;

        playerName.Value = new FixedString64Bytes(PlayerProfile.Name);
        persistentPlayerId.Value = new FixedString64Bytes(PlayerProfile.Id);
        avatarColor.Value = PlayerColorPalette.Pack(
            PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex]);
    }

    public void RestoreOwnerView(float yaw, float pitch, uint color)
    {
        if (IsServer && IsSpawned)
            RestoreOwnerViewRpc(yaw, pitch, color);
    }

    [Rpc(SendTo.Owner)]
    private void RestoreOwnerViewRpc(float yaw, float pitch, uint color)
    {
        if (playerLook == null)
            playerLook = GetComponent<PlayerLook>();
        playerLook?.RestoreView(yaw, pitch);
        if (color != 0)
        {
            avatarColor.Value = color;
            for (int i = 0; i < PlayerColorPalette.Colors.Length; i++)
                if (PlayerColorPalette.Pack(PlayerColorPalette.Colors[i]) == color)
                {
                    PlayerColorPalette.SelectedIndex = i;
                    break;
                }
        }
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
        kickSoundSource.volume = GameSettingsPreferences.SfxVolume;
        AnimateKickLeg();
        if (!IsOwner || Keyboard.current == null)
            return;

        StageManager stageManager = StageManager.Instance;
        if (stageManager == null || !stageManager.IsGameStarted)
        {
            requestedMove.Value = Vector2.zero;
            requestedSprint.Value = false;
            return;
        }

        Vector2 input = new Vector2(
            (GameKeyBindings.IsHeld(PlayerAction.MoveRight) ? 1f : 0f) -
                (GameKeyBindings.IsHeld(PlayerAction.MoveLeft) ? 1f : 0f),
            (GameKeyBindings.IsHeld(PlayerAction.MoveForward) ? 1f : 0f) -
                (GameKeyBindings.IsHeld(PlayerAction.MoveBackward) ? 1f : 0f));

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
            GameKeyBindings.IsHeld(PlayerAction.Sprint);
        if (requestedSprint.Value != sprintHeld)
            requestedSprint.Value = sprintHeld;

        if (!PauseManager.IsPaused &&
            GameKeyBindings.WasPressedThisFrame(PlayerAction.Jump))
            RequestJumpServerRpc();
        if (!PauseManager.IsPaused && Time.time >= nextKickInputTime &&
            GameKeyBindings.WasPressedThisFrame(PlayerAction.Kick))
        {
            nextKickInputTime = Time.time + KickCooldown;
            BeginKickAnimation();
            RequestKickServerRpc();
        }
        if (!PauseManager.IsPaused &&
            GameKeyBindings.WasPressedThisFrame(PlayerAction.Respawn))
            RequestRespawnServerRpc();
    }

    [ServerRpc]
    private void RequestRespawnServerRpc()
    {
        jumpRequested = false;
        boulderContactUntil = -1f;
        boulderKnockbackUntil = -1f;
        StageManager.Instance?.RespawnPlayer(this);
    }

    private void CreateKickLeg()
    {
        GameObject pivot = new GameObject("KickLegPivot");
        kickLegPivot = pivot.transform;
        kickLegPivot.SetParent(transform, false);
        kickLegPivot.localPosition = new Vector3(0.26f, -0.38f, 0.12f);

        Material bodyMaterial = GetComponent<MeshRenderer>()?.sharedMaterial;
        GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        leg.name = "KickLeg";
        leg.transform.SetParent(kickLegPivot, false);
        leg.transform.localPosition = new Vector3(0f, -0.37f, 0f);
        leg.transform.localScale = new Vector3(0.13f, 0.4f, 0.13f);
        leg.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
        Destroy(leg.GetComponent<Collider>());

        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foot.name = "KickFoot";
        foot.transform.SetParent(kickLegPivot, false);
        foot.transform.localPosition = new Vector3(0f, -0.77f, 0.12f);
        foot.transform.localScale = new Vector3(0.28f, 0.17f, 0.4f);
        foot.GetComponent<MeshRenderer>().sharedMaterial = bodyMaterial;
        Destroy(foot.GetComponent<Collider>());
        pivot.SetActive(false);
    }

    private void BeginKickAnimation()
    {
        if (Time.time - kickAnimationStarted < KickDuration)
            return;
        kickAnimationStarted = Time.time;
        kickLegPivot.gameObject.SetActive(true);
        if (kickSound != null)
            kickSoundSource.PlayOneShot(kickSound, 0.7f);
    }

    private void AnimateKickLeg()
    {
        if (kickLegPivot == null || !kickLegPivot.gameObject.activeSelf)
            return;

        float progress = (Time.time - kickAnimationStarted) / KickDuration;
        if (progress >= 1f)
        {
            kickLegPivot.gameObject.SetActive(false);
            return;
        }

        float extension = progress < 0.38f
            ? progress / 0.38f : 1f - (progress - 0.38f) / 0.62f;
        kickLegPivot.localRotation = Quaternion.Euler(-76f * Mathf.SmoothStep(0f, 1f, extension), 0f, 0f);
    }

    [ServerRpc]
    private void RequestKickServerRpc()
    {
        if (Time.time < nextKickServerTime || StageManager.Instance == null ||
            !StageManager.Instance.IsGameStarted || StageManager.Instance.RoomTimerFinished)
            return;

        nextKickServerTime = Time.time + KickCooldown;
        Vector3 direction = Quaternion.Euler(0f, requestedYaw.Value, 0f) * Vector3.forward;
        PlayKickAnimationRpc();
        StartCoroutine(ResolveKickAfterWindup(direction));
    }

    [Rpc(SendTo.Everyone)]
    private void PlayKickAnimationRpc()
    {
        BeginKickAnimation();
    }

    private IEnumerator ResolveKickAfterWindup(Vector3 direction)
    {
        yield return new WaitForSeconds(0.13f);
        if (!IsServer || body == null || StageManager.Instance == null ||
            !StageManager.Instance.IsGameStarted)
            yield break;

        Vector3 kickCenter = body.position + direction * 1.1f + Vector3.down * 0.35f;
        int count = Physics.OverlapSphereNonAlloc(kickCenter, 0.85f, kickHits,
            ~0, QueryTriggerInteraction.Ignore);
        Rigidbody target = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = kickHits[i];
            Rigidbody candidateBody = candidate != null ? candidate.attachedRigidbody : null;
            if (candidateBody == null || candidateBody == body || candidateBody.isKinematic ||
                (candidateBody.GetComponent<PlayerMovement>() == null &&
                 candidateBody.GetComponent<BoulderSlopeDrag>() == null))
                continue;

            Vector3 toTarget = candidateBody.worldCenterOfMass - body.worldCenterOfMass;
            toTarget.y = 0f;
            if (Vector3.Dot(direction, toTarget) <= 0.15f)
                continue;

            float distance = (candidate.ClosestPoint(kickCenter) - kickCenter).sqrMagnitude;
            if (distance >= nearestDistance)
                continue;
            nearestDistance = distance;
            target = candidateBody;
        }

        if (target == null)
            yield break;

        PlayerMovement otherPlayer = target.GetComponent<PlayerMovement>();
        if (otherPlayer != null)
            otherPlayer.ApplyKickKnockback(direction);
        else
            target.AddForce(direction * KickBoulderImpulse + Vector3.up * 2.5f,
                ForceMode.Impulse);
    }

    private void ApplyKickKnockback(Vector3 direction)
    {
        if (!IsServer || body == null)
            return;

        Vector3 velocity = body.linearVelocity;
        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        float forwardSpeed = Vector3.Dot(horizontal, direction);
        horizontal += direction * Mathf.Max(0f, KickPlayerSpeed - forwardSpeed);
        body.linearVelocity = new Vector3(horizontal.x, Mathf.Max(velocity.y, 1.25f), horizontal.z);
        body.WakeUp();
        boulderKnockbackUntil = Time.time + 0.4f;
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

        StageManager stageManager = StageManager.Instance;
        if (stageManager == null || !stageManager.IsGameStarted)
        {
            Vector3 idleVelocity = body.linearVelocity;
            body.linearVelocity = new Vector3(0f, idleVelocity.y, 0f);
            return;
        }

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
        bool touchingBoulder = Time.time < boulderContactUntil;
        if (direction.sqrMagnitude > 0f)
            body.AddForce(direction * (moveAcceleration * speedMultiplier *
                (touchingBoulder ? BoulderContactAccelerationMultiplier : 1f)),
                ForceMode.Acceleration);

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
        RecordBoulderContact(collision);
        TryApplyBoulderKnockback(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        RecordBoulderContact(collision);
        TryApplyBoulderKnockback(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (IsServer && collision.rigidbody != null &&
            collision.rigidbody.GetComponent<BoulderSlopeDrag>() != null)
            boulderContactUntil = -1f;
    }

    private void RecordBoulderContact(Collision collision)
    {
        if (IsServer && collision.rigidbody != null &&
            collision.rigidbody.GetComponent<BoulderSlopeDrag>() != null)
            boulderContactUntil = Time.time + Time.fixedDeltaTime * 1.5f;
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
