using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Camera))]
public class ReflectionPassCamera : MonoBehaviour
{
	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
	{
		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>>();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command)
		{
			commands[evt] = command;
		}

		public bool HasCommands(RenderPassEvent evt)
		{
			return commands.ContainsKey(evt) && commands[evt] != null;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (commands.ContainsKey(evt) && commands[evt] != null)
			{
				try
				{
					commands[evt].Invoke(commandBuffer, camera);
				}
				catch (System.Exception e)
				{
					Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}");
				}
			}
		}

		void OnDestroy()
		{
			commands.Clear();
		}
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

		if (gameObject.tag != "MainCamera")
		{
			gameObject.tag = "MainCamera";
		}

		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0;
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		reflectionMesh = new Mesh();
		reflectionMaterial = customReflectionMaterial != null ? customReflectionMaterial : CreateMaterial();
		transformMatrix = Matrix4x4.identity;

		InitializeCameras();
		ConfigureCameraStack();
		UpdateReflectionGeometry();
	}

	private Material CreateMaterial()
	{
		var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
		if (unlitShader == null)
		{
			Debug.LogError("ReflectionPassCamera: Universal Render Pipeline/Unlit shader not found! Ensure URP is installed.", this);
			return null;
		}

		var material = new Material(unlitShader)
		{
			renderQueue = (int)RenderQueue.Transparent
		};
		material.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f));
		material.SetFloat("_Surface", 1f);
		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		isMaterialDynamic = true;
		return material;
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
		var obj = new GameObject(name);
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
		{
			provider.RegisterCommand(events[i], commands[i]);
		}

		var data = obj.AddComponent<UniversalAdditionalCameraData>();
		data.renderType = CameraRenderType.Overlay;
		return camera;
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
		{
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
		}
		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();
		if (reflectionCamera != null)
		{
			mainCameraData.cameraStack.Add(reflectionCamera);
		}
		if (sceneCamera != null)
		{
			mainCameraData.cameraStack.Add(sceneCamera);
		}
	}

	void LateUpdate()
	{
		if (sceneCamera != null)
		{
			sceneCamera.fieldOfView = mainCamera.fieldOfView;
			sceneCamera.nearClipPlane = mainCamera.nearClipPlane;
			sceneCamera.farClipPlane = mainCamera.farClipPlane;
			sceneCamera.aspect = mainCamera.aspect;
			sceneCamera.orthographic = mainCamera.orthographic;
			sceneCamera.orthographicSize = mainCamera.orthographicSize;
			sceneCamera.transform.position = mainCamera.transform.position;
			sceneCamera.transform.rotation = mainCamera.transform.rotation;
		}

		if (reflectionCamera != null)
		{
			reflectionCamera.fieldOfView = mainCamera.fieldOfView;
			reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
			reflectionCamera.farClipPlane = mainCamera.farClipPlane;
			reflectionCamera.aspect = mainCamera.aspect;

			var n = planeNormal.normalized;
			var planePoint = n * offset;
			var reflectionMat = Matrix4x4.identity;
			reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
			reflectionMat[0, 1] = -2 * n.x * n.y;
			reflectionMat[0, 2] = -2 * n.x * n.z;
			reflectionMat[1, 0] = -2 * n.y * n.x;
			reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
			reflectionMat[1, 2] = -2 * n.y * n.z;
			reflectionMat[2, 0] = -2 * n.z * n.x;
			reflectionMat[2, 1] = -2 * n.z * n.y;
			reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
			var translateToOrigin = Matrix4x4.Translate(-planePoint);
			var translateBack = Matrix4x4.Translate(planePoint);
			reflectionMat = translateBack * reflectionMat * translateToOrigin;

			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
		}

		if (mainCamera != null)
		{
			mainCamera.ResetWorldToCameraMatrix();
			mainCamera.ResetProjectionMatrix();
		}

		UpdateReflectionGeometry();
	}

	private void UpdateReflectionGeometry()
	{
		if (sceneCamera == null || (planeNormal = planeNormal.normalized) == Vector3.zero)
		{
			reflectionMesh.Clear();
			if (sceneCamera == null) Debug.LogError("ReflectionPassCamera: sceneCamera is null", this);
			else Debug.LogWarning("ReflectionPassCamera: Invalid plane normal (zero vector)", this);
			return;
		}

		Plane plane = new Plane(planeNormal, planeNormal * offset);

		float near = -sceneCamera.nearClipPlane, far = -sceneCamera.farClipPlane;
		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad, halfFovTan = Mathf.Tan(fovRad * 0.5f);
		float aspect = sceneCamera.aspect;

		Vector3[] nearCorners = new Vector3[4], farCorners = new Vector3[4];
		float[] xs = { -halfFovTan * aspect, halfFovTan * aspect, halfFovTan * aspect, -halfFovTan * aspect };
		float[] ys = { halfFovTan, halfFovTan, -halfFovTan, -halfFovTan };
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = new Vector3(xs[i] * near, ys[i] * near, near);
			farCorners[i] = new Vector3(xs[i] * far, ys[i] * far, far);
		}

		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
		}

		List<Vector3> points = new List<Vector3>(12);
		for (int i = 0; i < 4; i++)
		{
			AddSegmentIntersection(plane, nearCorners[i], farCorners[i], points);
			AddSegmentIntersection(plane, nearCorners[i], nearCorners[(i + 1) % 4], points);
			AddSegmentIntersection(plane, farCorners[i], farCorners[(i + 1) % 4], points);
		}

		if (points.Count < 3)
		{
			reflectionMesh.Clear();
			return;
		}

		// Sort clockwise around centroid
		Vector3 centroid = points.Aggregate(Vector3.zero, (c, p) => c + p) / points.Count;
		points.Sort((a, b) =>
		{
			Vector3 va = a - centroid, vb = b - centroid;
			float cross = Vector3.Dot(planeNormal, Vector3.Cross(va, vb));
			float dot = Vector3.Dot(va, vb);
			return cross > 0 ? -1 : (cross < 0 ? 1 : dot.CompareTo(0));
		});

		// Fan triangulation
		reflectionMesh.Clear();
		reflectionMesh.vertices = points.ToArray();
		int[] tris = new int[(points.Count - 2) * 3];
		for (int i = 0, idx = 0; i < points.Count - 2; i++, idx += 3)
		{
			tris[idx] = 0; tris[idx + 1] = i + 1; tris[idx + 2] = i + 2;
		}
		reflectionMesh.triangles = tris;
		reflectionMesh.RecalculateBounds();
		reflectionMesh.RecalculateNormals();

		void AddSegmentIntersection(Plane p, Vector3 s, Vector3 e, List<Vector3> lst)
		{
			Vector3 d = (e - s).normalized;
			float len = Vector3.Distance(s, e);
			if (p.Raycast(new Ray(s, d), out float t) && t >= 0 && t <= len)
				lst.Add(s + d * t);
		}
	}

	void OnDestroy()
	{
		if (reflectionMaterial != null && isMaterialDynamic)
		{
			DestroyImmediate(reflectionMaterial);
		}
		if (reflectionMesh != null)
		{
			DestroyImmediate(reflectionMesh);
		}
		if (reflectionCamera != null)
		{
			DestroyImmediate(reflectionCamera.gameObject);
		}
		if (sceneCamera != null)
		{
			DestroyImmediate(sceneCamera.gameObject);
		}
	}
}