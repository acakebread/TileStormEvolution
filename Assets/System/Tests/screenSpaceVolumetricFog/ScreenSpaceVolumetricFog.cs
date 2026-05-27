using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using MassiveHadronLtd;

public class ScreenSpaceVolumetricFog : MonoBehaviour, IDirectCommandProvider
{
    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");

    [Header("Render")]
    [SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

    [Header("Fog")]
    [SerializeField] private Color fogColor = new Color(0.75f, 0.55f, 1.0f, 1.0f);

    private Material fogMaterial;

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

    private void ApplyMaterialParameters()
    {
        fogMaterial.SetColor(FogColorId, fogColor);
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
