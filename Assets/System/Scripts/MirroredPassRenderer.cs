//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using UnityEngine.Rendering.RenderGraphModule;

//[RequireComponent(typeof(Camera))]
//public class MirroredPassRenderer : MonoBehaviour
//{
//	[SerializeField] private Vector3 planeNormal = Vector3.up;
//	[SerializeField] private float offset;
//	[SerializeField, Range(float.Epsilon, 1f)] private float brightness = 1f;

//	private Camera _mainCam;
//	private Camera _mirrorCam;
//	private Light[] _sceneLights;
//	private float[] _originalLightIntensities;
//	private Color _originalAmbientLight;
//	private float _originalAmbientIntensity;
//	private CameraClearFlags _originalCameraClearFlags;
//	private MirrorRenderFeature _renderFeature;

//	private void Start()
//	{
//		InitializeMainCamera();
//		InitializeMirrorCamera();
//		InitializeSceneLights();
//		InitializeRenderFeature();
//	}

//	private void InitializeMainCamera()
//	{
//		_mainCam = GetComponent<Camera>();
//		if (_mainCam == null)
//		{
//			Debug.LogError("Main camera not found!");
//			enabled = false;
//			return;
//		}
//		_originalCameraClearFlags = _mainCam.clearFlags;
//		_mainCam.clearFlags = CameraClearFlags.Nothing; // Preserve mirror camera output
//		Debug.Log("Main camera initialized with ClearFlags.Nothing");
//	}

//	private void InitializeMirrorCamera()
//	{
//		var camObj = new GameObject("MirrorCamera");
//		_mirrorCam = camObj.AddComponent<Camera>();
//		_mirrorCam.enabled = false;
//		_mirrorCam.CopyFrom(_mainCam);
//		_mirrorCam.clearFlags = CameraClearFlags.SolidColor;
//		_mirrorCam.backgroundColor = Color.red; // Red for visibility
//		_mirrorCam.depth = _mainCam.depth - 1;
//		_mirrorCam.cullingMask = _mainCam.cullingMask; // Render same layers as main camera
//		Debug.Log("Mirror camera initialized with red clear color.");
//	}

//	private void InitializeSceneLights()
//	{
//		_sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
//		_originalLightIntensities = new float[_sceneLights.Length];
//		for (var i = 0; i < _sceneLights.Length; i++)
//		{
//			if (_sceneLights[i] != null)
//			{
//				_originalLightIntensities[i] = _sceneLights[i].intensity;
//			}
//		}
//		_originalAmbientLight = RenderSettings.ambientLight;
//		_originalAmbientIntensity = RenderSettings.ambientIntensity;
//	}

//	private void InitializeRenderFeature()
//	{
//		_renderFeature = ScriptableObject.CreateInstance<MirrorRenderFeature>();
//		_renderFeature.Initialize(_mirrorCam, _mainCam, planeNormal, offset, _sceneLights, _originalLightIntensities, _originalAmbientLight, _originalAmbientIntensity, brightness);

//		// Add the render feature to the URP renderer
//		var urpAsset = UniversalRenderPipeline.asset;
//		if (urpAsset != null)
//		{
//			var rendererDataList = urpAsset.GetType().GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(urpAsset) as ScriptableRendererData[];
//			if (rendererDataList != null && rendererDataList.Length > 0)
//			{
//				var rendererData = rendererDataList[0]; // Use the first renderer (e.g., Universal Renderer)
//				rendererData.rendererFeatures.Add(_renderFeature);
//				rendererData.SetDirty();
//				Debug.Log("MirrorRenderFeature successfully added to URP renderer.");
//			}
//			else
//			{
//				Debug.LogError("No renderer data found in URP asset!");
//				enabled = false;
//			}
//		}
//		else
//		{
//			Debug.LogError("URP asset not found!");
//			enabled = false;
//		}
//	}

//	private void OnDestroy()
//	{
//		if (_mirrorCam != null)
//		{
//			Destroy(_mirrorCam.gameObject);
//		}

//		if (_mainCam != null)
//		{
//			_mainCam.clearFlags = _originalCameraClearFlags;
//		}

//		// Clean up render feature
//		if (_renderFeature != null)
//		{
//			var urpAsset = UniversalRenderPipeline.asset;
//			if (urpAsset != null)
//			{
//				var rendererDataList = urpAsset.GetType().GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(urpAsset) as ScriptableRendererData[];
//				if (rendererDataList != null && rendererDataList.Length > 0)
//				{
//					var rendererData = rendererDataList[0];
//					rendererData.rendererFeatures.Remove(_renderFeature);
//					rendererData.SetDirty();
//				}
//			}
//			Destroy(_renderFeature);
//		}
//	}

//	// Custom Render Feature
//	public class MirrorRenderFeature : ScriptableRendererFeature
//	{
//		private MirrorRenderPass _mirrorRenderPass;

//		public void Initialize(Camera mirrorCam, Camera mainCam, Vector3 planeNormal, float offset, Light[] sceneLights, float[] originalLightIntensities, Color originalAmbientLight, float originalAmbientIntensity, float brightness)
//		{
//			_mirrorRenderPass = new MirrorRenderPass(mirrorCam, mainCam, planeNormal, offset, sceneLights, originalLightIntensities, originalAmbientLight, originalAmbientIntensity, brightness);
//		}

//		public override void Create()
//		{
//			// No additional creation logic needed
//		}

//		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
//		{
//			if (_mirrorRenderPass != null && renderingData.cameraData.camera == _mirrorRenderPass.MainCamera)
//			{
//				renderer.EnqueuePass(_mirrorRenderPass);
//			}
//		}
//	}

//	// Custom Render Pass
//	private class MirrorRenderPass : ScriptableRenderPass
//	{
//		private readonly Camera _mirrorCam;
//		private readonly Camera _mainCam;
//		private readonly Vector3 _planeNormal;
//		private readonly float _offset;
//		private readonly Light[] _sceneLights;
//		private readonly float[] _originalLightIntensities;
//		private readonly Color _originalAmbientLight;
//		private float _originalAmbientIntensity;
//		private readonly float _brightness;

//		public Camera MainCamera => _mainCam;

//		public MirrorRenderPass(Camera mirrorCam, Camera mainCam, Vector3 planeNormal, float offset, Light[] sceneLights, float[] originalLightIntensities, Color originalAmbientLight, float originalAmbientIntensity, float brightness)
//		{
//			_mirrorCam = mirrorCam;
//			_mainCam = mainCam;
//			_planeNormal = planeNormal;
//			_offset = offset;
//			_sceneLights = sceneLights;
//			_originalLightIntensities = originalLightIntensities;
//			_originalAmbientLight = originalAmbientLight;
//			_originalAmbientIntensity = originalAmbientIntensity;
//			_brightness = brightness;
//			renderPassEvent = RenderPassEvent.BeforeRenderingOpaques; // Render before main camera's opaque objects
//		}

//		// Legacy Execute method for compatibility mode
//		[System.Obsolete]
//		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
//		{
//			// Fallback for non-Render Graph mode
//			RenderMirrorPass(context);
//		}

//		public override void RecordRenderGraph(RenderGraph renderGraph, ref RenderingData renderingData)
//		{
//			if (!_mirrorCam || !_mainCam)
//			{
//				Debug.LogWarning("Mirror or main camera is null, skipping render pass.");
//				return;
//			}

//			using (var builder = renderGraph.AddRenderPass<RasterRenderPass>("MirrorRenderPass", out var passData))
//			{
//				Debug.Log("Recording MirrorRenderPass in Render Graph");

//				// Set up reflection matrix
//				var normalizedNormal = _planeNormal.normalized;
//				var pointOnPlane = normalizedNormal * _offset;

//				var reflectionMat = Matrix4x4.identity;
//				reflectionMat[0, 0] = 1 - 2 * normalizedNormal.x * normalizedNormal.x;
//				reflectionMat[0, 1] = -2 * normalizedNormal.x * normalizedNormal.y;
//				reflectionMat[0, 2] = -2 * normalizedNormal.x * normalizedNormal.z;
//				reflectionMat[1, 0] = -2 * normalizedNormal.y * normalizedNormal.x;
//				reflectionMat[1, 1] = 1 - 2 * normalizedNormal.y * normalizedNormal.y;
//				reflectionMat[1, 2] = -2 * normalizedNormal.y * normalizedNormal.z;
//				reflectionMat[2, 0] = -2 * normalizedNormal.z * normalizedNormal.x;
//				reflectionMat[2, 1] = -2 * normalizedNormal.z * normalizedNormal.y;
//				reflectionMat[2, 2] = 1 - 2 * normalizedNormal.z * normalizedNormal.z;

//				var translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
//				var translateBack = Matrix4x4.Translate(pointOnPlane);
//				reflectionMat = translateBack * reflectionMat * translateToOrigin;

//				// Update mirror camera transform
//				_mirrorCam.worldToCameraMatrix = _mainCam.worldToCameraMatrix * reflectionMat;
//				_mirrorCam.rect = new Rect(0, 0, 1, 1);
//				Debug.Log($"Mirror camera worldToCameraMatrix: {_mirrorCam.worldToCameraMatrix}");

//				// Adjust lighting
//				for (var i = 0; i < _sceneLights.Length; i++)
//				{
//					if (_sceneLights[i] != null && _sceneLights[i].enabled)
//					{
//						_sceneLights[i].intensity = _originalLightIntensities[i] * _brightness;
//					}
//				}

//				RenderSettings.ambientLight = _originalAmbientLight * _brightness;
//				RenderSettings.ambientIntensity = _originalAmbientIntensity * _brightness;

//				// Set up culling inversion
//				passData.cmd = CommandBufferPool.Get("MirrorCameraCullingFix");
//				Debug.Log("Inverting culling for mirror camera");
//				passData.cmd.SetInvertCulling(true);

//				// Configure render target
//				passData.camera = _mirrorCam;

//				// Set up the pass
//				builder.SetRenderFunc((RasterRenderPass pass, RenderGraphContext ctx) =>
//				{
//					ctx.cmd.ExecuteCommandBuffer(passData.cmd);
//					CommandBufferPool.Release(passData.cmd);

//					// Render the mirror camera
//					ctx.renderContext.ExecuteSingleCamera(_mirrorCam, ref renderingData);

//					// Reset lighting
//					for (var i = 0; i < _sceneLights.Length; i++)
//					{
//						if (_sceneLights[i] != null && _sceneLights[i].enabled)
//						{
//							_sceneLights[i].intensity = _originalLightIntensities[i];
//						}
//					}

//					RenderSettings.ambientLight = _originalAmbientLight;
//					RenderSettings.ambientIntensity = _originalAmbientIntensity;

//					// Reset culling
//					var resetCmd = CommandBufferPool.Get("MainCameraCullingReset");
//					resetCmd.SetInvertCulling(false);
//					ctx.cmd.ExecuteCommandBuffer(resetCmd);
//					CommandBufferPool.Release(resetCmd);
//				});
//			}
//		}

//		private void RenderMirrorPass(ScriptableRenderContext context)
//		{
//			if (!_mirrorCam || !_mainCam)
//			{
//				Debug.LogWarning("Mirror or main camera is null, skipping render pass.");
//				return;
//			}

//			Debug.Log("Executing MirrorRenderPass (legacy)");

//			// Set up reflection matrix
//			var normalizedNormal = _planeNormal.normalized;
//			var pointOnPlane = normalizedNormal * _offset;

//			var reflectionMat = Matrix4x4.identity;
//			reflectionMat[0, 0] = 1 - 2 * normalizedNormal.x * normalizedNormal.x;
//			reflectionMat[0, 1] = -2 * normalizedNormal.x * normalizedNormal.y;
//			reflectionMat[0, 2] = -2 * normalizedNormal.x * normalizedNormal.z;
//			reflectionMat[1, 0] = -2 * normalizedNormal.y * normalizedNormal.x;
//			reflectionMat[1, 1] = 1 - 2 * normalizedNormal.y * normalizedNormal.y;
//			reflectionMat[1, 2] = -2 * normalizedNormal.y * normalizedNormal.z;
//			reflectionMat[2, 0] = -2 * normalizedNormal.z * normalizedNormal.x;
//			reflectionMat[2, 1] = -2 * normalizedNormal.z * normalizedNormal.y;
//			reflectionMat[2, 2] = 1 - 2 * normalizedNormal.z * normalizedNormal.z;

//			var translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
//			var translateBack = Matrix4x4.Translate(pointOnPlane);
//			reflectionMat = translateBack * reflectionMat * translateToOrigin;

//			// Update mirror camera transform
//			_mirrorCam.worldToCameraMatrix = _mainCam.worldToCameraMatrix * reflectionMat;
//			_mirrorCam.rect = new Rect(0, 0, 1, 1);
//			Debug.Log($"Mirror camera worldToCameraMatrix: {_mirrorCam.worldToCameraMatrix}");

//			// Set up culling inversion
//			CommandBuffer cmd = CommandBufferPool.Get("MirrorCameraCullingFix");
//			Debug.Log("Inverting culling for mirror camera");
//			cmd.SetInvertCulling(true);
//			context.ExecuteCommandBuffer(cmd);
//			CommandBufferPool.Release(cmd);

//			// Adjust lighting
//			for (var i = 0; i < _sceneLights.Length; i++)
//			{
//				if (_sceneLights[i] != null && _sceneLights[i].enabled)
//				{
//					_sceneLights[i].intensity = _originalLightIntensities[i] * _brightness;
//				}
//			}

//			RenderSettings.ambientLight = _originalAmbientLight * _brightness;
//			RenderSettings.ambientIntensity = _originalAmbientIntensity * _brightness;

//			// Render the mirror camera
//#pragma warning disable CS0618
//			UniversalRenderPipeline.RenderSingleCamera(context, _mirrorCam);
//#pragma warning restore CS0618

//			// Reset lighting
//			for (var i = 0; i < _sceneLights.Length; i++)
//			{
//				if (_sceneLights[i] != null && _sceneLights[i].enabled)
//				{
//					_sceneLights[i].intensity = _originalLightIntensities[i];
//				}
//			}

//			RenderSettings.ambientLight = _originalAmbientLight;
//			RenderSettings.ambientIntensity = _originalAmbientIntensity;

//			// Reset culling
//			cmd = CommandBufferPool.Get("MainCameraCullingReset");
//			cmd.SetInvertCulling(false);
//			context.ExecuteCommandBuffer(cmd);
//			CommandBufferPool.Release(cmd);
//		}
//	}
//}