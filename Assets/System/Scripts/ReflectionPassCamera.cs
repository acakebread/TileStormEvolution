using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.Reflection;

[RequireComponent(typeof(Camera))]
public class ReflectionPassCamera : MonoBehaviour
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
				try
				{
					commands[evt].Invoke(commandBuffer, camera);
				}
				catch (Exception e)
				{
					Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}");
				}
			}
		}

		void OnDestroy() => commands.Clear();
	}

	private Camera mainCamera;
	private Camera reflectionCamera;
	private Camera effectCamera;
	private Mesh effectMesh;
	private Material effectMaterial;
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
		mainCamera.depth = -2;

		effectMesh = new Mesh();
		effectMaterial = customReflectionMaterial != null ? customReflectionMaterial : MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		isMaterialDynamic = customReflectionMaterial == null; // True if using script-created material

		reflectionCamera = InitializeCamera(
			"ReflectionCamera",
			CameraClearFlags.Nothing,
			0,
			new[] { RenderPassEvent.BeforeRendering, RenderPassEvent.AfterRendering },
			new Action<RasterCommandBuffer, Camera>[] {
				(cmd, cam) => { cmd.SetInvertCulling(true); },
				(cmd, cam) => { cmd.SetInvertCulling(false); }
			}
		);

		effectCamera = InitializeCamera(
			"EffectCamera",
			CameraClearFlags.Nothing,
			0,
			new[] { RenderPassEvent.BeforeRendering },
			new Action<RasterCommandBuffer, Camera>[] {
				(cmd, cam) => {
					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
					{
						effectMaterial.SetPass(0);
						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
					}
					else
					{
						Debug.LogWarning("ReflectionPassCamera: Invalid reflectionMesh or material", this);
					}
				}
			}
		);

		if (!mainCamera.TryGetComponent<UniversalAdditionalCameraData>(out var mainCameraData))
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

		mainCameraData.cameraStack.Clear();

		if (reflectionCamera != null)
			mainCameraData.cameraStack.Add(reflectionCamera);

		if (effectCamera != null)
			mainCameraData.cameraStack.Add(effectCamera);

		FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

		//local function
		Camera InitializeCamera(string name, CameraClearFlags clearFlags, int depth, RenderPassEvent[] events, Action<RasterCommandBuffer, Camera>[] commands)
		{
			var obj = new GameObject(name) { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			var camera = obj.AddComponent<Camera>();
			camera.clearFlags = clearFlags;
			camera.cullingMask = sceneCullingMask;
			camera.depth = depth;
			camera.enabled = true;
			camera.targetTexture = null;

			//workaround for this (camera.depth = depth) not being applied to UniversalAdditionalCameraData
			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;
			if (CameraClearFlags.Nothing == clearFlags)
			{
				// --- Try a public setter if URP added one in future versions ---
				var prop = typeof(UniversalAdditionalCameraData).GetProperty(
					"clearDepth", BindingFlags.Instance | BindingFlags.Public);
				if (prop != null && prop.CanWrite)
				{
					prop.SetValue(data, false);
				}
				else
				{
					// --- Fallback: set the private serialized backing field used in most URP versions ---
					var field = typeof(UniversalAdditionalCameraData).GetField(
						"m_ClearDepth", BindingFlags.Instance | BindingFlags.NonPublic);
					if (field != null)
						field.SetValue(data, false);
					else
						Debug.LogWarning("Couldn't set clearDepth on UniversalAdditionalCameraData (field not found). " +
							"Consider creating the overlay camera in the Editor or using a prefab with Clear Depth off.");
				}
			}

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError($"ReflectionPassCamera: Failed to add CameraCommandProvider to {name}", this);
				enabled = false;
				return null;
			}

			for (int i = 0; i < events.Length; i++)
				provider.RegisterCommand(events[i], commands[i]);

			return camera;
		}
	}

	void LateUpdate()
	{
		if (effectCamera != null)
		{
			effectCamera.fieldOfView = mainCamera.fieldOfView;
			effectCamera.nearClipPlane = mainCamera.nearClipPlane;
			effectCamera.farClipPlane = mainCamera.farClipPlane;
			effectCamera.aspect = mainCamera.aspect;
			effectCamera.orthographic = mainCamera.orthographic;
			effectCamera.orthographicSize = mainCamera.orthographicSize;
			effectCamera.transform.position = mainCamera.transform.position;
			effectCamera.transform.rotation = mainCamera.transform.rotation;
		}

		if (reflectionCamera != null)
		{
			reflectionCamera.fieldOfView = mainCamera.fieldOfView;
			reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
			reflectionCamera.farClipPlane = mainCamera.farClipPlane;
			reflectionCamera.aspect = mainCamera.aspect;
			reflectionCamera.clearFlags = CameraClearFlags.Nothing;

			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

		FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);
	}

	void OnDestroy()
	{
		if (effectMaterial != null && isMaterialDynamic)
			DestroyImmediate(effectMaterial);

		if (effectMesh != null)
			DestroyImmediate(effectMesh);

		if (reflectionCamera != null)
			DestroyImmediate(reflectionCamera.gameObject);
	}
}