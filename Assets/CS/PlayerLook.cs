using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerLook : NetworkBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float pitchMin = -80f;
    [SerializeField] private float pitchMax = 80f;

    private float yaw;
    private float pitch;
    private Vector3 cameraLocalPosition;

    public float Yaw => yaw;
    public float Pitch => pitch;

    public void RestoreView(float savedYaw, float savedPitch)
    {
        if (!IsOwner)
            return;
        yaw = savedYaw;
        pitch = Mathf.Clamp(savedPitch, pitchMin, pitchMax);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, cameraTransform.localEulerAngles.x);
        cameraLocalPosition = cameraTransform.localPosition;
        Cursor.lockState = StageManager.Instance != null && StageManager.Instance.IsGameStarted
            ? CursorLockMode.Locked : CursorLockMode.None;
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null || Mouse.current == null)
            return;

        if (StageManager.Instance == null || !StageManager.Instance.IsGameStarted)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        bool altHeld = Keyboard.current.altKey.isPressed || PauseManager.IsPaused;
        if (altHeld)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        Vector2 delta = Mouse.current.delta.ReadValue();
        float sensitivity = GameSettingsPreferences.MouseSensitivity;
        yaw += delta.x * sensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, pitchMin, pitchMax);
    }

    private void LateUpdate()
    {
        if (!IsOwner || cameraTransform == null)
            return;

        // Keep the camera inside the player body while rotating the view locally.
        cameraTransform.localPosition = cameraLocalPosition;
        cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
