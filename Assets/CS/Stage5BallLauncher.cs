using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fires server-owned stone balls downhill from the fifth-stage summit.</summary>
public class Stage5BallLauncher : MonoBehaviour
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Vector3 downhillDirection = new(-0.2f, 0f, -1f);
    [SerializeField] private float shotInterval = 3.5f;
    [SerializeField] private float launchSpeed = 18f;
    [SerializeField] private float yawSpreadDegrees = 16f;
    [SerializeField] private Vector2 upwardPitchRange = new(7f, 13f);

    private double nextShotAt;

    private void OnEnable()
    {
        nextShotAt = 0d;
    }

    private void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening || !network.IsServer ||
            projectilePrefab == null || muzzle == null)
            return;

        double now = network.ServerTime.Time;
        if (nextShotAt <= 0d)
            nextShotAt = now + 2d;
        if (now < nextShotAt)
            return;

        nextShotAt = now + Mathf.Max(0.5f, shotInterval);
        float yaw = Random.Range(-yawSpreadDegrees, yawSpreadDegrees);
        float pitch = Random.Range(upwardPitchRange.x, upwardPitchRange.y);
        Vector3 orientedDownhill = transform.TransformDirection(downhillDirection);
        Vector3 horizontal = Quaternion.AngleAxis(yaw, Vector3.up) *
            new Vector3(orientedDownhill.x, 0f, orientedDownhill.z).normalized;
        Vector3 direction = (horizontal + Vector3.up * Mathf.Tan(pitch * Mathf.Deg2Rad)).normalized;

        GameObject projectile = Instantiate(projectilePrefab, muzzle.position,
            Quaternion.LookRotation(direction));
        SceneManager.MoveGameObjectToScene(projectile, gameObject.scene);
        NetworkObject networkObject = projectile.GetComponent<NetworkObject>();
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        networkObject.Spawn();
        body.linearVelocity = direction * launchSpeed;
        body.WakeUp();
    }
}
