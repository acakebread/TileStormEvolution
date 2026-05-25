using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

public class ScreenSpaceVolumetricFog : MonoBehaviour, IDirectCommandProvider
{
    private static readonly int CausticTextureId = Shader.PropertyToID("_CausticTexture");
    private static readonly int FieldTintId = Shader.PropertyToID("_FieldTint");
    private static readonly int BlobTintId = Shader.PropertyToID("_BlobTint");
    private static readonly int FieldScaleId = Shader.PropertyToID("_FieldScale");
    private static readonly int FieldOffsetId = Shader.PropertyToID("_FieldOffset");
    private static readonly int BlobScaleId = Shader.PropertyToID("_BlobScale");
    private static readonly int BlobOffsetId = Shader.PropertyToID("_BlobOffset");
    private static readonly int FieldBiasId = Shader.PropertyToID("_FieldBias");
    private static readonly int DriftSpeedId = Shader.PropertyToID("_DriftSpeed");
    private static readonly int FieldDriftAmountId = Shader.PropertyToID("_FieldDriftAmount");
    private static readonly int BlobDriftAmountId = Shader.PropertyToID("_BlobDriftAmount");
    private static readonly int FieldPhaseOffsetId = Shader.PropertyToID("_FieldPhaseOffset");
    private static readonly int BlobPhaseOffsetId = Shader.PropertyToID("_BlobPhaseOffset");
    private static readonly int SphericalRepeatPowerId = Shader.PropertyToID("_SphericalRepeatPower");

    [Header("Input")]
    [SerializeField] private Texture2D causticTexture;

    [Header("Field")]
    [SerializeField] private Color fieldTint = Color.white;
    [SerializeField] private Vector2 fieldScale = Vector2.one;
    [SerializeField] private Vector2 fieldOffset = Vector2.zero;
    [SerializeField, Range(-1f, 1f)] private float fieldBias = 0f;

    [Header("Blob")]
    [SerializeField] private Color blobTint = new Color(0.7f, 0f, 1f, 1f);
    [SerializeField] private Vector2 blobScale = Vector2.one;
    [SerializeField] private Vector2 blobOffset = new Vector2(0.19f, -0.11f);

    [Header("Motion")]
    [SerializeField] private float driftSpeed = 0.35f;
    [SerializeField] private Vector2 fieldDriftAmount = new Vector2(0.04f, 0.03f);
    [SerializeField] private Vector2 blobDriftAmount = new Vector2(0.06f, 0.05f);
    [SerializeField] private float fieldPhaseOffset = 0f;
    [SerializeField] private float blobPhaseOffset = 1.5708f;
    [SerializeField, Range(0f, 6f)] private float sphericalRepeatPower = 0f;

    [Header("Render")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    private Material fogMaterial;
    private UniversalAdditionalCameraData additionalCameraData;

    private void Awake()
    {
        EnsureMaterial();
        EnsureCameraDepthTexture();
        ApplyMaterialParameters();
    }

    private void OnEnable()
    {
        EnsureMaterial();
        EnsureCameraDepthTexture();
        ApplyMaterialParameters();
    }

    private void OnValidate()
    {
        EnsureCameraDepthTexture();
        ApplyMaterialParameters();
    }

    private void Update()
    {
        ApplyMaterialParameters();
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

    private void EnsureCameraDepthTexture()
    {
        Camera cam = GetComponent<Camera>();
        if (cam == null)
            return;

        if (!cam.TryGetComponent(out additionalCameraData))
            additionalCameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        additionalCameraData.requiresDepthTexture = true;
    }

    private void ApplyMaterialParameters()
    {
        if (fogMaterial == null)
            return;

        fogMaterial.SetTexture(CausticTextureId, causticTexture);
        fogMaterial.SetColor(FieldTintId, fieldTint);
        fogMaterial.SetColor(BlobTintId, blobTint);
        fogMaterial.SetVector(FieldScaleId, new Vector4(fieldScale.x, fieldScale.y, 0f, 0f));
        fogMaterial.SetVector(FieldOffsetId, new Vector4(fieldOffset.x, fieldOffset.y, 0f, 0f));
        fogMaterial.SetVector(BlobScaleId, new Vector4(blobScale.x, blobScale.y, 0f, 0f));
        fogMaterial.SetVector(BlobOffsetId, new Vector4(blobOffset.x, blobOffset.y, 0f, 0f));
        fogMaterial.SetFloat(FieldBiasId, fieldBias);
        fogMaterial.SetFloat(DriftSpeedId, driftSpeed);
        fogMaterial.SetVector(FieldDriftAmountId, new Vector4(fieldDriftAmount.x, fieldDriftAmount.y, 0f, 0f));
        fogMaterial.SetVector(BlobDriftAmountId, new Vector4(blobDriftAmount.x, blobDriftAmount.y, 0f, 0f));
        fogMaterial.SetFloat(FieldPhaseOffsetId, fieldPhaseOffset);
        fogMaterial.SetFloat(BlobPhaseOffsetId, blobPhaseOffset);
        fogMaterial.SetFloat(SphericalRepeatPowerId, sphericalRepeatPower);
    }

    public bool HasCommands(RenderPassEvent evt)
    {
        return isActiveAndEnabled
            && evt == renderPassEvent
            && fogMaterial != null
            && causticTexture != null;
    }

    public bool RequiresColorTexture(RenderPassEvent evt)
    {
        return evt == renderPassEvent;
    }

    public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
    {
        if (!HasCommands(evt))
            return;

        ApplyMaterialParameters();
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
