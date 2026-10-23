using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Owner HUD and view gun; server-authoritative hitscan, ammo and target damage.</summary>
[DefaultExecutionOrder(200)]
public class PlayerHandgun : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private GameObject handgunPrefab;
    [SerializeField] private AudioClip shotClip;
    [SerializeField] private Texture2D handgunIcon;

    private const int MagazineSize = 12;
    private const float ReloadSeconds = 1.5f;
    private const float ShotInterval = 0.22f;
    private const float Range = 200f;
    private const float TracerSeconds = 0.1f;

    private readonly NetworkVariable<byte> selectedSlot = new(1,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<int> ammo = new(MagazineSize);
    private readonly NetworkVariable<bool> reloading = new(false);

    private Transform viewGun;
    private Transform worldGun;
    // Authored in the handgun model's local space; imported meshes need not be CPU-readable.
    [SerializeField] private Vector3 worldMuzzleLocal=new(.765f,.95f,0);
    private Transform remoteHand;
    private PlayerEyes remoteEyes;
    private MeshRenderer heldHandRenderer;
    private MeshRenderer originalHandRenderer;
    private MeshRenderer[] originalHandParts;
    private bool[] originalHandVisibility;
    private bool originalHandHidden;
    private MaterialPropertyBlock heldHandProperties;
    private Quaternion smoothedWorldAim;
    private static readonly Vector3 ModelGrip = new(-.38f,.20f,0);
    private float worldRecoil;
    private float worldReloadStarted;
    private bool worldWasReloading;
    private Vector3 gunRestPosition;
    private AudioSource shotSource;
    private GameObject hud;
    private RectTransform[] slotBorders;
    private RawImage fistIcon;
    private PlayerMovement playerMovement;
    private Text ammoText;
    private Text boulderHealthText;
    private RawImage crosshair;
    private RectTransform hitMarker;
    private CanvasGroup hitMarkerGroup;
    private float hitMarkerUntil;
    private float localNextShot;
    private float localReloadUntil;
    private float serverNextShot;
    private readonly RequestBudget shotBudget = new(20f, 40f);
    private readonly RequestBudget reloadBudget = new(8f, 16f);
    private float recoil;
    private float reloadAnimationTime = -10f;
    private static Material tracerMaterial;

    public bool IsGunEquipped => IsSpawned && selectedSlot.Value == 2;
    public bool ContainsWeapon(Transform part) =>
        (viewGun != null && part.IsChildOf(viewGun)) || (worldGun != null && part.IsChildOf(worldGun));

    public override void OnNetworkSpawn()
    {
        shotSource = gameObject.AddComponent<AudioSource>();
        shotSource.playOnAwake = false;
        shotSource.spatialBlend = IsOwner ? 0f : 1f;
        shotSource.minDistance = 2f;
        shotSource.maxDistance = 35f;
        if (!IsOwner)
        {
            CreateWorldGun();
            return;
        }

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
        playerMovement = GetComponent<PlayerMovement>();
        CreateViewGun();
        CreateGripHand(viewGun);
        CreateHud();
        UpdateHud();
    }

    public override void OnNetworkDespawn()
    {
        SetOriginalHandHidden(false);
        if (hud != null)
            Destroy(hud);
        if (viewGun != null)
            Destroy(viewGun.gameObject);
        if (worldGun != null)
            Destroy(worldGun.gameObject);
        if (shotSource != null) Destroy(shotSource);
    }

    private void CreateWorldGun()
    {
        if(handgunPrefab==null)return;
        remoteHand=transform.Find("Hand");
        remoteEyes=GetComponent<PlayerEyes>();
        smoothedWorldAim=transform.rotation;
        worldGun=new GameObject("RemoteHandgun").transform;
        worldGun.SetParent(transform,false);
        worldGun.localScale=Vector3.one*.256f;
        // Copy visual meshes only: a held weapon must not run pickup scripts or collide.
        foreach(var source in handgunPrefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var originalRenderer=source.GetComponent<MeshRenderer>();
            if(source.sharedMesh==null || originalRenderer==null)continue;
            var part=new GameObject(source.name,typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(worldGun,false);
            part.transform.localPosition=handgunPrefab.transform.InverseTransformPoint(source.transform.position);
            part.transform.localRotation=Quaternion.Inverse(handgunPrefab.transform.rotation)*source.transform.rotation;
            Vector3 rootScale=handgunPrefab.transform.lossyScale,partScale=source.transform.lossyScale;
            part.transform.localScale=new Vector3(partScale.x/rootScale.x,partScale.y/rootScale.y,partScale.z/rootScale.z);
            part.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
            part.GetComponent<MeshRenderer>().sharedMaterials=originalRenderer.sharedMaterials;
        }
        CreateGripHand(worldGun);
        worldGun.gameObject.SetActive(false);
    }

    private void CreateGripHand(Transform gun)
    {
        if(gun==null)return;
        if(remoteHand==null)remoteHand=transform.Find("Hand");
        if(remoteHand==null)return;
        var filter=remoteHand.GetComponent<MeshFilter>();
        originalHandRenderer=remoteHand.GetComponent<MeshRenderer>();
        if(filter==null || originalHandRenderer==null)return;
        originalHandParts=remoteHand.GetComponentsInChildren<MeshRenderer>(true);
        originalHandVisibility=new bool[originalHandParts.Length];
        var hand=new GameObject("WeaponGripHand",typeof(MeshFilter),typeof(MeshRenderer));
        hand.transform.SetParent(gun,false);hand.transform.localPosition=ModelGrip;
        hand.transform.localRotation=Quaternion.Euler(0,90,0);
        var sourceScale=remoteHand.lossyScale;var gunScale=gun.lossyScale;
        hand.transform.localScale=new Vector3(sourceScale.x/gunScale.x,sourceScale.y/gunScale.y,sourceScale.z/gunScale.z);
        hand.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
        heldHandRenderer=hand.GetComponent<MeshRenderer>();heldHandRenderer.sharedMaterials=originalHandRenderer.sharedMaterials;
        heldHandRenderer.shadowCastingMode=IsOwner?UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;
        heldHandProperties=new MaterialPropertyBlock();
    }

    private void SetOriginalHandHidden(bool hide)
    {
        if(originalHandParts==null)return;
        for(int i=0;i<originalHandParts.Length;i++)
        {
            var renderer=originalHandParts[i];
            if(renderer==null || ContainsWeapon(renderer.transform))continue;
            if(hide)
            {
                if(!originalHandHidden)originalHandVisibility[i]=renderer.enabled;
                renderer.enabled=false;
            }
            else if(originalHandHidden)renderer.enabled=originalHandVisibility[i];
        }
        originalHandHidden=hide;
    }

    private void LateUpdate()
    {
        if(!IsSpawned)return;
        SetOriginalHandHidden(IsGunEquipped && heldHandRenderer!=null);
        if(heldHandRenderer!=null && originalHandRenderer!=null)
        {
            originalHandRenderer.GetPropertyBlock(heldHandProperties);
            heldHandRenderer.SetPropertyBlock(heldHandProperties);
        }
        if(IsOwner)
        {
            if(IsGunEquipped && viewGun!=null && remoteHand!=null)
                remoteHand.SetPositionAndRotation(viewGun.TransformPoint(ModelGrip),viewGun.rotation*Quaternion.Euler(0,90,0));
            return;
        }
        if(worldGun==null)return;
        bool visible=IsGunEquipped && StageManager.Instance!=null && StageManager.Instance.IsGameStarted;
        worldGun.gameObject.SetActive(visible);
        if(!visible)return;
        if(reloading.Value && !worldWasReloading)worldReloadStarted=Time.time;
        worldWasReloading=reloading.Value;
        float dip=reloading.Value?Mathf.Sin(Mathf.Clamp01((Time.time-worldReloadStarted)/ReloadSeconds)*Mathf.PI):0;
        worldRecoil=Mathf.MoveTowards(worldRecoil,0,Time.deltaTime*8);
        Quaternion targetAim=transform.rotation*Quaternion.Euler(remoteEyes!=null?remoteEyes.CurrentPitch:0,0,0);
        smoothedWorldAim=Quaternion.Slerp(smoothedWorldAim,targetAim,1-Mathf.Exp(-18*Time.deltaTime));
        Quaternion rotation=smoothedWorldAim*Quaternion.Euler(-worldRecoil*10,-90,dip*30);
        // Gun and grip hand share one transform, including all reload and recoil motion.
        Vector3 position=transform.position+transform.up*.5f+smoothedWorldAim*
            new Vector3(.46f,-.45f-.28f*dip-.07f*worldRecoil,.72f-.1f*worldRecoil);
        worldGun.SetPositionAndRotation(position,rotation);
    }

    private void Update()
    {
        if (shotSource != null)
            shotSource.volume = GameSettingsPreferences.SfxVolume;
        if (!IsOwner || Keyboard.current == null || Mouse.current == null)
            return;

        bool playing = StageManager.Instance != null && StageManager.Instance.IsGameStarted;
        bool canControl = playing && !PauseManager.IsPaused &&
            !Keyboard.current.altKey.isPressed;
        if (hud != null)
            hud.SetActive(playing);

        if (canControl)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                selectedSlot.Value = 1;
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
                selectedSlot.Value = 2;
            if (IsGunEquipped)
            {
                bool leftClick = Mouse.current.leftButton.wasPressedThisFrame;
                bool reloadRequested = GameKeyBindings.WasPressedThisFrame(PlayerAction.Reload) ||
                    (leftClick && ammo.Value == 0);
                if (reloadRequested && !reloading.Value &&
                    ammo.Value < MagazineSize && Time.time >= localReloadUntil)
                {
                    reloadAnimationTime = Time.time;
                    localReloadUntil = Time.time + ReloadSeconds;
                    ReloadServerRpc();
                }
                else if (leftClick &&
                    Time.time >= localNextShot && !reloading.Value && ammo.Value > 0)
                {
                    localNextShot = Time.time + ShotInterval;
                    Fire();
                }
            }
        }

        if (viewGun != null)
        {
            viewGun.gameObject.SetActive(playing && IsGunEquipped);
            recoil = Mathf.MoveTowards(recoil, 0f, Time.deltaTime * 2.8f);
            float reloadProgress = (Time.time - reloadAnimationTime) / ReloadSeconds;
            float reloadDip = reloadProgress >= 0f && reloadProgress < 1f
                ? Mathf.Sin(reloadProgress * Mathf.PI) : 0f;
            viewGun.localPosition = gunRestPosition + new Vector3(0f,
                -0.28f * reloadDip - 0.07f * recoil, -0.1f * recoil);
            viewGun.localRotation = Quaternion.Euler(
                -10f * recoil, -90f, 30f * reloadDip);
        }
        UpdateHud();
    }

    private void Fire()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 end = ray.origin + ray.direction * Range;
        if (TryGetFirstHit(ray.origin, ray.direction, out RaycastHit hit))
            end = hit.point;
        PlayShot(ray.origin, end, true);
        recoil = 1f;
        FireServerRpc(ray.origin, ray.direction);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 origin, Vector3 direction)
    {
        if (!shotBudget.Allow() || !NetworkInputGuard.Playing ||
            !NetworkInputGuard.Finite(origin) || !NetworkInputGuard.Finite(direction) ||
            direction.sqrMagnitude > 1.1f || selectedSlot.Value != 2 || reloading.Value || ammo.Value <= 0 ||
            Time.time < serverNextShot || direction.sqrMagnitude < 0.9f ||
            Vector3.Distance(origin, transform.position) > 2.5f ||
            StageManager.Instance == null || !StageManager.Instance.IsGameStarted)
            return;

        Vector3 anchor = transform.position + Vector3.up * 0.5f;
        Vector3 offset = origin - anchor;
        foreach (RaycastHit wall in Physics.RaycastAll(anchor, offset.normalized, offset.magnitude,
            ~0, QueryTriggerInteraction.Ignore))
            if (!wall.collider.transform.IsChildOf(transform)) return;
        serverNextShot = Time.time + ShotInterval;
        ammo.Value--;
        direction.Normalize();
        Vector3 end = origin + direction * Range;
        string disabledObstacleId = string.Empty;
        if (TryGetFirstHit(origin, direction, out RaycastHit hit))
        {
            end = hit.point;
            IDamageable target = hit.collider.GetComponentInParent<BoulderHealth>() == null
                ? hit.collider.GetComponentInParent<IDamageable>() : null;
            if (target != null && (!(target is CannonWeakPoint weakpoint) || weakpoint.CanBeHitBy(OwnerClientId)))
            {
                target.TakeDamage(1, OwnerClientId);
                ConfirmHitRpc();
                if (target is ShootableObstacle obstacle && obstacle.IsDisabled)
                    disabledObstacleId = obstacle.ObstacleId;
            }
        }
        ShotFeedbackRpc(origin, end, disabledObstacleId);
    }

    private bool TryGetFirstHit(Vector3 origin, Vector3 direction, out RaycastHit first)
    {
        first = default;
        float distance = Range;
        bool found = false;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, Range,
            ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= distance)
                continue;
            first = hit;
            distance = hit.distance;
            found = true;
        }
        return found;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShotFeedbackRpc(Vector3 origin, Vector3 end, string disabledObstacleId)
    {
        if (!IsServer && !string.IsNullOrEmpty(disabledObstacleId))
            ShootableObstacle.Find(disabledObstacleId)?.DisableTemporarily();
        if (!IsOwner)
        {
            worldRecoil=1f;
            PlayShot(origin, end, false);
        }
    }

    [Rpc(SendTo.Owner)]
    private void ConfirmHitRpc()
    {
        hitMarkerUntil = Time.unscaledTime + 0.18f;
    }

    [ServerRpc]
    private void ReloadServerRpc()
    {
        if (reloadBudget.Allow() && NetworkInputGuard.Playing && selectedSlot.Value == 2 && !reloading.Value && ammo.Value < MagazineSize &&
            StageManager.Instance != null && StageManager.Instance.IsGameStarted)
            StartCoroutine(Reload());
    }

    private IEnumerator Reload()
    {
        reloading.Value = true;
        yield return new WaitForSeconds(ReloadSeconds);
        ammo.Value = MagazineSize;
        reloading.Value = false;
    }

    private void PlayShot(Vector3 origin, Vector3 end, bool useViewMuzzle)
    {
        if (shotClip != null && shotSource != null)
            shotSource.PlayOneShot(shotClip);

        if (tracerMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            tracerMaterial = new Material(shader);
        }
        GameObject tracer = new GameObject("HandgunTracer");
        LineRenderer line = tracer.AddComponent<LineRenderer>();
        line.sharedMaterial = tracerMaterial;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.027f;
        line.endWidth = 0.009f;
        line.startColor = new Color(1f, 0.9f, 0.18f, 0.55f);
        line.endColor = new Color(1f, 0.85f, 0.1f, 0.1f);
        Vector3 start = useViewMuzzle && playerCamera != null
            ? playerCamera.transform.TransformPoint(new Vector3(0.3f, -0.24f, 0.65f))
            : worldGun != null ? worldGun.TransformPoint(worldMuzzleLocal)
            : transform.TransformPoint(new Vector3(.46f,.3f,.9f));
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        Destroy(tracer, TracerSeconds);
    }

    private void CreateViewGun()
    {
        if (handgunPrefab == null || playerCamera == null)
            return;
        viewGun = Instantiate(handgunPrefab, playerCamera.transform).transform;
        viewGun.name = "ViewHandgun";
        gunRestPosition = new Vector3(0.46f, -0.45f, 0.72f);
        viewGun.localPosition = gunRestPosition;
        viewGun.localRotation = Quaternion.Euler(0f, -90f, 0f);
        viewGun.localScale = Vector3.one * 0.256f;
        foreach (Collider collider in viewGun.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
        foreach (Renderer renderer in viewGun.GetComponentsInChildren<Renderer>(true))
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        viewGun.gameObject.SetActive(false);
    }

    private void CreateHud()
    {
        hud = new GameObject("HandgunHud", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = hud.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        GameUiScaling.Configure(hud.GetComponent<Canvas>());
        hud.GetComponent<GraphicRaycaster>().enabled = false;

        crosshair = MakeBox("AimDot", hud.transform, Vector2.zero,
            new Vector2(6f, 6f), new Color(1f, 1f, 1f, 0.95f),
            new Vector2(0.5f, 0.5f));
        GameObject marker = new GameObject("HitMarker", typeof(RectTransform),
            typeof(CanvasGroup));
        hitMarker = marker.GetComponent<RectTransform>();
        hitMarker.SetParent(hud.transform, false);
        hitMarker.anchorMin = hitMarker.anchorMax = new Vector2(0.5f, 0.5f);
        hitMarker.anchoredPosition = Vector2.zero;
        hitMarker.sizeDelta = new Vector2(36f, 36f);
        hitMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
        hitMarkerGroup = marker.GetComponent<CanvasGroup>();
        hitMarkerGroup.blocksRaycasts = false;
        hitMarkerGroup.interactable = false;
        for (int arm = 0; arm < 4; arm++)
        {
            float angle = arm * 90f;
            Vector2 position = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * 12f;
            RawImage stroke = MakeBox("HitStroke" + arm, hitMarker, position,
                new Vector2(10f, 3f), Color.white, new Vector2(0.5f, 0.5f));
            stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        marker.SetActive(false);
        slotBorders = new RectTransform[2];
        for (int index = 0; index < 2; index++)
        {
            float x = index == 0 ? -43f : 43f;
            RawImage slot = MakeBox("Slot" + (index + 1), hud.transform,
                new Vector2(x, 56f), new Vector2(78f, 78f),
                new Color(0.09f, 0.11f, 0.15f, 0.75f), new Vector2(0.5f, 0f));
            slotBorders[index] = slot.rectTransform;
            AddBorder(slot.rectTransform);
            RawImage icon = MakeBox(index == 0 ? "FistIcon" : "HandgunIcon",
                slot.transform, new Vector2(5f, 5f), new Vector2(62f, 62f),
                Color.white,
                new Vector2(0.5f, 0.5f));
            icon.texture = index == 0 ? Texture2D.whiteTexture : handgunIcon;
            if (index == 0)
                fistIcon = icon;
            MakeText((index + 1).ToString(), slot.transform,
                new Vector2(-29f, -26f), new Vector2(20f, 22f), 17);
        }
        RawImage ammoPanel = MakeBox("AmmoPanel", hud.transform,
            new Vector2(132f, 54f), new Vector2(230f, 68f),
            new Color(0.08f, 0.1f, 0.14f, 0.85f), new Vector2(0f, 0f));
        ammoText = MakeText("12 / ∞", ammoPanel.transform, Vector2.zero,
            new Vector2(214f, 58f), 34);
        RawImage healthPanel = MakeBox("BoulderHealthPanel", hud.transform,
            new Vector2(132f, 114f), new Vector2(230f, 42f),
            new Color(0.08f, 0.1f, 0.14f, 0.85f), new Vector2(0f, 0f));
        boulderHealthText = MakeText("BOULDER  5 / 5", healthPanel.transform,
            Vector2.zero, new Vector2(220f, 36f), 22);
    }

    private void UpdateHud()
    {
        if (slotBorders == null)
            return;
        for (int index = 0; index < slotBorders.Length; index++)
            foreach (Transform edge in slotBorders[index])
                if (edge.name.StartsWith("Border"))
                    edge.gameObject.SetActive(selectedSlot.Value == index + 1);
        crosshair.enabled = IsGunEquipped;
        hitMarker.gameObject.SetActive(IsGunEquipped && Time.unscaledTime < hitMarkerUntil);
        hitMarkerGroup.alpha = Mathf.Clamp01((hitMarkerUntil - Time.unscaledTime) / 0.08f);
        Color32 avatarColor = playerMovement != null
            ? playerMovement.AvatarColor : PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex];
        fistIcon.color = avatarColor.a == 0
            ? PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex] : avatarColor;
        ammoText.transform.parent.gameObject.SetActive(IsGunEquipped);
        ammoText.text = reloading.Value ? GameLocalization.T("RELOADING") : ammo.Value + " / ∞";
        BoulderHealth boulder = BoulderHealth.Instance;
        boulderHealthText.transform.parent.gameObject.SetActive(boulder != null);
        if (boulder != null)
        {
            boulderHealthText.text = GameLocalization.T("BOULDER  {0} / {1}", boulder.CurrentHealth, boulder.MaximumHealth);
            boulderHealthText.color = boulder.CurrentHealth <= 2
                ? new Color(1f, 0.35f, 0.3f) : Color.white;
        }
    }

    private static RawImage MakeBox(string name, Transform parent, Vector2 position,
        Vector2 size, Color color, Vector2 anchor)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(RawImage));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        RawImage image = obj.GetComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text MakeText(string content, Transform parent, Vector2 position,
        Vector2 size, int fontSize, Vector2? anchor = null)
    {
        GameObject obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Text label = obj.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = content;
        GameLocalization.Style(label);
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private static void AddBorder(RectTransform rect)
    {
        Color white = new Color(1f, 1f, 1f, 0.95f);
        float halfWidth = rect.sizeDelta.x * 0.5f;
        float halfHeight = rect.sizeDelta.y * 0.5f;
        MakeBox("BorderTop", rect, new Vector2(0f, halfHeight - 1.5f),
            new Vector2(rect.sizeDelta.x, 3f),
            white, new Vector2(0.5f, 0.5f));
        MakeBox("BorderBottom", rect, new Vector2(0f, -halfHeight + 1.5f),
            new Vector2(rect.sizeDelta.x, 3f),
            white, new Vector2(0.5f, 0.5f));
        MakeBox("BorderLeft", rect, new Vector2(-halfWidth + 1.5f, 0f),
            new Vector2(3f, rect.sizeDelta.y),
            white, new Vector2(0.5f, 0.5f));
        MakeBox("BorderRight", rect, new Vector2(halfWidth - 1.5f, 0f),
            new Vector2(3f, rect.sizeDelta.y),
            white, new Vector2(0.5f, 0.5f));
    }
}
