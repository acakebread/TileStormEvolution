using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

public class ScreenSpaceVolumetricFog : MonoBehaviour, IDirectCommandProvider
{
    private const int MaxDepthLayerCount = 8;
    private const int MaxVisibleDepthLayerCount = MaxDepthLayerCount + 2;

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int PseudoDepthId = Shader.PropertyToID("_PseudoDepth");
    private static readonly int DepthLayerCountId = Shader.PropertyToID("_DepthLayerCount");
    private static readonly int FogFarPlaneId = Shader.PropertyToID("_FogFarPlane");
    private static readonly int LayerRotationCompensationsId = Shader.PropertyToID("_LayerRotationCompensations");

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
    private Camera trackedRotationCamera;
    private Quaternion lastCameraRotation;
    private bool hasLayerRotationTracking;
    private readonly Quaternion[] layerRotations = new Quaternion[MaxVisibleDepthLayerCount];
    private readonly Vector4[] layerRotationCompensationVectors = new Vector4[MaxVisibleDepthLayerCount];

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
        trackedRotationCamera = null;
        lastCameraRotation = Quaternion.identity;
        hasLayerRotationTracking = false;
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

    private static Quaternion Normalize(Quaternion rotation)
    {
        float magnitude = Mathf.Sqrt(
            rotation.x * rotation.x
            + rotation.y * rotation.y
            + rotation.z * rotation.z
            + rotation.w * rotation.w);

        if (magnitude < 1e-6f)
            return Quaternion.identity;

        float inverseMagnitude = 1.0f / magnitude;
        return new Quaternion(
            rotation.x * inverseMagnitude,
            rotation.y * inverseMagnitude,
            rotation.z * inverseMagnitude,
            rotation.w * inverseMagnitude);
    }

    private void UpdateLayerRotations(Camera camera)
    {
        Quaternion currentCameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;

        if (!hasLayerRotationTracking || trackedRotationCamera != camera)
        {
            trackedRotationCamera = camera;
            lastCameraRotation = currentCameraRotation;
            hasLayerRotationTracking = true;

            for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
                layerRotations[i] = currentCameraRotation;
        }
        else
        {
            Quaternion cameraDelta = currentCameraRotation * Quaternion.Inverse(lastCameraRotation);
            if (cameraDelta.w < 0.0f)
                cameraDelta = new Quaternion(-cameraDelta.x, -cameraDelta.y, -cameraDelta.z, -cameraDelta.w);

            lastCameraRotation = currentCameraRotation;

            for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
                layerRotations[i] = Normalize(cameraDelta * layerRotations[i]);
        }

        Quaternion inverseCameraRotation = Quaternion.Inverse(currentCameraRotation);
        for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
        {
            Quaternion compensation = Normalize(layerRotations[i] * inverseCameraRotation);
            layerRotationCompensationVectors[i] = new Vector4(compensation.x, compensation.y, compensation.z, compensation.w);
        }

        fogMaterial.SetVectorArray(LayerRotationCompensationsId, layerRotationCompensationVectors);
    }

    private void ApplyMaterialParameters(Camera camera)
    {
        float resolvedPseudoDepth = ResolvePseudoDepth(camera);
        UpdateLayerRotations(camera);
        fogMaterial.SetColor(FogColorId, fogColor);
        fogMaterial.SetFloat(PseudoDepthId, resolvedPseudoDepth);
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





