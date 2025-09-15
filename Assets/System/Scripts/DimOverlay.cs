using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class DimOverlay : MonoBehaviour
{
	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
	[SerializeField] private Material dimMaterial; // Required: Assign URP Unlit material in Inspector
	[SerializeField] private Camera reflectionCamera; // Reference to Reflection Camera

	private Camera sceneCamera;
	private GameObject dimObject;
	private Mesh dimMesh;
	private CommandBuffer dimCommandBuffer;
	private int currentFrame;

	void Awake()
	{
		sceneCamera = GetComponent<Camera>();
		if (sceneCamera == null || reflectionCamera == null || dimMaterial == null)
		{
			enabled = false;
			return;
		}

		// Create dim object (for transform only)
		dimObject = new GameObject("DimOverlay");
		dimObject.transform.SetParent(transform, false);
		dimObject.transform.localPosition = Vector3.zero;
		dimObject.transform.localRotation = Quaternion.identity;
		dimObject.transform.localScale = Vector3.one;

		// Set material color
		dimMaterial.SetColor(dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", dimColor);

		// Initialize CommandBuffer
		dimMesh = new Mesh();
		dimCommandBuffer = new CommandBuffer { name = "DimOverlay" };
		RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
	}

	void LateUpdate()
	{
		if (sceneCamera == null || dimMaterial == null || reflectionCamera == null) return;

		// Update dim color
		string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
		if (dimMaterial.GetColor(colorProperty) != dimColor)
		{
			dimMaterial.SetColor(colorProperty, dimColor);
		}

		UpdateDimGeometry();
	}

	void UpdateDimGeometry()
	{
		// Get plane from ReflectionCamera
		var reflectionCameraComponent = reflectionCamera.GetComponent<ReflectionCamera>();
		if (reflectionCameraComponent == null) return;

		Vector3 planeNormal = reflectionCameraComponent.planeNormal;
		float offset = reflectionCameraComponent.offset;
		var n = planeNormal.normalized;
		var planePoint = n * offset;
		var plane = new Plane(n, planePoint);

		float near = sceneCamera.nearClipPlane;
		float far = sceneCamera.farClipPlane;
		float fovRad = sceneCamera.fieldOfView * Mathf.Deg2Rad;
		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
		float aspect = sceneCamera.aspect;

		// Negated corner calculations
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
			if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
			{
				intersectionPoints.Add(start + dir * distance);
			}
		}

		// Intersect near and far planes if needed
		if (intersectionPoints.Count < 6)
		{
			Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, nearQuad));
			Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, farQuad));
		}

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
	}

	void UpdateDimMesh(List<Vector3> points)
	{
		if (points.Count < 3) return;

		// Use world-space vertices (CommandBuffer uses Matrix4x4.identity)
		Vector3[] vertices = new Vector3[points.Count];
		for (int i = 0; i < points.Count; i++)
		{
			vertices[i] = points[i];
		}

		dimMesh.Clear();
		dimMesh.vertices = vertices;

		// Fan triangulation
		List<int> triangles = new List<int>();
		for (int i = 1; i < points.Count - 1; i++)
		{
			triangles.Add(0);
			triangles.Add(i + 1);
			triangles.Add(i);
		}
		dimMesh.triangles = triangles.ToArray();
		dimMesh.RecalculateBounds();
		dimMesh.RecalculateNormals();
	}

	void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		if (camera != reflectionCamera || dimMesh.vertexCount == 0 || Time.frameCount == currentFrame)
			return;

		dimCommandBuffer.Clear();
		dimCommandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
		context.ExecuteCommandBuffer(dimCommandBuffer);
		context.Submit();
		currentFrame = Time.frameCount;
	}

	Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
	{
		List<Vector3> points = new List<Vector3>();
		for (int i = 0; i < 4; i++)
		{
			Vector3 start = quad[i];
			Vector3 end = quad[(i + 1) % 4];
			Vector3 dir = (end - start).normalized;
			if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
			{
				points.Add(start + dir * distance);
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

	void OnDisable()
	{
		RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
		if (dimObject != null)
		{
			Destroy(dimObject);
		}
		if (dimMaterial != null)
		{
			if (dimMaterial.GetTag("CreatedByScript", false) == "True")
				Destroy(dimMaterial);
		}
		if (dimMesh != null)
		{
			Destroy(dimMesh);
		}
		if (dimCommandBuffer != null)
		{
			dimCommandBuffer.Dispose();
		}
	}
}


//using UnityEngine;
//using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class DimOverlay : MonoBehaviour
//{
//	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
//	[SerializeField] private Material dimMaterial; // Required: Assign URP Unlit material in Inspector
//	[SerializeField] private Vector3 planeNormal = Vector3.up; // Duplicated for now
//	[SerializeField] private float offset = -0.2f; // Duplicated for now

//	private Camera sceneCamera;
//	private GameObject dimObject;
//	private MeshFilter dimMeshFilter;
//	private MeshRenderer dimMeshRenderer;
//	private Mesh dimMesh;

//	void Awake()
//	{
//		sceneCamera = GetComponent<Camera>();
//		if (sceneCamera == null)
//		{
//			Debug.LogError("Scene camera is null!", this);
//			enabled = false;
//			return;
//		}

//		// Create dim object
//		dimObject = new GameObject("DimOverlay");
//		dimObject.transform.SetParent(transform, false);
//		dimObject.transform.localPosition = Vector3.zero;
//		dimObject.transform.localRotation = Quaternion.identity;
//		dimObject.transform.localScale = Vector3.one;
//		dimObject.layer = LayerMask.NameToLayer("Default"); // Ensure visible layer
//		dimMeshFilter = dimObject.AddComponent<MeshFilter>();
//		dimMeshRenderer = dimObject.AddComponent<MeshRenderer>();
//		dimMesh = new Mesh();
//		dimMeshFilter.mesh = dimMesh;

//		// Require assigned material
//		if (dimMaterial == null)
//		{
//			Debug.LogError("DimOverlay requires an assigned dimMaterial (URP Unlit, Transparent, Two-Sided).");
//			enabled = false;
//			return;
//		}

//		Debug.Log($"Using assigned dimMaterial: {dimMaterial.name}, shader: {dimMaterial.shader.name}");
//		dimMaterial.SetColor(dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", dimColor);
//		dimMeshRenderer.material = dimMaterial;
//		Debug.Log($"MeshRenderer enabled: {dimMeshRenderer.enabled}, material: {dimMeshRenderer.material.name}");
//	}

//	void LateUpdate()
//	{
//		if (sceneCamera == null || dimMaterial == null) return;

//		// Update dim color
//		string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
//		if (dimMaterial.GetColor(colorProperty) != dimColor)
//		{
//			dimMaterial.SetColor(colorProperty, dimColor);
//			Debug.Log($"Updated dimColor: {dimColor}");
//		}

//		UpdateDimGeometry();
//	}

//	void UpdateDimGeometry()
//	{
//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var plane = new Plane(n, planePoint);

//		Debug.Log($"Plane normal: {n}, point: {planePoint}, distance: {plane.distance}");

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
//			Debug.Log($"Near corner {i}: {nearCorners[i]}, Far corner {i}: {farCorners[i]}");
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
//				Vector3 point = start + dir * distance;
//				intersectionPoints.Add(point);
//				Debug.Log($"Raycast hit at distance: {distance}, point: {point}");
//				float planeDistance = Vector3.Dot(point - planePoint, n);
//				Debug.Log($"Point {point} distance from plane: {planeDistance} (should be ~0)");
//			}
//			else
//			{
//				Debug.Log($"Raycast {i} missed plane.");
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
//		Debug.Log($"Total intersection points after quad intersections: {intersectionPoints.Count}");
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

//			for (int i = 0; i < intersectionPoints.Count; i++)
//				Debug.Log($"Final intersection point {i}: {intersectionPoints[i]}");

//			UpdateDimMesh(intersectionPoints);
//		}
//		else
//		{
//			dimMeshRenderer.enabled = false;
//			Debug.LogWarning("Insufficient intersection points for dim geometry.");
//		}
//	}

//	void UpdateDimMesh(List<Vector3> points)
//	{
//		if (points.Count < 3)
//		{
//			dimMeshRenderer.enabled = false;
//			Debug.LogWarning("UpdateDimMesh: Insufficient points.");
//			return;
//		}

//		// Convert world-space points to local space
//		Vector3[] localPoints = new Vector3[points.Count];
//		for (int i = 0; i < points.Count; i++)
//		{
//			localPoints[i] = dimObject.transform.InverseTransformPoint(points[i]);
//			Debug.Log($"Local mesh vertex {i}: {localPoints[i]}");
//		}

//		dimMesh.Clear();
//		dimMesh.vertices = localPoints;

//		// Fan triangulation
//		List<int> triangles = new List<int>();
//		for (int i = 1; i < points.Count - 1; i++)
//		{
//			triangles.Add(0);
//			triangles.Add(i + 1);
//			triangles.Add(i);
//		}
//		dimMesh.triangles = triangles.ToArray();
//		dimMesh.RecalculateBounds();
//		dimMesh.RecalculateNormals();

//		// Validate mesh
//		Debug.Log($"Mesh vertex count: {dimMesh.vertexCount}, triangle count: {dimMesh.triangles.Length / 3}");
//		Debug.Log($"Mesh bounds: {dimMesh.bounds}");
//		Debug.Log($"MeshRenderer enabled: {dimMeshRenderer.enabled}");

//		dimMeshRenderer.enabled = true;

//		for (int i = 0; i < localPoints.Length; i++)
//		{
//			Vector3 worldVertex = dimObject.transform.TransformPoint(localPoints[i]);
//			Debug.Log($"Mesh vertex {i} (world space): {worldVertex}");
//		}

//		Debug.Log($"Dim mesh updated with {points.Count} vertices.");
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
//		Debug.Log($"Quad intersection points: {points.Count}");
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

//	void OnDisable()
//	{
//		if (dimObject != null)
//		{
//			Destroy(dimObject);
//		}
//		if (dimMaterial != null)
//		{
//			if (dimMaterial.GetTag("CreatedByScript", false) == "True")
//				Destroy(dimMaterial);
//		}
//		if (dimMesh != null)
//		{
//			Destroy(dimMesh);
//		}
//	}
//}