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

    public double SecondsUntilNextShot
    {
        get
        {
            double now = SynchronizedTime();
            return nextShotAt <= 0d ? 2d : System.Math.Max(0d, nextShotAt - now);
        }
    }

    public void RestoreShotDelay(double seconds)
    {
        nextShotAt = SynchronizedTime() + System.Math.Max(0d, seconds);
    }

    public void RestoreProjectile(SavedProjectileState saved)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer || projectilePrefab == null ||
            saved == null || saved.body == null)
            return;
        GameObject projectile = Instantiate(projectilePrefab,
            saved.body.position, saved.body.rotation);
        SceneManager.MoveGameObjectToScene(projectile, gameObject.scene);
        projectile.GetComponent<NetworkObject>().Spawn();
        Rigidbody body = projectile.GetComponent<Rigidbody>();
        body.linearVelocity = saved.body.velocity;
        body.angularVelocity = saved.body.angularVelocity;
        projectile.GetComponent<Stage5Projectile>()?.RestoreLifetime(saved.remainingLifetime);
        body.WakeUp();
    }

    private void OnEnable()
    {
        nextShotAt = 0d;
    }

    private void Update()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsListening || !network.IsServer ||
            StageManager.Instance == null || !StageManager.Instance.IsGameStarted ||
            StageManager.Instance.CurrentSection != 5 ||
            projectilePrefab == null || muzzle == null)
            return;

        double now = SynchronizedTime();
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

    private static double SynchronizedTime()
    {
        StageManager stage = StageManager.Instance;
        return stage != null ? stage.ObstacleTime :
            NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : 0d;
    }
}
