using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class DimOverlay : MonoBehaviour
{
	[SerializeField] private Color dimColor = new Color(1f, 0f, 0f, 0.7f); // Red with 70% transparency
	[SerializeField] private Material dimMaterial; // Assign Custom/UnlitFixedColor
	[SerializeField] private Camera reflectionCamera; // Reference to Reflection Camera
	[SerializeField] private bool useFallbackQuad = false; // Toggle for debugging
	[SerializeField] private bool debugVisualizePoints = false; // Visualize intersection points (default off)
	[SerializeField] private float debugPointDuration = 0.1f; // Debug line duration

	private Camera sceneCamera;
	private Mesh dimMesh;
	private static int instanceCount = 0;
	private int instanceId;

	void Awake()
	{
		instanceCount++;
		instanceId = instanceCount;

		sceneCamera = GetComponent<Camera>();
		if (sceneCamera == null || reflectionCamera == null || dimMaterial == null)
		{
			Debug.LogError($"DimOverlay {instanceId}: Missing required components: sceneCamera={sceneCamera}, reflectionCamera={reflectionCamera}, dimMaterial={dimMaterial}");
			enabled = false;
			return;
		}

		// Check for multiple components
		var overlayComponents = GetComponents<DimOverlay>();
		if (overlayComponents.Length > 1)
		{
			Debug.LogError($"DimOverlay {instanceId}: Multiple DimOverlay components on {gameObject.name}. Disabling this instance.");
			enabled = false;
			return;
		}

		var rgSettings = GetComponents<CommandBufferSettingsRG>();
		if (rgSettings.Length > 1)
		{
			Debug.LogError($"DimOverlay {instanceId}: Multiple CommandBufferSettingsRG components on {gameObject.name}.");
		}

		// Create mesh
		dimMesh = new Mesh();
		if (dimMesh == null)
		{
			Debug.LogError($"DimOverlay {instanceId}: Failed to create dimMesh");
			enabled = false;
			return;
		}

		// Force Custom/UnlitFixedColor material
		Shader unlitShader = Shader.Find("Custom/UnlitFixedColor");
		if (unlitShader == null)
		{
			Debug.LogWarning($"DimOverlay {instanceId}: Failed to find Custom/UnlitFixedColor shader. Falling back to URP Unlit.");
			unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
			if (unlitShader == null)
			{
				Debug.LogError($"DimOverlay {instanceId}: Failed to find URP Unlit shader.");
				enabled = false;
				return;
			}
		}
		if (dimMaterial.shader.name != "Custom/UnlitFixedColor")
		{
			Debug.LogWarning($"DimOverlay {instanceId}: Invalid shader: {dimMaterial.shader.name}. Creating new material with {unlitShader.name}.");
			dimMaterial = new Material(unlitShader);
		}

		// Configure material
		dimMaterial.SetFloat("_Surface", 1.0f);
		dimMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		dimMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		dimMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
		dimMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
		dimMaterial.SetFloat("_ZWrite", 0.0f);
		dimMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual); // Fixed to LessEqual
		dimMaterial.SetFloat("_AlphaClip", 0.0f);
		dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		dimMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		dimMaterial.DisableKeyword("_ALPHAMODULATE_ON");
		dimMaterial.renderQueue = 3100;

		// Set color
		string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
		if (dimMaterial.HasProperty(colorProperty))
		{
			dimMaterial.SetColor(colorProperty, dimColor);
			dimMaterial.SetVector("_BaseColor", dimColor);
		}

		// Register DrawMesh command
		var commandBufferSettings = GetComponent<CommandBufferSettingsRG>();
		if (commandBufferSettings == null)
		{
			Debug.LogError($"DimOverlay {instanceId}: CommandBufferSettingsRG component missing on {gameObject.name}");
			enabled = false;
			return;
		}
		commandBufferSettings.OnAfterRender += (commandBuffer) =>
		{
			UpdateDimGeometry();
			if (dimMesh.vertexCount < 3 || dimMesh.triangles.Length < 3)
			{
				Debug.LogWarning($"DimOverlay {instanceId}: dimMesh is invalid: VertexCount={dimMesh.vertexCount}, TriangleCount={dimMesh.triangles.Length / 3}");
				return;
			}
			commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
		};
	}

	void UpdateDimGeometry()
	{
		var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionCamera>();
		if (reflectionCameraComponent == null)
		{
			Debug.LogWarning($"DimOverlay {instanceId}: ReflectionCamera component missing on {reflectionCamera.name}");
			if (useFallbackQuad)
			{
				CreateFallbackQuadMesh();
			}
			return;
		}

		Vector3 planeNormal = reflectionCameraComponent.planeNormal;
		float offset = reflectionCameraComponent.offset;
		if (planeNormal == Vector3.zero)
		{
			Debug.LogWarning($"DimOverlay {instanceId}: Invalid planeNormal (zero vector)");
			if (useFallbackQuad)
			{
				CreateFallbackQuadMesh();
			}
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

		// Calculate frustum corners (legacy negation)
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
				if (debugVisualizePoints)
				{
					Debug.DrawLine(start, point, Color.red, debugPointDuration);
					Debug.DrawLine(point, point + Vector3.up * 0.1f, Color.green, debugPointDuration);
				}
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
			Debug.LogWarning($"DimOverlay {instanceId}: Insufficient intersection points ({intersectionPoints.Count}) for mesh");
			if (useFallbackQuad)
			{
				CreateFallbackQuadMesh();
			}
			else
			{
				dimMesh.Clear();
			}
		}
	}

	void UpdateDimMesh(List<Vector3> points)
	{
		if (points.Count < 3)
		{
			Debug.LogWarning($"DimOverlay {instanceId}: Too few points ({points.Count}) to create mesh");
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

		if (debugVisualizePoints)
		{
			for (int i = 0; i < vertices.Length; i++)
			{
				Debug.DrawLine(vertices[i], vertices[i] + Vector3.up * 0.2f, Color.blue, debugPointDuration);
			}
		}
	}

	void CreateFallbackQuadMesh()
	{
		float depth = 10f;
		float halfHeight = Mathf.Tan(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth;
		float halfWidth = halfHeight * sceneCamera.aspect;

		Vector3[] vertices = new Vector3[]
		{
			new Vector3(-halfWidth, -halfHeight, depth),
			new Vector3(halfWidth, -halfHeight, depth),
			new Vector3(halfWidth, halfHeight, depth),
			new Vector3(-halfWidth, halfHeight, depth)
		};
		int[] triangles = new int[] { 0, 2, 1, 0, 3, 2 };
		Vector2[] uvs = new Vector2[]
		{
			new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
		};
		Vector3[] normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };

		dimMesh.Clear();
		dimMesh.vertices = vertices;
		dimMesh.triangles = triangles;
		dimMesh.uv = uvs;
		dimMesh.normals = normals;
		dimMesh.RecalculateBounds();
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
				if (debugVisualizePoints)
				{
					Debug.DrawLine(start, point, Color.yellow, debugPointDuration);
					Debug.DrawLine(point, point + Vector3.up * 0.1f, Color.green, debugPointDuration);
				}
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
		instanceCount--;
		if (dimMesh != null)
		{
			Destroy(dimMesh);
		}
	}
}

//using UnityEngine;
//using System.Collections.Generic;
//using UnityEngine.Rendering;

//[RequireComponent(typeof(Camera))]
//public class DimOverlay : CommandBufferSettings
//{
//	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
//	[SerializeField] private Material dimMaterial; // Required: Assign URP Unlit material in Inspector
//	[SerializeField] private Camera reflectionCamera; // Reference to Reflection Camera

//	private Camera sceneCamera;
//	private Mesh dimMesh;

//	void Awake()
//	{
//		sceneCamera = GetComponent<Camera>();
//		if (sceneCamera == null || reflectionCamera == null || dimMaterial == null)
//		{
//			enabled = false;
//			return;
//		}

//		//create mesh
//		dimMesh = new Mesh();
//		// Set material color
//		dimMaterial.SetColor(dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", dimColor);

//		OnBeforeRender += onBeforeRender;
//	}

//	void LateUpdate()
//	{
//		if (sceneCamera == null || dimMaterial == null || reflectionCamera == null) return;

//		// Update dim color
//		string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
//		if (dimMaterial.GetColor(colorProperty) != dimColor) dimMaterial.SetColor(colorProperty, dimColor);
//		UpdateDimGeometry();
//	}

//	void UpdateDimGeometry()
//	{
//		// Get plane from ReflectionCamera
//		var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionCamera>();
//		if (reflectionCameraComponent == null) return;

//		Vector3 planeNormal = reflectionCameraComponent.planeNormal;
//		float offset = reflectionCameraComponent.offset;
//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var plane = new Plane(n, planePoint);

//		float near = sceneCamera.nearClipPlane;
//		float far = sceneCamera.farClipPlane;
//		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
//		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//		float aspect = sceneCamera.aspect;

//		// Negated corner calculations
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
//			if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
//			{
//				intersectionPoints.Add(start + dir * distance);
//			}
//		}

//		// Intersect near and far planes if needed
//		if (intersectionPoints.Count < 6)
//		{
//			Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
//			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, nearQuad));
//			Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
//			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, farQuad));
//		}

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
//	}

//	void UpdateDimMesh(List<Vector3> points)
//	{
//		if (points.Count < 3) return;

//		// Use world-space vertices (CommandBuffer uses Matrix4x4.identity)
//		Vector3[] vertices = new Vector3[points.Count];
//		for (int i = 0; i < points.Count; i++)
//		{
//			vertices[i] = points[i];
//		}

//		dimMesh.Clear();
//		dimMesh.vertices = vertices;

//		// Fan triangulation
//		List<int> triangles = new List<int>();
//		for (int i = 1; i < points.Count - 1; i++)
//		{
//			triangles.Add(0);
//			triangles.Add(i);
//			triangles.Add(i + 1);
//		}
//		dimMesh.triangles = triangles.ToArray();
//		dimMesh.RecalculateBounds();
//		dimMesh.RecalculateNormals();
//	}

//	private void onBeforeRender(CommandBuffer commandBuffer)
//	{
//		commandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
//	}

//	Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//	{
//		List<Vector3> points = new List<Vector3>();
//		for (int i = 0; i < 4; i++)
//		{
//			Vector3 start = quad[i];
//			Vector3 end = quad[(i + 1) % 4];
//			Vector3 dir = (end - start).normalized;
//			if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
//			{
//				points.Add(start + dir * distance);
//			}
//		}
//		return points.ToArray();
//	}

//	List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
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

//	void OnDestroy()
//	{
//		if (dimMesh != null)
//		{
//			Destroy(dimMesh);
//		}
//	}
//}
