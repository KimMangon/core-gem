using Unity.Netcode;
using UnityEngine;

/// <summary>Shared tutorial shooting objectives with a local overlay on every player's screen.</summary>
[RequireComponent(typeof(NetworkObject))]
public class TutorialCombatLesson : NetworkBehaviour
{
    [SerializeField] private ShootableObstacle redTrap;
    [SerializeField] private MonsterSpawnZone monsterZone;
    [SerializeField] private Transform monsterSpawn;
    [SerializeField] private Font font;
    private readonly NetworkVariable<bool> trapCleared = new(false);
    private readonly NetworkVariable<bool> monsterActivated = new(false);
    private readonly NetworkVariable<bool> monsterCleared = new(false);
    private readonly NetworkVariable<NetworkObjectReference> monsterReference = new();
    private GameObject hud;
    private RectTransform canvasRect;
    private RectTransform marker;
    private UnityEngine.UI.Text title;
    private UnityEngine.UI.Text controls;
    public bool CanFinish => trapCleared.Value && monsterCleared.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            trapCleared.Value = false;
            monsterActivated.Value = false;
            monsterCleared.Value = false;
        }
        CreateHud();
    }

    public override void OnNetworkDespawn()
    {
        if (hud != null) Destroy(hud);
    }

    private void Update()
    {
        if (!IsSpawned || hud == null) return;
        StageManager stage = StageManager.Instance;
        bool tutorial = stage != null && stage.IsTutorialStage;
        hud.SetActive(tutorial && !PauseManager.IsPaused);
        if (!tutorial) return;
        if (IsServer)
        {
            if (redTrap != null && redTrap.IsDisabled) trapCleared.Value = true;
            if (monsterZone != null && monsterZone.HasTriggered)
            {
                monsterActivated.Value = true;
                BoulderMonster living = monsterZone.FirstLivingMonster;
                if (living != null) monsterReference.Value = new NetworkObjectReference(living.NetworkObject);
                if (monsterZone.IsEncounterCleared) monsterCleared.Value = true;
            }
        }

        Transform target = null;
        if (!trapCleared.Value)
        {
            title.text = GameLocalization.T("SHOOT THE RED TRAP  /  3 HITS");
            target = redTrap != null ? redTrap.transform : null;
        }
        else if (!monsterActivated.Value)
            title.text = GameLocalization.T("TRAP DISABLED!  PUSH THE BOULDER FORWARD");
        else if (!monsterCleared.Value)
        {
            title.text = GameLocalization.T("SHOOT THE MONSTER!  PROTECT THE BOULDER");
            target = monsterReference.Value.TryGet(out NetworkObject enemy)
                ? enemy.transform : monsterSpawn;
        }
        else title.text = GameLocalization.T("TRAINING COMPLETE!  PUSH THE BOULDER TO THE GOAL");
        controls.text = GameLocalization.T("2: EQUIP GUN     LEFT CLICK: FIRE     {0}: RELOAD", GameKeyBindings.GetLabel(PlayerAction.Reload));
        NetworkObject player = NetworkManager.LocalClient?.PlayerObject;
        Camera camera = player != null ? player.GetComponentInChildren<Camera>(true) : null;
        bool show = target != null && camera != null && !CanFinish;
        if (show)
        {
            Vector3 screen = camera.WorldToScreenPoint(target.position +
                Vector3.up * (target == redTrap?.transform ? 0.2f : 1f));
            show = screen.z > 0f && screen.x > 0f && screen.x < Screen.width &&
                screen.y > 0f && screen.y < Screen.height;
            if (show && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                screen, null, out Vector2 local)) marker.anchoredPosition = local;
        }
        marker.gameObject.SetActive(show);
    }

    private void CreateHud()
    {
        hud = new GameObject("TutorialShootingHud", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler));
        canvasRect = hud.GetComponent<RectTransform>();
        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        GameUiScaling.Configure(canvas);
        RectTransform card = Rect("ShootingInstructions", canvasRect, new Vector2(0.5f, 0f),
            new Vector2(0f, 205f), new Vector2(900f, 112f));
        var background = card.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.05f, 0.07f, 0.1f, 0.9f);
        background.raycastTarget = false;
        title = Label("Objective", card, new Vector2(0f, 22f), new Vector2(870f, 44f), 27,
            new Color(1f, 0.9f, 0.1f));
        controls = Label("ShootingControls", card, new Vector2(0f, -25f), new Vector2(870f, 36f), 22, Color.white);
        marker = Rect("ShootTarget", canvasRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
        var shoot = Label("ShootLabel", marker, new Vector2(0f, 79f), new Vector2(220f, 50f), 36,
            new Color(1f, 0.9f, 0.1f));
        GameLocalization.Bind(shoot, "SHOOT");
        Color yellow = new Color(1f, 0.9f, 0.1f);
        for (int side = -1; side <= 1; side += 2)
            for (int vertical = -1; vertical <= 1; vertical += 2)
            {
                Stroke(marker, new Vector2(side * 33f, vertical * 40f), new Vector2(17f, 4f), 0f, yellow);
                Stroke(marker, new Vector2(side * 40f, vertical * 33f), new Vector2(4f, 17f), 0f, yellow);
            }
        Stroke(marker, new Vector2(0f, 52f), new Vector2(4f, 20f), 0f, yellow);
        Stroke(marker, new Vector2(-5f, 46f), new Vector2(4f, 15f), 45f, yellow);
        Stroke(marker, new Vector2(5f, 46f), new Vector2(4f, 15f), -45f, yellow);
    }

    private UnityEngine.UI.Text Label(string name, Transform parent, Vector2 position,
        Vector2 size, int fontSize, Color color)
    {
        RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
        var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
        label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameLocalization.Style(label);
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        var shadow = rect.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(2f, -2f);
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor,
        Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stroke(Transform parent, Vector2 position, Vector2 size, float angle, Color color)
    {
        RectTransform rect = Rect("TargetStroke", parent, new Vector2(0.5f, 0.5f), position, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
