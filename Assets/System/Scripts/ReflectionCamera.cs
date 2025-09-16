using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : CommandBufferSettings
{
	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
	[SerializeField] public Vector3 planeNormal = Vector3.up; // Public for potential sharing
	[SerializeField] public float offset = -0.2f; // Public for potential sharing

	private Camera reflectionCamera;

	private void onBeforeRender(CommandBuffer commandBuffer) { commandBuffer.SetInvertCulling(true); }
	private void onAfterRender(CommandBuffer commandBuffer) { commandBuffer.SetInvertCulling(false); }

	void Awake()
	{
		reflectionCamera = GetComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.targetTexture = null; // Render to framebuffer

		if (referenceCamera == null)
		{
			Debug.LogError("Reference camera is null!", this);
			enabled = false;
			return;
		}

		reflectionCamera.cullingMask = referenceCamera.cullingMask;

		OnBeforeRender += onBeforeRender;
		OnAfterRender += onAfterRender;
	}

	void LateUpdate()
	{
		if (referenceCamera == null || reflectionCamera == null) return;

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


//using UnityEngine;
//using UnityEngine.Rendering;

//[RequireComponent(typeof(Camera))]
//public class ReflectionCamera : MonoBehaviour
//{
//	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
//	[SerializeField] public Vector3 planeNormal = Vector3.up; // Public for potential sharing
//	[SerializeField] public float offset = -0.2f; // Public for potential sharing

//	private Camera reflectionCamera;
//	//private CommandBuffer commandBuffer;

//	private void OnBeforeRender(CommandBuffer commandBuffer) { commandBuffer.SetInvertCulling(true); }
//	private void OnAfterRender(CommandBuffer commandBuffer) { commandBuffer.SetInvertCulling(false); }

//	void Awake()
//	{
//		reflectionCamera = GetComponent<Camera>();
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.targetTexture = null; // Render to framebuffer

//		if (referenceCamera == null)
//		{
//			Debug.LogError("Reference camera is null!", this);
//			enabled = false;
//			return;
//		}

//		reflectionCamera.cullingMask = referenceCamera.cullingMask;

//		//commandBuffer = new CommandBuffer { name = "ReflectionCulling" };
//		//reflectionCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commandBuffer);
//		//RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

//		GetComponent<CommandBufferSettings>().BeforeRender += OnBeforeRender;
//		GetComponent<CommandBufferSettings>().AfterRender += OnAfterRender;
//	}

//	//void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
//	//{
//	//	commandBuffer.Clear();
//	//	commandBuffer.SetInvertCulling(true);
//	//	context.ExecuteCommandBuffer(commandBuffer);
//	//	context.Submit();
//	//}

//	void LateUpdate()
//	{
//		if (referenceCamera == null || reflectionCamera == null) return;

//		reflectionCamera.fieldOfView = referenceCamera.fieldOfView;
//		reflectionCamera.nearClipPlane = referenceCamera.nearClipPlane;
//		reflectionCamera.farClipPlane = referenceCamera.farClipPlane;
//		reflectionCamera.aspect = referenceCamera.aspect;

//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var reflectionMat = Matrix4x4.identity;
//		reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
//		reflectionMat[0, 1] = -2 * n.x * n.y;
//		reflectionMat[0, 2] = -2 * n.x * n.z;
//		reflectionMat[1, 0] = -2 * n.y * n.x;
//		reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
//		reflectionMat[1, 2] = -2 * n.y * n.z;
//		reflectionMat[2, 0] = -2 * n.z * n.x;
//		reflectionMat[2, 1] = -2 * n.z * n.y;
//		reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
//		var translateToOrigin = Matrix4x4.Translate(-planePoint);
//		var translateBack = Matrix4x4.Translate(planePoint);
//		reflectionMat = translateBack * reflectionMat * translateToOrigin;

//		reflectionCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix * reflectionMat;
//		reflectionCamera.projectionMatrix = referenceCamera.projectionMatrix;
//	}

//	void OnDisable()
//	{
//		//if (commandBuffer != null)
//		//{
//		//	reflectionCamera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, commandBuffer);
//		//	commandBuffer.Release();
//		//}

//		//if (commandBuffer != null)
//		//{
//		//	commandBuffer.Dispose();
//		//}
//	}
//}


//using UnityEngine;
//using UnityEngine.Rendering;
////using System.Collections.Generic;

//[RequireComponent(typeof(Camera))]
//public class ReflectionCamera : MonoBehaviour
//{
//	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
//	[SerializeField] private Vector3 planeNormal = Vector3.up;
//	[SerializeField] private float offset = -0.2f;
//	//[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);

//	private Camera reflectionCamera;
//	private CommandBuffer cullingCommandBuffer;
//	//private GameObject triangleMeshObject; // For dim geometry
//	//private Material dimMaterial;

//	void Awake()
//	{
//		reflectionCamera = GetComponent<Camera>();
//		reflectionCamera.clearFlags = CameraClearFlags.Depth;
//		reflectionCamera.targetTexture = null; // Render to framebuffer

//		if (referenceCamera == null)
//		{
//			Debug.LogError("Reference camera is null!", this);
//			return;
//		}

//		int reflectionDimLayer = LayerMask.NameToLayer("ReflectionDim");
//		if (reflectionDimLayer == -1)
//		{
//			Debug.LogError("ReflectionDim layer not found!", this);
//			return;
//		}
//		reflectionCamera.cullingMask = referenceCamera.cullingMask | (1 << reflectionDimLayer);

//		cullingCommandBuffer = new CommandBuffer { name = "ReflectionCulling" };
//		cullingCommandBuffer.SetInvertCulling(true);
//		reflectionCamera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);

//		//// Dim material (Unlit/TransparentDim)
//		//dimMaterial = new Material(Shader.Find("Unlit/TransparentDim"));
//		//if (dimMaterial == null)
//		//{
//		//	Debug.LogWarning("Shader 'Unlit/TransparentDim' not found, using fallback Unlit/Color (may not support transparency).", this);
//		//	dimMaterial = new Material(Shader.Find("Unlit/Color"));
//		//}
//		//dimMaterial.SetColor("_Color", dimColor);

//		//// Dim mesh
//		//triangleMeshObject = new GameObject("DimTriangleMesh");
//		//triangleMeshObject.AddComponent<MeshFilter>();
//		//triangleMeshObject.AddComponent<MeshRenderer>().material = dimMaterial;
//		//triangleMeshObject.layer = reflectionDimLayer; // Render only in Reflection Camera
//	}

//	void LateUpdate()
//	{
//		if (referenceCamera == null || reflectionCamera == null) return;

//		reflectionCamera.fieldOfView = referenceCamera.fieldOfView;
//		reflectionCamera.nearClipPlane = referenceCamera.nearClipPlane;
//		reflectionCamera.farClipPlane = referenceCamera.farClipPlane;
//		reflectionCamera.aspect = referenceCamera.aspect;

//		var n = planeNormal.normalized;
//		var planePoint = n * offset;
//		var reflectionMat = Matrix4x4.identity;
//		reflectionMat[0, 0] = 1 - 2 * n.x * n.x;
//		reflectionMat[0, 1] = -2 * n.x * n.y;
//		reflectionMat[0, 2] = -2 * n.x * n.z;
//		reflectionMat[1, 0] = -2 * n.y * n.x;
//		reflectionMat[1, 1] = 1 - 2 * n.y * n.y;
//		reflectionMat[1, 2] = -2 * n.y * n.z;
//		reflectionMat[2, 0] = -2 * n.z * n.x;
//		reflectionMat[2, 1] = -2 * n.z * n.y;
//		reflectionMat[2, 2] = 1 - 2 * n.z * n.z;
//		var translateToOrigin = Matrix4x4.Translate(-planePoint);
//		var translateBack = Matrix4x4.Translate(planePoint);
//		reflectionMat = translateBack * reflectionMat * translateToOrigin;

//		reflectionCamera.worldToCameraMatrix = referenceCamera.worldToCameraMatrix * reflectionMat;
//		reflectionCamera.projectionMatrix = referenceCamera.projectionMatrix;

//		//UpdateDimGeometry();
//		Graphics.ExecuteCommandBuffer(cullingCommandBuffer);
//	}

//	//void UpdateDimGeometry()
//	//{
//	//	// Define plane
//	//	var n = planeNormal.normalized;
//	//	var planePoint = n * offset;
//	//	var plane = new Plane(n, planePoint);

//	//	// Frustum corners
//	//	float near = reflectionCamera.nearClipPlane;
//	//	float far = reflectionCamera.farClipPlane;
//	//	float fovRad = reflectionCamera.fieldOfView * Mathf.Deg2Rad;
//	//	float halfFovTan = Mathf.Tan(fovRad * 0.5f);
//	//	float aspect = reflectionCamera.aspect;

//	//	Vector3[] nearCorners = new Vector3[4];
//	//	Vector3[] farCorners = new Vector3[4];
//	//	nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
//	//	nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);  // Top-right
//	//	nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
//	//	nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
//	//	farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far); // Top-left
//	//	farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);  // Top-right
//	//	farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-right
//	//	farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far); // Bottom-left

//	//	// Transform to world space
//	//	Matrix4x4 viewToWorld = reflectionCamera.cameraToWorldMatrix;
//	//	for (int i = 0; i < 4; i++)
//	//	{
//	//		nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
//	//		farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
//	//	}

//	//	// Intersect frustum edges with plane
//	//	List<Vector3> intersectionPoints = new List<Vector3>();
//	//	for (int i = 0; i < 4; i++)
//	//	{
//	//		Vector3 start = nearCorners[i];
//	//		Vector3 end = farCorners[i];
//	//		Vector3 dir = (end - start).normalized;
//	//		if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
//	//		{
//	//			intersectionPoints.Add(start + dir * distance);
//	//		}
//	//	}

//	//	// Intersect near and far planes
//	//	if (intersectionPoints.Count < 6)
//	//	{
//	//		Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
//	//		intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, nearQuad));
//	//		Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
//	//		intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, farQuad));
//	//	}

//	//	// Remove duplicates and limit to 6 vertices
//	//	intersectionPoints = RemoveDuplicates(intersectionPoints, 0.01f);
//	//	if (intersectionPoints.Count > 6) intersectionPoints = intersectionPoints.GetRange(0, 6);

//	//	// Update dim geometry
//	//	if (intersectionPoints.Count >= 3)
//	//	{
//	//		Vector3 centroid = Vector3.zero;
//	//		foreach (var pt in intersectionPoints) centroid += pt;
//	//		centroid /= intersectionPoints.Count;
//	//		Vector3 refDir = (intersectionPoints[0] - centroid).normalized;
//	//		intersectionPoints.Sort((a, b) =>
//	//		{
//	//			Vector3 va = a - centroid;
//	//			Vector3 vb = b - centroid;
//	//			float angleA = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, va), n), Vector3.Dot(refDir, va));
//	//			float angleB = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, vb), n), Vector3.Dot(refDir, vb));
//	//			return angleA.CompareTo(angleB);
//	//		});

//	//		UpdateDimTriangles(intersectionPoints);
//	//	}
//	//	else
//	//	{
//	//		if (triangleMeshObject != null) triangleMeshObject.SetActive(false);
//	//	}
//	//}

//	//void UpdateDimTriangles(List<Vector3> points)
//	//{
//	//	if (points.Count < 3)
//	//	{
//	//		if (triangleMeshObject != null) triangleMeshObject.SetActive(false);
//	//		return;
//	//	}

//	//	// Create mesh
//	//	Mesh mesh = new Mesh();
//	//	Vector3[] vertices = points.ToArray();

//	//	// Fan triangulation (reversed winding)
//	//	List<int> triangles = new List<int>();
//	//	for (int i = 1; i < points.Count - 1; i++)
//	//	{
//	//		triangles.Add(0);
//	//		triangles.Add(i + 1);
//	//		triangles.Add(i);
//	//	}

//	//	mesh.vertices = vertices;
//	//	mesh.triangles = triangles.ToArray();
//	//	mesh.RecalculateBounds();
//	//	mesh.RecalculateNormals();

//	//	// Assign to GameObject
//	//	triangleMeshObject.SetActive(true);
//	//	MeshFilter meshFilter = triangleMeshObject.GetComponent<MeshFilter>();
//	//	meshFilter.mesh = mesh;
//	//}

//	//Vector3[] IntersectPlaneWithQuad(Plane plane, Vector3[] quad)
//	//{
//	//	List<Vector3> points = new List<Vector3>();
//	//	for (int i = 0; i < 4; i++)
//	//	{
//	//		Vector3 start = quad[i];
//	//		Vector3 end = quad[(i + 1) % 4];
//	//		Vector3 dir = (end - start).normalized;
//	//		if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
//	//		{
//	//			points.Add(start + dir * distance);
//	//		}
//	//	}
//	//	return points.ToArray();
//	//}

//	//List<Vector3> RemoveDuplicates(List<Vector3> points, float threshold)
//	//{
//	//	List<Vector3> unique = new List<Vector3>();
//	//	foreach (var pt in points)
//	//	{
//	//		bool isUnique = true;
//	//		foreach (var u in unique)
//	//		{
//	//			if (Vector3.Distance(pt, u) < threshold)
//	//			{
//	//				isUnique = false;
//	//				break;
//	//			}
//	//		}
//	//		if (isUnique) unique.Add(pt);
//	//	}
//	//	return unique;
//	//}

//	void OnDisable()
//	{
//		if (cullingCommandBuffer != null)
//		{
//			reflectionCamera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);
//			cullingCommandBuffer.Release();
//		}
//		//if (dimMaterial != null)
//		//{
//		//	Destroy(dimMaterial);
//		//}
//		//if (triangleMeshObject != null)
//		//{
//		//	Destroy(triangleMeshObject);
//		//}
//	}
//}