using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[RequireComponent(typeof(Camera))]
public class ReflectionCamera : MonoBehaviour
{
	[SerializeField] private Camera referenceCamera; // Overlay camera (Scene Camera)
	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = -0.2f;
	[SerializeField] private Color dimColor = new Color(0.1f, 0.1f, 0.1f, 0.7f);
	[SerializeField] private Material dimMaterial; // Optional URP Unlit material (Transparent, Two-Sided)

	private Camera reflectionCamera;
	private CommandBuffer cullingCommandBuffer;
	private CommandBuffer dimCommandBuffer;
	private Mesh dimMesh;
	private int currentFrame = -1; // Frame tracking for Pause mode

	void Awake()
	{
		reflectionCamera = GetComponent<Camera>();
		reflectionCamera.clearFlags = CameraClearFlags.Depth;
		reflectionCamera.targetTexture = null; // Render to framebuffer

		if (referenceCamera == null)
			return;

		reflectionCamera.cullingMask = referenceCamera.cullingMask;

		// Culling CommandBuffer
		cullingCommandBuffer = new CommandBuffer { name = "ReflectionCulling" };
		cullingCommandBuffer.SetInvertCulling(true);
		reflectionCamera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);

		// Dim CommandBuffer
		dimCommandBuffer = new CommandBuffer { name = "DimOverlay" };
		RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

		// Dim material: Use provided material or create URP Unlit
		if (dimMaterial == null)
		{
			Shader urpUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
			Shader fallbackShader = Shader.Find("Unlit/Color");
			if (urpUnlitShader == null && fallbackShader == null)
			{
				Debug.LogError("No suitable shader found (Universal Render Pipeline/Unlit or Unlit/Color).");
				return;
			}

			dimMaterial = new Material(urpUnlitShader != null ? urpUnlitShader : fallbackShader);
			if (urpUnlitShader != null)
			{
				// Configure URP Unlit for transparency
				dimMaterial.SetOverrideTag("RenderType", "Transparent");
				dimMaterial.SetInt("_Surface", 1); // Transparent
				dimMaterial.SetInt("_Blend", 0); // Alpha blend
				dimMaterial.SetInt("_ZWrite", 0); // No depth write
				dimMaterial.SetInt("_Cull", 0); // Cull Off (Two-Sided)
				dimMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
				dimMaterial.renderQueue = 3000; // Transparent queue

				// Validate shader (critical step from validateMaterial)
				Shader shader = Shader.Find(dimMaterial.shader.name);
				if (shader != null)
				{
					dimMaterial.shader = shader; // Reassign to force pipeline update
				}
				else
				{
					Debug.LogWarning("Shader not found: " + dimMaterial.shader.name);
				}
			}
			else
			{
				// Configure fallback Unlit/Color (opaque, but better than nothing)
				dimMaterial.SetColor("_Color", dimColor);
			}
			dimMaterial.SetOverrideTag("CreatedByScript", "True");
		}
		dimMaterial.SetColor(dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", dimColor);

		// Dim mesh
		dimMesh = new Mesh();
	}

	void LateUpdate()
	{
		if (referenceCamera == null || reflectionCamera == null || dimMaterial == null) return;

		// Update camera transform
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

		// Update dim color
		string colorProperty = dimMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
		if (dimMaterial.GetColor(colorProperty) != dimColor)
			dimMaterial.SetColor(colorProperty, dimColor);

		UpdateDimGeometry();

#if UNITY_EDITOR
		// Force render in Pause mode
		if (UnityEditor.EditorApplication.isPaused && Time.frameCount != currentFrame && dimMesh.vertexCount > 0)
		{
			ScriptableRenderContext context = default;
			dimCommandBuffer.Clear();
			dimCommandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
			context.ExecuteCommandBuffer(dimCommandBuffer);
			context.Submit();
			currentFrame = Time.frameCount;
		}
#endif
	}

	void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
	{
		if (camera != reflectionCamera || dimMesh.vertexCount == 0)
			return;

		dimCommandBuffer.Clear();
		dimCommandBuffer.DrawMesh(dimMesh, Matrix4x4.identity, dimMaterial, 0, -1);
		context.ExecuteCommandBuffer(dimCommandBuffer);
		context.Submit();
		currentFrame = Time.frameCount;
	}

	void UpdateDimGeometry()
	{
		var n = planeNormal.normalized;
		var planePoint = n * offset;
		var plane = new Plane(n, planePoint);

		float near = reflectionCamera.nearClipPlane;
		float far = reflectionCamera.farClipPlane;
		float fovRad = reflectionCamera.fieldOfView * Mathf.Deg2Rad;
		float halfFovTan = Mathf.Tan(fovRad * 0.5f);
		float aspect = reflectionCamera.aspect;

		Vector3[] nearCorners = new Vector3[4];
		Vector3[] farCorners = new Vector3[4];
		nearCorners[0] = -new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near);
		nearCorners[1] = -new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);
		nearCorners[2] = -new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near);
		nearCorners[3] = -new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near);
		farCorners[0] = -new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far);
		farCorners[1] = -new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);
		farCorners[2] = -new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far);
		farCorners[3] = -new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far);

		Matrix4x4 viewToWorld = reflectionCamera.cameraToWorldMatrix;
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
			if (plane.Raycast(new Ray(start, dir), out float distance) && distance >= 0 && distance <= Vector3.Distance(start, end))
			{
				intersectionPoints.Add(start + dir * distance);
			}
		}

		if (intersectionPoints.Count < 6)
		{
			Vector3[] nearQuad = { nearCorners[0], nearCorners[1], nearCorners[2], nearCorners[3] };
			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, nearQuad));
			Vector3[] farQuad = { farCorners[0], farCorners[1], farCorners[2], farCorners[3] };
			intersectionPoints.AddRange(IntersectPlaneWithQuad(plane, farQuad));
		}

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

			UpdateDimMesh(intersectionPoints);
		}
		else
		{
			dimCommandBuffer.Clear();
		}
	}

	void UpdateDimMesh(List<Vector3> points)
	{
		if (points.Count < 3)
			return;

		dimMesh.Clear();
		dimMesh.vertices = points.ToArray();

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
		if (cullingCommandBuffer != null)
		{
			reflectionCamera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, cullingCommandBuffer);
			cullingCommandBuffer.Release();
		}
		if (dimCommandBuffer != null)
		{
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
			dimCommandBuffer.Release();
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
	}
}