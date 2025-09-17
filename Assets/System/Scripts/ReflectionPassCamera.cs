using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[RequireComponent(typeof(Camera), typeof(CommandBufferSettings))]
public class ReflectionPassCamera : MonoBehaviour
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
		SyncCameraProperties(reflectionCamera);
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.cullingMask = sceneCullingMask;
		reflectionCamera.depth = -1;
		reflectionCamera.enabled = true; // Required for manual rendering

		// Add ReflectionRender component
		var reflection = reflectionObj.AddComponent<ReflectionRender>();
		reflection.planeNormal = planeNormal;
		reflection.offset = offset;
		// Set fields via reflection before Awake runs
		System.Reflection.FieldInfo referenceCameraField = typeof(ReflectionRender).GetField("referenceCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (referenceCameraField != null)
		{
			referenceCameraField.SetValue(reflection, mainCamera);
			Debug.Log($"Set referenceCamera to {mainCamera} in ReflectionRender", this);
		}
		else
		{
			Debug.LogError("Field referenceCamera not found in ReflectionRender", this);
		}
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
		SyncCameraProperties(sceneCamera);
		sceneCamera.clearFlags = CameraClearFlags.Nothing;
		sceneCamera.cullingMask = sceneCullingMask;
		sceneCamera.depth = 0;
		sceneCamera.enabled = true; // Required for manual rendering

		// Add SceneRender component
		var sceneRender = sceneObj.AddComponent<SceneRender>();
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
		// Set fields via reflection before Awake runs
		System.Reflection.FieldInfo sceneCameraField = typeof(SceneRender).GetField("sceneCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo reflectionCameraField = typeof(SceneRender).GetField("reflectionCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo dimColorField = typeof(SceneRender).GetField("dimColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		System.Reflection.FieldInfo dimMaterialField = typeof(SceneRender).GetField("dimMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (sceneCameraField != null) sceneCameraField.SetValue(sceneRender, sceneCamera);
		if (reflectionCameraField != null) reflectionCameraField.SetValue(sceneRender, reflectionCamera);
		if (dimColorField != null) dimColorField.SetValue(sceneRender, dimColor);
		if (dimMaterialField != null) dimMaterialField.SetValue(sceneRender, dimMaterial);
		Debug.Log($"Set SceneRender fields: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
		Debug.Log("Initialized SceneCamera with SceneRender", this);

		// Add UniversalAdditionalCameraData
		var sceneData = sceneObj.AddComponent<UniversalAdditionalCameraData>();
		sceneData.renderType = CameraRenderType.Overlay;
	}

	private void SyncCameraProperties(Camera targetCamera)
	{
		targetCamera.fieldOfView = mainCamera.fieldOfView;
		targetCamera.nearClipPlane = mainCamera.nearClipPlane;
		targetCamera.farClipPlane = mainCamera.farClipPlane;
		targetCamera.transform.position = mainCamera.transform.position;
		targetCamera.transform.rotation = mainCamera.transform.rotation;
		targetCamera.aspect = mainCamera.aspect;
		targetCamera.orthographic = mainCamera.orthographic;
		targetCamera.orthographicSize = mainCamera.orthographicSize;
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

	private void SetPrivateField(object target, string fieldName, object value)
	{
		var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		if (field != null)
		{
			field.SetValue(target, value);
		}
		else
		{
			Debug.LogError($"Field {fieldName} not found in {target.GetType().Name}", this);
		}
	}

	void LateUpdate()
	{
		if (sceneCamera != null)
		{
			SyncCameraProperties(sceneCamera);
			var sceneRender = sceneCamera.GetComponent<SceneRender>();
			if (sceneRender != null)
			{
				SetPrivateField(sceneRender, "sceneCamera", sceneCamera);
				SetPrivateField(sceneRender, "reflectionCamera", reflectionCamera);
				SetPrivateField(sceneRender, "dimColor", dimColor);
				SetPrivateField(sceneRender, "dimMaterial", dimMaterial);
				sceneRender.enabled = true;
			}
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
	}

	// Internal class: ReflectionRender (formerly ReflectionCamera)
	[RequireComponent(typeof(Camera))]
	private class ReflectionRender : CommandBufferSettings
	{
		[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
		[SerializeField] public Vector3 planeNormal = Vector3.up; // Public for potential sharing
		[SerializeField] public float offset = -0.2f; // Public for potential sharing

		private Camera reflectionCamera;
		private bool initialized;

		void Awake()
		{
			reflectionCamera = GetComponent<Camera>();
			reflectionCamera.clearFlags = CameraClearFlags.Depth;
			reflectionCamera.targetTexture = null; // Render to framebuffer

			// Defer referenceCamera validation to LateUpdate
			if (referenceCamera == null)
			{
				Debug.LogWarning("Reference camera is null in Awake, will check in LateUpdate", this);
			}
			else
			{
				Initialize();
			}
		}

		private void Initialize()
		{
			if (referenceCamera == null)
			{
				Debug.LogError("Reference camera is null!", this);
				enabled = false;
				return;
			}

			if (null != referenceCamera.GetComponent<ReflectionPassCamera>())
				reflectionCamera.cullingMask = referenceCamera.GetComponent<ReflectionPassCamera>().sceneCullingMask;
			else
				reflectionCamera.cullingMask = referenceCamera.cullingMask;

			var commandBufferSettings = GetComponent<CommandBufferSettings>();
			if (commandBufferSettings == null)
			{
				Debug.LogError("CommandBufferSettings component missing on Reflection Camera", this);
				enabled = false;
				return;
			}

			commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques,
				(commandBuffer, camera) => commandBuffer.SetInvertCulling(true), reflectionCamera.name);
			commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.AfterRendering,
				(commandBuffer, camera) => commandBuffer.SetInvertCulling(false), reflectionCamera.name);
			initialized = true;
			Debug.Log("ReflectionRender initialized", this);
		}

		void LateUpdate()
		{
			if (!initialized && referenceCamera != null)
			{
				Initialize();
			}

			if (!initialized || referenceCamera == null || reflectionCamera == null)
			{
				return;
			}

			reflectionCamera.fieldOfView = referenceCamera.fieldOfView;
			reflectionCamera.nearClipPlane = referenceCamera.nearClipPlane;
			reflectionCamera.farClipPlane = referenceCamera.farClipPlane;
			reflectionCamera.aspect = referenceCamera.aspect;

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

			reflectionCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix * reflectionMat;
			reflectionCamera.projectionMatrix = referenceCamera.projectionMatrix;
		}
	}

	// Internal class: SceneRender (formerly DimOverlay)
	[RequireComponent(typeof(Camera))]
	private class SceneRender : CommandBufferSettings
	{
		[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Inky black with 50% transparency
		[SerializeField] private Material dimMaterial; // Assign Custom/UnlitFixedColor
		[SerializeField] private Camera reflectionCamera; // Reference to Reflection Camera

		private Camera sceneCamera;
		private Mesh dimMesh;
		private bool initialized;

		void Awake()
		{
			sceneCamera = GetComponent<Camera>();
			dimMesh = new Mesh();

			// Defer validation to LateUpdate
			if (sceneCamera == null || reflectionCamera == null)
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera} in Awake, will check in LateUpdate", this);
			}
			else
			{
				Initialize();
			}
		}

		public void Initialize(Camera newSceneCamera = null, Camera newReflectionCamera = null, Material newDimMaterial = null, Color? newDimColor = null)
		{
			if (initialized)
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: Already initialized, skipping", this);
				return;
			}

			// Update fields if provided
			sceneCamera = newSceneCamera != null ? newSceneCamera : sceneCamera;
			reflectionCamera = newReflectionCamera != null ? newReflectionCamera : reflectionCamera;
			dimColor = newDimColor.HasValue ? newDimColor.Value : dimColor;

			// Validate cameras
			if (sceneCamera == null || reflectionCamera == null)
			{
				Debug.LogError($"SceneRender on {gameObject.name}: Missing required components: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}", this);
				enabled = false;
				return;
			}

			// Check for multiple components
			var overlayComponents = GetComponents<SceneRender>();
			if (overlayComponents.Length > 1)
			{
				Debug.LogError($"SceneRender on {gameObject.name}: Multiple SceneRender components. Disabling this instance.", this);
				enabled = false;
				return;
			}

			var rgSettings = GetComponents<CommandBufferSettings>();
			if (rgSettings.Length > 1)
			{
				Debug.LogError($"SceneRender on {gameObject.name}: Multiple CommandBufferSettings components.", this);
			}

			// Create material if none assigned
			if (dimMaterial == null)
			{
				if (newDimMaterial != null)
				{
					dimMaterial = newDimMaterial;
				}
				else
				{
					Shader unlitShader = Shader.Find("Custom/UnlitFixedColor") ?? Shader.Find("Universal Render Pipeline/Unlit");
					if (unlitShader == null)
					{
						Debug.LogError($"SceneRender on {gameObject.name}: Failed to find Unlit shader.", this);
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
				}
			}

			// Validate shader
			if (dimMaterial.shader.name != "Custom/UnlitFixedColor" && dimMaterial.shader.name != "Universal Render Pipeline/Unlit")
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.", this);
			}

			// Set initial color
			UpdateMaterialColor();

			// Register DrawMesh command
			var commandBufferSettings = GetComponent<CommandBufferSettings>();
			if (commandBufferSettings == null)
			{
				Debug.LogError($"SceneRender on {gameObject.name}: CommandBufferSettings component missing", this);
				enabled = false;
				return;
			}
			commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques, (commandBuffer, camera) =>
			{
				UpdateDimGeometry();
				if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
				{
					return;
				}
				commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
			}, sceneCamera.name);

			initialized = true;
		}

		void Update()
		{
			if (!initialized && sceneCamera != null && reflectionCamera != null)
			{
				Initialize();
			}
			UpdateMaterialColor();
		}

		private void UpdateMaterialColor()
		{
			if (dimMaterial != null && (dimMaterial.HasProperty("_BaseColor") || dimMaterial.HasProperty("_Color")))
			{
				string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
				dimMaterial.SetColor(colorProperty, dimColor);
			}
		}

		void UpdateDimGeometry()
		{
			var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionRender>();
			if (reflectionCameraComponent == null)
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: ReflectionRender component missing on {reflectionCamera.name}", this);
				return;
			}

			Vector3 planeNormal = reflectionCameraComponent.planeNormal;
			float offset = reflectionCameraComponent.offset;
			if (planeNormal == Vector3.zero)
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: Invalid planeNormal (zero vector)", this);
				return;
			}
			var n = planeNormal.normalized;
			var planePoint = n * offset;
			var plane = new Plane(n, planePoint);

			float near = sceneCamera.nearClipPlane;
			float far = sceneCamera.farClipPlane;
			float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
			float halfFovTan = Mathf.Tan(fovRad * 0.5f);
			float aspect = sceneCamera.aspect;

			// Calculate frustum corners (remove legacy negation)
			Vector3[] nearCorners = new Vector3[4];
			Vector3[] farCorners = new Vector3[4];
			nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
			nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
			nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
			nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
			farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
			farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
			farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
			farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

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

		void UpdateDimMesh(List<Vector3> points)
		{
			if (points.Count < 3)
			{
				Debug.LogWarning($"SceneRender on {gameObject.name}: Too few points ({points.Count}) to create mesh", this);
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

		Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
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

		List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
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
			if (dimMesh != null)
			{
				Destroy(dimMesh);
			}
		}
	}
}



//using UnityEngine;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//[RequireComponent(typeof(Camera), typeof(CommandBufferSettings))]
//public class ReflectionPassCamera : MonoBehaviour
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
//		var reflectionObj = new GameObject("ReflectionCamera");// { hideFlags = HideFlags.HideAndDontSave };
//		reflectionObj.transform.SetParent(transform, false);
//		reflectionCamera = reflectionObj.AddComponent<Camera>();
//		SyncCameraProperties(reflectionCamera);
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.cullingMask = sceneCullingMask;
//		reflectionCamera.depth = -1;
//		reflectionCamera.enabled = true; // Required for manual rendering

//		// Add ReflectionCamera component
//		var reflection = reflectionObj.AddComponent<ReflectionCamera>();
//		reflection.planeNormal = planeNormal;
//		reflection.offset = offset;
//		// Set fields via reflection before Awake runs
//		System.Reflection.FieldInfo referenceCameraField = typeof(ReflectionCamera).GetField("referenceCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		if (referenceCameraField != null)
//		{
//			referenceCameraField.SetValue(reflection, mainCamera);
//			Debug.Log($"Set referenceCamera to {mainCamera} in ReflectionCamera", this);
//		}
//		else
//		{
//			Debug.LogError("Field referenceCamera not found in ReflectionCamera", this);
//		}
//		Debug.Log("Initialized ReflectionCamera", this);

//		// Add UniversalAdditionalCameraData
//		var reflectionData = reflectionObj.AddComponent<UniversalAdditionalCameraData>();
//		reflectionData.renderType = CameraRenderType.Overlay;
//	}

//	private void InitializeSceneCamera()
//	{
//		var sceneObj = new GameObject("SceneCamera");// { hideFlags = HideFlags.HideAndDontSave };
//		sceneObj.transform.SetParent(transform, false);
//		sceneCamera = sceneObj.AddComponent<Camera>();
//		SyncCameraProperties(sceneCamera);
//		sceneCamera.clearFlags = CameraClearFlags.Nothing;
//		sceneCamera.cullingMask = sceneCullingMask;
//		sceneCamera.depth = 0;
//		sceneCamera.enabled = true; // Required for manual rendering

//		// Add DimOverlay component
//		var dimOverlay = sceneObj.AddComponent<DimOverlay>();
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
//		// Set fields via reflection before Awake runs
//		System.Reflection.FieldInfo sceneCameraField = typeof(DimOverlay).GetField("sceneCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		System.Reflection.FieldInfo reflectionCameraField = typeof(DimOverlay).GetField("reflectionCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		System.Reflection.FieldInfo dimColorField = typeof(DimOverlay).GetField("dimColor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		System.Reflection.FieldInfo dimMaterialField = typeof(DimOverlay).GetField("dimMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		if (sceneCameraField != null) sceneCameraField.SetValue(dimOverlay, sceneCamera);
//		if (reflectionCameraField != null) reflectionCameraField.SetValue(dimOverlay, reflectionCamera);
//		if (dimColorField != null) dimColorField.SetValue(dimOverlay, dimColor);
//		if (dimMaterialField != null) dimMaterialField.SetValue(dimOverlay, dimMaterial);
//		Debug.Log($"Set DimOverlay fields: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}, dimColor={dimColor}, dimMaterial={dimMaterial}", this);
//		Debug.Log("Initialized SceneCamera with DimOverlay", this);

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

//	private void SetPrivateField(object target, string fieldName, object value)
//	{
//		var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
//		if (field != null)
//		{
//			field.SetValue(target, value);
//			//Debug.Log($"Set {fieldName} to {value} in {target.GetType().Name}", this);
//		}
//		else
//		{
//			Debug.LogError($"Field {fieldName} not found in {target.GetType().Name}", this);
//		}
//	}

//	void LateUpdate()
//	{
//		// Sync camera properties
//		//if (reflectionCamera != null)// copy is not currently needed because the reflection camera successfully copies properties in time
//		//{
//		//	SyncCameraProperties(reflectionCamera);
//		//	var reflection = reflectionCamera.GetComponent<ReflectionCamera>();
//		//	if (reflection != null)
//		//	{
//		//		reflection.planeNormal = planeNormal;
//		//		reflection.offset = offset;
//		//		SetPrivateField(reflection, "referenceCamera", mainCamera);
//		//		reflection.enabled = true;
//		//	}
//		//}
//		if (sceneCamera != null)
//		{
//			SyncCameraProperties(sceneCamera);
//			var dimOverlay = sceneCamera.GetComponent<DimOverlay>();
//			if (dimOverlay != null)
//			{
//				SetPrivateField(dimOverlay, "sceneCamera", sceneCamera);
//				SetPrivateField(dimOverlay, "reflectionCamera", reflectionCamera);
//				SetPrivateField(dimOverlay, "dimColor", dimColor);
//				SetPrivateField(dimOverlay, "dimMaterial", dimMaterial);
//				dimOverlay.enabled = true;
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
//	}
//}