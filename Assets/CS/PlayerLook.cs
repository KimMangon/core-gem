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
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Update()
    {
        if (!IsOwner || Keyboard.current == null || Mouse.current == null)
            return;

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
