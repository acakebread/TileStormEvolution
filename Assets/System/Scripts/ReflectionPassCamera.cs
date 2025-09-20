using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class ReflectionPassCamera : MonoBehaviour
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
	private Mesh reflectionMesh;
	private Material reflectionMaterial;
	private Matrix4x4 transformMatrix;
	private bool isMaterialDynamic;
	private bool isTextureDynamic;
	[HideInInspector] public LayerMask sceneCullingMask = ~0;
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = -0.2f;
	[SerializeField] private bool usePerfectMirror = true;
	[SerializeField] private bool useSurfaceFilm = false;
	[SerializeField] private Material customReflectionMaterial;
	[SerializeField] private Texture2D noiseTexture;
	[SerializeField, Range(0, 0.5f)] private float filmIntensity = 0.2f;
	[SerializeField, Range(0.1f, 50f)] private float noiseScale = 1f;

	void Awake()
	{
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("ReflectionPassCamera: Main camera component missing!", this);
			enabled = false;
			return;
		}

		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0;
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		reflectionMesh = new Mesh();

		if (!usePerfectMirror && useSurfaceFilm)
		{
			noiseTexture = noiseTexture != null ? noiseTexture : TextureUtils.GeneratePerlinNoiseTexture();
			isTextureDynamic = noiseTexture == null;
		}

		UpdateMaterial();

		transformMatrix = Matrix4x4.identity;

		// Debug log with safe texture checks
		string noiseTexName = reflectionMaterial != null && reflectionMaterial.HasProperty("_NoiseTex") ? reflectionMaterial.GetTexture("_NoiseTex")?.name : "null";
		string mainTexName = reflectionMaterial != null && reflectionMaterial.HasProperty("_MainTex") ? reflectionMaterial.GetTexture("_MainTex")?.name : "null";
		Debug.Log($"Awake: reflectionMaterial shader={(reflectionMaterial != null ? reflectionMaterial.shader.name : "null")}, noiseTexture={noiseTexName}, mainTex={mainTexName}, filmIntensity={filmIntensity}, noiseScale={noiseScale}, usePerfectMirror={usePerfectMirror}, useSurfaceFilm={useSurfaceFilm}");

		InitializeCameras();
		ConfigureCameraStack();
		if (sceneCamera != null)
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(sceneCamera, planeNormal, offset, reflectionMesh);
		else
			Debug.LogWarning("ReflectionPassCamera: sceneCamera is null, skipping mesh generation", this);
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
		}
		else if (usePerfectMirror || !useSurfaceFilm)
		{
			reflectionMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
			isMaterialDynamic = true;
		}
		else if (useSurfaceFilm)
		{
			reflectionMaterial = MaterialUtils.CreateSurfaceFilmMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f), noiseTexture, filmIntensity, noiseScale);
			isMaterialDynamic = true;
		}

		if (reflectionMaterial == null)
		{
			Debug.LogWarning("ReflectionPassCamera: Failed to create material, falling back to default Unlit");
			reflectionMaterial = MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
			isMaterialDynamic = true;
		}
	}

	private void InitializeCameras()
	{
		reflectionCamera = InitializeCamera(
			"ReflectionCamera",
			CameraClearFlags.Depth,
			-1,
			new[] { RenderPassEvent.BeforeRendering, RenderPassEvent.AfterRendering },
			new Action<RasterCommandBuffer, Camera>[] {
				(cmd, cam) => { cmd.SetInvertCulling(true); },
				(cmd, cam) => { cmd.SetInvertCulling(false); }
			}
		);

		sceneCamera = InitializeCamera(
			"SceneCamera",
			CameraClearFlags.Nothing,
			0,
			new[] { RenderPassEvent.AfterRenderingTransparents },
			new Action<RasterCommandBuffer, Camera>[] {
				(cmd, cam) => {
					if (reflectionMesh != null && reflectionMesh.vertexCount >= 3 && reflectionMesh.triangles.Length >= 3 && reflectionMaterial != null)
					{
						reflectionMaterial.SetPass(0);
						cmd.DrawMesh(reflectionMesh, transformMatrix, reflectionMaterial, 0, 0);
					}
					else
					{
						Debug.LogWarning("ReflectionPassCamera: Invalid reflectionMesh or material", this);
					}
				}
			}
		);
	}

	private Camera InitializeCamera(string name, CameraClearFlags clearFlags, int depth, RenderPassEvent[] events, Action<RasterCommandBuffer, Camera>[] commands)
	{
		var obj = new GameObject(name) { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
		obj.transform.SetParent(transform, false);
		var camera = obj.AddComponent<Camera>();
		camera.clearFlags = clearFlags;
		camera.cullingMask = sceneCullingMask;
		camera.depth = depth;
		camera.enabled = true;
		camera.targetTexture = null; // No render texture needed without frosted effect

		var provider = obj.AddComponent<CameraCommandProvider>();
		if (provider == null)
		{
			Debug.LogError($"ReflectionPassCamera: Failed to add CameraCommandProvider to {name}", this);
			enabled = false;
			return null;
		}

		for (int i = 0; i < events.Length; i++)
			provider.RegisterCommand(events[i], commands[i]);

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;
		return camera;
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();

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
		if (reflectionCamera != null)
		{
			SyncCameraProperties(mainCamera, reflectionCamera);
			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

		if (sceneCamera != null)
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(sceneCamera, planeNormal, offset, reflectionMesh);

		if (!usePerfectMirror && useSurfaceFilm && reflectionMaterial != null && reflectionMaterial.shader.name == "Unlit/URPSurfaceFilm")
		{
			reflectionMaterial.SetTexture("_NoiseTex", noiseTexture);
			reflectionMaterial.SetFloat("_FilmIntensity", filmIntensity);
			reflectionMaterial.SetFloat("_NoiseScale", noiseScale);
		}
	}

	void OnDestroy()
	{
		if (isMaterialDynamic && reflectionMaterial != null) DestroyImmediate(reflectionMaterial);
		DestroyImmediate(reflectionMesh);
		DestroyImmediate(reflectionCamera?.gameObject);
		DestroyImmediate(sceneCamera?.gameObject);
		if (isTextureDynamic && useSurfaceFilm) DestroyImmediate(noiseTexture);
	}
}