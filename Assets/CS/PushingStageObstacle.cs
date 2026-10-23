using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class PushingStageObstacle : MonoBehaviour
{
    [SerializeField] private Vector3 localPushDirection = Vector3.left;
    [SerializeField] private float strokeDistance = 7f;
    [SerializeField] private float cycleSeconds = 5.6f;
    [SerializeField] private float extendSeconds = 0.45f;
    [SerializeField] private float holdSeconds = 0.25f;
    [SerializeField] private float retractSeconds = 0.65f;
    [SerializeField] private float phaseOffsetSeconds;
    [SerializeField] private float boulderHitImpulse = 36f;

    private Rigidbody body;
    private Vector3 restPosition;
    private Vector3 worldPushDirection;
    private int currentCycle = int.MinValue;
    private bool hitBoulderThisCycle;
    private bool striking;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        restPosition = transform.position;
        worldPushDirection = transform.TransformDirection(localPushDirection.normalized);
        body.position = PositionAtTime(GetSynchronizedTime());
    }

    private void FixedUpdate()
    {
        double time = GetSynchronizedTime();
        int cycle = Mathf.FloorToInt((float)((time + phaseOffsetSeconds) / cycleSeconds));
        if (cycle != currentCycle)
        {
            currentCycle = cycle;
            hitBoulderThisCycle = false;
        }

        float phase = Mathf.Repeat((float)(time + phaseOffsetSeconds), cycleSeconds);
        striking = phase < extendSeconds + holdSeconds;
        body.MovePosition(PositionAtTime(time));
    }

    private void OnCollisionEnter(Collision collision) => HitBoulder(collision);
    private void OnCollisionStay(Collision collision) => HitBoulder(collision);

    private void HitBoulder(Collision collision)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (!striking || hitBoulderThisCycle || network == null || !network.IsServer)
            return;

        Rigidbody other = collision.rigidbody;
        if (other == null || other.GetComponent<BoulderSlopeDrag>() == null)
            return;

        hitBoulderThisCycle = true;
        other.AddForce(worldPushDirection * boulderHitImpulse, ForceMode.Impulse);
    }

    private Vector3 PositionAtTime(double time)
    {
        float phase = Mathf.Repeat((float)(time + phaseOffsetSeconds), cycleSeconds);
        float extension;
        if (phase < extendSeconds)
            extension = phase / extendSeconds;
        else if (phase < extendSeconds + holdSeconds)
            extension = 1f;
        else if (phase < extendSeconds + holdSeconds + retractSeconds)
            extension = 1f - (phase - extendSeconds - holdSeconds) / retractSeconds;
        else
            extension = 0f;

        return restPosition + worldPushDirection * (extension * strokeDistance);
    }

    private static double GetSynchronizedTime()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network != null && network.IsListening && StageManager.Instance != null
            ? StageManager.Instance.ObstacleTime
            : Time.fixedTimeAsDouble;
    }
}
