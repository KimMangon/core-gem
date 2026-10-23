using Unity.Netcode;
using UnityEngine;

/// <summary>Googly eyes that face with the avatar and follow its vertical look.</summary>
public class PlayerEyes : NetworkBehaviour
{
    [SerializeField] private Material eyeWhiteMaterial;
    [SerializeField] private Material pupilMaterial;

    private readonly NetworkVariable<float> lookPitch = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private Transform eyeRoot;
    private readonly Transform[] pupils = new Transform[2];
    private readonly Vector2[] dartOffsets = new Vector2[2];
    private readonly Vector2[] dartTargets = new Vector2[2];
    private readonly float[] nextDartTimes = new float[2];
    private PlayerLook playerLook;
    private PlayerArm playerArm;
    private Transform hand;

    public bool ContainsEye(Transform target) =>
        eyeRoot != null && target != null && target.IsChildOf(eyeRoot);

    public float CurrentPitch => lookPitch.Value;

    private void Awake()
    {
        playerLook = GetComponent<PlayerLook>();
        playerArm = GetComponent<PlayerArm>();
        hand = transform.Find("Hand");
        eyeRoot = new GameObject("PlayerEyesVisual").transform;
        eyeRoot.SetParent(transform, false);

        CreateEye(0, new Vector3(-0.18f, 0.68f, 0.435f));
        CreateEye(1, new Vector3(0.18f, 0.65f, 0.435f));
    }

    public override void OnNetworkSpawn()
    {
        // The first-person camera is inside the head; only other players see the eyes.
        eyeRoot.gameObject.SetActive(!IsOwner);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || playerLook == null)
            return;

        float pitch = playerLook.Pitch;
        if (Mathf.Abs(Mathf.DeltaAngle(lookPitch.Value, pitch)) >= 1.5f)
            lookPitch.Value = pitch;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || IsOwner)
            return;

        bool followHand = playerArm != null && playerArm.IsPushing && hand != null;
        float verticalGaze = Mathf.Clamp(-lookPitch.Value / 80f, -1f, 1f) * 0.16f;
        for (int i = 0; i < pupils.Length; i++)
        {
            if (followHand)
            {
                Transform eye = pupils[i].parent;
                Vector3 toHand = hand.position - eye.position;
                Vector3 localDirection = eye.InverseTransformDirection(toHand.normalized);
                Vector2 handOffset = Vector2.ClampMagnitude(
                    new Vector2(localDirection.x, localDirection.y) * 0.28f, 0.23f);
                dartOffsets[i] = Vector2.Lerp(dartOffsets[i], handOffset,
                    Time.deltaTime * 20f);
                pupils[i].localPosition = new Vector3(dartOffsets[i].x,
                    dartOffsets[i].y, 0.46f);
                continue;
            }

            if (Time.time >= nextDartTimes[i])
            {
                dartTargets[i] = Random.insideUnitCircle * 0.13f;
                nextDartTimes[i] = Time.time + Random.Range(0.17f, 0.48f);
            }

            dartOffsets[i] = Vector2.Lerp(dartOffsets[i], dartTargets[i],
                Time.deltaTime * 16f);
            float wobbleX = Mathf.Sin(Time.time * (11.7f + i * 1.9f)) * 0.035f;
            float wobbleY = Mathf.Sin(Time.time * (8.9f + i * 2.4f)) * 0.027f;
            Vector2 offset = dartOffsets[i] + new Vector2(wobbleX,
                verticalGaze + wobbleY);
            offset = Vector2.ClampMagnitude(offset, 0.23f);
            pupils[i].localPosition = new Vector3(offset.x, offset.y, 0.46f);
        }
    }

    private void CreateEye(int index, Vector3 position)
    {
        var white = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        white.name = index == 0 ? "LeftEyeWhite" : "RightEyeWhite";
        white.transform.SetParent(eyeRoot, false);
        white.transform.localPosition = position;
        white.transform.localScale = new Vector3(0.31f, 0.33f, 0.25f);
        white.GetComponent<MeshRenderer>().sharedMaterial = eyeWhiteMaterial;
        RemoveCollider(white);

        var pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pupil.name = index == 0 ? "LeftEyePupil" : "RightEyePupil";
        pupil.transform.SetParent(white.transform, false);
        pupil.transform.localPosition = new Vector3(0f, 0f, 0.46f);
        pupil.transform.localScale = new Vector3(0.34f, 0.34f, 0.25f);
        pupil.GetComponent<MeshRenderer>().sharedMaterial = pupilMaterial;
        RemoveCollider(pupil);
        pupils[index] = pupil.transform;
    }

    private static void RemoveCollider(GameObject part)
    {
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        UnityEngine.Object.Destroy(collider);
    }
}
