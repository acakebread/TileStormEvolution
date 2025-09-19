using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections.Generic;

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
		if (sceneCamera == null)
		{
			Debug.LogError("ReflectionPassCamera: sceneCamera is null", this);
			return;
		}

		if (planeNormal == Vector3.zero)
		{
			Debug.LogWarning("ReflectionPassCamera: Invalid planeNormal (zero vector)", this);
			return;
		}

		var n = planeNormal.normalized;
		var planePoint = n * offset;
		var plane = new Plane(n, planePoint);

		float near = -sceneCamera.nearClipPlane;
		float far = -sceneCamera.farClipPlane;
		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
		float aspect = sceneCamera.aspect;

		Vector3[] nearCorners = new Vector3[4];
		Vector3[] farCorners = new Vector3[4];
		nearCorners[0] = new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near);
		nearCorners[1] = new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);
		nearCorners[2] = new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near);
		nearCorners[3] = new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near);
		farCorners[0] = new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far);
		farCorners[1] = new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);
		farCorners[2] = new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far);
		farCorners[3] = new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far);

		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
		}

		List<Vector3> intersectionPoints = new List<Vector3>();
		for (int i = 0; i < 4; i++)
		{
			Vector3 start = nearCorners[i];
			Vector3 end = farCorners[i];
			Vector3 dir = (end - start).normalized;
			float maxDistance = Vector3.Distance(start, end);
			bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
			if (raycastHit && distance >= 0 && distance <= maxDistance)
			{
				Vector3 point = start + dir * distance;
				intersectionPoints.Add(point);
			}
		}

		Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
		var nearPoints = IntersectPlaneWithQuad(plane, nearQuad);
		intersectionPoints.AddRange(nearPoints);
		Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
		var farPoints = IntersectPlaneWithQuad(plane, farQuad);
		intersectionPoints.AddRange(farPoints);

		intersectionPoints = RemoveDuplicates(intersectionPoints, 0.01f);
		if (intersectionPoints.Count > 6) intersectionPoints = intersectionPoints.GetRange(0, 6);

		if (intersectionPoints.Count >= 3)
		{
			Vector3 centroid = Vector3.zero;
			foreach (var pt in intersectionPoints) centroid += pt;
			centroid /= intersectionPoints.Count;
			Vector3 refDir = (intersectionPoints[0] - centroid).normalized;
			intersectionPoints.Sort((a, b) =>
			{
				Vector3 va = a - centroid;
				Vector3 vb = b - centroid;
				float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, va), n), Vector3.Dot(refDir, va));
				float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, vb), n), Vector3.Dot(refDir, vb));
				return angleA.CompareTo(angleB);
			});

			UpdateReflectionMesh(intersectionPoints);
		}
		else
		{
			reflectionMesh.Clear();
			Debug.LogWarning("ReflectionPassCamera: Insufficient intersection points for reflectionMesh", this);
		}
	}

	private void UpdateReflectionMesh(List<Vector3> points)
	{
		if (points.Count < 3)
		{
			Debug.LogWarning($"ReflectionPassCamera: Too few points ({points.Count}) to create mesh", this);
			reflectionMesh.Clear();
			return;
		}

		Vector3[] vertices = new Vector3[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			vertices[i] = points[i];
		}

		List<int> triangles = new List<int>();
		for (int i = 1; i < points.Count - 1; i++)
		{
			triangles.Add(0);
			triangles.Add(i);
			triangles.Add(i + 1);
		}

		reflectionMesh.Clear();
		reflectionMesh.vertices = vertices;
		reflectionMesh.triangles = triangles.ToArray();
		reflectionMesh.RecalculateBounds();
		reflectionMesh.RecalculateNormals();
	}

	private Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
	{
		List<Vector3> points = new List<Vector3>();
		for (int i = 0; i < 4; i++)
		{
			Vector3 start = quad[i];
			Vector3 end = quad[(i + 1) % 4];
			Vector3 dir = (end - start).normalized;
			float maxDistance = Vector3.Distance(start, end);
			bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
			if (raycastHit && distance >= 0 && distance <= maxDistance)
			{
				Vector3 point = start + dir * distance;
				points.Add(point);
			}
		}
		return points.ToArray();
	}

	private List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
	{
		List<Vector3> unique = new List<Vector3>();
		foreach (var pt in points)
		{
			bool isUnique = true;
			foreach (var u in unique)
			{
				if (Vector3.Distance(pt, u) < threshold)
				{
					isUnique = false;
					break;
				}
			}
			if (isUnique) unique.Add(pt);
		}
		return unique;
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