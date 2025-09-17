using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class ReflectionPassCamera : CommandBufferSettings
{
	[SerializeField] private Vector3 planeNormal = Vector3.up; // Reflection plane normal
	[SerializeField] private float offset = -0.2f; // Reflection plane offset
	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dim overlay color
	[SerializeField] private Material dimMaterial; // Custom/UnlitFixedColor or URP Unlit
	[HideInInspector] public LayerMask sceneCullingMask = ~0; // Culling mask for reflection and scene cameras

	private Camera mainCamera; // Skybox camera (this GameObject, Base, MainCamera tag)
	private Camera reflectionCamera; // Child Reflection camera (Overlay)
	private Camera sceneCamera; // Child Scene camera (Overlay)
	private CommandBufferSettings commandBufferSettings;
	private bool isDimMaterialDynamic; // Track if dimMaterial was created dynamically
	private Mesh dimMesh; // Mesh for dim overlay

	void Awake()
	{
		// Initialize main camera (Skybox, Base)
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("Main camera component missing!", this);
			enabled = false;
			return;
		}

		// Ensure MainCamera tag
		if (gameObject.tag != "MainCamera")
		{
			gameObject.tag = "MainCamera";
			Debug.Log("Set MainCamera tag on Skybox camera", this);
		}

		// Configure main camera
		sceneCullingMask = mainCamera.cullingMask;
		mainCamera.clearFlags = CameraClearFlags.Skybox;
		mainCamera.cullingMask = 0; // Nothing, as skybox is rendered by material
		mainCamera.depth = -2;
		mainCamera.enabled = true;

		// Get CommandBufferSettings
		commandBufferSettings = GetComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError("CommandBufferSettings component missing!", this);
			enabled = false;
			return;
		}
		Debug.Log("CommandBufferSettings initialized", this);

		// Create child cameras
		InitializeReflectionCamera();
		InitializeSceneCamera();

		// Configure URP camera stack (for compatibility)
		ConfigureCameraStack();

		Debug.Log("ReflectionPassCamera initialized successfully", this);
	}

	private void InitializeReflectionCamera()
	{
		var reflectionObj = new GameObject("ReflectionCamera");
		reflectionObj.transform.SetParent(transform, false);
		reflectionCamera = reflectionObj.AddComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.cullingMask = sceneCullingMask;
		reflectionCamera.depth = -1;
		reflectionCamera.enabled = true; // Required for manual rendering
		reflectionCamera.targetTexture = null; // Render to framebuffer

		// Add CommandBufferSettings component for render graph
		var commandBufferSettings = reflectionObj.AddComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError("CommandBufferSettings component missing on ReflectionCamera", this);
			enabled = false;
			return;
		}

		// Register command buffers for culling inversion
		commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques,
			(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
		commandBufferSettings.RegisterCommand(RenderPassMode.AfterRendering,
			(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);
		Debug.Log("Reflection command buffer initialized for ReflectionCamera", this);
		Debug.Log("Initialized ReflectionCamera", this);

		// Add UniversalAdditionalCameraData
		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
		reflectionData.renderType = CameraRenderType.Overlay;
	}

	private void InitializeSceneCamera()
	{
		var sceneObj = new GameObject("SceneCamera");
		sceneObj.transform.SetParent(transform, false);
		sceneCamera = sceneObj.AddComponent<Camera>();
		sceneCamera.clearFlags = CameraClearFlags.Nothing;
		sceneCamera.cullingMask = sceneCullingMask;
		sceneCamera.depth = 0;
		sceneCamera.enabled = true; // Required for manual rendering

		// Create dim mesh
		dimMesh = new Mesh();

		// Create dim material if not assigned
		if (dimMaterial == null)
		{
			Shader unlitShader = Shader.Find("Custom/UnlitFixedColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
			if (unlitShader == null)
			{
				Debug.LogError("Failed to find Unlit shader!", this);
				enabled = false;
				return;
			}
			dimMaterial = new Material(unlitShader);
			dimMaterial.SetFloat("_Surface", 1.0f); // Transparent
			dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
			dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
			dimMaterial.SetFloat("_ZWrite", 0.0f);
			dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
			dimMaterial.SetFloat("_AlphaClip", 0.0f);
			dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			dimMaterial.renderQueue = (int)RenderQueue.Transparent;
			isDimMaterialDynamic = true;
			Debug.Log("Created dynamic dimMaterial", this);
		}

		// Validate shader
		if (dimMaterial.shader.name != "Custom/UnlitFixedColor" && dimMaterial.shader.name != "Universal Render Pipeline/Unlit")
		{
			Debug.LogWarning($"SceneCamera: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.", this);
		}

		// Add CommandBufferSettings component for render graph
		var commandBufferSettings = sceneObj.AddComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError($"SceneCamera: CommandBufferSettings component missing", this);
			enabled = false;
			return;
		}

		// Validate inputs
		if (dimMesh == null)
		{
			Debug.LogError($"SceneCamera: dimMesh is null", this);
			enabled = false;
			return;
		}
		if (dimMaterial == null)
		{
			Debug.LogError($"SceneCamera: dimMaterial is null", this);
			enabled = false;
			return;
		}

		// Register DrawMesh command
		commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques, (commandBuffer, camera) =>
		{
			if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
			{
				return;
			}
			commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
		}, sceneCamera.name);
		Debug.Log($"Initialized SceneCamera: sceneCamera={sceneCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
		Debug.Log("Initialized SceneCamera with CommandBufferSettings", this);

		// Add UniversalAdditionalCameraData
		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
		sceneData.renderType = CameraRenderType.Overlay;
	}

	private void ConfigureCameraStack()
	{
		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
		if (mainCameraData == null)
		{
			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
			Debug.Log("Added UniversalAdditionalCameraData to MainCamera", this);
		}
		mainCameraData.renderType = CameraRenderType.Base;
		mainCameraData.cameraStack.Clear();
		if (reflectionCamera != null)
		{
			mainCameraData.cameraStack.Add(reflectionCamera);
			Debug.Log("Added ReflectionCamera to stack", this);
		}
		if (sceneCamera != null)
		{
			mainCameraData.cameraStack.Add(sceneCamera);
			Debug.Log("Added SceneCamera to stack", this);
		}
		Debug.Log($"Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
	}

	private void UpdateMaterialColor()
	{
		if (dimMaterial != null && (dimMaterial.HasProperty("_BaseColor") || dimMaterial.HasProperty("_Color")))
		{
			string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
			dimMaterial.SetColor(colorProperty, dimColor);
		}
	}

	private void UpdateDimGeometry()
	{
		if (sceneCamera == null)
		{
			Debug.LogError($"ReflectionPassCamera: sceneCamera is null", this);
			return;
		}

		if (planeNormal == Vector3.zero)
		{
			Debug.LogWarning($"ReflectionPassCamera: Invalid planeNormal (zero vector)", this);
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

		// Calculate frustum corners
		Vector3[] nearCorners = new Vector3[4];
		Vector3[] farCorners = new Vector3[4];
		nearCorners[0] = new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
		nearCorners[1] = new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
		nearCorners[2] = new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
		nearCorners[3] = new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
		farCorners[0] = new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
		farCorners[1] = new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
		farCorners[2] = new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
		farCorners[3] = new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

		// Transform to world space
		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
		}

		// Intersect frustum edges with plane
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

		// Intersect near and far planes
		Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
		var nearPoints = IntersectPlaneWithQuad(plane, nearQuad);
		intersectionPoints.AddRange(nearPoints);
		Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
		var farPoints = IntersectPlaneWithQuad(plane, farQuad);
		intersectionPoints.AddRange(farPoints);

		// Remove duplicates and limit to 6 vertices
		intersectionPoints = RemoveDuplicates(intersectionPoints, 0.01f);
		if (intersectionPoints.Count > 6) intersectionPoints = intersectionPoints.GetRange(0, 6);

		if (intersectionPoints.Count >= 3)
		{
			// Sort points for convex polygon
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

			UpdateDimMesh(intersectionPoints);
		}
		else
		{
			dimMesh.Clear();
		}
	}

	private void UpdateDimMesh(List<Vector3> points)
	{
		if (points.Count < 3)
		{
			Debug.LogWarning($"ReflectionPassCamera: Too few points ({points.Count}) to create mesh", this);
			dimMesh.Clear();
			return;
		}

		// Use world-space vertices
		Vector3[] vertices = new Vector3[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			vertices[i] = points[i];
		}

		// Fan triangulation
		List<int> triangles = new List<int>();
		for (int i = 1; i < points.Count - 1; i++)
		{
			triangles.Add(0);
			triangles.Add(i);
			triangles.Add(i + 1);
		}

		dimMesh.Clear();
		dimMesh.vertices = vertices;
		dimMesh.triangles = triangles.ToArray();
		dimMesh.RecalculateBounds();
		dimMesh.RecalculateNormals();
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

	void LateUpdate()
	{
		// Update dim geometry and material color
		UpdateMaterialColor();
		UpdateDimGeometry();

		// SceneCamera updates
		if (sceneCamera != null)
		{
			sceneCamera.fieldOfView = mainCamera.fieldOfView;
			sceneCamera.nearClipPlane = mainCamera.nearClipPlane;
			sceneCamera.farClipPlane = mainCamera.farClipPlane;
			sceneCamera.aspect = mainCamera.aspect;
			sceneCamera.orthographic = mainCamera.orthographic;
			sceneCamera.orthographicSize = mainCamera.orthographicSize;
		}

		// ReflectionCamera updates
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
	}

	void OnDestroy()
	{
		if (reflectionCamera != null)
		{
			DestroyImmediate(reflectionCamera.gameObject, true);
		}
		if (sceneCamera != null)
		{
			DestroyImmediate(sceneCamera.gameObject, true);
		}
		if (dimMaterial != null && isDimMaterialDynamic)
		{
			DestroyImmediate(dimMaterial, true);
			Debug.Log("Destroyed dynamic dimMaterial", this);
		}
		if (dimMesh != null)
		{
			Destroy(dimMesh);
			Debug.Log("Destroyed dimMesh", this);
		}
	}
}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class ReflectionPassCamera : CommandBufferSettings
//{
//	[SerializeField] private Vector3 planeNormal = Vector3.up; // Reflection plane normal
//	[SerializeField] private float offset = -0.2f; // Reflection plane offset
//	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dim overlay color
//	[SerializeField] private Material dimMaterial; // Custom/UnlitFixedColor or URP Unlit
//	[HideInInspector] public LayerMask sceneCullingMask = ~0; // Culling mask for reflection and scene cameras

//	private Camera mainCamera; // Skybox camera (this GameObject, Base, MainCamera tag)
//	private Camera reflectionCamera; // Child Reflection camera (Overlay)
//	private Camera sceneCamera; // Child Scene camera (Overlay)
//	private CommandBufferSettings commandBufferSettings;
//	private bool isDimMaterialDynamic; // Track if dimMaterial was created dynamically
//	private Mesh dimMesh; // Mesh for dim overlay

//	void Awake()
//	{
//		// Initialize main camera (Skybox, Base)
//		mainCamera = GetComponent<Camera>();
//		if (mainCamera == null)
//		{
//			Debug.LogError("Main camera component missing!", this);
//			enabled = false;
//			return;
//		}

//		// Ensure MainCamera tag
//		if (gameObject.tag != "MainCamera")
//		{
//			gameObject.tag = "MainCamera";
//			Debug.Log("Set MainCamera tag on Skybox camera", this);
//		}

//		// Configure main camera
//		sceneCullingMask = mainCamera.cullingMask;
//		mainCamera.clearFlags = CameraClearFlags.Skybox;
//		mainCamera.cullingMask = 0; // Nothing, as skybox is rendered by material
//		mainCamera.depth = -2;
//		mainCamera.enabled = true;

//		// Get CommandBufferSettings
//		commandBufferSettings = GetComponent<CommandBufferSettings>();
//		if (commandBufferSettings == null)
//		{
//			Debug.LogError("CommandBufferSettings component missing!", this);
//			enabled = false;
//			return;
//		}
//		Debug.Log("CommandBufferSettings initialized", this);

//		// Create child cameras
//		InitializeReflectionCamera();
//		InitializeSceneCamera();

//		// Configure URP camera stack (for compatibility)
//		ConfigureCameraStack();

//		Debug.Log("ReflectionPassCamera initialized successfully", this);
//	}

//	private void InitializeReflectionCamera()
//	{
//		var reflectionObj = new GameObject("ReflectionCamera");
//		reflectionObj.transform.SetParent(transform, false);
//		reflectionCamera = reflectionObj.AddComponent<Camera>();
//		SyncCameraProperties(reflectionCamera);
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.cullingMask = sceneCullingMask;
//		reflectionCamera.depth = -1;
//		reflectionCamera.enabled = true; // Required for manual rendering
//		reflectionCamera.targetTexture = null; // Render to framebuffer

//		// Add ReflectionRender component and set up command buffer
//		var reflection = reflectionObj.AddComponent<ReflectionRender>();
//		reflection.SetupCommandBuffer();
//		Debug.Log($"Set referenceCamera properties for ReflectionCamera", this);
//		Debug.Log("Initialized ReflectionCamera", this);

//		// Add UniversalAdditionalCameraData
//		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
//		reflectionData.renderType = CameraRenderType.Overlay;
//	}

//	private void InitializeSceneCamera()
//	{
//		var sceneObj = new GameObject("SceneCamera");
//		sceneObj.transform.SetParent(transform, false);
//		sceneCamera = sceneObj.AddComponent<Camera>();
//		SyncCameraProperties(sceneCamera);
//		sceneCamera.clearFlags = CameraClearFlags.Nothing;
//		sceneCamera.cullingMask = sceneCullingMask;
//		sceneCamera.depth = 0;
//		sceneCamera.enabled = true; // Required for manual rendering

//		// Create dim mesh
//		dimMesh = new Mesh();

//		// Create dim material if not assigned
//		if (dimMaterial == null)
//		{
//			Shader unlitShader = Shader.Find("Custom/UnlitFixedColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
//			if (unlitShader == null)
//			{
//				Debug.LogError("Failed to find Unlit shader!", this);
//				enabled = false;
//				return;
//			}
//			dimMaterial = new Material(unlitShader);
//			dimMaterial.SetFloat("_Surface", 1.0f); // Transparent
//			dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
//			dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
//			dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
//			dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
//			dimMaterial.SetFloat("_ZWrite", 0.0f);
//			dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
//			dimMaterial.SetFloat("_AlphaClip", 0.0f);
//			dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
//			dimMaterial.renderQueue = (int)RenderQueue.Transparent;
//			isDimMaterialDynamic = true;
//			Debug.Log("Created dynamic dimMaterial", this);
//		}

//		// Validate shader
//		if (dimMaterial.shader.name != "Custom/UnlitFixedColor" && dimMaterial.shader.name != "Universal Render Pipeline/Unlit")
//		{
//			Debug.LogWarning($"SceneCamera: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.", this);
//		}

//		// Add CommandBufferSettings component for render graph
//		var commandBufferSettings = sceneObj.AddComponent<CommandBufferSettings>();
//		if (commandBufferSettings == null)
//		{
//			Debug.LogError($"SceneCamera: CommandBufferSettings component missing", this);
//			enabled = false;
//			return;
//		}

//		// Validate inputs
//		if (dimMesh == null)
//		{
//			Debug.LogError($"SceneCamera: dimMesh is null", this);
//			enabled = false;
//			return;
//		}
//		if (dimMaterial == null)
//		{
//			Debug.LogError($"SceneCamera: dimMaterial is null", this);
//			enabled = false;
//			return;
//		}

//		// Register DrawMesh command
//		commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques, (commandBuffer, camera) =>
//		{
//			if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
//			{
//				return;
//			}
//			commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
//		}, sceneCamera.name);
//		Debug.Log($"Initialized SceneCamera: sceneCamera={sceneCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
//		Debug.Log("Initialized SceneCamera with CommandBufferSettings", this);

//		// Add UniversalAdditionalCameraData
//		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
//		sceneData.renderType = CameraRenderType.Overlay;
//	}

//	private void SyncCameraProperties(Camera targetCamera)
//	{
//		targetCamera.fieldOfView = mainCamera.fieldOfView;
//		targetCamera.nearClipPlane = mainCamera.nearClipPlane;
//		targetCamera.farClipPlane = mainCamera.farClipPlane;
//		targetCamera.transform.position = mainCamera.transform.position;
//		targetCamera.transform.rotation = mainCamera.transform.rotation;
//		targetCamera.aspect = mainCamera.aspect;
//		targetCamera.orthographic = mainCamera.orthographic;
//		targetCamera.orthographicSize = mainCamera.orthographicSize;
//	}

//	private void ConfigureCameraStack()
//	{
//		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
//		if (mainCameraData == null)
//		{
//			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
//			Debug.Log("Added UniversalAdditionalCameraData to MainCamera", this);
//		}
//		mainCameraData.renderType = CameraRenderType.Base;
//		mainCameraData.cameraStack.Clear();
//		if (reflectionCamera != null)
//		{
//			mainCameraData.cameraStack.Add(reflectionCamera);
//			Debug.Log("Added ReflectionCamera to stack", this);
//		}
//		if (sceneCamera != null)
//		{
//			mainCameraData.cameraStack.Add(sceneCamera);
//			Debug.Log("Added SceneCamera to stack", this);
//		}
//		Debug.Log($"Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
//	}

//	private void UpdateMaterialColor()
//	{
//		if (dimMaterial != null && (dimMaterial.HasProperty("_BaseColor") || dimMaterial.HasProperty("_Color")))
//		{
//			string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
//			dimMaterial.SetColor(colorProperty, dimColor);
//		}
//	}

//	private void UpdateDimGeometry()
//	{
//		if (sceneCamera == null)
//		{
//			Debug.LogError($"ReflectionPassCamera: sceneCamera is null", this);
//			return;
//		}

//		if (planeNormal == Vector3.zero)
//		{
//			Debug.LogWarning($"ReflectionPassCamera: Invalid planeNormal (zero vector)", this);
//			return;
//		}
//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var plane = new Plane(n, planePoint);

//		float near = sceneCamera.nearClipPlane;
//		float far = sceneCamera.farClipPlane;
//		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
//		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//		float aspect = sceneCamera.aspect;

//		// Calculate frustum corners
//		Vector3[] nearCorners = new Vector3[4];
//		Vector3[] farCorners = new Vector3[4];
//		nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
//		nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
//		nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
//		nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
//		farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
//		farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
//		farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
//		farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

//		// Transform to world space
//		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
//		for (int i = 0; i < 4; i++)
//		{
//			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
//			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
//		}

//		// Intersect frustum edges with plane
//		List<Vector3> intersectionPoints = new List<Vector3>();
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

//			UpdateDimMesh(intersectionPoints);
//		}
//		else
//		{
//			dimMesh.Clear();
//		}
//	}

//	private void UpdateDimMesh(List<Vector3> points)
//	{
//		if (points.Count < 3)
//		{
//			Debug.LogWarning($"ReflectionPassCamera: Too few points ({points.Count}) to create mesh", this);
//			dimMesh.Clear();
//			return;
//		}

//		// Use world-space vertices
//		Vector3[] vertices = new Vector3[points.Count];
//		for (int i = 0; i < points.Count; i++)
//		{
//			vertices[i] = points[i];
//		}

//		// Fan triangulation
//		List<int> triangles = new List<int>();
//		for (int i = 1; i < points.Count - 1; i++)
//		{
//			triangles.Add(0);
//			triangles.Add(i);
//			triangles.Add(i + 1);
//		}

//		dimMesh.Clear();
//		dimMesh.vertices = vertices;
//		dimMesh.triangles = triangles.ToArray();
//		dimMesh.RecalculateBounds();
//		dimMesh.RecalculateNormals();
//	}

//	private Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//	{
//		List<Vector3> points = new List<Vector3>();
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

//	private List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
//	{
//		List<Vector3> unique = new List<Vector3>();
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

//	void LateUpdate()
//	{
//		// Update dim geometry and material color
//		UpdateMaterialColor();
//		UpdateDimGeometry();

//		// SceneCamera updates
//		if (sceneCamera != null)
//		{
//			SyncCameraProperties(sceneCamera);
//		}

//		// ReflectionRender updates
//		if (reflectionCamera != null)
//		{
//			var reflection = reflectionCamera.GetComponent<ReflectionRender>();
//			if (reflection != null)
//			{
//				reflectionCamera.fieldOfView = mainCamera.fieldOfView;
//				reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
//				reflectionCamera.farClipPlane = mainCamera.farClipPlane;
//				reflectionCamera.aspect = mainCamera.aspect;

//				var n = planeNormal.normalized;
//				var planePoint = n * offset;
//				var reflectionMat = Matrix4x4.identity;
//				reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
//				reflectionMat[0, 1] = -2 * n.x * n.y;
//				reflectionMat[0, 2] = -2 * n.x * n.z;
//				reflectionMat[1, 0] = -2 * n.y * n.x;
//				reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
//				reflectionMat[1, 2] = -2 * n.y * n.z;
//				reflectionMat[2, 0] = -2 * n.z * n.x;
//				reflectionMat[2, 1] = -2 * n.z * n.y;
//				reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
//				var translateToOrigin = Matrix4x4.Translate(-planePoint);
//				var translateBack = Matrix4x4.Translate(planePoint);
//				reflectionMat = translateBack * reflectionMat * translateToOrigin;

//				reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//				reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
//			}
//		}
//	}

//	void OnDestroy()
//	{
//		if (reflectionCamera != null)
//		{
//			DestroyImmediate(reflectionCamera.gameObject, true);
//		}
//		if (sceneCamera != null)
//		{
//			DestroyImmediate(sceneCamera.gameObject, true);
//		}
//		if (dimMaterial != null && isDimMaterialDynamic)
//		{
//			DestroyImmediate(dimMaterial, true);
//			Debug.Log("Destroyed dynamic dimMaterial", this);
//		}
//		if (dimMesh != null)
//		{
//			Destroy(dimMesh);
//			Debug.Log("Destroyed dimMesh", this);
//		}
//	}

//	// Internal class: ReflectionRender
//	[RequireComponent(typeof(Camera))]
//	private class ReflectionRender : CommandBufferSettings
//	{
//		private Camera reflectionCamera;

//		public void SetupCommandBuffer()
//		{
//			reflectionCamera = GetComponent<Camera>();
//			var commandBufferSettings = GetComponent<CommandBufferSettings>();
//			if (commandBufferSettings == null)
//			{
//				Debug.LogError("CommandBufferSettings component missing on sceneCamera", this);
//				enabled = false;
//				return;
//			}

//			commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques,
//				(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
//			commandBufferSettings.RegisterCommand(RenderPassMode.AfterRendering,
//				(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);
//			Debug.Log("ReflectionRender command buffer initialized for sceneCamera", this);
//		}
//	}
//}

//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;
//using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class ReflectionPassCamera : CommandBufferSettings
//{
//	[SerializeField] private Vector3 planeNormal = Vector3.up; // Reflection plane normal
//	[SerializeField] private float offset = -0.2f; // Reflection plane offset
//	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Dim overlay color
//	[SerializeField] private Material dimMaterial; // Custom/UnlitFixedColor or URP Unlit
//	[HideInInspector] public LayerMask sceneCullingMask = ~0; // Culling mask for reflection and scene cameras

//	private Camera mainCamera; // Skybox camera (this GameObject, Base, MainCamera tag)
//	private Camera reflectionCamera; // Child Reflection camera (Overlay)
//	private Camera sceneCamera; // Child Scene camera (Overlay)
//	private CommandBufferSettings commandBufferSettings;
//	private bool isDimMaterialDynamic; // Track if dimMaterial was created dynamically
//	private Mesh dimMesh; // Mesh for dim overlay

//	void Awake()
//	{
//		// Initialize main camera (Skybox, Base)
//		mainCamera = GetComponent<Camera>();
//		if (mainCamera == null)
//		{
//			Debug.LogError("Main camera component missing!", this);
//			enabled = false;
//			return;
//		}

//		// Ensure MainCamera tag
//		if (gameObject.tag != "MainCamera")
//		{
//			gameObject.tag = "MainCamera";
//			Debug.Log("Set MainCamera tag on Skybox camera", this);
//		}

//		// Configure main camera
//		sceneCullingMask = mainCamera.cullingMask;
//		mainCamera.clearFlags = CameraClearFlags.Skybox;
//		mainCamera.cullingMask = 0; // Nothing, as skybox is rendered by material
//		mainCamera.depth = -2;
//		mainCamera.enabled = true;

//		// Get CommandBufferSettings
//		commandBufferSettings = GetComponent<CommandBufferSettings>();
//		if (commandBufferSettings == null)
//		{
//			Debug.LogError("CommandBufferSettings component missing!", this);
//			enabled = false;
//			return;
//		}
//		Debug.Log("CommandBufferSettings initialized", this);

//		// Create child cameras
//		InitializeReflectionCamera();
//		InitializeSceneCamera();

//		// Configure URP camera stack (for compatibility)
//		ConfigureCameraStack();

//		Debug.Log("ReflectionPassCamera initialized successfully", this);
//	}

//	private void InitializeReflectionCamera()
//	{
//		var reflectionObj = new GameObject("ReflectionCamera");
//		reflectionObj.transform.SetParent(transform, false);
//		reflectionCamera = reflectionObj.AddComponent<Camera>();
//		SyncCameraProperties(reflectionCamera);
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.cullingMask = sceneCullingMask;
//		reflectionCamera.depth = -1;
//		reflectionCamera.enabled = true; // Required for manual rendering
//		reflectionCamera.targetTexture = null; // Render to framebuffer

//		// Add ReflectionRender component and set up command buffer
//		var reflection = reflectionObj.AddComponent<ReflectionRender>();
//		reflection.SetupCommandBuffer();
//		Debug.Log($"Set referenceCamera properties for ReflectionCamera", this);
//		Debug.Log("Initialized ReflectionCamera", this);

//		// Add UniversalAdditionalCameraData
//		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
//		reflectionData.renderType = CameraRenderType.Overlay;
//	}

//	private void InitializeSceneCamera()
//	{
//		var sceneObj = new GameObject("SceneCamera");
//		sceneObj.transform.SetParent(transform, false);
//		sceneCamera = sceneObj.AddComponent<Camera>();
//		SyncCameraProperties(sceneCamera);
//		sceneCamera.clearFlags = CameraClearFlags.Nothing;
//		sceneCamera.cullingMask = sceneCullingMask;
//		sceneCamera.depth = 0;
//		sceneCamera.enabled = true; // Required for manual rendering

//		// Create dim mesh
//		dimMesh = new Mesh();

//		// Create dim material if not assigned
//		if (dimMaterial == null)
//		{
//			Shader unlitShader = Shader.Find("Custom/UnlitFixedColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
//			if (unlitShader == null)
//			{
//				Debug.LogError("Failed to find Unlit shader!", this);
//				enabled = false;
//				return;
//			}
//			dimMaterial = new Material(unlitShader);
//			dimMaterial.SetFloat("_Surface", 1.0f); // Transparent
//			dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
//			dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
//			dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
//			dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
//			dimMaterial.SetFloat("_ZWrite", 0.0f);
//			dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
//			dimMaterial.SetFloat("_AlphaClip", 0.0f);
//			dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
//			dimMaterial.renderQueue = (int)RenderQueue.Transparent;
//			isDimMaterialDynamic = true;
//			Debug.Log("Created dynamic dimMaterial", this);
//		}

//		// Validate shader
//		if (dimMaterial.shader.name != "Custom/UnlitFixedColor" && dimMaterial.shader.name != "Universal Render Pipeline/Unlit")
//		{
//			Debug.LogWarning($"SceneCamera: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.", this);
//		}

//		// Add SceneRender component and set up command buffer
//		var sceneRender = sceneObj.AddComponent<SceneRender>();
//		sceneRender.SetupCommandBuffer(this, dimMesh, dimMaterial);
//		Debug.Log($"Initialized SceneCamera: sceneCamera={sceneCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
//		Debug.Log("Initialized SceneCamera with SceneRender", this);

//		// Add UniversalAdditionalCameraData
//		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
//		sceneData.renderType = CameraRenderType.Overlay;
//	}

//	private void SyncCameraProperties(Camera targetCamera)
//	{
//		targetCamera.fieldOfView = mainCamera.fieldOfView;
//		targetCamera.nearClipPlane = mainCamera.nearClipPlane;
//		targetCamera.farClipPlane = mainCamera.farClipPlane;
//		targetCamera.transform.position = mainCamera.transform.position;
//		targetCamera.transform.rotation = mainCamera.transform.rotation;
//		targetCamera.aspect = mainCamera.aspect;
//		targetCamera.orthographic = mainCamera.orthographic;
//		targetCamera.orthographicSize = mainCamera.orthographicSize;
//	}

//	private void ConfigureCameraStack()
//	{
//		var mainCameraData = mainCamera.GetComponent<UniversalAdditionalCameraData>();
//		if (mainCameraData == null)
//		{
//			mainCameraData = mainCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
//			Debug.Log("Added UniversalAdditionalCameraData to MainCamera", this);
//		}
//		mainCameraData.renderType = CameraRenderType.Base;
//		mainCameraData.cameraStack.Clear();
//		if (reflectionCamera != null)
//		{
//			mainCameraData.cameraStack.Add(reflectionCamera);
//			Debug.Log("Added ReflectionCamera to stack", this);
//		}
//		if (sceneCamera != null)
//		{
//			mainCameraData.cameraStack.Add(sceneCamera);
//			Debug.Log("Added SceneCamera to stack", this);
//		}
//		Debug.Log($"Camera stack configured: {mainCameraData.cameraStack.Count} cameras", this);
//	}

//	private void UpdateMaterialColor()
//	{
//		if (dimMaterial != null && (dimMaterial.HasProperty("_BaseColor") || dimMaterial.HasProperty("_Color")))
//		{
//			string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
//			dimMaterial.SetColor(colorProperty, dimColor);
//		}
//	}

//	private void UpdateDimGeometry()
//	{
//		if (sceneCamera == null)
//		{
//			Debug.LogError($"ReflectionPassCamera: sceneCamera is null", this);
//			return;
//		}

//		if (planeNormal == Vector3.zero)
//		{
//			Debug.LogWarning($"ReflectionPassCamera: Invalid planeNormal (zero vector)", this);
//			return;
//		}
//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var plane = new Plane(n, planePoint);

//		float near = sceneCamera.nearClipPlane;
//		float far = sceneCamera.farClipPlane;
//		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
//		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//		float aspect = sceneCamera.aspect;

//		// Calculate frustum corners
//		Vector3[] nearCorners = new Vector3[4];
//		Vector3[] farCorners = new Vector3[4];
//		nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
//		nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
//		nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
//		nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
//		farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
//		farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
//		farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
//		farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

//		// Transform to world space
//		Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
//		for (int i = 0; i < 4; i++)
//		{
//			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
//			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
//		}

//		// Intersect frustum edges with plane
//		List<Vector3> intersectionPoints = new List<Vector3>();
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

//			UpdateDimMesh(intersectionPoints);
//		}
//		else
//		{
//			dimMesh.Clear();
//		}
//	}

//	private void UpdateDimMesh(List<Vector3> points)
//	{
//		if (points.Count < 3)
//		{
//			Debug.LogWarning($"ReflectionPassCamera: Too few points ({points.Count}) to create mesh", this);
//			dimMesh.Clear();
//			return;
//		}

//		// Use world-space vertices
//		Vector3[] vertices = new Vector3[points.Count];
//		for (int i = 0; i < points.Count; i++)
//		{
//			vertices[i] = points[i];
//		}

//		// Fan triangulation
//		List<int> triangles = new List<int>();
//		for (int i = 1; i < points.Count - 1; i++)
//		{
//			triangles.Add(0);
//			triangles.Add(i);
//			triangles.Add(i + 1);
//		}

//		dimMesh.Clear();
//		dimMesh.vertices = vertices;
//		dimMesh.triangles = triangles.ToArray();
//		dimMesh.RecalculateBounds();
//		dimMesh.RecalculateNormals();
//	}

//	private Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//	{
//		List<Vector3> points = new List<Vector3>();
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

//	private List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
//	{
//		List<Vector3> unique = new List<Vector3>();
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

//	void LateUpdate()
//	{
//		// Update dim geometry and material color
//		UpdateMaterialColor();
//		UpdateDimGeometry();

//		// SceneRender updates
//		if (sceneCamera != null)
//		{
//			SyncCameraProperties(sceneCamera);
//			var sceneRender = sceneCamera.GetComponent<SceneRender>();
//			if (sceneRender != null)
//			{
//				sceneRender.enabled = true;
//			}
//		}

//		// ReflectionRender updates
//		if (reflectionCamera != null)
//		{
//			var reflection = reflectionCamera.GetComponent<ReflectionRender>();
//			if (reflection != null)
//			{
//				reflectionCamera.fieldOfView = mainCamera.fieldOfView;
//				reflectionCamera.nearClipPlane = mainCamera.nearClipPlane;
//				reflectionCamera.farClipPlane = mainCamera.farClipPlane;
//				reflectionCamera.aspect = mainCamera.aspect;

//				var n = planeNormal.normalized;
//				var planePoint = n * offset;
//				var reflectionMat = Matrix4x4.identity;
//				reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
//				reflectionMat[0, 1] = -2 * n.x * n.y;
//				reflectionMat[0, 2] = -2 * n.x * n.z;
//				reflectionMat[1, 0] = -2 * n.y * n.x;
//				reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
//				reflectionMat[1, 2] = -2 * n.y * n.z;
//				reflectionMat[2, 0] = -2 * n.z * n.x;
//				reflectionMat[2, 1] = -2 * n.z * n.y;
//				reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
//				var translateToOrigin = Matrix4x4.Translate(-planePoint);
//				var translateBack = Matrix4x4.Translate(planePoint);
//				reflectionMat = translateBack * reflectionMat * translateToOrigin;

//				reflectionCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//				reflectionCamera.projectionMatrix = mainCamera.projectionMatrix;
//			}
//		}
//	}

//	void OnDestroy()
//	{
//		if (reflectionCamera != null)
//		{
//			DestroyImmediate(reflectionCamera.gameObject, true);
//		}
//		if (sceneCamera != null)
//		{
//			DestroyImmediate(sceneCamera.gameObject, true);
//		}
//		if (dimMaterial != null && isDimMaterialDynamic)
//		{
//			DestroyImmediate(dimMaterial, true);
//			Debug.Log("Destroyed dynamic dimMaterial", this);
//		}
//		if (dimMesh != null)
//		{
//			Destroy(dimMesh);
//			Debug.Log("Destroyed dimMesh", this);
//		}
//	}

//	// Internal class: ReflectionRender
//	[RequireComponent(typeof(Camera))]
//	private class ReflectionRender : CommandBufferSettings
//	{
//		private Camera reflectionCamera;

//		public void SetupCommandBuffer()
//		{
//			reflectionCamera = GetComponent<Camera>();
//			var commandBufferSettings = GetComponent<CommandBufferSettings>();
//			if (commandBufferSettings == null)
//			{
//				Debug.LogError("CommandBufferSettings component missing on Reflection Camera", this);
//				enabled = false;
//				return;
//			}

//			commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques,
//				(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
//			commandBufferSettings.RegisterCommand(RenderPassMode.AfterRendering,
//				(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);
//			Debug.Log("ReflectionRender command buffer initialized", this);
//		}
//	}

//	// Internal class: SceneRender
//	[RequireComponent(typeof(Camera))]
//	private class SceneRender : CommandBufferSettings
//	{
//		public void SetupCommandBuffer(ReflectionPassCamera parent, Mesh dimMesh, Material dimMaterial)
//		{
//			// Validate inputs
//			if (parent == null || parent.sceneCamera == null)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: Missing parent or sceneCamera", this);
//				enabled = false;
//				return;
//			}
//			if (dimMesh == null)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: dimMesh is null", this);
//				enabled = false;
//				return;
//			}
//			if (dimMaterial == null)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: dimMaterial is null", this);
//				enabled = false;
//				return;
//			}

//			// Check for multiple components
//			var overlayComponents = GetComponents<SceneRender>();
//			if (overlayComponents.Length > 1)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: Multiple SceneRender components. Disabling this instance.", this);
//				enabled = false;
//				return;
//			}

//			var rgSettings = GetComponents<CommandBufferSettings>();
//			if (rgSettings.Length > 1)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: Multiple CommandBufferSettings components.", this);
//			}

//			// Register DrawMesh command
//			var commandBufferSettings = GetComponent<CommandBufferSettings>();
//			if (commandBufferSettings == null)
//			{
//				Debug.LogError($"SceneRender on {gameObject.name}: CommandBufferSettings component missing", this);
//				enabled = false;
//				return;
//			}
//			commandBufferSettings.RegisterCommand(RenderPassMode.BeforeRenderingOpaques, (commandBuffer, camera) =>
//			{
//				if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
//				{
//					return;
//				}
//				commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
//			}, parent.sceneCamera.name);
//			Debug.Log("SceneRender command buffer initialized", this);
//		}
//	}
//}