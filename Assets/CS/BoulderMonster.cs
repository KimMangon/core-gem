using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Server-controlled melee monster. Its sole attack target is the shared boulder.</summary>
[RequireComponent(typeof(NetworkObject), typeof(NavMeshAgent), typeof(Rigidbody))]
public class BoulderMonster : NetworkBehaviour, IDamageable
{
    [SerializeField] private Animator animator;
    [SerializeField, Min(1)] private int maximumHealth = 7;
    [SerializeField, Min(0)] private int healthPerAdditionalPlayer = 2;
    [SerializeField, Min(1)] private int attackDamage = 1;
    [SerializeField, Min(0.1f)] private float attackReach = 1.35f;
    [SerializeField, Min(0f)] private float strikeRangeTolerance = 0.65f;
    [SerializeField, Min(0.05f)] private float attackHitWindow = 0.3f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.8f;
    [SerializeField, Min(0.05f)] private float attackWindup = 0.45f;
    [SerializeField, Min(0.1f)] private float attackDuration = 1.2f;
    [SerializeField, Min(0.1f)] private float deathSeconds = 1.2f;
    [Header("Stationary artillery")]
    [SerializeField] private bool stationaryRanged;
    [SerializeField] private MonsterShell shellPrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(1f)] private float firingRange = 75f;
    [SerializeField, Min(1f)] private float shellSpeed = 18f;
    [Header("Charging hunter")]
    [SerializeField] private bool chargingHunter;
    [SerializeField] private float chargeRange = 13f;
    [SerializeField] private float chargeWindup = 0.8f;
    [SerializeField] private float chargeSpeed = 15f;
    [SerializeField] private float chargeDuration = 1.1f;
    [SerializeField] private float chargeRecovery = 1.7f;
    private byte chargePhase;
    private double chargePhaseUntil;
    private Vector3 chargeDirection;

    private const byte Idle = 0, Walking = 1, Attacking = 2, Dead = 3;
    private static readonly string[] AnimationStates = { "Idle", "Walk", "Attack", "Die" };
    private readonly NetworkVariable<int> health = new(7);
    private readonly NetworkVariable<int> scaledMaximumHealth = new(7);
    private readonly NetworkVariable<byte> state = new(Idle);
    private NavMeshAgent agent;
    private Collider[] colliders;
    private SphereCollider boulderCollider;
    private BoulderHealth target;
    private double attackStartedAt;
    private double nextAttackAt;
    private float nextPathUpdate;
    private bool attackApplied;
    private int savedHealth;
    private Renderer[] hitRenderers;
    private MaterialPropertyBlock[][] originalColors;
    private MaterialPropertyBlock flashColors;
    private bool hitFlashing;
    private float hitFlashUntil;
    private bool kickKnockback;
    private double kickRecoverAt;
    private float kickStartHeight;
    private Rigidbody kickBody;

    public void ApplyKickKnockback(Vector3 direction, float speed)
    {
        if (!IsServer || !IsSpawned || IsDead) return;
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (direction.sqrMagnitude < .1f) return;
        if (agent.enabled) agent.enabled = false;
        kickBody = GetComponent<Rigidbody>();
        kickBody.isKinematic = false;
        kickBody.useGravity = true;
        kickBody.constraints = RigidbodyConstraints.FreezeRotation;
        kickBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        kickBody.linearVelocity = direction * speed + Vector3.up * Mathf.Max(1.25f, kickBody.linearVelocity.y);
        kickBody.WakeUp();
        kickKnockback = true;
        kickRecoverAt = NetworkManager.ServerTime.Time + .4d;
        kickStartHeight = transform.position.y;
        chargePhase = 0;
        attackApplied = true;
        nextAttackAt = kickRecoverAt;
        SetState(Idle);
    }

    private void UpdateKickKnockback()
    {
        if (transform.position.y < kickStartHeight - 12f)
        {
            DefeatForStageCompletion();
            return;
        }
        if (NetworkManager.ServerTime.Time < kickRecoverAt || kickBody.linearVelocity.y > .2f) return;
        bool supported = false;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position + Vector3.up * .3f,
            Vector3.down, .65f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform) &&
                hit.collider.GetComponentInParent<PlayerMovement>() == null &&
                hit.collider.GetComponentInParent<BoulderMonster>() == null && hit.normal.y > .5f)
                supported = true;
        if (!supported) return;
        Vector3 position = transform.position;
        if (!stationaryRanged)
        {
            if (!NavMesh.SamplePosition(position, out NavMeshHit nav, .8f, agent.areaMask)) return;
            position = nav.position;
        }
        kickBody.linearVelocity = Vector3.zero;
        kickBody.angularVelocity = Vector3.zero;
        kickBody.isKinematic = true;
        kickBody.useGravity = false;
        kickBody.position = position;
        transform.position = position;
        if (!stationaryRanged)
        {
            agent.enabled = true;
            agent.Warp(position);
        }
        kickKnockback = false;
    }

    public int CurrentHealth => health.Value;
    public int MaximumHealth => scaledMaximumHealth.Value;
    public int HealthForPlayers(int players) =>
        maximumHealth + healthPerAdditionalPlayer * (Mathf.Clamp(players, 1, 4) - 1);
    public bool IsDead => state.Value == Dead;
    public int SpawnEntryIndex { get; set; }
    public int SpawnVariant { get; set; }
    public bool RequiresNavigation => !stationaryRanged;

    private void Awake()
    {
        flashColors = new MaterialPropertyBlock();
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
        GetComponent<Rigidbody>().isKinematic = true;
        GetComponent<Rigidbody>().useGravity = false;
        colliders = GetComponentsInChildren<Collider>(true);
        hitRenderers = GetComponentsInChildren<Renderer>(true);
        originalColors = new MaterialPropertyBlock[hitRenderers.Length][];
        for (int i = 0; i < hitRenderers.Length; i++)
        {
            originalColors[i] = new MaterialPropertyBlock[hitRenderers[i].sharedMaterials.Length];
            for (int slot = 0; slot < originalColors[i].Length; slot++)
                originalColors[i][slot] = new MaterialPropertyBlock();
        }
    }

    public void PrepareSavedHealth(int value) => savedHealth = value;

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += OnStateChanged;
        if (IsServer)
        {
            scaledMaximumHealth.Value = HealthForPlayers(RoomPlayerCount());
            health.Value = savedHealth > 0
                ? Mathf.Clamp(savedHealth, 1, scaledMaximumHealth.Value) : scaledMaximumHealth.Value;
            if (stationaryRanged)
                agent.enabled = false;
            else if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, agent.areaMask))
            {
                transform.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
            }
            else
            {
                Debug.LogWarning($"No monster NavMesh near {name}; despawning.", this);
                StartCoroutine(DespawnAfter(0f));
            }
        }
        PlayState(state.Value);
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= OnStateChanged;
        agent.enabled = false;
        StopAllCoroutines();
        RestoreHitColors();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || IsDead) return;
        int requiredHealth = HealthForPlayers(RoomPlayerCount());
        if (requiredHealth > scaledMaximumHealth.Value)
        {
            // Keep damage already dealt; leaving and rejoining cannot repeatedly heal a hunter.
            health.Value += requiredHealth - scaledMaximumHealth.Value;
            scaledMaximumHealth.Value = requiredHealth;
        }
        StageManager stage = StageManager.Instance;
        if (stage == null || !stage.IsGameStarted || stage.RoomTimerFinished)
        {
            NetworkObject.Despawn(true);
            return;
        }
        if (kickKnockback)
        {
            UpdateKickKnockback();
            return;
        }
        if (!stationaryRanged && (!agent.enabled || !agent.isOnNavMesh)) return;
        if (target == null)
        {
            target = BoulderHealth.Instance;
            if (target != null) boulderCollider = target.GetComponent<SphereCollider>();
        }
        if (target == null || boulderCollider == null || target.IsRecovering)
        {
            chargePhase = 0;
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            SetState(Idle);
            return;
        }

        double now = NetworkManager.ServerTime.Time;
        if (stationaryRanged)
        {
            UpdateArtillery(now);
            return;
        }
        if (chargingHunter)
        {
            UpdateChargingHunter(now);
            return;
        }
        if (state.Value == Attacking)
        {
            FaceBoulder();
            if (!attackApplied && now - attackStartedAt >= attackWindup)
            {
                if (now - attackStartedAt > attackWindup + attackHitWindow)
                    attackApplied = true;
                else if (CanStrikeBoulder(attackReach + strikeRangeTolerance))
                {
                    attackApplied = true;
                    target.TakeMonsterDamage(this, attackDamage);
                }
            }
            if (now - attackStartedAt >= attackDuration) SetState(Idle);
            return;
        }
        if (CanStrikeBoulder(attackReach))
        {
            agent.isStopped = true;
            FaceBoulder();
            if (now >= nextAttackAt)
            {
                attackStartedAt = now;
                nextAttackAt = now + Mathf.Max(attackInterval, attackDuration);
                attackApplied = false;
                SetState(Attacking);
            }
            else SetState(Idle);
            return;
        }

        agent.isStopped = false;
        SetState(Walking);
        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + 0.25f;
            Bounds sphere = boulderCollider.bounds;
            Vector3 feet = new Vector3(sphere.center.x, sphere.min.y + 0.1f, sphere.center.z);
            if (NavMesh.SamplePosition(feet, out NavMeshHit hit, 2f, agent.areaMask))
                agent.SetDestination(hit.position);
            else
            {
                agent.isStopped = true;
                SetState(Idle);
            }
        }
    }

    private bool CanStrikeBoulder(float reach)
    {
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        if (Vector3.Distance(origin, boulderCollider.ClosestPoint(origin)) > reach)
            return false;
        Vector3 direction = boulderCollider.bounds.center - origin;
        float firstDistance = float.PositiveInfinity;
        Collider first = null;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized,
            direction.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= firstDistance) continue;
            // Players and other hunters are not melee targets or shields for the shared boulder.
            if (hit.collider.GetComponentInParent<PlayerMovement>() != null ||
                hit.collider.GetComponentInParent<BoulderMonster>() != null) continue;
            firstDistance = hit.distance;
            first = hit.collider;
        }
        return first != null && first.GetComponentInParent<BoulderHealth>() == target;
    }

    private void UpdateChargingHunter(double now)
    {
        if (chargePhase == 1)
        {
            agent.isStopped = true;
            if (now < chargePhaseUntil) return;
            chargePhase = 2;
            chargePhaseUntil = now + chargeDuration;
            attackApplied = false;
        }
        if (chargePhase == 2)
        {
            // A locked direction gives players time to move the boulder aside.
            // NavMesh raycasts stop the dash at walls and the edge of the course.
            Vector3 step = chargeDirection * (chargeSpeed * Mathf.Min(Time.deltaTime, .05f));
            bool blocked = agent.Raycast(transform.position + step, out NavMeshHit wall);
            if (!blocked) agent.Move(step);
            if (!attackApplied && CanStrikeBoulder(attackReach + strikeRangeTolerance))
            {
                attackApplied = true;
                target.TakeMonsterDamage(this, attackDamage);
                Rigidbody rock = target.GetComponent<Rigidbody>();
                if (rock != null) rock.AddForce(chargeDirection * 7f, ForceMode.Impulse);
            }
            if (attackApplied || blocked || now >= chargePhaseUntil)
            {
                chargePhase = 3;
                chargePhaseUntil = now + chargeRecovery;
                SetState(Idle);
            }
            return;
        }
        if (chargePhase == 3)
        {
            agent.isStopped = true;
            if (now >= chargePhaseUntil) chargePhase = 0;
            return;
        }
        FaceBoulder();
        if (CanStrikeBoulder(chargeRange))
        {
            chargeDirection = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
            if (chargeDirection.sqrMagnitude < .1f) chargeDirection = transform.forward;
            transform.rotation = Quaternion.LookRotation(chargeDirection);
            agent.isStopped = true;
            agent.ResetPath();
            chargePhase = 1;
            chargePhaseUntil = now + chargeWindup;
            SetState(Attacking);
            TelegraphChargeRpc();
            return;
        }
        agent.isStopped = false;
        SetState(Walking);
        if (Time.time >= nextPathUpdate)
        {
            nextPathUpdate = Time.time + .25f;
            Vector3 feet = boulderCollider.bounds.center;
            feet.y = boulderCollider.bounds.min.y + .1f;
            if (NavMesh.SamplePosition(feet, out NavMeshHit hit, 3f, agent.areaMask))
                agent.SetDestination(hit.position);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void TelegraphChargeRpc()
    {
        StartCoroutine(ShowChargeWarning());
    }

    private IEnumerator ShowChargeWarning()
    {
        GameObject warning = new GameObject("ChargeWarning");
        warning.transform.SetParent(transform, false);
        warning.transform.localPosition = Vector3.up * .15f;
        LineRenderer line = warning.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 17;
        for (int i = 0; i < line.positionCount; i++)
        {
            float distance = Mathf.Lerp(1f, chargeRange, i / 16f);
            Vector3 point = transform.position + transform.forward * distance;
            if (NavMesh.SamplePosition(point, out NavMeshHit surface, chargeRange * .4f, NavMesh.AllAreas))
                point = surface.position;
            line.SetPosition(i, warning.transform.InverseTransformPoint(point + Vector3.up * .15f));
        }
        line.startWidth = .35f; line.endWidth = 1f;
        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = new Color(1f, .45f, .05f, .8f);
        line.sharedMaterial = material;
        Destroy(warning, chargeWindup);
        Destroy(material, chargeWindup);
        yield break;
    }

    private void UpdateArtillery(double now)
    {
        FaceBoulder();
        if (state.Value == Attacking)
        {
            if (!attackApplied && now - attackStartedAt >= attackWindup)
            {
                attackApplied = true;
                FireShell();
            }
            if (now - attackStartedAt >= attackDuration) SetState(Idle);
            return;
        }
        if (shellPrefab != null && now >= nextAttackAt &&
            Vector3.Distance(transform.position, target.transform.position) <= firingRange)
        {
            attackStartedAt = now;
            nextAttackAt = now + Mathf.Max(attackInterval, attackDuration);
            attackApplied = false;
            SetState(Attacking);
        }
    }

    private void FireShell()
    {
        if (shellPrefab == null || target == null || target.IsRecovering) return;
        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1.2f;
        Vector3 aim = boulderCollider.bounds.center;
        float flight = Mathf.Clamp(Vector3.Distance(origin, aim) / shellSpeed, 0.5f, 4f);
        Rigidbody rock = target.GetComponent<Rigidbody>();
        if (rock != null)
            aim += Vector3.ClampMagnitude(rock.linearVelocity * Mathf.Min(flight, 0.5f), 2.5f);
        Vector3 velocity = (aim - origin - 0.5f * MonsterShell.Gravity * flight * flight) / flight;
        MonsterShell shell = Instantiate(shellPrefab, origin, Quaternion.LookRotation(velocity));
        shell.Launch(this, velocity, attackDamage);
        shell.NetworkObject.Spawn(true);
    }

    private void FaceBoulder()
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 360f * Time.deltaTime);
    }

    private int RoomPlayerCount()
    {
        int difficultyPlayers = StageManager.Instance != null
            ? StageManager.Instance.ClearPlayerCount : 0;
        return difficultyPlayers > 0 ? difficultyPlayers : NetworkManager.ConnectedClientsIds.Count;
    }

    public void TakeDamage(int amount, ulong attackerClientId)
    {
        if (!IsServer || !IsSpawned || IsDead || amount <= 0) return;
        health.Value = Mathf.Max(0, health.Value - amount);
        FlashHitRpc();
        if (health.Value > 0) return;
        Die();
    }

    public void DefeatForStageCompletion()
    {
        if (!IsServer || !IsSpawned || IsDead) return;
        health.Value = 0;
        Die();
    }

    private void Die()
    {
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        agent.enabled = false;
        SetState(Dead);
        StartCoroutine(DespawnAfter(deathSeconds));
    }

    private void SetState(byte value)
    {
        if (state.Value != value) state.Value = value;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void FlashHitRpc()
    {
        for (int i = 0; i < hitRenderers.Length; i++)
        {
            if (hitRenderers[i] == null) continue;
            for (int slot = 0; slot < originalColors[i].Length; slot++)
            {
                if (!hitFlashing)
                    hitRenderers[i].GetPropertyBlock(originalColors[i][slot], slot);
                hitRenderers[i].GetPropertyBlock(flashColors, slot);
                flashColors.SetColor("_BaseColor", new Color(1f, 0.06f, 0.04f));
                flashColors.SetColor("_Color", new Color(1f, 0.06f, 0.04f));
                hitRenderers[i].SetPropertyBlock(flashColors, slot);
            }
        }
        hitFlashing = true;
        hitFlashUntil = Time.unscaledTime + 0.15f;
    }

    private void LateUpdate()
    {
        if (hitFlashing && Time.unscaledTime >= hitFlashUntil)
            RestoreHitColors();
    }

    private void RestoreHitColors()
    {
        if (!hitFlashing) return;
        for (int i = 0; i < hitRenderers.Length; i++)
            if (hitRenderers[i] != null)
                for (int slot = 0; slot < originalColors[i].Length; slot++)
                    hitRenderers[i].SetPropertyBlock(originalColors[i][slot], slot);
        hitFlashing = false;
    }

    private void OnStateChanged(byte previous, byte current) => PlayState(current);

    private void PlayState(byte value)
    {
        if (value == Dead)
            foreach (Collider item in colliders)
                if (item != null) item.enabled = false;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.CrossFadeInFixedTime(AnimationStates[Mathf.Clamp(value, 0, 3)], 0.08f, 0, 0f);
    }

    private IEnumerator DespawnAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (IsServer && IsSpawned) NetworkObject.Despawn(true);
    }
}
