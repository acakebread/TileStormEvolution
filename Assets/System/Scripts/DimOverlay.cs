using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class DimOverlay : CommandBufferSettings
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
			Debug.LogWarning($"DimOverlay on {gameObject.name}: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera} in Awake, will check in LateUpdate", this);
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
			Debug.LogWarning($"DimOverlay on {gameObject.name}: Already initialized, skipping", this);
			return;
		}

		// Update fields if provided
		sceneCamera = newSceneCamera != null ? newSceneCamera : sceneCamera;
		reflectionCamera = newReflectionCamera != null ? newReflectionCamera : reflectionCamera;
		dimColor = newDimColor.HasValue ? newDimColor.Value : dimColor;

		// Validate cameras
		if (sceneCamera == null || reflectionCamera == null)
		{
			Debug.LogError($"DimOverlay on {gameObject.name}: Missing required components: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}", this);
			enabled = false;
			return;
		}

		// Check for multiple components
		var overlayComponents = GetComponents<DimOverlay>();
		if (overlayComponents.Length > 1)
		{
			Debug.LogError($"DimOverlay on {gameObject.name}: Multiple DimOverlay components. Disabling this instance.", this);
			enabled = false;
			return;
		}

		var rgSettings = GetComponents<CommandBufferSettings>();
		if (rgSettings.Length > 1)
		{
			Debug.LogError($"DimOverlay on {gameObject.name}: Multiple CommandBufferSettings components.", this);
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
					Debug.LogError($"DimOverlay on {gameObject.name}: Failed to find Unlit shader.", this);
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
			Debug.LogWarning($"DimOverlay on {gameObject.name}: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.", this);
		}

		// Set initial color
		UpdateMaterialColor();

		// Register DrawMesh command
		var commandBufferSettings = GetComponent<CommandBufferSettings>();
		if (commandBufferSettings == null)
		{
			Debug.LogError($"DimOverlay on {gameObject.name}: CommandBufferSettings component missing", this);
			enabled = false;
			return;
		}
		commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.BeforeRenderingOpaques, (commandBuffer, camera) =>
		{
			UpdateDimGeometry();
			if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
			{
				//Debug.LogWarning($"DimOverlay on {gameObject.name}: dimMesh is invalid: VertexCount={dimMesh.vertexCount}, TriangleCount={dimMesh.triangles.Length / 3}", this);
				return;
			}
			commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
			//Debug.Log($"DimOverlay: Drawing mesh with {dimMesh.vertexCount} vertices, {dimMesh.triangles.Length / 3} triangles", this);
		}, sceneCamera.name);

		initialized = true;
		//Debug.Log($"DimOverlay initialized: sceneCamera={sceneCamera.name}, reflectionCamera={reflectionCamera.name}", this);
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
		var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionCamera>();
		if (reflectionCameraComponent == null)
		{
			Debug.LogWarning($"DimOverlay on {gameObject.name}: ReflectionCamera component missing on {reflectionCamera.name}", this);
			return;
		}

		Vector3 planeNormal = reflectionCameraComponent.planeNormal;
		float offset = reflectionCameraComponent.offset;
		if (planeNormal == Vector3.zero)
		{
			Debug.LogWarning($"DimOverlay on {gameObject.name}: Invalid planeNormal (zero vector)", this);
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

		//// Debug intersection points
		//Debug.Log($"DimOverlay: Plane normal={planeNormal}, offset={offset}, intersectionPoints={intersectionPoints.Count}", this);
		//for (int i = 0; i < intersectionPoints.Count; i++)
		//{
		//	Debug.Log($"Intersection point {i}: {intersectionPoints[i]}", this);
		//}

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
			//Debug.LogWarning($"DimOverlay on {gameObject.name}: Insufficient intersection points ({intersectionPoints.Count}) for mesh", this);
			dimMesh.Clear();
		}
	}

	void UpdateDimMesh(List<Vector3> points)
	{
		if (points.Count < 3)
		{
			Debug.LogWarning($"DimOverlay on {gameObject.name}: Too few points ({points.Count}) to create mesh", this);
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
		//Debug.Log($"DimOverlay: Updated mesh with {vertices.Length} vertices, {triangles.Count / 3} triangles", this);
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


//using UnityEngine;
//using UnityEngine.Rendering;
//using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class DimOverlay : CommandBufferSettings
//{
//    [SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.5f); // Inky black with 50% transparency
//    [SerializeField] private Material dimMaterial; // Assign Custom/UnlitFixedColor
//    [SerializeField] private Camera reflectionCamera; // Reference to Reflection Camera

//    private Camera sceneCamera;
//    private Mesh dimMesh;

//    void Awake()
//    {
//        sceneCamera = GetComponent<Camera>();
//        if (sceneCamera == null || reflectionCamera == null)
//        {
//            Debug.LogError($"DimOverlay on {gameObject.name}: Missing required components: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}");
//            enabled = false;
//            return;
//        }

//        // Check for multiple components
//        var overlayComponents = GetComponents<DimOverlay>();
//        if (overlayComponents.Length > 1)
//        {
//            Debug.LogError($"DimOverlay on {gameObject.name}: Multiple DimOverlay components. Disabling this instance.");
//            enabled = false;
//            return;
//        }

//        var rgSettings = GetComponents<CommandBufferSettings>();
//        if (rgSettings.Length > 1)
//        {
//            Debug.LogError($"DimOverlay on {gameObject.name}: Multiple CommandBufferSettings components.");
//        }

//        // Create mesh
//        dimMesh = new Mesh();
//        if (dimMesh == null)
//        {
//            Debug.LogError($"DimOverlay on {gameObject.name}: Failed to create dimMesh");
//            enabled = false;
//            return;
//        }

//        // Create material if none assigned
//        if (dimMaterial == null)
//        {
//            Shader unlitShader = Shader.Find("Custom/UnlitFixedColor");
//            if (unlitShader == null)
//            {
//                Debug.LogWarning($"DimOverlay on {gameObject.name}: Failed to find Custom/UnlitFixedColor shader. Falling back to URP Unlit.");
//                unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
//                if (unlitShader == null)
//                {
//                    Debug.LogError($"DimOverlay on {gameObject.name}: Failed to find URP Unlit shader.");
//                    enabled = false;
//                    return;
//                }
//            }
//            dimMaterial = new Material(unlitShader);
//            // Configure for transparency
//            dimMaterial.SetFloat("_Surface", 1.0f); // Transparent
//            dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
//            dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
//            dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
//            dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
//            dimMaterial.SetFloat("_ZWrite", 0.0f);
//            dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
//            dimMaterial.SetFloat("_AlphaClip", 0.0f);
//            dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
//            dimMaterial.renderQueue = (int)RenderQueue.Transparent;
//        }

//        // Validate shader
//        if (dimMaterial.shader.name != "Custom/UnlitFixedColor" && dimMaterial.shader.name != "Universal Render Pipeline/Unlit")
//        {
//            Debug.LogWarning($"DimOverlay on {gameObject.name}: Invalid shader {dimMaterial.shader.name}. Expected Custom/UnlitFixedColor or URP/Unlit.");
//        }

//        // Set initial color
//        UpdateMaterialColor();

//        // Register DrawMesh command
//        var commandBufferSettings = GetComponent<CommandBufferSettings>();
//        if (commandBufferSettings == null)
//        {
//            Debug.LogError($"DimOverlay on {gameObject.name}: CommandBufferSettings component missing");
//            enabled = false;
//            return;
//        }
//        commandBufferSettings.RegisterCommand(CommandBufferSettings.RenderPassMode.AfterRendering, (commandBuffer, camera) =>
//        {
//            UpdateDimGeometry();
//            if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
//            {
//                Debug.LogWarning($"DimOverlay on {gameObject.name}: dimMesh is invalid: VertexCount={dimMesh.vertexCount}, TriangleCount={dimMesh.triangles.Length / 3}");
//                return;
//            }
//            commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
//        }, sceneCamera.name);
//    }

//    void Update()
//    {
//        UpdateMaterialColor();
//    }

//    private void UpdateMaterialColor()
//    {
//        if (dimMaterial != null && (dimMaterial.HasProperty("_BaseColor") || dimMaterial.HasProperty("_Color")))
//        {
//            string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
//            dimMaterial.SetColor(colorProperty, dimColor);
//        }
//    }

//    void UpdateDimGeometry()
//    {
//        var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionCamera>();
//        if (reflectionCameraComponent == null)
//        {
//            Debug.LogWarning($"DimOverlay on {gameObject.name}: ReflectionCamera component missing on {reflectionCamera.name}");
//            return;
//        }

//        Vector3 planeNormal = reflectionCameraComponent.planeNormal;
//        float offset = reflectionCameraComponent.offset;
//        if (planeNormal == Vector3.zero)
//        {
//            Debug.LogWarning($"DimOverlay on {gameObject.name}: Invalid planeNormal (zero vector)");
//            return;
//        }
//        var n = planeNormal.normalized;
//        var planePoint = n * offset;
//        var plane = new Plane(n, planePoint);

//        float near = sceneCamera.nearClipPlane;
//        float far = sceneCamera.farClipPlane;
//        float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
//        float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//        float aspect = sceneCamera.aspect;

//        // Calculate frustum corners (legacy negation)
//        Vector3[] nearCorners = new Vector3[4];
//        Vector3[] farCorners = new Vector3[4];
//        nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
//        nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
//        nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
//        nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
//        farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
//        farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
//        farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
//        farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

//        // Transform to world space
//        Matrix4x4 viewToWorld = sceneCamera.cameraToWorldMatrix;
//        for (int i = 0; i < 4; i++)
//        {
//            nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
//            farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
//        }

//        // Intersect frustum edges with plane
//        List<Vector3> intersectionPoints = new List<Vector3>();
//        for (int i = 0; i < 4; i++)
//        {
//            Vector3 start = nearCorners[i];
//            Vector3 end = farCorners[i];
//            Vector3 dir = (end - start).normalized;
//            float maxDistance = Vector3.Distance(start, end);
//            bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
//            if (raycastHit && distance >= 0 && distance <= maxDistance)
//            {
//                Vector3 point = start + dir * distance;
//                intersectionPoints.Add(point);
//            }
//        }

//        // Intersect near and far planes
//        Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
//        var nearPoints = IntersectPlaneWithQuad(plane, nearQuad);
//        intersectionPoints.AddRange(nearPoints);
//        Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
//        var farPoints = IntersectPlaneWithQuad(plane, farQuad);
//        intersectionPoints.AddRange(farPoints);

//        // Remove duplicates and limit to 6 vertices
//        intersectionPoints = RemoveDuplicates(intersectionPoints, 0.01f);
//        if (intersectionPoints.Count > 6) intersectionPoints = intersectionPoints.GetRange(0, 6);

//        if (intersectionPoints.Count >= 3)
//        {
//            // Sort points for convex polygon
//            Vector3 centroid = Vector3.zero;
//            foreach (var pt in intersectionPoints) centroid += pt;
//            centroid /= intersectionPoints.Count;
//            Vector3 refDir = (intersectionPoints[0] - centroid).normalized;
//            intersectionPoints.Sort((a, b) =>
//            {
//                Vector3 va = a - centroid;
//                Vector3 vb = b - centroid;
//                float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, va), n), Vector3.Dot(refDir, va));
//                float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, vb), n), Vector3.Dot(refDir, vb));
//                return angleA.CompareTo(angleB);
//            });

//            UpdateDimMesh(intersectionPoints);
//        }
//        else
//        {
//            Debug.LogWarning($"DimOverlay on {gameObject.name}: Insufficient intersection points ({intersectionPoints.Count}) for mesh");
//            dimMesh.Clear();
//        }
//    }

//    void UpdateDimMesh(List<Vector3> points)
//    {
//        if (points.Count < 3)
//        {
//            Debug.LogWarning($"DimOverlay on {gameObject.name}: Too few points ({points.Count}) to create mesh");
//            dimMesh.Clear();
//            return;
//        }

//        // Use world-space vertices
//        Vector3[] vertices = new Vector3[points.Count];
//        for (int i = 0; i < points.Count; i++)
//        {
//            vertices[i] = points[i];
//        }

//        // Fan triangulation
//        List<int> triangles = new List<int>();
//        for (int i = 1; i < points.Count - 1; i++)
//        {
//            triangles.Add(0);
//            triangles.Add(i);
//            triangles.Add(i + 1);
//        }

//        dimMesh.Clear();
//        dimMesh.vertices = vertices;
//        dimMesh.triangles = triangles.ToArray();
//        dimMesh.RecalculateBounds();
//        dimMesh.RecalculateNormals();
//    }

//    Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//    {
//        List<Vector3> points = new List<Vector3>();
//        for (int i = 0; i < 4; i++)
//        {
//            Vector3 start = quad[i];
//            Vector3 end = quad[(i + 1) % 4];
//            Vector3 dir = (end - start).normalized;
//            float maxDistance = Vector3.Distance(start, end);
//            bool raycastHit = plane.Raycast(new Ray(start, dir), out float distance);
//            if (raycastHit && distance >= 0 && distance <= maxDistance)
//            {
//                Vector3 point = start + dir * distance;
//                points.Add(point);
//            }
//        }
//        return points.ToArray();
//    }

//    List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
//    {
//        List<Vector3> unique = new List<Vector3>();
//        foreach (var pt in points)
//        {
//            bool isUnique = true;
//            foreach (var u in unique)
//            {
//                if (Vector3.Distance(pt, u) < threshold)
//                {
//                    isUnique = false;
//                    break;
//                }
//            }
//            if (isUnique) unique.Add(pt);
//        }
//        return unique;
//    }

//    void OnDestroy()
//    {
//        if (dimMesh != null)
//        {
//            Destroy(dimMesh);
//        }
//    }
//}