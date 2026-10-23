using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A red course hazard that disappears after three hits and returns seven seconds later.</summary>
public class ShootableObstacle : MonoBehaviour, IDamageable
{
    [SerializeField] private string obstacleId;
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private float respawnSeconds = 7f;

    private static readonly Dictionary<string, ShootableObstacle> ById = new();
    private Renderer[] renderers;
    private Collider[] colliders;
    private bool[] rendererWasEnabled;
    private bool[] colliderWasEnabled;
    private Behaviour movement;
    private bool movementWasEnabled;
    private int health;
    private bool disabled;

    public string ObstacleId => obstacleId;
    public bool IsDisabled => disabled;

    public static ShootableObstacle Find(string id) =>
        !string.IsNullOrEmpty(id) && ById.TryGetValue(id, out ShootableObstacle obstacle)
            ? obstacle : null;

    private void Awake()
    {
        health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rendererWasEnabled = new bool[renderers.Length];
        colliderWasEnabled = new bool[colliders.Length];
        movement = GetComponent<PushingStageObstacle>();
        if (movement == null) movement = GetComponent<SwingingStageObstacle>();
        if (movement == null) movement = GetComponent<Stage5CompressionGate>();
        if (movement == null) movement = GetComponent<RotatingStageObstacle>();
    }

    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(obstacleId))
            ById[obstacleId] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(obstacleId) && ById.TryGetValue(obstacleId, out var current) &&
            current == this)
            ById.Remove(obstacleId);
    }

    public void TakeDamage(int amount, ulong attackerClientId)
    {
        if (disabled || amount <= 0)
            return;
        health = Mathf.Max(0, health - amount);
        if (health == 0)
            DisableTemporarily();
    }

    public void DisableTemporarily()
    {
        if (disabled)
            return;
        disabled = true;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
            {
                rendererWasEnabled[i] = renderers[i].enabled;
                renderers[i].enabled = false;
            }
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null)
            {
                colliderWasEnabled[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }
        if (movement != null)
        {
            movementWasEnabled = movement.enabled;
            // Keep its synchronized motion running while invisible. Resuming a frozen
            // hazard would otherwise jump across several seconds of its cycle.
        }
        StartCoroutine(RestoreLater());
    }

    private IEnumerator RestoreLater()
    {
        yield return new WaitForSeconds(respawnSeconds);
        // Disabled colliders have empty world bounds, so derive the occupied volume
        // from their local shape without briefly enabling them inside a player.
        while (RespawnSpaceOccupied())
            yield return new WaitForFixedUpdate();
        health = maxHealth;
        if (movement != null)
            movement.enabled = movementWasEnabled;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = rendererWasEnabled[i];
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderWasEnabled[i];
        disabled = false;
    }

    private bool RespawnSpaceOccupied()
    {
        Physics.SyncTransforms();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider shape = colliders[i];
            if (shape == null || !colliderWasEnabled[i] || shape.isTrigger) continue;
            Bounds local;
            if (shape is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (shape is SphereCollider sphere)
                local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
            else if (shape is CapsuleCollider capsule)
            {
                Vector3 size = Vector3.one * capsule.radius * 2f;
                size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2f);
                local = new Bounds(capsule.center, size);
            }
            else if (shape is MeshCollider mesh && mesh.sharedMesh != null)
                local = mesh.sharedMesh.bounds;
            else continue;
            Transform part = shape.transform;
            Vector3 scale = part.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            Vector3 half = Vector3.Scale(local.extents, scale) + Vector3.one * 0.15f;
            foreach (Collider other in Physics.OverlapBox(part.TransformPoint(local.center),
                half, part.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.transform.IsChildOf(transform)) continue;
                if (other.GetComponentInParent<PlayerMovement>() != null ||
                    other.GetComponentInParent<BoulderHealth>() != null)
                    return true;
            }
        }
        return false;
    }
}
