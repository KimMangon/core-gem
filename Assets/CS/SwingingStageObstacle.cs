using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SwingingStageObstacle : MonoBehaviour
{
    [SerializeField] private float amplitudeDegrees = 52f;
    [SerializeField] private float cycleSeconds = 4.2f;
    [SerializeField] private float phaseOffsetSeconds;
    [SerializeField] private float boulderHitImpulse = 18f;

    private Rigidbody body;
    private Quaternion restRotation;
    private double lastBoulderHitTime = double.NegativeInfinity;
    private float swingDirection;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        restRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        double time = GetSynchronizedTime();
        float phase = (float)((time + phaseOffsetSeconds) *
            (2.0 * Mathf.PI / cycleSeconds));
        float angle = Mathf.Sin(phase) * amplitudeDegrees;
        swingDirection = Mathf.Sign(Mathf.Cos(phase));
        body.MoveRotation(restRotation * Quaternion.AngleAxis(angle, Vector3.forward));
    }

    private void OnCollisionEnter(Collision collision) => HitBoulder(collision);
    private void OnCollisionStay(Collision collision) => HitBoulder(collision);

    private void HitBoulder(Collision collision)
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network == null || !network.IsServer)
            return;

        Rigidbody other = collision.rigidbody;
        if (other == null || other.GetComponent<BoulderSlopeDrag>() == null)
            return;

        double time = GetSynchronizedTime();
        if (time - lastBoulderHitTime < 0.65)
            return;

        lastBoulderHitTime = time;
        other.AddForce(restRotation * Vector3.right *
            (swingDirection * boulderHitImpulse), ForceMode.Impulse);
    }

    private static double GetSynchronizedTime()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network != null && network.IsListening && StageManager.Instance != null
            ? StageManager.Instance.ObstacleTime
            : Time.fixedTimeAsDouble;
    }
}
