using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceVolumetricFogTemporalSystem : ScreenSpaceVolumetricFogSystem, IScreenSpaceVolumetricFogTemporalProvider
{
    private static readonly int TemporalHistoryBlendId = Shader.PropertyToID("_TemporalHistoryBlend");

    [Header("Temporal")]
    [Tooltip("Blends fog opacity with the previous frame. Zero disables temporal history.")]
    [SerializeField, Range(0.0f, 0.95f)] private float temporalAccumulation = 0.82f;

    private RenderTexture previousTemporalHistoryTexture;
    private RenderTexture currentTemporalHistoryTexture;
    private RTHandle previousTemporalHistoryHandle;
    private RTHandle currentTemporalHistoryHandle;
    private int temporalHistoryWidth;
    private int temporalHistoryHeight;
    private bool temporalHistoryValid;
    private bool temporalHistoryPreparedThisFrame;

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
    }

    protected override void ApplyMaterialParameters(Camera camera)
    {
        base.ApplyMaterialParameters(camera);
        FogMaterial.SetFloat(TemporalHistoryBlendId, temporalHistoryValid ? temporalAccumulation : 0.0f);
    }

    protected override int GetMaterialPassIndex(RenderPassEvent evt)
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

    protected override void OnDestroy()
    {
        ReleaseTemporalHistory();
        base.OnDestroy();
    }
}
