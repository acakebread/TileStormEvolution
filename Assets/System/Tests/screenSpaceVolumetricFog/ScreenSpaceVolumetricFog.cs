using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

public class ScreenSpaceVolumetricFog : MonoBehaviour, IDirectCommandProvider
{
    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int PseudoDepthId = Shader.PropertyToID("_PseudoDepth");
    private static readonly int DepthLayerCountId = Shader.PropertyToID("_DepthLayerCount");
    private static readonly int FogFarPlaneId = Shader.PropertyToID("_FogFarPlane");

    [Header("Render")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [Header("Fog")]
    [SerializeField] private Color fogColor = new Color(0.75f, 0.55f, 1.0f, 1.0f);
    [SerializeField, Range(-2.0f, 2.0f)] private float pseudoDepth;
    [SerializeField, Min(0.01f)] private float fogFarPlane = 20.0f;
    [SerializeField, Range(1, 8)] private int depthLayerCount = 2;

    private Material fogMaterial;
    private Camera trackedDepthCamera;
    private Vector3 lastCameraWorldPosition;
    private float accumulatedCameraDepth;
    private bool hasCameraDepthTracking;

    private void Awake()
    {
        EnsureMaterial();
    }

    private void OnEnable()
    {
        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (fogMaterial != null)
            return;

        Shader shader = Shader.Find("Hidden/ScreenSpaceVolumetricFog");
        if (shader == null)
        {
            Debug.LogError("ScreenSpaceVolumetricFog: Missing shader Hidden/ScreenSpaceVolumetricFog.", this);
            enabled = false;
            return;
        }

        fogMaterial = new Material(shader)
        {
            name = "ScreenSpaceVolumetricFog_Runtime"
        };

        Debug.Log("ScreenSpaceVolumetricFog: runtime material created.", this);
    }

    [ContextMenu("Reset Camera Depth Origin")]
    private void ResetCameraDepthOrigin()
    {
        trackedDepthCamera = null;
        lastCameraWorldPosition = default;
        accumulatedCameraDepth = 0.0f;
        hasCameraDepthTracking = false;
    }

    private float ResolvePseudoDepth(Camera camera)
    {
        if (camera == null)
            return pseudoDepth;

        Transform cameraTransform = camera.transform;
        Vector3 currentCameraPosition = cameraTransform.position;

        if (!hasCameraDepthTracking || trackedDepthCamera != camera)
        {
            trackedDepthCamera = camera;
            lastCameraWorldPosition = currentCameraPosition;
            accumulatedCameraDepth = 0.0f;
            hasCameraDepthTracking = true;
        }
        else
        {
            Vector3 depthPlaneNormal = Vector3.Cross(cameraTransform.right, cameraTransform.up);
            if (depthPlaneNormal.sqrMagnitude < 1e-8f)
                depthPlaneNormal = cameraTransform.forward;
            else
                depthPlaneNormal.Normalize();

            Plane currentDepthPlane = new Plane(depthPlaneNormal, currentCameraPosition);
            float cameraSpaceZDelta = currentDepthPlane.GetDistanceToPoint(lastCameraWorldPosition);
            accumulatedCameraDepth += cameraSpaceZDelta;
            lastCameraWorldPosition = currentCameraPosition;
        }

        float fogRange = Mathf.Max(fogFarPlane, 1e-5f);
        // Match the shader's layer compression so one fog-range traversal
        // advances through one full pseudo-depth unit per active layer pair.
        float motionScale = Mathf.Max(depthLayerCount, 1) / fogRange;
        return pseudoDepth + accumulatedCameraDepth * motionScale;
    }

    private void ApplyMaterialParameters(Camera camera)
    {
        fogMaterial.SetColor(FogColorId, fogColor);
        fogMaterial.SetFloat(PseudoDepthId, ResolvePseudoDepth(camera));
        fogMaterial.SetFloat(DepthLayerCountId, Mathf.Max(depthLayerCount, 1));
        fogMaterial.SetFloat(FogFarPlaneId, fogFarPlane);
    }

    public bool HasCommands(RenderPassEvent evt)
    {
        return isActiveAndEnabled
            && evt == renderPassEvent
            && fogMaterial != null;
    }

    public bool RequiresColorTexture(RenderPassEvent evt)
    {
        return evt == renderPassEvent;
    }

    public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
    {
        if (!HasCommands(evt))
            return;

        ApplyMaterialParameters(camera);
        commandBuffer.DrawProcedural(Matrix4x4.identity, fogMaterial, 0, MeshTopology.Triangles, 3, 1);
    }

    private void OnDestroy()
    {
        if (fogMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(fogMaterial);
        else
            DestroyImmediate(fogMaterial);
    }
}





