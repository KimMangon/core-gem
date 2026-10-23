using Unity.Netcode;
using UnityEngine;

/// <summary>Moves one side of the stage-five gate inward on a shared room clock.</summary>
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class Stage5CompressionGate : MonoBehaviour
{
    [SerializeField] private Vector3 travelDirection = Vector3.right;
    [SerializeField] private float travelDistance = 8f;
    [SerializeField] private float cycleSeconds = 8f;
    [SerializeField] private float warningSeconds = 1.2f;
    [SerializeField] private float closingSeconds = 1f;
    [SerializeField] private float holdSeconds = 1.2f;
    [SerializeField] private float openingSeconds = 1f;
    [SerializeField] private Renderer warningLamp;

    private Rigidbody body;
    private Vector3 restPosition;
    private MaterialPropertyBlock lampProperties;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        restPosition = transform.position;
        body.position = PositionAtTime(SynchronizedTime());
        lampProperties = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (warningLamp == null)
            return;

        float phase = Mathf.Repeat((float)SynchronizedTime(), cycleSeconds);
        bool movingOrClosed = phase >= warningSeconds &&
            phase < warningSeconds + closingSeconds + holdSeconds + openingSeconds;
        bool warningFlash = phase < warningSeconds && Mathf.FloorToInt(phase * 6f) % 2 == 0;
        Color color = movingOrClosed || warningFlash
            ? new Color(1f, 0.08f, 0.05f)
            : new Color(1f, 0.67f, 0.06f);
        warningLamp.GetPropertyBlock(lampProperties);
        lampProperties.SetColor("_BaseColor", color);
        warningLamp.SetPropertyBlock(lampProperties);
    }

    private void FixedUpdate()
    {
        body.MovePosition(PositionAtTime(SynchronizedTime()));
    }

    private Vector3 PositionAtTime(double time)
    {
        float phase = Mathf.Repeat((float)time, cycleSeconds);
        float extension = 0f;
        float closingEnds = warningSeconds + closingSeconds;
        float holdEnds = closingEnds + holdSeconds;
        float openingEnds = holdEnds + openingSeconds;

        if (phase >= warningSeconds && phase < closingEnds)
            extension = Mathf.SmoothStep(0f, 1f, (phase - warningSeconds) / closingSeconds);
        else if (phase >= closingEnds && phase < holdEnds)
            extension = 1f;
        else if (phase >= holdEnds && phase < openingEnds)
            extension = Mathf.SmoothStep(1f, 0f, (phase - holdEnds) / openingSeconds);

        return restPosition + travelDirection.normalized * (extension * travelDistance);
    }

    private static double SynchronizedTime()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network != null && network.IsListening && StageManager.Instance != null
            ? StageManager.Instance.ObstacleTime
            : Time.fixedTimeAsDouble;
    }
}
