using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

public class ScreenSpaceVolumetricFogSystem : MonoBehaviour, IDirectCommandProvider, IDirectDepthTextureProvider
{
    private const int MaxDepthLayerCount = 8;
    private const int MaxVisibleDepthLayerCount = MaxDepthLayerCount + 2;
    private const float FogAnimationSpeedScale = 0.4f;
    private const float ConvectionSpeedScale = 0.1f;

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int PseudoDepthId = Shader.PropertyToID("_PseudoDepth");
    private static readonly int DepthLayerCountId = Shader.PropertyToID("_DepthLayerCount");
    private static readonly int FogFarPlaneId = Shader.PropertyToID("_FogFarPlane");
    private static readonly int GroundPlaneFalloffId = Shader.PropertyToID("_GroundPlaneFalloff");
    private static readonly int ClipBelowGroundId = Shader.PropertyToID("_ClipBelowGround");
    private static readonly int FogSeedOffsetId = Shader.PropertyToID("_FogSeedOffset");
    private static readonly int DebugFogId = Shader.PropertyToID("_DebugFog");
    private static readonly int LayerRotationCompensationsId = Shader.PropertyToID("_LayerRotationCompensations");

    [Header("Render")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [Header("Fog")]
    [SerializeField] private Color fogColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
    [SerializeField, Min(0.01f)] private float fogFarPlane = 20.0f;
    [SerializeField, Min(0.0f)] private float groundPlaneFalloff;
    [SerializeField] private bool clipBelowGround;
    [SerializeField, Range(0.0f, 1.0f)] private float fogAnimationSpeed;
    [SerializeField, Range(-1.0f, 1.0f)] private float convection;
    [SerializeField, Range(1, 8)] private int depthLayerCount = 2;
    [SerializeField] private bool debugFog = true;

    private Material fogMaterial;
    private Camera trackedDepthCamera;
    private Vector3 lastCameraWorldPosition;
    private float accumulatedCameraDepth;
    private float fogSeedOffset;
    private bool hasCameraDepthTracking;
    private Camera trackedRotationCamera;
    private Vector3 lastRotationCameraWorldPosition;
    private Quaternion lastCameraRotation;
    private int lastRotationFirstLayerIndex;
    private bool hasLayerRotationTracking;
    private readonly Quaternion[] remappedLayerRotations = new Quaternion[MaxVisibleDepthLayerCount];
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
            Debug.LogError($"{GetType().Name}: Missing shader Hidden/ScreenSpaceVolumetricFog.", this);
            enabled = false;
            return;
        }

        fogMaterial = new Material(shader)
        {
            name = "ScreenSpaceVolumetricFog_Runtime"
        };

        Debug.Log($"{GetType().Name}: runtime material created.", this);
    }

    private Vector3 GetConvectionWorldDelta(Camera camera)
    {
        if (camera == null || convection <= 0.0f)
            return Vector3.zero;

        return Vector3.up * fogFarPlane * -convection * ConvectionSpeedScale * Time.deltaTime;
    }

    [ContextMenu("Reset Camera Depth Origin")]
    private void ResetCameraDepthOrigin()
    {
        trackedDepthCamera = null;
        lastCameraWorldPosition = default;
        accumulatedCameraDepth = 0.0f;
        fogSeedOffset = 0.0f;
        hasCameraDepthTracking = false;
        trackedRotationCamera = null;
        lastRotationCameraWorldPosition = default;
        lastCameraRotation = Quaternion.identity;
        lastRotationFirstLayerIndex = 0;
        hasLayerRotationTracking = false;
    }

    private float ResolvePseudoDepth(Camera camera)
    {
        if (camera == null)
            return 0.0f;

        Transform cameraTransform = camera.transform;
        Vector3 currentCameraPosition = cameraTransform.position;
        Vector3 syntheticCameraPosition = currentCameraPosition + GetConvectionWorldDelta(camera);

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

            Plane currentDepthPlane = new Plane(depthPlaneNormal, syntheticCameraPosition);
            float cameraSpaceZDelta = currentDepthPlane.GetDistanceToPoint(lastCameraWorldPosition);
            accumulatedCameraDepth += cameraSpaceZDelta;
            lastCameraWorldPosition = currentCameraPosition;
        }

        float fogRange = Mathf.Max(fogFarPlane, 1e-5f);
        // Match the shader's layer compression so one fog-range traversal
        // advances through one full pseudo-depth unit per active layer pair.
        float motionScale = Mathf.Max(depthLayerCount, 1) / fogRange;
        return accumulatedCameraDepth * motionScale;
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

    private float GetLayerRotationScale(float resolvedPseudoDepth, int localLayerIndex)
    {
        int activeDepthLayerCount = Mathf.Clamp(depthLayerCount, 1, MaxDepthLayerCount);
        float layerScale = 1.0f / activeDepthLayerCount;
        int firstLayerIndex = GetFirstLayerIndex(resolvedPseudoDepth);
        int layerIndex = firstLayerIndex + localLayerIndex;
        float rawBandStart = (resolvedPseudoDepth + layerIndex) * layerScale;
        float rawBandEnd = rawBandStart + layerScale;
        float visibleBandStart = Mathf.Max(rawBandStart, 0.0f);
        float visibleBandEnd = Mathf.Min(rawBandEnd, 1.0f);

        if (visibleBandEnd - visibleBandStart <= 1e-5f)
            return 0.0f;

        return Mathf.Clamp01((visibleBandStart + visibleBandEnd) * 0.5f);
    }

    private static int GetFirstLayerIndex(float resolvedPseudoDepth)
    {
        return -(int)Mathf.Floor(resolvedPseudoDepth) - 1;
    }

    private Quaternion GetStrafeRotationDelta(Vector3 cameraSpaceDelta)
    {
        float fogRange = Mathf.Max(fogFarPlane, 1e-5f);

        float yawDegrees = Mathf.Atan2(cameraSpaceDelta.x, fogRange) * Mathf.Rad2Deg;
        float pitchDegrees = -Mathf.Atan2(cameraSpaceDelta.y, fogRange) * Mathf.Rad2Deg;

        Quaternion yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(pitchDegrees, Vector3.right);
        return Normalize(yaw * pitch);
    }

    private void RemapLayerRotations(int newFirstLayerIndex)
    {
        if (newFirstLayerIndex == lastRotationFirstLayerIndex)
            return;

        for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
        {
            int logicalLayerIndex = newFirstLayerIndex + i;
            int previousLocalIndex = logicalLayerIndex - lastRotationFirstLayerIndex;

            if (previousLocalIndex < 0)
                remappedLayerRotations[i] = layerRotations[0];
            else if (previousLocalIndex >= MaxVisibleDepthLayerCount)
                remappedLayerRotations[i] = layerRotations[MaxVisibleDepthLayerCount - 1];
            else
                remappedLayerRotations[i] = layerRotations[previousLocalIndex];
        }

        for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
            layerRotations[i] = remappedLayerRotations[i];

        lastRotationFirstLayerIndex = newFirstLayerIndex;
    }

    private void UpdateLayerRotations(Camera camera, float resolvedPseudoDepth)
    {
        Quaternion currentCameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
        int firstLayerIndex = GetFirstLayerIndex(resolvedPseudoDepth);

        if (!hasLayerRotationTracking || trackedRotationCamera != camera)
        {
            trackedRotationCamera = camera;
            lastRotationCameraWorldPosition = camera != null ? camera.transform.position : default;
            lastCameraRotation = currentCameraRotation;
            lastRotationFirstLayerIndex = firstLayerIndex;
            hasLayerRotationTracking = true;

            for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
                layerRotations[i] = currentCameraRotation;
        }
        else
        {
            RemapLayerRotations(firstLayerIndex);

            Vector3 currentCameraPosition = camera != null ? camera.transform.position : default;
            Vector3 syntheticCameraPosition = currentCameraPosition + GetConvectionWorldDelta(camera);
            Vector3 worldDelta = syntheticCameraPosition - lastRotationCameraWorldPosition;
            Vector3 cameraSpaceDelta = Quaternion.Inverse(currentCameraRotation) * worldDelta;
            Quaternion strafeDelta = GetStrafeRotationDelta(cameraSpaceDelta);
            Quaternion cameraDelta = Quaternion.Inverse(lastCameraRotation) * currentCameraRotation;
            if (cameraDelta.w < 0.0f)
                cameraDelta = new Quaternion(-cameraDelta.x, -cameraDelta.y, -cameraDelta.z, -cameraDelta.w);

            lastRotationCameraWorldPosition = currentCameraPosition;
            lastCameraRotation = currentCameraRotation;

            for (int i = 0; i < MaxVisibleDepthLayerCount; i++)
            {
                float rotationScale = GetLayerRotationScale(resolvedPseudoDepth, i);
                Quaternion layerDelta = Quaternion.SlerpUnclamped(Quaternion.identity, cameraDelta, rotationScale);
                Quaternion currentLayerRotation = Normalize(layerRotations[i] * layerDelta);
                layerRotations[i] = Normalize(currentLayerRotation * strafeDelta);
            }
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
        fogSeedOffset = Mathf.Repeat(fogSeedOffset + Time.deltaTime * fogAnimationSpeed * FogAnimationSpeedScale, 1024.0f);

        float resolvedPseudoDepth = ResolvePseudoDepth(camera);
        UpdateLayerRotations(camera, resolvedPseudoDepth);
        fogMaterial.SetColor(FogColorId, fogColor);
        fogMaterial.SetFloat(PseudoDepthId, resolvedPseudoDepth);
        fogMaterial.SetFloat(DepthLayerCountId, Mathf.Max(depthLayerCount, 1));
        fogMaterial.SetFloat(FogFarPlaneId, fogFarPlane);
        fogMaterial.SetFloat(GroundPlaneFalloffId, groundPlaneFalloff);
        fogMaterial.SetFloat(ClipBelowGroundId, clipBelowGround ? 1.0f : 0.0f);
        fogMaterial.SetFloat(FogSeedOffsetId, fogSeedOffset);
        fogMaterial.SetFloat(DebugFogId, debugFog ? 1.0f : 0.0f);
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

    public bool RequiresActiveDepthTexture(RenderPassEvent evt)
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





