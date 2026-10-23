using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class SkyCloudBackdrop : MonoBehaviour
{
    private const float Distance = 500f;

    private static readonly float[] Azimuths = { 4f, 42f, 78f, 121f, 157f, 196f, 233f, 271f, 309f, 345f };
    private static readonly float[] Elevations = { 27f, 36f, 24f, 42f, 30f, 25f, 39f, 28f, 35f, 24f };
    private static readonly float[] Widths = { 205f, 175f, 225f, 165f, 200f, 215f, 170f, 195f, 180f, 220f };

    private Transform[] clouds;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void Awake()
    {
        Material cloudMaterial = Resources.Load<Material>("SkyCloudMaterial");
        if (cloudMaterial == null)
        {
            Debug.LogWarning("Sky cloud material is missing from Resources.", this);
            enabled = false;
            return;
        }

        clouds = new Transform[Azimuths.Length];
        for (int i = 0; i < clouds.Length; i++)
        {
            GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cloud.name = $"Sky Cloud {i + 1}";
            cloud.transform.SetParent(transform, false);

            Collider cloudCollider = cloud.GetComponent<Collider>();
            if (cloudCollider != null)
                Destroy(cloudCollider);

            MeshRenderer renderer = cloud.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = cloudMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            clouds[i] = cloud.transform;
        }
    }

    private void LateUpdate()
    {
        Camera camera = GetViewingCamera();
        if (camera != null)
            PositionClouds(camera);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Game)
            PositionClouds(camera);
    }

    private void PositionClouds(Camera camera)
    {
        if (clouds == null)
            return;

        for (int i = 0; i < clouds.Length; i++)
        {
            float azimuth = Azimuths[i] * Mathf.Deg2Rad;
            float elevation = Elevations[i] * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(
                Mathf.Sin(azimuth) * Mathf.Cos(elevation),
                Mathf.Sin(elevation),
                Mathf.Cos(azimuth) * Mathf.Cos(elevation));

            Transform cloud = clouds[i];
            cloud.position = camera.transform.position + direction * Distance;
            cloud.rotation = Quaternion.LookRotation(-direction, Vector3.up);
            cloud.localScale = new Vector3(Widths[i], Widths[i] * 0.48f, 1f);
        }
    }

    private static Camera GetViewingCamera()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsClient && networkManager.LocalClient != null)
        {
            NetworkObject player = networkManager.LocalClient.PlayerObject;
            if (player != null)
            {
                Camera playerCamera = player.GetComponentInChildren<Camera>(true);
                if (playerCamera != null && playerCamera.isActiveAndEnabled)
                    return playerCamera;
            }
        }

        return Camera.main;
    }
}
