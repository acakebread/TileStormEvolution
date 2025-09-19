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
	[HideInInspector] public LayerMask sceneCullingMask = ~0;
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = -0.2f;
	[SerializeField] private Material customReflectionMaterial;

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
		reflectionMaterial = customReflectionMaterial != null ? customReflectionMaterial : MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		isMaterialDynamic = customReflectionMaterial == null;
		transformMatrix = Matrix4x4.identity;

		InitializeCameras();
		ConfigureCameraStack();
		if (sceneCamera != null)
			FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(sceneCamera, planeNormal, offset, reflectionMesh);
		else
			Debug.LogWarning("ReflectionPassCamera: sceneCamera is null, skipping mesh generation", this);
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
			new[] { RenderPassEvent.BeforeRendering },
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
		camera.targetTexture = null;

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
	}

	void OnDestroy()
	{
		if (isMaterialDynamic) DestroyImmediate(reflectionMaterial);
		DestroyImmediate(reflectionMesh);
		DestroyImmediate(reflectionCamera?.gameObject);
		DestroyImmediate(sceneCamera?.gameObject);
	}
}