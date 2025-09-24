//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//public class FrostedGlassRenderFeature : ScriptableRendererFeature
//{
//	class FrostedGlassPass : ScriptableRenderPass
//	{
//		private Material frostedMaterial;
//		private RTHandle blurTextureHandle;
//		private RTHandle sourceHandle;
//		private string profilerTag;

//		public FrostedGlassPass(string profilerTag, Material material, RenderTexture blurTexture)
//		{
//			this.profilerTag = profilerTag;
//			this.frostedMaterial = material;
//			this.blurTextureHandle = RTHandles.Alloc(blurTexture);
//		}

//		public void Setup(RTHandle source)
//		{
//			this.sourceHandle = source;
//		}

//		[System.Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.")]
//		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
//		{
//			ConfigureTarget(blurTextureHandle);
//			ConfigureClear(ClearFlag.Color, Color.clear);
//		}

//		[System.Obsolete("This rendering path is for compatibility mode only (when Render Graph is disabled). Use Render Graph API instead.")]
//		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//		{
//			if (frostedMaterial == null || blurTextureHandle == null)
//			{
//				Debug.LogWarning("FrostedGlassPass: Material or blur texture is null, skipping execution.");
//				return;
//			}

//			CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

//			// First pass: Render FrostedBlur to blurTexture
//			cmd.SetGlobalTexture("_MainTex", sourceHandle);
//			cmd.Blit(sourceHandle, blurTextureHandle, frostedMaterial, 0); // Pass index 0 = FrostedBlur

//			// Second pass: Render FrostedFinal to camera target
//			cmd.SetGlobalTexture("_BlurTex", blurTextureHandle);
//#pragma warning disable CS0618 // Suppress obsolete warning for cameraColorTargetHandle
//			cmd.Blit(blurTextureHandle, renderingData.cameraData.renderer.cameraColorTargetHandle, frostedMaterial, 1); // Pass index 1 = FrostedFinal
//#pragma warning restore CS0618

//			context.ExecuteCommandBuffer(cmd);
//			CommandBufferPool.Release(cmd);
//		}

//		public override void OnCameraCleanup(CommandBuffer cmd)
//		{
//			// No additional cleanup needed here
//		}

//		public void Release()
//		{
//			if (blurTextureHandle != null)
//			{
//				blurTextureHandle.Release();
//				blurTextureHandle = null;
//			}
//		}
//	}

//	[SerializeField] private Material frostedMaterial;
//	[SerializeField] private RenderTexture blurTexture;
//	[SerializeField] private RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

//	private FrostedGlassPass frostedPass;

//	public override void Create()
//	{
//		frostedPass = new FrostedGlassPass("FrostedGlassPass", frostedMaterial, blurTexture);
//		frostedPass.renderPassEvent = renderPassEvent;
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//#pragma warning disable CS0618 // Suppress obsolete warning for cameraColorTargetHandle
//		frostedPass.Setup(renderer.cameraColorTargetHandle);
//#pragma warning restore CS0618
//		renderer.EnqueuePass(frostedPass);
//	}

//	protected override void Dispose(bool disposing)
//	{
//		if (disposing)
//		{
//			frostedPass?.Release();
//		}
//	}
//}