using UnityEngine;

public static class FrustumPlaneIntersection
{
	public static bool GenerateFrustumPlaneIntersectionMesh(Camera camera, Vector3 planeNormal, float planeOffset, Mesh targetMesh)
	{
		if (camera == null || planeNormal.normalized == Vector3.zero)
		{
			targetMesh.Clear();
			if (camera == null) Debug.LogError("FrustumPlaneIntersection: Camera is null");
			else Debug.LogWarning("FrustumPlaneIntersection: Invalid plane normal (zero vector)");
			return false;
		}

		Plane plane = new Plane(planeNormal.normalized, planeNormal.normalized * planeOffset);

		// Get frustum parameters
		float near = -camera.nearClipPlane, far = -camera.farClipPlane;
		float aspect = camera.aspect;
		Vector3[] nearCorners = new Vector3[4], farCorners = new Vector3[4];

		if (camera.orthographic)
		{
			// Orthographic camera: use orthographicSize
			float halfHeight = camera.orthographicSize;
			float halfWidth = halfHeight * aspect;
			nearCorners[0] = new Vector3(-halfWidth, halfHeight, near); // Top-left
			nearCorners[1] = new Vector3(halfWidth, halfHeight, near); // Top-right
			nearCorners[2] = new Vector3(halfWidth, -halfHeight, near); // Bottom-right
			nearCorners[3] = new Vector3(-halfWidth, -halfHeight, near); // Bottom-left
			farCorners[0] = new Vector3(-halfWidth, halfHeight, far);
			farCorners[1] = new Vector3(halfWidth, halfHeight, far);
			farCorners[2] = new Vector3(halfWidth, -halfHeight, far);
			farCorners[3] = new Vector3(-halfWidth, -halfHeight, far);
		}
		else
		{
			// Perspective camera: use fieldOfView
			float fovRad = camera.fieldOfView * Mathf.Deg2Rad;
			float halfFovTan = Mathf.Tan(fovRad * 0.5f);
			nearCorners[0] = new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near); // Top-left
			nearCorners[1] = new Vector3(halfFovTan * aspect * near, halfFovTan * near, near); // Top-right
			nearCorners[2] = new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-right
			nearCorners[3] = new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near); // Bottom-left
			farCorners[0] = new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far);
			farCorners[1] = new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);
			farCorners[2] = new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far);
			farCorners[3] = new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far);
		}

		// Transform corners to world space
		Matrix4x4 viewToWorld = camera.cameraToWorldMatrix;
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
		}

		// Collect intersections in order: near-to-far, near quad, far quad
		Vector3[] points = new Vector3[6];
		int pointCount = 0;
		for (int i = 0; i < 4 && pointCount < 6; i++)
		{
			// Near-to-far edges
			if (AddIntersection(plane, nearCorners[i], farCorners[i], out Vector3 point) && pointCount < 6)
				points[pointCount++] = point;
			// Near plane quad edges (clockwise: 0->1, 1->2, 2->3, 3->0)
			if (AddIntersection(plane, nearCorners[i], nearCorners[(i + 1) % 4], out point) && pointCount < 6)
				points[pointCount++] = point;
			// Far plane quad edges (clockwise: 0->1, 1->2, 2->3, 3->0)
			if (AddIntersection(plane, farCorners[i], farCorners[(i + 1) % 4], out point) && pointCount < 6)
				points[pointCount++] = point;
		}

		if (pointCount < 3)
		{
			targetMesh.Clear();
			Debug.LogWarning($"FrustumPlaneIntersection: Too few intersection points ({pointCount}) to create mesh");
			return false;
		}

		// Create triangle fan
		targetMesh.Clear();
		Vector3[] vertices = new Vector3[pointCount];
		System.Array.Copy(points, vertices, pointCount);
		targetMesh.vertices = vertices;
		int[] tris = new int[(pointCount - 2) * 3];
		for (int i = 0, idx = 0; i < pointCount - 2; i++, idx += 3)
		{
			tris[idx] = 0;
			tris[idx + 1] = i + 1;
			tris[idx + 2] = i + 2;
		}
		targetMesh.triangles = tris;
		targetMesh.RecalculateBounds();
		targetMesh.RecalculateNormals();
		return true;

		bool AddIntersection(Plane plane, Vector3 start, Vector3 end, out Vector3 point)
		{
			point = Vector3.zero;
			Vector3 dir = (end - start).normalized;
			float len = Vector3.Distance(start, end);
			if (plane.Raycast(new Ray(start, dir), out float t) && t >= 0 && t <= len)
			{
				point = start + dir * t;
				return true;
			}
			return false;
		}
	}
}