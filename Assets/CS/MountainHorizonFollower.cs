using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

public class MountainHorizonFollower : MonoBehaviour
{
    private float fixedHeight;

    private void Awake()
    {
        fixedHeight = transform.position.y;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void LateUpdate()
    {
        Camera camera = GetViewingCamera();
        if (camera != null)
            PositionFor(camera);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType == CameraType.Game)
            PositionFor(camera);
    }

    private void PositionFor(Camera camera)
    {
        Vector3 cameraPosition = camera.transform.position;
        transform.position = new Vector3(cameraPosition.x, fixedHeight, cameraPosition.z);
    }

    private static Camera GetViewingCamera()
    {
        NetworkManager network = NetworkManager.Singleton;
        if (network != null && network.IsClient && network.LocalClient != null)
        {
            NetworkObject player = network.LocalClient.PlayerObject;
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
