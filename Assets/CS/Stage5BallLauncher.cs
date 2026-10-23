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
    [SerializeField] private Vector2 randomShotInterval;
    [SerializeField] private float launchSpeed = 18f;
    [SerializeField] private float yawSpreadDegrees = 16f;
    [SerializeField] private bool useCustomYawRange;
    [SerializeField] private Vector2 customYawRange;
    [SerializeField] private Vector2 upwardPitchRange = new(7f, 13f);
    [SerializeField] private int activeSection = 5;
    [SerializeField] private float initialDelay = 2f;
    public int ActiveSection => activeSection;
    private bool disabledByControl;
    public void SetControlDisabled(bool value)
    {
        disabledByControl = value;
        if (value) nextShotAt = 0d;
    }

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
        projectile.GetComponent<Stage5Projectile>()?.RestoreHealth(saved.health);
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
            StageManager.Instance.CurrentSection != activeSection ||
            StageManager.Instance.RoomTimerFinished ||
            disabledByControl || projectilePrefab == null || muzzle == null)
        {
            nextShotAt = 0d;
            return;
        }

        double now = SynchronizedTime();
        if (nextShotAt <= 0d)
            nextShotAt = now + initialDelay;
        if (now < nextShotAt)
            return;

        Vector2 intervalRange = activeSection == 8 ? new Vector2(1.6f, 2.3f) : randomShotInterval;
        float interval = intervalRange.x > 0f && intervalRange.y >= intervalRange.x
            ? Random.Range(intervalRange.x, intervalRange.y) : shotInterval;
        nextShotAt = now + Mathf.Max(0.5f, interval);
        float yaw = useCustomYawRange
            ? Random.Range(Mathf.Min(customYawRange.x,customYawRange.y),Mathf.Max(customYawRange.x,customYawRange.y))
            : Random.Range(-yawSpreadDegrees, yawSpreadDegrees);
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
