using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class ReflectionEffectCamera : MonoBehaviour
{
	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
	{
		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (commands.ContainsKey(evt) && commands[evt] != null)
			{
				try { commands[evt].Invoke(commandBuffer, camera); }
				catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
			}
		}

		void OnDestroy() => commands.Clear();
	}

	public RenderTexture renderTexture;
	public Camera textureCamera;

	private Camera mainCamera;
	private Mesh effectMesh;
	private Material effectMaterial;
	private bool isMaterialDynamic;

	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = 0f;

	[SerializeField, Range(1, 120)] private float frostRadius = 64f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);

	Camera overlayCamera;

	void Start()
	{
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("Camera component missing.");
			enabled = false;
			return;
		}

		// Allocate render texture with depth buffer
		renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
		{
			name = "RenderTexture",
			useMipMap = false,
			autoGenerateMips = false,
			filterMode = FilterMode.Bilinear,
			useDynamicScale = true
		};
		renderTexture.Create();

		{
			var obj = new GameObject("RenderCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			textureCamera = obj.AddComponent<Camera>();
			textureCamera.CopyFrom(mainCamera);
			textureCamera.clearFlags = CameraClearFlags.Skybox;
			textureCamera.cullingMask = mainCamera.cullingMask;
			textureCamera.depth = 0;
			textureCamera.enabled = true;
			textureCamera.targetTexture = renderTexture;
			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Base;
		}

		{
			var obj = new GameObject("OverlayCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			overlayCamera = obj.AddComponent<Camera>();
			overlayCamera.clearFlags = CameraClearFlags.Nothing;
			overlayCamera.cullingMask = mainCamera.cullingMask;
			overlayCamera.enabled = true;
			overlayCamera.targetTexture = renderTexture;
			overlayCamera.fieldOfView = mainCamera.fieldOfView * 0.75f;

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraMain", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;

			// Set clearDepth in a safe, “Inspector-like” way
			URPCameraHelper.SetClearDepth(data, false);

			var texturecam_data = textureCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
			texturecam_data.cameraStack.Clear();
			texturecam_data.cameraStack.Add(overlayCamera);
		}

		effectMesh = new Mesh();
		effectMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, renderTexture, null, 0);
		isMaterialDynamic = true;

		{
			CameraCommandProvider provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
			if (provider == null)
				provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

			provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents,
				(cmd, cam) =>
				{
					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
					{
						effectMaterial.SetPass(0);
						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
					}
				}
			);
		}
	}

	public void Update()
	{
		effectMaterial.SetFloat("_Radius", frostRadius);
		effectMaterial.SetColor("_BaseColor", baseColor);
	}

	private void LateUpdate()
	{
		if (overlayCamera != null)
		{
			//SyncCameraProperties(mainCamera, reflectionCameraMain);
			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			overlayCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			overlayCamera.projectionMatrix = mainCamera.projectionMatrix;
		}
	}

	void OnDestroy()
	{
		if (mainCamera != null)
			mainCamera.targetTexture = null;

		if (effectMaterial != null && isMaterialDynamic)
			DestroyImmediate(effectMaterial);

		if (effectMesh != null)
			DestroyImmediate(effectMesh);
	}
}



//using System;
//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class ReflectionEffectCamera : MonoBehaviour
//{
//	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
//	{
//		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

//		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

//		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
//		{
//			if (commands.TryGetValue(evt, out var command))
//			{
//				try { command.Invoke(commandBuffer, camera); }
//				catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
//			}
//		}

//		void OnDestroy() => commands.Clear();
//	}

//	private Camera mainCamera;
//	private Camera reflectionCameraMain;
//	private Camera renderCamera;
//	private Camera reflectionCameraRender;
//	private Mesh effectMesh;
//	private Material effectMaterial;
//	private Matrix4x4 transformMatrix;
//	private bool isMaterialDynamic;
//	private bool isTextureDynamic;
//	private RenderTexture effectTexture; // For frosted effect
//	[HideInInspector] public LayerMask sceneCullingMask = ~0;
//	[SerializeField] private Vector3 planeNormal = Vector3.up;
//	[SerializeField] private float offset = -0.2f;
//	[SerializeField] private bool usePerfectMirror = true;
//	[SerializeField] private bool useSurfaceFilm = false;
//	[SerializeField] private bool useFrostedEffect = false;
//	[SerializeField] private Material customEffectMaterial;
//	[SerializeField] private Texture2D noiseTexture;
//	[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
//	[SerializeField, Range(0.1f, 50f)] private float noiseScale = 1f;
//	[SerializeField, Range(1, 120)] private float frostRadius = 64f;
//	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);
//	[SerializeField, Range(0, 0.1f)] private float noiseStrength = 0.02f;

//	void Awake()
//	{
//		mainCamera = GetComponent<Camera>();
//		if (mainCamera == null)
//		{
//			Debug.LogError("ReflectionEffectCamera: Main camera component missing!", this);
//			enabled = false;
//			return;
//		}

//		sceneCullingMask = mainCamera.cullingMask;
//		mainCamera.clearFlags = CameraClearFlags.Skybox;
//		mainCamera.depth = -2;
//		mainCamera.enabled = true;

//		effectMesh = new Mesh();

//		// Initialize noise texture for surface film or frosted effect
//		if (!usePerfectMirror && (useSurfaceFilm || useFrostedEffect))
//		{
//			noiseTexture = noiseTexture != null ? noiseTexture : TextureUtils.GeneratePerlinNoiseTexture();
//			isTextureDynamic = noiseTexture == null;
//		}

//		// Initialize RenderTexture for frosted effect with depth buffer
//		if (useFrostedEffect)
//		{
//			int width = Screen.width;
//			int height = Screen.height;
//			effectTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
//			{
//				name = "ReflectionTexture",
//				useMipMap = false,
//				autoGenerateMips = false,
//				filterMode = FilterMode.Bilinear,
//				useDynamicScale = true
//			};
//			effectTexture.Create();
//		}

//		UpdateMaterial();

//		transformMatrix = Matrix4x4.identity;

//		// Debug log with safe texture checks
//		string noiseTexName = effectMaterial != null && effectMaterial.HasProperty("_NoiseTex") ? effectMaterial.GetTexture("_NoiseTex")?.name : "null";
//		string mainTexName = effectMaterial != null && effectMaterial.HasProperty("_MainTex") ? effectMaterial.GetTexture("_MainTex")?.name : "null";
//		Debug.Log($"Awake: reflectionMaterial shader={(effectMaterial != null ? effectMaterial.shader.name : "null")}, noiseTexture={noiseTexName}, mainTex={mainTexName}, filmIntensity={filmIntensity}, noiseScale={noiseScale}, frostRadius={frostRadius}, noiseStrength={noiseStrength}, usePerfectMirror={usePerfectMirror}, useSurfaceFilm={useSurfaceFilm}, useFrostedEffect={useFrostedEffect}");

//		// Initialize reflectionCameraMain (overlay, no target texture) for mainCamera stack
//		{
//			var obj = new GameObject("ReflectionCameraMain");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//			obj.transform.SetParent(transform, false);
//			reflectionCameraMain = obj.AddComponent<Camera>();
//			reflectionCameraMain.clearFlags = CameraClearFlags.Nothing;
//			reflectionCameraMain.cullingMask = mainCamera.cullingMask;
//			reflectionCameraMain.depth = 0;
//			reflectionCameraMain.enabled = true;
//			reflectionCameraMain.targetTexture = null;

//			var provider = obj.AddComponent<CameraCommandProvider>();
//			if (provider == null)
//			{
//				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraMain", this);
//				enabled = false;
//				return;
//			}

//			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.renderType = CameraRenderType.Overlay;

//			// Set clearDepth in a safe, “Inspector-like” way
//			URPCameraHelper.SetClearDepth(data, false);
//		}

//		// Initialize renderCamera (base, renders to RenderTexture for frosted effect)
//		if (useFrostedEffect)
//		{
//			var obj = new GameObject("RenderCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//			obj.transform.SetParent(transform, false);
//			renderCamera = obj.AddComponent<Camera>();
//			renderCamera.CopyFrom(mainCamera);
//			renderCamera.clearFlags = CameraClearFlags.Skybox;
//			renderCamera.cullingMask = mainCamera.cullingMask;
//			renderCamera.depth = 0;
//			renderCamera.enabled = true;
//			renderCamera.targetTexture = effectTexture;

//			var provider = obj.AddComponent<CameraCommandProvider>();
//			if (provider == null)
//			{
//				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to RenderCamera", this);
//				enabled = false;
//				return;
//			}

//			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.renderType = CameraRenderType.Base;

//			// Initialize reflectionCameraRender (overlay, no target texture) for renderCamera stack
//			{
//				var renderObj = new GameObject("ReflectionCameraRender");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//				renderObj.transform.SetParent(renderCamera.transform, false);
//				reflectionCameraRender = renderObj.AddComponent<Camera>();
//				reflectionCameraRender.CopyFrom(renderCamera);
//				reflectionCameraRender.clearFlags = CameraClearFlags.Nothing;
//				reflectionCameraRender.cullingMask = mainCamera.cullingMask;
//				reflectionCameraRender.depth = -1;
//				reflectionCameraRender.enabled = true;

//				var renderProvider = renderObj.AddComponent<CameraCommandProvider>();
//				if (renderProvider == null)
//				{
//					Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraRender", this);
//					enabled = false;
//					return;
//				}

//				renderProvider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//				renderProvider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

//				var renderData = renderObj.AddComponent<UniversalAdditionalCameraData>();
//				renderData.renderType = CameraRenderType.Overlay;

//				// Set clearDepth in a safe, “Inspector-like” way
//				URPCameraHelper.SetClearDepth(data, false);
//			}
//		}

//		// Add command provider to main camera for rendering the effect mesh
//		{
//			CameraCommandProvider provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
//			if (provider == null)
//				provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

//			provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents,
//				(cmd, cam) =>
//				{
//					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

//					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
//					{
//						effectMaterial.SetPass(0);
//						cmd.DrawMesh(effectMesh, transformMatrix, effectMaterial, 0, 0);
//					}
//					else
//					{
//						Debug.LogWarning("ReflectionEffectCamera: Invalid reflectionMesh or material", this);
//					}
//				}
//			);
//		}

//		ConfigureCameraStack();
//	}

//	private void UpdateMaterial()
//	{
//		if (effectMaterial != null && isMaterialDynamic)
//		{
//			DestroyImmediate(effectMaterial);
//			effectMaterial = null;
//		}

//		if (customEffectMaterial != null)
//		{
//			effectMaterial = customEffectMaterial;
//			isMaterialDynamic = false;
//			if (useFrostedEffect && effectTexture != null)
//			{
//				effectMaterial.SetTexture("_MainTex", effectTexture);
//				effectMaterial.SetFloat("_Radius", frostRadius);
//				effectMaterial.SetColor("_BaseColor", baseColor);
//				effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
//				if (noiseTexture != null)
//					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//			}
//			else
//			{
//				if (effectMaterial.HasProperty("_MainTex"))
//					effectMaterial.SetTexture("_MainTex", null);
//				if (effectMaterial.HasProperty("_NoiseTex"))
//					effectMaterial.SetTexture("_NoiseTex", null);
//				if (effectMaterial.HasProperty("_Radius"))
//					effectMaterial.SetFloat("_Radius", 0);
//				if (effectMaterial.HasProperty("_NoiseStrength"))
//					effectMaterial.SetFloat("_NoiseStrength", 0);
//				if (effectMaterial.HasProperty("_FilmIntensity"))
//					effectMaterial.SetFloat("_FilmIntensity", 0);
//				if (effectMaterial.HasProperty("_NoiseScale"))
//					effectMaterial.SetFloat("_NoiseScale", 0);
//			}
//		}
//		else if (usePerfectMirror)
//		{
//			effectMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
//			isMaterialDynamic = true;
//		}
//		else if (useSurfaceFilm)
//		{
//			effectMaterial = MaterialUtils.CreateSurfaceFilmMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f), noiseTexture, filmIntensity, noiseScale);
//			isMaterialDynamic = true;
//		}
//		else if (useFrostedEffect)
//		{
//			effectMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, effectTexture, noiseTexture, noiseStrength);
//			isMaterialDynamic = true;
//		}
//		else
//		{
//			Debug.LogWarning("ReflectionEffectCamera: No valid effect selected, falling back to default Unlit");
//			effectMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
//			isMaterialDynamic = true;
//		}

//		if (effectMaterial == null)
//		{
//			Debug.LogWarning("ReflectionEffectCamera: Failed to create material, falling back to default Unlit");
//			effectMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
//			isMaterialDynamic = true;
//		}
//	}

//	private void ConfigureCameraStack()
//	{
//		// Configure mainCamera stack
//		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
//		if (mainCameraData == null)
//			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

//		mainCameraData.renderType = CameraRenderType.Base;
//		mainCameraData.cameraStack.Clear();

//		if (reflectionCameraMain != null)
//			mainCameraData.cameraStack.Add(reflectionCameraMain);

//		// Configure renderCamera stack
//		if (useFrostedEffect && renderCamera != null)
//		{
//			var renderCameraData = renderCamera.GetComponent<UniversalAdditionalCameraData>();
//			if (renderCameraData == null)
//				renderCameraData = renderCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

//			renderCameraData.renderType = CameraRenderType.Base;
//			renderCameraData.cameraStack.Clear();

//			if (reflectionCameraRender != null)
//				renderCameraData.cameraStack.Add(reflectionCameraRender);
//		}
//	}

//	private void SyncCameraProperties(Camera source, Camera target)
//	{
//		if (target != null)
//		{
//			target.fieldOfView = source.fieldOfView;
//			target.nearClipPlane = source.nearClipPlane;
//			target.farClipPlane = source.farClipPlane;
//			target.aspect = source.aspect;
//			target.orthographic = source.orthographic;
//			target.orthographicSize = source.orthographicSize;
//			target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
//		}
//	}

//	public void OnValidate()
//	{
//		UpdateMaterial();
//	}

//	void LateUpdate()
//	{
//		SyncCameraProperties(mainCamera, reflectionCameraMain);
//		if (renderCamera != null)
//		{
//			SyncCameraProperties(mainCamera, renderCamera);
//			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//			renderCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//			renderCamera.projectionMatrix = mainCamera.projectionMatrix;
//		}
//		if (reflectionCameraMain != null)
//		{
//			SyncCameraProperties(mainCamera, reflectionCameraMain);
//			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//			reflectionCameraMain.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//			reflectionCameraMain.projectionMatrix = mainCamera.projectionMatrix;
//		}
//		if (reflectionCameraRender != null)
//		{
//			SyncCameraProperties(mainCamera, reflectionCameraRender);
//			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//			reflectionCameraRender.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//			reflectionCameraRender.projectionMatrix = mainCamera.projectionMatrix;
//		}

//		if (effectMaterial != null)
//		{
//			if (!usePerfectMirror && useFrostedEffect)
//			{
//				effectMaterial.SetTexture("_MainTex", effectTexture);
//				effectMaterial.SetFloat("_Radius", frostRadius);
//				effectMaterial.SetColor("_BaseColor", baseColor);
//				effectMaterial.SetFloat("_NoiseStrength", noiseStrength);
//				if (noiseTexture != null)
//					effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//				if (effectMaterial.HasProperty("_FilmIntensity"))
//					effectMaterial.SetFloat("_FilmIntensity", 0);
//				if (effectMaterial.HasProperty("_NoiseScale"))
//					effectMaterial.SetFloat("_NoiseScale", 0);
//			}
//			else if (!usePerfectMirror && useSurfaceFilm && effectMaterial.shader.name == "Unlit/URPSurfaceFilm")
//			{
//				effectMaterial.SetTexture("_NoiseTex", noiseTexture);
//				effectMaterial.SetFloat("_FilmIntensity", filmIntensity);
//				effectMaterial.SetFloat("_NoiseScale", noiseScale);
//				effectMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f));
//				if (effectMaterial.HasProperty("_MainTex"))
//					effectMaterial.SetTexture("_MainTex", null);
//				if (effectMaterial.HasProperty("_Radius"))
//					effectMaterial.SetFloat("_Radius", 0);
//				if (effectMaterial.HasProperty("_NoiseStrength"))
//					effectMaterial.SetFloat("_NoiseStrength", 0);
//			}
//			else if (usePerfectMirror)
//			{
//				effectMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f));
//				if (effectMaterial.HasProperty("_MainTex"))
//					effectMaterial.SetTexture("_MainTex", null);
//				if (effectMaterial.HasProperty("_NoiseTex"))
//					effectMaterial.SetTexture("_NoiseTex", null);
//				if (effectMaterial.HasProperty("_Radius"))
//					effectMaterial.SetFloat("_Radius", 0);
//				if (effectMaterial.HasProperty("_NoiseStrength"))
//					effectMaterial.SetFloat("_NoiseStrength", 0);
//				if (effectMaterial.HasProperty("_FilmIntensity"))
//					effectMaterial.SetFloat("_FilmIntensity", 0);
//				if (effectMaterial.HasProperty("_NoiseScale"))
//					effectMaterial.SetFloat("_NoiseScale", 0);
//			}
//		}
//	}

//	void OnDestroy()
//	{
//		if (isMaterialDynamic && effectMaterial != null) DestroyImmediate(effectMaterial);
//		if (effectMesh != null) DestroyImmediate(effectMesh);
//		if (reflectionCameraMain != null) DestroyImmediate(reflectionCameraMain.gameObject);
//		if (reflectionCameraRender != null) DestroyImmediate(reflectionCameraRender.gameObject);
//		if (renderCamera != null) DestroyImmediate(renderCamera.gameObject);
//		if (isTextureDynamic && (useSurfaceFilm || useFrostedEffect)) DestroyImmediate(noiseTexture);
//		if (effectTexture != null)
//		{
//			effectTexture.Release();
//			DestroyImmediate(effectTexture);
//		}
//	}
//}