using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class ReflectionEffectCamera : MonoBehaviour
{
	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
	{
		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>>();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (commands.TryGetValue(evt, out var command))
				command.Invoke(commandBuffer, camera);
		}

		void OnDestroy() => commands.Clear();
	}

	private Camera mainCamera;
	private Camera reflectionCamera;
	private Camera sceneCamera;
	private Camera frostedCamera; // Camera for frosted effect
	private Mesh reflectionMesh;
	private Material reflectionMaterial;
	private Matrix4x4 transformMatrix;
	private bool isMaterialDynamic;
	private bool isTextureDynamic;
	private RenderTexture reflectionTexture; // For frosted effect
	[HideInInspector] public LayerMask sceneCullingMask = ~0;
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = -0.2f;
	[SerializeField] private bool usePerfectMirror = true;
	[SerializeField] private bool useSurfaceFilm = false;
	[SerializeField] private bool useFrostedEffect = false;
	[SerializeField] private Material customReflectionMaterial;
	[SerializeField] private Texture2D noiseTexture;
	[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
	[SerializeField, Range(0.1f, 50f)] private float noiseScale = 1f;
	[SerializeField, Range(1, 120)] private float frostRadius = 64f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);
	[SerializeField, Range(0, 0.1f)] private float noiseStrength = 0.02f;

	void Awake()
	{
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("ReflectionEffectCamera: Main camera component missing!", this);
			enabled = false;
			return;
		}

		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0;
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		reflectionMesh = new Mesh();

		// Initialize noise texture for surface film or frosted effect
		if (!usePerfectMirror && (useSurfaceFilm || useFrostedEffect))
		{
			noiseTexture = noiseTexture != null ? noiseTexture : TextureUtils.GeneratePerlinNoiseTexture();
			isTextureDynamic = noiseTexture == null;
		}

		// Initialize RenderTexture for frosted effect with depth buffer
		if (useFrostedEffect)
		{
			int width = Screen.width;
			int height = Screen.height;
			reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
			reflectionTexture.filterMode = FilterMode.Bilinear;
			reflectionTexture.useDynamicScale = true;
			reflectionTexture.Create();
		}

		UpdateMaterial();

		transformMatrix = Matrix4x4.identity;

		// Debug log with safe texture checks
		string noiseTexName = reflectionMaterial != null && reflectionMaterial.HasProperty("_NoiseTex") ? reflectionMaterial.GetTexture("_NoiseTex")?.name : "null";
		string mainTexName = reflectionMaterial != null && reflectionMaterial.HasProperty("_MainTex") ? reflectionMaterial.GetTexture("_MainTex")?.name : "null";
		Debug.Log($"Awake: reflectionMaterial shader={(reflectionMaterial != null ? reflectionMaterial.shader.name : "null")}, noiseTexture={noiseTexName}, mainTex={mainTexName}, filmIntensity={filmIntensity}, noiseScale={noiseScale}, frostRadius={frostRadius}, noiseStrength={noiseStrength}, usePerfectMirror={usePerfectMirror}, useSurfaceFilm={useSurfaceFilm}, useFrostedEffect={useFrostedEffect}");

		// Initialize reflection camera (overlay, no target texture)
		{
			var obj = new GameObject("ReflectionCamera") { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			reflectionCamera = obj.AddComponent<Camera>();
			reflectionCamera.clearFlags = CameraClearFlags.Depth;
			reflectionCamera.cullingMask = sceneCullingMask;
			reflectionCamera.depth = -1;
			reflectionCamera.enabled = true;
			reflectionCamera.targetTexture = null;

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCamera", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;
		}

		// Initialize frosted camera (base, renders to RenderTexture)
		if (useFrostedEffect)
		{
			var obj = new GameObject("FrostedCamera") { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			frostedCamera = obj.AddComponent<Camera>();
			frostedCamera.CopyFrom(mainCamera);
			frostedCamera.clearFlags = CameraClearFlags.Skybox;
			frostedCamera.cullingMask = sceneCullingMask;
			frostedCamera.depth = -3;
			frostedCamera.enabled = true;
			frostedCamera.targetTexture = reflectionTexture;

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to FrostedCamera", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Base;
		}

		// Initialize scene camera (overlay, draws reflection mesh)
		{
			var obj = new GameObject("SceneCamera") { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			sceneCamera = obj.AddComponent<Camera>();
			sceneCamera.clearFlags = CameraClearFlags.Nothing;
			sceneCamera.cullingMask = sceneCullingMask;
			sceneCamera.depth = 0;
			sceneCamera.enabled = true;
			sceneCamera.targetTexture = null;

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to SceneCamera", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents, (cmd, cam) =>
			{
				if (reflectionMesh != null && reflectionMesh.vertexCount >= 3 && reflectionMesh.triangles.Length >= 3 && reflectionMaterial != null)
				{
					reflectionMaterial.SetPass(0);
					cmd.DrawMesh(reflectionMesh, transformMatrix, reflectionMaterial, 0, 0);
				}
				else
				{
					Debug.LogWarning("ReflectionEffectCamera: Invalid reflectionMesh or material", this);
				}
			});

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;
		}

		ConfigureCameraStack();

		if (sceneCamera != null)
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(sceneCamera, planeNormal, offset, reflectionMesh);
		else
			Debug.LogWarning("ReflectionEffectCamera: sceneCamera is null, skipping mesh generation", this);
	}

	private void UpdateMaterial()
	{
		// Destroy old material only if dynamic
		if (reflectionMaterial != null && isMaterialDynamic)
		{
			DestroyImmediate(reflectionMaterial);
			reflectionMaterial = null;
		}

		if (customReflectionMaterial != null)
		{
			reflectionMaterial = customReflectionMaterial;
			isMaterialDynamic = false;
			// Update properties only for frosted effect
			if (useFrostedEffect && reflectionTexture != null)
			{
				reflectionMaterial.SetTexture("_MainTex", reflectionTexture);
				reflectionMaterial.SetFloat("_Radius", frostRadius);
				reflectionMaterial.SetColor("_BaseColor", baseColor);
				reflectionMaterial.SetFloat("_NoiseStrength", noiseStrength);
				if (noiseTexture != null)
					reflectionMaterial.SetTexture("_NoiseTex", noiseTexture);
			}
			else
			{
				// Clear effect-specific properties for non-frosted modes
				if (reflectionMaterial.HasProperty("_MainTex"))
					reflectionMaterial.SetTexture("_MainTex", null);
				if (reflectionMaterial.HasProperty("_NoiseTex"))
					reflectionMaterial.SetTexture("_NoiseTex", null);
				if (reflectionMaterial.HasProperty("_Radius"))
					reflectionMaterial.SetFloat("_Radius", 0);
				if (reflectionMaterial.HasProperty("_NoiseStrength"))
					reflectionMaterial.SetFloat("_NoiseStrength", 0);
				if (reflectionMaterial.HasProperty("_FilmIntensity"))
					reflectionMaterial.SetFloat("_FilmIntensity", 0);
				if (reflectionMaterial.HasProperty("_NoiseScale"))
					reflectionMaterial.SetFloat("_NoiseScale", 0);
			}
		}
		else if (usePerfectMirror)
		{
			reflectionMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f)); // Restored original darkened color
			isMaterialDynamic = true;
		}
		else if (useSurfaceFilm)
		{
			reflectionMaterial = MaterialUtils.CreateSurfaceFilmMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f), noiseTexture, filmIntensity, noiseScale); // Darkened color
			isMaterialDynamic = true;
		}
		else if (useFrostedEffect)
		{
			reflectionMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, reflectionTexture, noiseTexture, noiseStrength);
			isMaterialDynamic = true;
		}
		else
		{
			Debug.LogWarning("ReflectionEffectCamera: No valid effect selected, falling back to default Unlit");
			reflectionMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
			isMaterialDynamic = true;
		}

		if (reflectionMaterial == null)
		{
			Debug.LogWarning("ReflectionEffectCamera: Failed to create material, falling back to default Unlit");
			reflectionMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
			isMaterialDynamic = true;
		}
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();

		// Only add overlay cameras to the stack
		if (reflectionCamera != null)
			mainCameraData.cameraStack.Add(reflectionCamera);
		if (sceneCamera != null)
			mainCameraData.cameraStack.Add(sceneCamera);
	}

	private void SyncCameraProperties(Camera source, Camera target)
	{
		if (target != null)
		{
			target.fieldOfView = source.fieldOfView;
			target.nearClipPlane = source.nearClipPlane;
			target.farClipPlane = source.farClipPlane;
			target.aspect = source.aspect;
			target.orthographic = source.orthographic;
			target.orthographicSize = source.orthographicSize;
			target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
		}
	}

	public void OnValidate()
	{
		UpdateMaterial();
	}

	void LateUpdate()
	{
		SyncCameraProperties(mainCamera, sceneCamera);
		if (frostedCamera != null)
		{
			SyncCameraProperties(mainCamera, frostedCamera);
			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			frostedCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			frostedCamera.projectionMatrix = mainCamera.projectionMatrix;
		}
		if (reflectionCamera != null)
		{
			SyncCameraProperties(mainCamera, reflectionCamera);
			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

		if (sceneCamera != null)
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(sceneCamera, planeNormal, offset, reflectionMesh);

		// Update material properties based on mode
		if (reflectionMaterial != null)
		{
			if (!usePerfectMirror && useFrostedEffect)
			{
				reflectionMaterial.SetTexture("_MainTex", reflectionTexture);
				reflectionMaterial.SetFloat("_Radius", frostRadius);
				reflectionMaterial.SetColor("_BaseColor", baseColor);
				reflectionMaterial.SetFloat("_NoiseStrength", noiseStrength);
				if (noiseTexture != null)
					reflectionMaterial.SetTexture("_NoiseTex", noiseTexture);
				// Clear surface film properties
				if (reflectionMaterial.HasProperty("_FilmIntensity"))
					reflectionMaterial.SetFloat("_FilmIntensity", 0);
				if (reflectionMaterial.HasProperty("_NoiseScale"))
					reflectionMaterial.SetFloat("_NoiseScale", 0);
			}
			else if (!usePerfectMirror && useSurfaceFilm && reflectionMaterial.shader.name == "Unlit/URPSurfaceFilm")
			{
				reflectionMaterial.SetTexture("_NoiseTex", noiseTexture);
				reflectionMaterial.SetFloat("_FilmIntensity", filmIntensity);
				reflectionMaterial.SetFloat("_NoiseScale", noiseScale);
				reflectionMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f)); // Ensure consistent color
																							  // Clear frosted properties
				if (reflectionMaterial.HasProperty("_MainTex"))
					reflectionMaterial.SetTexture("_MainTex", null);
				if (reflectionMaterial.HasProperty("_Radius"))
					reflectionMaterial.SetFloat("_Radius", 0);
				if (reflectionMaterial.HasProperty("_NoiseStrength"))
					reflectionMaterial.SetFloat("_NoiseStrength", 0);
			}
			else if (usePerfectMirror)
			{
				reflectionMaterial.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f)); // Ensure consistent color
																							  // Clear all effect-specific properties
				if (reflectionMaterial.HasProperty("_MainTex"))
					reflectionMaterial.SetTexture("_MainTex", null);
				if (reflectionMaterial.HasProperty("_NoiseTex"))
					reflectionMaterial.SetTexture("_NoiseTex", null);
				if (reflectionMaterial.HasProperty("_Radius"))
					reflectionMaterial.SetFloat("_Radius", 0);
				if (reflectionMaterial.HasProperty("_NoiseStrength"))
					reflectionMaterial.SetFloat("_NoiseStrength", 0);
				if (reflectionMaterial.HasProperty("_FilmIntensity"))
					reflectionMaterial.SetFloat("_FilmIntensity", 0);
				if (reflectionMaterial.HasProperty("_NoiseScale"))
					reflectionMaterial.SetFloat("_NoiseScale", 0);
			}
		}
	}

	void OnDestroy()
	{
		if (isMaterialDynamic && reflectionMaterial != null) DestroyImmediate(reflectionMaterial);
		if (reflectionMesh != null) DestroyImmediate(reflectionMesh);
		if (reflectionCamera != null) DestroyImmediate(reflectionCamera.gameObject);
		if (sceneCamera != null) DestroyImmediate(sceneCamera.gameObject);
		if (frostedCamera != null) DestroyImmediate(frostedCamera.gameObject);
		if (isTextureDynamic && (useSurfaceFilm || useFrostedEffect)) DestroyImmediate(noiseTexture);
		if (reflectionTexture != null) DestroyImmediate(reflectionTexture);
	}
}