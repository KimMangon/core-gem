using Unity.Netcode;
using UnityEngine;

/// <summary>Local-only clear-screen tour. It never moves players or advances game state.</summary>
[DefaultExecutionOrder(1000)]
public sealed class MenuCourseCamera : MonoBehaviour
{
    [SerializeField] private Camera tourCamera;
    [SerializeField] private Camera originalMenuCamera;
    [Header("Main menu boulder orbit")]
    [SerializeField] private bool orbitBoulderInMenu = true;
    [SerializeField] private Transform menuBoulder;
    [SerializeField, Min(4f)] private float orbitRadius = 8f;
    [SerializeField, Min(2f)] private float orbitHeight = 3.2f;
    [SerializeField, Min(30f)] private float orbitSeconds = 100f;
    private Renderer boulderRenderer;
    private float orbitElapsed;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float originalFieldOfView;
    [SerializeField, Min(30f)] private float loopSeconds = 240f;
    [SerializeField] private Vector3[] positions =
    {
        new(90f,100f,-80f), new(-85f,115f,120f),
        new(140f,110f,265f), new(-80f,145f,485f),
        new(235f,220f,760f), new(130f,220f,1030f),
        new(240f,200f,560f), new(-140f,155f,240f)
    };
    [SerializeField] private Vector3[] targets =
    {
        new(20f,30f,60f), new(35f,50f,130f),
        new(50f,45f,285f), new(55f,80f,535f),
        new(64f,120f,805f), new(64f,130f,850f),
        new(50f,75f,550f), new(35f,50f,265f)
    };
    private AudioListener tourListener;
    private Camera controlledPlayerCamera;
    private AudioListener controlledPlayerListener;
    private bool playerCameraWasEnabled;
    private bool playerListenerWasEnabled;
    private float elapsed;
    private bool showingTour;

    private void Awake()
    {
        if (tourCamera == null) tourCamera = GetComponent<Camera>();
        tourListener = tourCamera != null ? tourCamera.GetComponent<AudioListener>() : null;
        if (originalMenuCamera != null)
        {
            originalPosition = originalMenuCamera.transform.position;
            originalRotation = originalMenuCamera.transform.rotation;
            originalFieldOfView = originalMenuCamera.fieldOfView;
            originalMenuCamera.enabled = true;
            var listener = originalMenuCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }
        if (tourCamera != null) tourCamera.enabled = false;
        if (tourListener != null) tourListener.enabled = false;
        UpdatePose(0f, true);
    }

    private void LateUpdate()
    {
        if (tourCamera == null) return;
        var stage = StageManager.Instance;
        bool started = stage != null && stage.IsGameStarted;
        bool showTour = started && stage.RoomTimerFinished;
        bool showMenu = !started;
        if (showMenu && orbitBoulderInMenu) UpdateMenuOrbit();
        if (showTour && !showingTour)
        {
            elapsed = 0f;
            UpdatePose(0f, true);
        }
        showingTour = showTour;
        if (originalMenuCamera != null)
        {
            if (showMenu && !originalMenuCamera.gameObject.activeSelf)
                originalMenuCamera.gameObject.SetActive(true);
            originalMenuCamera.enabled = showMenu;
            var listener = originalMenuCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = showMenu;
        }
        var network = NetworkManager.Singleton;
        var player = network != null && network.IsClient ? network.LocalClient?.PlayerObject : null;
        Camera playerCamera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        if (controlledPlayerCamera != playerCamera)
        {
            RestorePlayerCamera();
            controlledPlayerCamera = playerCamera;
            controlledPlayerListener = playerCamera != null ? playerCamera.GetComponent<AudioListener>() : null;
            playerCameraWasEnabled = playerCamera != null && playerCamera.enabled;
            playerListenerWasEnabled = controlledPlayerListener != null && controlledPlayerListener.enabled;
        }
        if (controlledPlayerCamera != null)
            controlledPlayerCamera.enabled = started && !showTour && playerCameraWasEnabled;
        if (controlledPlayerListener != null)
            controlledPlayerListener.enabled = started && !showTour && playerListenerWasEnabled;
        tourCamera.enabled = showTour;
        if (tourListener != null) tourListener.enabled = showTour;
        if (!showTour) return;
        elapsed += Time.unscaledDeltaTime;
        UpdatePose(elapsed, false);
    }

    private void UpdateMenuOrbit()
    {
        if (originalMenuCamera == null) return;
        if (menuBoulder == null)
        {
            var boulder = FindAnyObjectByType<BoulderHealth>();
            if (boulder == null) return;
            menuBoulder = boulder.transform;
            boulderRenderer = boulder.GetComponentInChildren<Renderer>();
        }
        if (boulderRenderer == null) boulderRenderer = menuBoulder.GetComponentInChildren<Renderer>();
        Vector3 center = boulderRenderer != null ? boulderRenderer.bounds.center : menuBoulder.position;
        float top = boulderRenderer != null ? boulderRenderer.bounds.max.y : center.y + 1.3f;
        orbitElapsed += Time.unscaledDeltaTime;
        float angle = Mathf.PI + orbitElapsed / Mathf.Max(30f, orbitSeconds) * Mathf.PI * 2f;
        // Horizontal circle only: the camera always stays above the rock's top.
        float height = Mathf.Max(orbitHeight, top - center.y + 1.5f);
        Vector3 position = center + new Vector3(Mathf.Sin(angle) * orbitRadius, height, Mathf.Cos(angle) * orbitRadius);
        Vector3 target = center + Vector3.up * .3f;
        originalMenuCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        originalMenuCamera.fieldOfView = 48f;
    }

    private void UpdatePose(float time, bool snap)
    {
        if (tourCamera == null || positions == null || targets == null ||
            positions.Length < 4 || positions.Length != targets.Length) return;
        float sample = Mathf.Repeat(time / Mathf.Max(30f, loopSeconds), 1f) * positions.Length;
        int segment = Mathf.FloorToInt(sample);
        float t = sample - segment;
        Vector3 position = Sample(positions, segment, t);
        Vector3 target = Sample(targets, segment, t);
        tourCamera.transform.position = position;
        Quaternion rotation = Quaternion.LookRotation(target - position, Vector3.up);
        tourCamera.transform.rotation = snap ? rotation : Quaternion.Slerp(tourCamera.transform.rotation,
            rotation, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));
    }

    private static Vector3 Sample(Vector3[] points, int i, float t)
    {
        int n = points.Length;
        Vector3 a = points[(i + n - 1) % n], b = points[i % n];
        Vector3 c = points[(i + 1) % n], d = points[(i + 2) % n];
        return 0.5f * ((2f * b) + (-a + c) * t +
            (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t);
    }

    private void RestorePlayerCamera()
    {
        if (controlledPlayerCamera != null) controlledPlayerCamera.enabled = playerCameraWasEnabled;
        if (controlledPlayerListener != null) controlledPlayerListener.enabled = playerListenerWasEnabled;
    }

    private void OnDisable()
    {
        if (originalMenuCamera != null && originalFieldOfView > 0f)
        {
            originalMenuCamera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            originalMenuCamera.fieldOfView = originalFieldOfView;
        }
        RestorePlayerCamera();
        if (tourCamera != null) tourCamera.enabled = false;
        if (tourListener != null) tourListener.enabled = false;
    }
}
