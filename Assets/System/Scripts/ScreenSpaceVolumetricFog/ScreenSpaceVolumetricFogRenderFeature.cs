using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public interface IScreenSpaceVolumetricFogTemporalProvider
{
    bool RequiresTemporalHistory(RenderPassEvent evt);
    void PrepareTemporalHistory(RenderPassEvent evt, Camera camera, RenderTextureDescriptor cameraDescriptor);
    RTHandle GetPreviousTemporalHistory(RenderPassEvent evt);
    RTHandle GetCurrentTemporalHistory(RenderPassEvent evt);
    void CompleteTemporalHistory(RenderPassEvent evt, Camera camera);
}

public class ScreenSpaceVolumetricFogRenderPass : ScriptableRenderPass
{
    private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
    private static readonly int DirectCameraDepthTextureId = Shader.PropertyToID("_DirectCameraDepthTexture");
    private static readonly int DirectTemporalHistoryTextureId = Shader.PropertyToID("_DirectTemporalHistoryTexture");

    private readonly string passLabel;

    public ScreenSpaceVolumetricFogRenderPass(RenderPassEvent renderEvent)
    {
        passLabel = $"ScreenSpaceVolumetricFog_{renderEvent}";
        renderPassEvent = renderEvent;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        Camera camera = cameraData.camera;
        ScreenSpaceVolumetricFogSystem fog = camera.GetComponent<ScreenSpaceVolumetricFogSystem>();
        if (fog == null || !fog.HasCommands(renderPassEvent))
            return;

        TextureHandle sourceColor = TextureHandle.nullHandle;
        if (fog.RequiresColorTexture(renderPassEvent))
        {
            var sourceDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
            sourceDesc.name = $"{passLabel}_SourceColor";
            sourceDesc.clearBuffer = false;

            sourceColor = renderGraph.CreateTexture(sourceDesc);
            renderGraph.AddBlitPass(resourceData.activeColorTexture, sourceColor, Vector2.one, Vector2.zero, passName: $"{passLabel}_CopySource");
        }

        bool requiresActiveDepthTexture = fog.RequiresActiveDepthTexture(renderPassEvent);
        TextureHandle sourceDepth = requiresActiveDepthTexture ? resourceData.cameraDepth : TextureHandle.nullHandle;

        TextureHandle previousTemporalHistory = TextureHandle.nullHandle;
        TextureHandle currentTemporalHistory = TextureHandle.nullHandle;
        IScreenSpaceVolumetricFogTemporalProvider temporalProvider = fog as IScreenSpaceVolumetricFogTemporalProvider;
        bool requiresTemporalHistory = temporalProvider != null && temporalProvider.RequiresTemporalHistory(renderPassEvent);
        if (requiresTemporalHistory)
        {
            temporalProvider.PrepareTemporalHistory(renderPassEvent, camera, cameraData.cameraTargetDescriptor);
            RTHandle previousHistory = temporalProvider.GetPreviousTemporalHistory(renderPassEvent);
            RTHandle currentHistory = temporalProvider.GetCurrentTemporalHistory(renderPassEvent);
            if (previousHistory != null && currentHistory != null)
            {
                previousTemporalHistory = renderGraph.ImportTexture(previousHistory);
                currentTemporalHistory = renderGraph.ImportTexture(currentHistory);
            }
            else
            {
                requiresTemporalHistory = false;
            }
        }

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(passLabel, out var passData))
        {
            passData.camera = camera;
            passData.fog = fog;
            passData.temporalProvider = requiresTemporalHistory ? temporalProvider : null;
            passData.colorSource = sourceColor;
            passData.depthSource = sourceDepth;
            passData.previousTemporalHistory = previousTemporalHistory;
            passData.currentTemporalHistory = currentTemporalHistory;

            if (sourceColor.IsValid())
                builder.UseTexture(passData.colorSource, AccessFlags.Read);
            if (sourceDepth.IsValid())
                builder.UseTexture(passData.depthSource, AccessFlags.Read);
            if (previousTemporalHistory.IsValid())
                builder.UseTexture(passData.previousTemporalHistory, AccessFlags.Read);

            builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
            if (currentTemporalHistory.IsValid())
                builder.SetRenderAttachment(passData.currentTemporalHistory, 1, AccessFlags.Write);
            if (renderPassEvent != RenderPassEvent.BeforeRendering && !requiresActiveDepthTexture)
                builder.SetRenderAttachmentDepth(resourceData.cameraDepth, AccessFlags.ReadWrite);

            builder.AllowPassCulling(false);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                if (data.colorSource.IsValid())
                    context.cmd.SetGlobalTexture(BlitTextureId, data.colorSource);
                if (data.depthSource.IsValid())
                    context.cmd.SetGlobalTexture(DirectCameraDepthTextureId, data.depthSource);
                if (data.previousTemporalHistory.IsValid())
                    context.cmd.SetGlobalTexture(DirectTemporalHistoryTextureId, data.previousTemporalHistory);

                data.fog.ExecuteCommands(renderPassEvent, context.cmd, data.camera);
                data.temporalProvider?.CompleteTemporalHistory(renderPassEvent, data.camera);
            });
        }
    }

    private class PassData
    {
        public ScreenSpaceVolumetricFogSystem fog;
        public IScreenSpaceVolumetricFogTemporalProvider temporalProvider;
        public Camera camera;
        public TextureHandle colorSource;
        public TextureHandle depthSource;
        public TextureHandle previousTemporalHistory;
        public TextureHandle currentTemporalHistory;
    }
}

[CreateAssetMenu(menuName = "Rendering/Screen Space Volumetric Fog")]
public class ScreenSpaceVolumetricFogRenderFeature : ScriptableRendererFeature
{
    private Dictionary<RenderPassEvent, ScreenSpaceVolumetricFogRenderPass> renderPasses;

    public override void Create()
    {
        renderPasses = new Dictionary<RenderPassEvent, ScreenSpaceVolumetricFogRenderPass>();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        ScreenSpaceVolumetricFogSystem fog = camera.GetComponent<ScreenSpaceVolumetricFogSystem>();
        if (fog == null)
            return;

        foreach (RenderPassEvent evt in System.Enum.GetValues(typeof(RenderPassEvent)))
        {
            if (!fog.HasCommands(evt))
                continue;

            renderPasses ??= new Dictionary<RenderPassEvent, ScreenSpaceVolumetricFogRenderPass>();
            if (!renderPasses.TryGetValue(evt, out ScreenSpaceVolumetricFogRenderPass pass))
            {
                pass = new ScreenSpaceVolumetricFogRenderPass(evt);
                renderPasses[evt] = pass;
            }

            renderer.EnqueuePass(pass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        renderPasses?.Clear();
    }
}
