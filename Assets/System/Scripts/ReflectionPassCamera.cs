using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;

[RequireComponent(typeof(Camera))]
public class ReflectionPassCamera : MonoBehaviour
{
	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
	{
		private readonly Dictionary<RenderPassEvent, List<Action<RasterCommandBuffer, Camera>>> commands = new Dictionary<RenderPassEvent, List<Action<RasterCommandBuffer, Camera>>>();

		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command)
		{
			if (!commands.ContainsKey(evt))
			{
				commands[evt] = new List<Action<RasterCommandBuffer, Camera>>();
			}
			commands[evt].Add(command);
			//Debug.Log($"Registered command for {gameObject.name} at {evt}");
		}

		public bool HasCommands(RenderPassEvent evt)
		{
			bool hasCommands = commands.ContainsKey(evt) && commands[evt].Count > 0;
			//Debug.Log($"HasCommands({evt}) for {gameObject.name}: {hasCommands}");
			return hasCommands;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
		{
			if (!commands.ContainsKey(evt)) return;
			foreach (var command in commands[evt])
			{
				try
				{
					command?.Invoke(commandBuffer, camera);
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
	private CameraCommandProvider mainCommandProvider;
	private CameraCommandProvider reflectionCommandProvider;
	private CameraCommandProvider sceneCommandProvider;
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
			Debug.Log("ReflectionPassCamera: Set MainCamera tag", this);
		}

		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0;
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		// Add CameraCommandProvider for main camera (no commands)
		mainCommandProvider = gameObject.GetComponent<CameraCommandProvider>();
		if (mainCommandProvider == null)
		{
			mainCommandProvider = gameObject.AddComponent<CameraCommandProvider>();
			Debug.Log("ReflectionPassCamera: Added CameraCommandProvider to main camera", this);
		}

		reflectionMesh = new Mesh();
		reflectionMaterial = customReflectionMaterial != null ? customReflectionMaterial : CreateMaterial();
		transformMatrix = Matrix4x4.identity;

		InitializeReflectionCamera();
		InitializeSceneCamera();
		ConfigureCameraStack();
		UpdateReflectionGeometry();

		Debug.Log("ReflectionPassCamera: Initialized successfully", this);
	}

	private Material CreateMaterial()
	{
		var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
		if (unlitShader == null)
		{
			unlitShader = Shader.Find("Custom/UnlitAlpha");
			if (unlitShader == null)
			{
				Debug.LogError("ReflectionPassCamera: Neither URP Unlit nor Custom/UnlitAlpha shader found!");
				return null;
			}
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

	private void InitializeReflectionCamera()
	{
		var reflectionObj = new GameObject("ReflectionCamera");
		reflectionObj.transform.SetParent(transform, false);
		reflectionCamera = reflectionObj.AddComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.cullingMask = sceneCullingMask;
		reflectionCamera.depth = -1;
		reflectionCamera.enabled = true;
		reflectionCamera.targetTexture = null;

		reflectionCommandProvider = reflectionObj.AddComponent<CameraCommandProvider>();
		if (reflectionCommandProvider == null)
		{
			Debug.LogError("ReflectionPassCamera: Failed to add CameraCommandProvider to ReflectionCamera", this);
			enabled = false;
			return;
		}

		reflectionCommandProvider.RegisterCommand(RenderPassEvent.BeforeRendering,
			(cmd, cam) =>
			{
				//Debug.Log($"Executing SetInvertCulling(true) for {cam.name} at {RenderPassEvent.BeforeRendering}");
				cmd.SetInvertCulling(true);
			});
		reflectionCommandProvider.RegisterCommand(RenderPassEvent.AfterRendering,
			(cmd, cam) =>
			{
				//Debug.Log($"Executing SetInvertCulling(false) for {cam.name} at {RenderPassEvent.AfterRendering}");
				cmd.SetInvertCulling(false);
			});

		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
		reflectionData.renderType = CameraRenderType.Overlay;
		Debug.Log("ReflectionPassCamera: Initialized ReflectionCamera", this);
	}

	private void InitializeSceneCamera()
	{
		var sceneObj = new GameObject("SceneCamera");
		sceneObj.transform.SetParent(transform, false);
		sceneCamera = sceneObj.AddComponent<Camera>();
		sceneCamera.clearFlags = CameraClearFlags.Nothing;
		sceneCamera.cullingMask = sceneCullingMask;
		sceneCamera.depth = 0;
		sceneCamera.enabled = true;

		sceneCommandProvider = sceneObj.AddComponent<CameraCommandProvider>();
		if (sceneCommandProvider == null)
		{
			Debug.LogError("ReflectionPassCamera: Failed to add CameraCommandProvider to SceneCamera", this);
			enabled = false;
			return;
		}

		sceneCommandProvider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) =>
		{
			//Debug.Log($"Executing DrawMesh for {cam.name} at {RenderPassEvent.BeforeRendering}");
			if (reflectionMesh != null && reflectionMesh.vertexCount >= 3 && reflectionMesh.triangles.Length >= 3 && reflectionMaterial != null)
			{
				reflectionMaterial.SetPass(0);
				cmd.DrawMesh(reflectionMesh, transformMatrix, reflectionMaterial, 0, 0);
			}
			else
			{
				Debug.LogWarning("ReflectionPassCamera: Invalid reflectionMesh or material", this);
			}
		});

		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
		sceneData.renderType = CameraRenderType.Overlay;
		Debug.Log("ReflectionPassCamera: Initialized SceneCamera", this);
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
		{
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
			Debug.Log("ReflectionPassCamera: Added UniversalAdditionalCameraData to MainCamera", this);
		}
		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();
		if (reflectionCamera != null)
		{
			mainCameraData.cameraStack.Add(reflectionCamera);
			Debug.Log("ReflectionPassCamera: Added ReflectionCamera to camera stack", this);
		}
		if (sceneCamera != null)
		{
			mainCameraData.cameraStack.Add(sceneCamera);
			Debug.Log("ReflectionPassCamera: Added SceneCamera to camera stack", this);
		}
		Debug.Log($"ReflectionPassCamera: Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
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

		// Ensure main camera uses its own transform
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
			Debug.Log("ReflectionPassCamera: Destroyed dynamic material");
		}
		if (reflectionMesh != null)
		{
			DestroyImmediate(reflectionMesh);
			Debug.Log("ReflectionPassCamera: Destroyed reflectionMesh");
		}
		if (reflectionCamera != null)
		{
			DestroyImmediate(reflectionCamera.gameObject);
			Debug.Log("ReflectionPassCamera: Destroyed ReflectionCamera");
		}
		if (sceneCamera != null)
		{
			DestroyImmediate(sceneCamera.gameObject);
			Debug.Log("ReflectionPassCamera: Destroyed SceneCamera");
		}
	}
}


//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//[RequireComponent(typeof(Camera))]
//public class ReflectionPassCamera : CommandBufferSettings
//{
//	private Camera mainCamera;
//	private Camera reflectionCamera; // Child overlay camera for reflection
//	private Camera sceneCamera; // Child overlay camera for reflectionMesh
//	private Mesh reflectionMesh;
//	private Material reflectionMaterial;
//	private Matrix4x4 transformMatrix;
//	private bool isMaterialDynamic;
//	private CommandBufferSettings commandBufferSettings; // For main camera
//	private CommandBufferSettings sceneCommandBufferSettings; // For SceneCamera
//	private CommandBufferSettings reflectionCommandBufferSettings; // For ReflectionCamera
//	[HideInInspector] public LayerMask sceneCullingMask = ~0; // Backup culling mask for reflection and scene cameras

//	// Serialized fields for reflection geometry
//	[SerializeField] private Vector3 planeNormal = Vector3.up; // Reflection plane normal
//	[SerializeField] private float offset = -0.2f; // Reflection plane offset
//	[SerializeField] private Material customReflectionMaterial; // Custom material for reflection geometry (e.g., Unlit or custom shader)

//	void Awake()
//	{
//		mainCamera = GetComponent<Camera>();
//		if (mainCamera == null)
//		{
//			Debug.LogError("ReflectionPassCamera: Main camera component missing!", this);
//			enabled = false;
//			return;
//		}

//		// Ensure MainCamera tag
//		if (gameObject.tag != "MainCamera")
//		{
//			gameObject.tag = "MainCamera";
//			Debug.Log("ReflectionPassCamera: Set MainCamera tag", this);
//		}

//		// Backup culling mask and configure main camera for skybox
//		sceneCullingMask = mainCamera.cullingMask;
//		mainCamera.clearFlags = CameraClearFlags.Skybox;
//		mainCamera.cullingMask = 0; // Nothing, as skybox is rendered
//		mainCamera.depth = -2;
//		mainCamera.enabled = true;

//		// Get or add CommandBufferSettings for main camera
//		commandBufferSettings = GetComponent<CommandBufferSettings>();
//		if (commandBufferSettings == null)
//		{
//			commandBufferSettings = gameObject.AddComponent<CommandBufferSettings>();
//			Debug.Log("ReflectionPassCamera: Added CommandBufferSettings component to main camera", this);
//		}

//		// Initialize reflection mesh and material
//		reflectionMesh = new Mesh();
//		reflectionMaterial = customReflectionMaterial != null ? customReflectionMaterial : CreateMaterial(); // Use assigned material or create one
//		transformMatrix = Matrix4x4.identity; // Use identity since vertices are in world space

//		// Initialize cameras
//		InitializeReflectionCamera();
//		InitializeSceneCamera();

//		// Configure camera stack
//		ConfigureCameraStack();

//		// Initial reflection geometry update
//		UpdateReflectionGeometry();

//		//// Register a dummy command for main camera (optional, can be removed if not needed)
//		//commandBufferSettings.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { }, mainCamera.name);

//		Debug.Log("ReflectionPassCamera: Initialized successfully", this);
//	}

//	private Material CreateMaterial()
//	{
//		var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
//		if (unlitShader == null)
//		{
//			unlitShader = Shader.Find("Custom/UnlitAlpha");
//			if (unlitShader == null)
//			{
//				Debug.LogError("ReflectionPassCamera: Neither URP Unlit nor Custom/UnlitAlpha shader found!");
//				return null;
//			}
//		}

//		var material = new Material(unlitShader)
//		{
//			renderQueue = (int)RenderQueue.Transparent
//		};
//		material.SetColor("_BaseColor", new Color(0.1f, 0.1f, 0.1f, 0.5f)); // Default color
//		material.SetFloat("_Surface", 1f); // Transparent
//		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
//		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
//		material.SetFloat("_ZWrite", 0f);
//		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
//		material.SetOverrideTag("RenderType", "Transparent");
//		isMaterialDynamic = true;
//		return material;
//	}

//	private void InitializeReflectionCamera()
//	{
//		var reflectionObj = new GameObject("ReflectionCamera");
//		reflectionObj.transform.SetParent(transform, false);
//		reflectionCamera = reflectionObj.AddComponent<Camera>();
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.cullingMask = sceneCullingMask; // Use backed-up culling mask
//		reflectionCamera.depth = -1;
//		reflectionCamera.enabled = true; // Required for manual rendering
//		reflectionCamera.targetTexture = null; // Render to framebuffer

//		// Add CommandBufferSettings component for ReflectionCamera
//		reflectionCommandBufferSettings = reflectionObj.AddComponent<CommandBufferSettings>();
//		if (reflectionCommandBufferSettings == null)
//		{
//			Debug.LogError("ReflectionPassCamera: Failed to add CommandBufferSettings to ReflectionCamera", this);
//			enabled = false;
//			return;
//		}

//		// Register command buffers for culling inversion
//		reflectionCommandBufferSettings.RegisterCommand(RenderPassEvent.BeforeRendering,
//			(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
//		reflectionCommandBufferSettings.RegisterCommand(RenderPassEvent.AfterRendering,
//			(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);

//		// Add UniversalAdditionalCameraData
//		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
//		reflectionData.renderType = CameraRenderType.Overlay;

//		Debug.Log("ReflectionPassCamera: Initialized ReflectionCamera", this);
//	}

//	private void InitializeSceneCamera()
//	{
//		var sceneObj = new GameObject("SceneCamera");
//		sceneObj.transform.SetParent(transform, false);
//		sceneCamera = sceneObj.AddComponent<Camera>();
//		sceneCamera.clearFlags = CameraClearFlags.Nothing;
//		sceneCamera.cullingMask = sceneCullingMask; // Use backed-up culling mask
//		sceneCamera.depth = 0;
//		sceneCamera.enabled = true; // Required for manual rendering

//		// Add CommandBufferSettings component for SceneCamera
//		sceneCommandBufferSettings = sceneObj.AddComponent<CommandBufferSettings>();
//		if (sceneCommandBufferSettings == null)
//		{
//			Debug.LogError("ReflectionPassCamera: Failed to add CommandBufferSettings to SceneCamera", this);
//			enabled = false;
//			return;
//		}

//		// Register DrawMesh command for SceneCamera
//		if (sceneCommandBufferSettings != null)
//		{
//			sceneCommandBufferSettings.RegisterCommand(RenderPassEvent.BeforeRendering, (commandBuffer, camera) =>
//			{
//				if (reflectionMesh != null && reflectionMesh.vertexCount >= 3 && reflectionMesh.triangles.Length >= 3 && reflectionMaterial != null)
//				{
//					// Ensure material properties are set before rendering
//					reflectionMaterial.SetPass(0); // Set the first pass of the material
//					commandBuffer.DrawMesh(reflectionMesh, transformMatrix, reflectionMaterial, 0, 0);
//				}
//				else
//				{
//					Debug.LogWarning("ReflectionPassCamera: Invalid reflectionMesh or quadMaterial in DrawMesh command", this);
//				}
//			}, sceneCamera.name);
//		}

//		// Add UniversalAdditionalCameraData
//		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
//		sceneData.renderType = CameraRenderType.Overlay;

//		Debug.Log("ReflectionPassCamera: Initialized SceneCamera", this);
//	}

//	private void ConfigureCameraStack()
//	{
//		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
//		if (mainCameraData == null)
//		{
//			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
//			Debug.Log("ReflectionPassCamera: Added UniversalAdditionalCameraData to MainCamera", this);
//		}
//		mainCameraData.renderType = CameraRenderType.Base;
//		mainCameraData.cameraStack.Clear();
//		if (reflectionCamera != null)
//		{
//			mainCameraData.cameraStack.Add(reflectionCamera);
//			Debug.Log("ReflectionPassCamera: Added ReflectionCamera to camera stack", this);
//		}
//		if (sceneCamera != null)
//		{
//			mainCameraData.cameraStack.Add(sceneCamera);
//			Debug.Log("ReflectionPassCamera: Added SceneCamera to camera stack", this);
//		}
//		Debug.Log($"ReflectionPassCamera: Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
//	}

//	void LateUpdate()
//	{
//		// Sync SceneCamera parameters with mainCamera
//		if (sceneCamera != null)
//		{
//			sceneCamera.fieldOfView = mainCamera.fieldOfView;
//			sceneCamera.nearClipPlane = mainCamera.nearClipPlane;
//			sceneCamera.farClipPlane = mainCamera.farClipPlane;
//			sceneCamera.aspect = mainCamera.aspect;
//			sceneCamera.orthographic = mainCamera.orthographic;
//			sceneCamera.orthographicSize = mainCamera.orthographicSize;
//			sceneCamera.transform.position = mainCamera.transform.position;
//			sceneCamera.transform.rotation = mainCamera.transform.rotation;
//		}

//		// Update ReflectionCamera
//		if (reflectionCamera != null)
//		{
//			reflectionCamera.fieldOfView = mainCamera.fieldOfView;
//			reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
//			reflectionCamera.farClipPlane = mainCamera.farClipPlane;
//			reflectionCamera.aspect = mainCamera.aspect;

//			var n = planeNormal.normalized;
//			var planePoint = n * offset;
//			var reflectionMat = Matrix4x4.identity;
//			reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
//			reflectionMat[0, 1] = -2 * n.x * n.y;
//			reflectionMat[0, 2] = -2 * n.x * n.z;
//			reflectionMat[1, 0] = -2 * n.y * n.x;
//			reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
//			reflectionMat[1, 2] = -2 * n.y * n.z;
//			reflectionMat[2, 0] = -2 * n.z * n.x;
//			reflectionMat[2, 1] = -2 * n.z * n.y;
//			reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
//			var translateToOrigin = Matrix4x4.Translate(-planePoint);
//			var translateBack = Matrix4x4.Translate(planePoint);
//			reflectionMat = translateBack * reflectionMat * translateToOrigin;

//			reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//			reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
//		}

//		// Update reflection geometry each frame
//		UpdateReflectionGeometry();
//	}

//	private void UpdateReflectionGeometry()
//	{
//		if (sceneCamera == null)
//		{
//			Debug.LogError("ReflectionPassCamera: sceneCamera is null", this);
//			return;
//		}

//		if (planeNormal == Vector3.zero)
//		{
//			Debug.LogWarning("ReflectionPassCamera: Invalid planeNormal (zero vector)", this);
//			return;
//		}

//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var plane = new Plane(n, planePoint);

//		float near = -sceneCamera.nearClipPlane;
//		float far = -sceneCamera.farClipPlane;
//		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
//		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//		float aspect = sceneCamera.aspect;

//		// Calculate frustum corners
//		Vector3[] nearCorners = new Vector3[4];
//		Vector3[] farCorners = new Vector3[4];
//		nearCorners[0] = new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
//		nearCorners[1] = new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
//		nearCorners[2] = new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
//		nearCorners[3] = new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
//		farCorners[0] = new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
//		farCorners[1] = new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
//		farCorners[2] = new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
//		farCorners[3] = new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

//		// Transform to world space
//		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
//		for (int i = 0; i < 4; i++)
//		{
//			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
//			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
//		}

//		// Intersect frustum edges with plane
//		System.Collections.Generic.List<Vector3> intersectionPoints = new System.Collections.Generic.List<Vector3>();
//		for (int i = 0; i < 4; i++)
//		{
//			Vector3 start = nearCorners[i];
//			Vector3 end = farCorners[i];
//			Vector3 dir = (end - start).normalized;
//			float maxDistance = Vector3.Distance(start, end);
//			bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
//			if (raycastHit && distance >= 0 && distance <= maxDistance)
//			{
//				Vector3 point = start + dir * distance;
//				intersectionPoints.Add(point);
//			}
//		}

//		// Intersect near and far planes
//		Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
//		var nearPoints = IntersectPlaneWithQuad(plane, nearQuad);
//		intersectionPoints.AddRange(nearPoints);
//		Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
//		var farPoints = IntersectPlaneWithQuad(plane, farQuad);
//		intersectionPoints.AddRange(farPoints);

//		// Remove duplicates and limit to 6 vertices
//		intersectionPoints = RemoveDuplicates(intersectionPoints, 0.01f);
//		if (intersectionPoints.Count > 6) intersectionPoints = intersectionPoints.GetRange(0, 6);

//		if (intersectionPoints.Count >= 3)
//		{
//			// Sort points for convex polygon
//			Vector3 centroid = Vector3.zero;
//			foreach (var pt in intersectionPoints) centroid += pt;
//			centroid /= intersectionPoints.Count;
//			Vector3 refDir = (intersectionPoints[0] - centroid).normalized;
//			intersectionPoints.Sort((a, b) =>
//			{
//				Vector3 va = a - centroid;
//				Vector3 vb = b - centroid;
//				float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, va), n), Vector3.Dot(refDir, va));
//				float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, vb), n), Vector3.Dot(refDir, vb));
//				return angleA.CompareTo(angleB);
//			});

//			UpdateReflectionMesh(intersectionPoints);
//		}
//		else
//		{
//			reflectionMesh.Clear();
//			Debug.LogWarning("ReflectionPassCamera: Insufficient intersection points for reflectionMesh", this);
//		}
//	}

//	private void UpdateReflectionMesh(System.Collections.Generic.List<Vector3> points)
//	{
//		if (points.Count < 3)
//		{
//			Debug.LogWarning($"ReflectionPassCamera: Too few points ({points.Count}) to create mesh", this);
//			reflectionMesh.Clear();
//			return;
//		}

//		// Use world-space vertices
//		Vector3[] vertices = new Vector3[points.Count];
//		for (int i = 0; i < points.Count; i++)
//		{
//			vertices[i] = points[i];
//		}

//		// Fan triangulation
//		System.Collections.Generic.List<int> triangles = new System.Collections.Generic.List<int>();
//		for (int i = 1; i < points.Count - 1; i++)
//		{
//			triangles.Add(0);
//			triangles.Add(i);
//			triangles.Add(i + 1);
//		}

//		reflectionMesh.Clear();
//		reflectionMesh.vertices = vertices;
//		reflectionMesh.triangles = triangles.ToArray();
//		reflectionMesh.RecalculateBounds();
//		reflectionMesh.RecalculateNormals();
//	}

//	private Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//	{
//		System.Collections.Generic.List<Vector3> points = new System.Collections.Generic.List<Vector3>();
//		for (int i = 0; i < 4; i++)
//		{
//			Vector3 start = quad[i];
//			Vector3 end = quad[(i + 1) % 4];
//			Vector3 dir = (end - start).normalized;
//			float maxDistance = Vector3.Distance(start, end);
//			bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
//			if (raycastHit && distance >= 0 && distance <= maxDistance)
//			{
//				Vector3 point = start + dir * distance;
//				points.Add(point);
//			}
//		}
//		return points.ToArray();
//	}

//	private System.Collections.Generic.List<Vector3> RemoveDuplicates(System.Collections.Generic.List<Vector3> points, float threshold)
//	{
//		System.Collections.Generic.List<Vector3> unique = new System.Collections.Generic.List<Vector3>();
//		foreach (var pt in points)
//		{
//			bool isUnique = true;
//			foreach (var u in unique)
//			{
//				if (Vector3.Distance(pt, u) < threshold)
//				{
//					isUnique = false;
//					break;
//				}
//			}
//			if (isUnique) unique.Add(pt);
//		}
//		return unique;
//	}

//	void OnDestroy()
//	{
//		if (reflectionMaterial != null && isMaterialDynamic)
//		{
//			DestroyImmediate(reflectionMaterial);
//			Debug.Log("ReflectionPassCamera: Destroyed dynamic material");
//		}
//		if (reflectionMesh != null)
//		{
//			DestroyImmediate(reflectionMesh);
//			Debug.Log("ReflectionPassCamera: Destroyed reflectionMesh");
//		}
//		if (reflectionCamera != null)
//		{
//			DestroyImmediate(reflectionCamera.gameObject);
//			Debug.Log("ReflectionPassCamera: Destroyed ReflectionCamera");
//		}
//		if (sceneCamera != null)
//		{
//			DestroyImmediate(sceneCamera.gameObject);
//			Debug.Log("ReflectionPassCamera: Destroyed SceneCamera");
//		}
//	}
//}