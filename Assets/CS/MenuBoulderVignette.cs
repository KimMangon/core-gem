using UnityEngine;

/// <summary>Decorative menu actors only: no networking, colliders or gameplay physics.</summary>
[DefaultExecutionOrder(900)]
public sealed class MenuBoulderVignette : MonoBehaviour
{
    [SerializeField] private GameObject visuals;
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform stone;
    [SerializeField] private Transform actor;
    [SerializeField] private Transform torso;
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private MeshRenderer[] avatarParts;
    [SerializeField] private float cycleSeconds = 9f;
    private float elapsed;
    private Vector3 cameraPosition;
    private Quaternion cameraRotation;
    private int lastColor = -1;

    private void Awake()
    {
        if (menuCamera != null)
        {
            cameraPosition = menuCamera.transform.position;
            cameraRotation = menuCamera.transform.rotation;
        }
    }

    private void LateUpdate()
    {
        bool menu = StageManager.Instance == null || !StageManager.Instance.IsGameStarted;
        if (visuals != null && visuals.activeSelf != menu) visuals.SetActive(menu);
        if (!menu || actor == null || stone == null) return;
        elapsed += Time.unscaledDeltaTime;
        float phase = Mathf.Repeat(elapsed / Mathf.Max(1f, cycleSeconds), 1f);
        bool slipping = phase >= 0.65f && phase < 0.85f;
        float travel = phase < 0.65f ? Mathf.SmoothStep(-0.8f, 0.8f, phase / 0.65f) :
            phase < 0.85f ? Mathf.SmoothStep(0.8f, -0.8f, (phase - 0.65f) / 0.2f) : -0.8f;
        float steps = Mathf.Sin(elapsed * (slipping ? 14f : 7f));
        float effort = Mathf.SmoothStep(0f, 1f, phase / 0.08f) *
            (phase < 0.85f ? 1f : Mathf.SmoothStep(1f, 0f, (phase - 0.85f) / 0.15f));
        actor.localPosition = new Vector3(4.5f + travel, 0f, 0f);
        stone.localPosition = new Vector3(7.2f + travel, 1.75f, 0f);
        stone.localRotation = Quaternion.Euler(0f, 0f, -travel / 1.75f * Mathf.Rad2Deg);
        if (torso != null) torso.localRotation = Quaternion.Euler(0f, 0f,
            -12f - effort * 7f + steps * 1.5f * effort);
        AnimateFoot(leftFoot, steps, effort);
        AnimateFoot(rightFoot, -steps, effort);
        int color = PlayerColorPalette.SelectedIndex;
        if (lastColor != color)
        {
            lastColor = color;
            var block = new MaterialPropertyBlock();
            Color chosen = PlayerColorPalette.Colors[color];
            block.SetColor("_BaseColor", chosen);
            block.SetColor("_Color", chosen);
            foreach (var part in avatarParts) if (part != null) part.SetPropertyBlock(block);
        }
        if (menuCamera != null)
        {
            menuCamera.transform.position = cameraPosition + transform.TransformVector(
                new Vector3(Mathf.Sin(elapsed * 0.13f) * 0.12f, Mathf.Sin(elapsed * 0.19f) * 0.06f, 0f));
            menuCamera.transform.rotation = cameraRotation;
        }
    }

    private static void AnimateFoot(Transform foot, float step, float effort)
    {
        if (foot == null) return;
        Vector3 p = foot.localPosition;
        p.x = step * 0.22f * effort;
        p.y = 0.3f + Mathf.Max(0f, step) * 0.09f * effort;
        foot.localPosition = p;
        foot.localRotation = Quaternion.Euler(0f, 0f, -step * 18f * effort);
    }
}
