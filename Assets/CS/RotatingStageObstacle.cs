using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RotatingStageObstacle : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 40f;
    [SerializeField] private float phaseOffsetDegrees;

    private Rigidbody body;
    private Quaternion startingRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        startingRotation = transform.rotation;
    }

    private void FixedUpdate()
    {
        NetworkManager network = NetworkManager.Singleton;
        double time = network != null && network.IsListening && StageManager.Instance != null
            ? StageManager.Instance.ObstacleTime
            : Time.fixedTimeAsDouble;
        float angle = Mathf.Repeat((float)(time * degreesPerSecond) + phaseOffsetDegrees, 360f);
        body.MoveRotation(startingRotation * Quaternion.Euler(0f, angle, 0f));
    }
}
