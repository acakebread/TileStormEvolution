using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceVolumetricFogSystem : MonoBehaviour, IScreenSpaceVolumetricFogTemporalProvider
{
    private const int MaxDepthLayerCount = 8;
    private const int MaxVisibleDepthLayerCount = MaxDepthLayerCount + 2;
    protected const int FogPass = 0;
    protected const int TemporalFogPass = 1;
    private const float FogAnimationSpeedScale = 0.4f;
    private const float ConvectionSpeedScale = 0.1f;

    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int PseudoDepthId = Shader.PropertyToID("_PseudoDepth");
    private static readonly int DepthLayerCountId = Shader.PropertyToID("_DepthLayerCount");
    private static readonly int FogFarPlaneId = Shader.PropertyToID("_FogFarPlane");
    private static readonly int GroundPlaneFalloffId = Shader.PropertyToID("_GroundPlaneFalloff");
    private static readonly int FogSeedOffsetId = Shader.PropertyToID("_FogSeedOffset");
    private static readonly int DebugFogId = Shader.PropertyToID("_DebugFog");
    private static readonly int LayerRotationCompensationsId = Shader.PropertyToID("_LayerRotationCompensations");
    private static readonly int TemporalHistoryBlendId = Shader.PropertyToID("_TemporalHistoryBlend");

    [Header("Render")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [Header("Fog")]
    [SerializeField] private Color fogColor = new Color(1.0f, 1.0f, 1.0f, 0.5f);
    [SerializeField, Min(0.0f)] private float fogFarPlane;
    [SerializeField, Min(0.0f)] private float groundPlaneFalloff;
    [SerializeField, Range(0.0f, 1.0f)] private float fogAnimationSpeed = 0.3f;
    [SerializeField, Range(-1.0f, 1.0f)] private float convection = 0.25f;
    [SerializeField, Range(1, 8)] private int depthLayerCount = 2;
    [SerializeField] private bool debugFog = false;

    [Header("Temporal")]
    [Tooltip("Blends fog opacity with the previous frame. Zero disables temporal history.")]
    [SerializeField, Range(0.0f, 0.95f)] private float temporalAccumulation = 0.82f;
    [Tooltip("Automatically ignores temporal history after a sudden camera jump or cut.")]
    [SerializeField] private bool resetTemporalOnCameraCut = true;
    [Tooltip("Camera translation threshold for history reset, as a fraction of the fog far plane.")]
    [SerializeField, Range(0.0f, 1.0f)] private float temporalCutDistanceFraction = 0.2f;
    [Tooltip("Camera rotation threshold in degrees for history reset.")]
    [SerializeField, Range(0.0f, 180.0f)] private float temporalCutRotationDegrees = 35.0f;

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
    private RenderTexture previousTemporalHistoryTexture;
    private RenderTexture currentTemporalHistoryTexture;
    private RTHandle previousTemporalHistoryHandle;
    private RTHandle currentTemporalHistoryHandle;
    private int temporalHistoryWidth;
    private int temporalHistoryHeight;
    private bool temporalHistoryValid;
    private bool temporalHistoryPreparedThisFrame;
    private Camera temporalHistoryCamera;
    private Vector3 lastTemporalCameraPosition;
    private Quaternion lastTemporalCameraRotation;
    private bool hasTemporalCameraPose;

    protected Material FogMaterial => fogMaterial;
    protected bool DebugFogEnabled => debugFog;

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
        if (camera == null || Mathf.Abs(convection) <= 1e-6f)
            return Vector3.zero;

        return Vector3.up * ResolveFogFarPlane(camera) * -convection * ConvectionSpeedScale * Time.deltaTime;
    }

    protected float ResolveFogFarPlane(Camera camera)
    {
        if (fogFarPlane > 1e-6f)
            return fogFarPlane;

        return camera != null ? Mathf.Max(camera.farClipPlane, 1e-5f) : 1.0f;
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
        ReleaseTemporalHistory();
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

        float fogRange = ResolveFogFarPlane(camera);
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

    private Quaternion GetStrafeRotationDelta(Vector3 cameraSpaceDelta, Camera camera)
    {
        float fogRange = ResolveFogFarPlane(camera);

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
            Quaternion strafeDelta = GetStrafeRotationDelta(cameraSpaceDelta, camera);
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

    protected virtual void ApplyMaterialParameters(Camera camera)
    {
        fogSeedOffset = Mathf.Repeat(fogSeedOffset + Time.deltaTime * fogAnimationSpeed * FogAnimationSpeedScale, 1024.0f);

        float resolvedPseudoDepth = ResolvePseudoDepth(camera);
        UpdateLayerRotations(camera, resolvedPseudoDepth);
        fogMaterial.SetColor(FogColorId, fogColor);
        fogMaterial.SetFloat(PseudoDepthId, resolvedPseudoDepth);
        fogMaterial.SetFloat(DepthLayerCountId, Mathf.Max(depthLayerCount, 1));
        fogMaterial.SetFloat(FogFarPlaneId, fogFarPlane);
        fogMaterial.SetFloat(GroundPlaneFalloffId, groundPlaneFalloff);
        fogMaterial.SetFloat(FogSeedOffsetId, fogSeedOffset);
        fogMaterial.SetFloat(DebugFogId, debugFog ? 1.0f : 0.0f);
        fogMaterial.SetFloat(TemporalHistoryBlendId, temporalHistoryValid ? temporalAccumulation : 0.0f);
    }

    public bool RequiresTemporalHistory(RenderPassEvent evt)
    {
        return HasCommands(evt)
            && !DebugFogEnabled
            && temporalAccumulation > 1e-5f;
    }

    public void PrepareTemporalHistory(RenderPassEvent evt, Camera camera, RenderTextureDescriptor cameraDescriptor)
    {
        if (!RequiresTemporalHistory(evt))
            return;

        int width = Mathf.Max(1, cameraDescriptor.width);
        int height = Mathf.Max(1, cameraDescriptor.height);
        if (previousTemporalHistoryTexture != null
            && currentTemporalHistoryTexture != null
            && temporalHistoryWidth == width
            && temporalHistoryHeight == height)
        {
            temporalHistoryPreparedThisFrame = true;
            ResetTemporalHistoryIfCameraCut(camera);
            return;
        }

        ReleaseTemporalHistory();

        temporalHistoryWidth = width;
        temporalHistoryHeight = height;
        previousTemporalHistoryTexture = CreateTemporalHistoryTexture(width, height, "ScreenSpaceVolumetricFog_PreviousHistory");
        currentTemporalHistoryTexture = CreateTemporalHistoryTexture(width, height, "ScreenSpaceVolumetricFog_CurrentHistory");
        previousTemporalHistoryHandle = RTHandles.Alloc(previousTemporalHistoryTexture);
        currentTemporalHistoryHandle = RTHandles.Alloc(currentTemporalHistoryTexture);
        temporalHistoryValid = false;
        temporalHistoryPreparedThisFrame = true;
        ResetTemporalCameraPose(camera);
    }

    public RTHandle GetPreviousTemporalHistory(RenderPassEvent evt)
    {
        return RequiresTemporalHistory(evt) ? previousTemporalHistoryHandle : null;
    }

    public RTHandle GetCurrentTemporalHistory(RenderPassEvent evt)
    {
        return RequiresTemporalHistory(evt) ? currentTemporalHistoryHandle : null;
    }

    public void CompleteTemporalHistory(RenderPassEvent evt, Camera camera)
    {
        if (!RequiresTemporalHistory(evt))
            return;

        (previousTemporalHistoryTexture, currentTemporalHistoryTexture) = (currentTemporalHistoryTexture, previousTemporalHistoryTexture);
        (previousTemporalHistoryHandle, currentTemporalHistoryHandle) = (currentTemporalHistoryHandle, previousTemporalHistoryHandle);
        temporalHistoryValid = true;
        temporalHistoryPreparedThisFrame = false;
        ResetTemporalCameraPose(camera);
    }

    private void ResetTemporalHistoryIfCameraCut(Camera camera)
    {
        if (!resetTemporalOnCameraCut || camera == null)
            return;

        if (!hasTemporalCameraPose || temporalHistoryCamera != camera)
        {
            temporalHistoryValid = false;
            ResetTemporalCameraPose(camera);
            return;
        }

        Transform cameraTransform = camera.transform;
        float fogRange = Mathf.Max(ResolveFogFarPlane(camera), 1e-5f);
        float cutDistance = fogRange * temporalCutDistanceFraction;
        float positionDelta = Vector3.Distance(lastTemporalCameraPosition, cameraTransform.position);
        float rotationDelta = Quaternion.Angle(lastTemporalCameraRotation, cameraTransform.rotation);

        if ((cutDistance > 1e-5f && positionDelta > cutDistance)
            || (temporalCutRotationDegrees > 1e-5f && rotationDelta > temporalCutRotationDegrees))
            temporalHistoryValid = false;
    }

    private void ResetTemporalCameraPose(Camera camera)
    {
        if (camera == null)
        {
            temporalHistoryCamera = null;
            hasTemporalCameraPose = false;
            return;
        }

        Transform cameraTransform = camera.transform;
        temporalHistoryCamera = camera;
        lastTemporalCameraPosition = cameraTransform.position;
        lastTemporalCameraRotation = cameraTransform.rotation;
        hasTemporalCameraPose = true;
    }

    private static RenderTexture CreateTemporalHistoryTexture(int width, int height, string textureName)
    {
        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.Create();
        return texture;
    }

    private void ReleaseTemporalHistory()
    {
        previousTemporalHistoryHandle?.Release();
        currentTemporalHistoryHandle?.Release();
        previousTemporalHistoryHandle = null;
        currentTemporalHistoryHandle = null;

        ReleaseTemporalHistoryTexture(previousTemporalHistoryTexture);
        ReleaseTemporalHistoryTexture(currentTemporalHistoryTexture);
        previousTemporalHistoryTexture = null;
        currentTemporalHistoryTexture = null;
        temporalHistoryValid = false;
        temporalHistoryPreparedThisFrame = false;
        temporalHistoryWidth = 0;
        temporalHistoryHeight = 0;
        temporalHistoryCamera = null;
        lastTemporalCameraPosition = default;
        lastTemporalCameraRotation = Quaternion.identity;
        hasTemporalCameraPose = false;
    }

    private static void ReleaseTemporalHistoryTexture(RenderTexture texture)
    {
        if (texture == null)
            return;

        texture.Release();
        if (Application.isPlaying)
            Destroy(texture);
        else
            DestroyImmediate(texture);
    }

    public virtual bool HasCommands(RenderPassEvent evt)
    {
        return isActiveAndEnabled
            && evt == renderPassEvent
            && fogMaterial != null;
    }

    public virtual bool RequiresColorTexture(RenderPassEvent evt)
    {
        return evt == renderPassEvent;
    }

    public virtual bool RequiresActiveDepthTexture(RenderPassEvent evt)
    {
        return evt == renderPassEvent;
    }

    protected virtual int GetMaterialPassIndex(RenderPassEvent evt)
    {
        if (RequiresTemporalHistory(evt)
            && temporalHistoryPreparedThisFrame
            && previousTemporalHistoryHandle != null
            && currentTemporalHistoryHandle != null)
            return TemporalFogPass;

        temporalHistoryValid = false;
        temporalHistoryPreparedThisFrame = false;
        return FogPass;
    }

    public virtual void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
    {
        if (!HasCommands(evt))
            return;

        ApplyMaterialParameters(camera);
        commandBuffer.DrawProcedural(Matrix4x4.identity, fogMaterial, GetMaterialPassIndex(evt), MeshTopology.Triangles, 3, 1);
    }

    protected virtual void OnDestroy()
    {
        ReleaseTemporalHistory();
        if (fogMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(fogMaterial);
        else
            DestroyImmediate(fogMaterial);
    }
}






