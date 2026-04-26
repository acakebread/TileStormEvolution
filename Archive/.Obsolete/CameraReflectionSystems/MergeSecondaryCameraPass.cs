//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine;
//using UnityEngine.Rendering.Universal;


//[RequireComponent(typeof(Camera))]
//public class SecondaryCameraTarget : MonoBehaviour
//{
//	[HideInInspector] public Camera cam;
//	[HideInInspector] public RenderTexture colorRT;
//	[HideInInspector] public RenderTexture depthRT;

//	void Awake()
//	{
//		cam = GetComponent<Camera>();

//		int width = Screen.width;
//		int height = Screen.height;

//		colorRT = new RenderTexture(width, height, 0, RenderTextureFormat.DefaultHDR);
//		depthRT = new RenderTexture(width, height, 24, RenderTextureFormat.Depth);

//		cam.targetTexture = colorRT;
//		cam.SetTargetBuffers(colorRT.colorBuffer, depthRT.depthBuffer);
//		cam.clearFlags = CameraClearFlags.SolidColor;
//		cam.backgroundColor = Color.clear;
//	}

//	public void RenderToRT()
//	{
//		cam.Render();
//	}

//	void OnDestroy()
//	{
//		if (colorRT != null) colorRT.Release();
//		if (depthRT != null) depthRT.Release();
//	}
//}

//public class MergeSecondaryCameraPass : ScriptableRenderPass
//{
//	private SecondaryCameraTarget secondary;
//	private Material blitDepthMat;

//	public MergeSecondaryCameraPass(SecondaryCameraTarget secondaryCamera)
//	{
//		secondary = secondaryCamera;
//		renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

//		// Depth-only blit shader
//		blitDepthMat = new Material(Shader.Find("Hidden/CopyDepth"));
//	}

//	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//	{
//		if (secondary == null || secondary.colorRT == null || secondary.depthRT == null)
//			return;

//		CommandBuffer cmd = CommandBufferPool.Get("MergeSecondaryCamera");

//		// 1. Render secondary camera into its RT
//		secondary.RenderToRT();

//		// 2. Copy depth into main camera
//		cmd.Blit(secondary.depthRT, renderingData.cameraData.renderer.cameraColorTarget, blitDepthMat);

//		// 3. Blit color on top of main camera
//		cmd.Blit(secondary.colorRT, renderingData.cameraData.renderer.cameraColorTarget);

//		context.ExecuteCommandBuffer(cmd);
//		CommandBufferPool.Release(cmd);
//	}
//}




//public class MergeSecondaryCameraFeature : ScriptableRendererFeature
//{
//	public SecondaryCameraTarget secondaryCamera;
//	private MergeSecondaryCameraPass pass;

//	public override void Create()
//	{
//		if (secondaryCamera != null)
//			pass = new MergeSecondaryCameraPass(secondaryCamera);
//	}

//	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//	{
//		if (pass != null)
//			renderer.EnqueuePass(pass);
//	}
//}
