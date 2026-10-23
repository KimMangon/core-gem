using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerArm : NetworkBehaviour
{
    [SerializeField] private Transform handTransform; // Hand 연결
    [SerializeField] private GameObject middleFinger;
    [SerializeField] private Camera playerCamera; // PlayerCamera 연결
    [SerializeField] private float reachDistance = 1.7f; // 팔 길이
    [SerializeField] private float handMoveSpeed = 10f;

    private bool wasOnButton = false; //직전 프레임에 버튼에 닿아있었는지
    private float lastPressTime = -1f; // 추가: 마지막으로 눌린 시각
    [SerializeField] private float pressCooldown = 0.08f; // 추가: 최소 재입력 간격
    [SerializeField] private float pushForce = 55f;
    [SerializeField] private float pushReach = 0.4f;
    [SerializeField] private float maxPushSpeed = 3.5f;

    private readonly NetworkVariable<bool> isPushing = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<bool> isGesturing = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly Collider[] pushHits = new Collider[8];
    private Vector3 previousHandPosition;

    private Vector3 restLocalPos;
    private bool hasPressedThisHold = false;

    public bool IsPushing => isPushing.Value;
    public bool IsGesturing => isGesturing.Value;

    public override void OnNetworkSpawn()
    {
        restLocalPos = handTransform.localPosition;
        previousHandPosition = handTransform.position;
        isGesturing.OnValueChanged += OnGestureChanged;
        if (middleFinger != null)
            middleFinger.SetActive(isGesturing.Value);
    }

    public override void OnNetworkDespawn()
    {
        isGesturing.OnValueChanged -= OnGestureChanged;
    }

    private void OnGestureChanged(bool previous, bool current)
    {
        if (middleFinger != null)
            middleFinger.SetActive(current);
    }

    

    void Update()
    {
        if (!IsOwner || Mouse.current == null || Keyboard.current == null)
            return;

        if (StageManager.Instance == null || !StageManager.Instance.IsGameStarted)
        {
            if (isPushing.Value)
                isPushing.Value = false;
            if (isGesturing.Value)
                isGesturing.Value = false;
            handTransform.localPosition = Vector3.Lerp(handTransform.localPosition,
                restLocalPos, Time.deltaTime * handMoveSpeed);
            return;
        }

        if (Keyboard.current.altKey.isPressed || PauseManager.IsPaused)
        {
            if (isPushing.Value)
                isPushing.Value = false;
            if (isGesturing.Value)
                isGesturing.Value = false;
            handTransform.localPosition = Vector3.Lerp(handTransform.localPosition, restLocalPos, Time.deltaTime * handMoveSpeed);
            wasOnButton = false; 
            return;
        }

        bool holding = GameKeyBindings.IsHeld(PlayerAction.Push);
        bool gesturing = GameKeyBindings.IsHeld(PlayerAction.Gesture);
        if (isGesturing.Value != gesturing)
            isGesturing.Value = gesturing;
        if (middleFinger != null)
            middleFinger.SetActive(gesturing);
        if (isPushing.Value != holding)
            isPushing.Value = holding;

        if (holding)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = playerCamera.ScreenPointToRay(mousePos);

            Vector3 targetWorldPos;
            bool hitButton = false;

            if (Physics.Raycast(ray, out RaycastHit hit, reachDistance))
            {
                targetWorldPos = hit.point;
                hitButton = hit.collider.CompareTag("Button");
            }
            else
            {
                targetWorldPos = ray.origin + ray.direction * reachDistance;
            }

            handTransform.position = Vector3.Lerp(handTransform.position, targetWorldPos, Time.deltaTime * handMoveSpeed);

            if (hitButton && !wasOnButton && Time.time - lastPressTime >= pressCooldown) // 수정: 쿨다운 조건 추가
            {
                lastPressTime = Time.time;
                FindAnyObjectByType<GameManager>().PressButtonServerRpc(NetworkManager.Singleton.LocalClientId);
            }
            wasOnButton = hitButton; // 이번 프레임 접촉 상태 기록
        }
        else
        {
            handTransform.localPosition = Vector3.Lerp(handTransform.localPosition, restLocalPos, Time.deltaTime * handMoveSpeed);
            wasOnButton = false; 
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || handTransform == null)
            return;

        Vector3 handPosition = handTransform.position;
        Vector3 handVelocity = (handPosition - previousHandPosition) / Time.fixedDeltaTime;
        previousHandPosition = handPosition;
        if (!isPushing.Value)
            return;

        int count = Physics.OverlapSphereNonAlloc(
            handPosition, pushReach, pushHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = pushHits[i];
            if (hit == null || hit.gameObject.name != "Boulder")
                continue;

            Rigidbody boulder = hit.attachedRigidbody;
            if (boulder == null || boulder.isKinematic)
                continue;

            Vector3 direction = boulder.worldCenterOfMass - handPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                continue;

            direction.Normalize();
            if (Vector3.Dot(boulder.linearVelocity, direction) >= maxPushSpeed)
                continue;

            // Keep pushing while the hand is held against the stone.
            // Moving the hand toward it adds a small burst, but is not required.
            float approachSpeed = Mathf.Clamp(Vector3.Dot(handVelocity, direction), 0f, 2f);
            float force = pushForce * (1f + 0.25f * approachSpeed);
            boulder.AddForceAtPosition(direction * force,
                hit.ClosestPoint(handPosition), ForceMode.Force);
        }
    }
}
