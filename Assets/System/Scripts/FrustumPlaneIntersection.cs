using UnityEngine;

public static class FrustumPlaneIntersection
{
	/// <summary>
	/// Generates a mesh representing the intersection of a camera frustum with a plane.
	/// </summary>
	/// <param name="camera">The camera whose frustum to intersect.</param>
	/// <param name="planeNormal">The plane's normal vector (will be normalized).</param>
	/// <param name="planeOffset">The distance from the origin along the normal to the plane.</param>
	/// <param name="targetMesh">The mesh to store the intersection geometry.</param>
	/// <returns>True if a valid mesh was generated, false otherwise.</returns>
	public static bool GenerateFrustumPlaneIntersectionMesh(Camera camera, Vector3 planeNormal, float planeOffset, Mesh targetMesh)
	{
		if (!camera || planeNormal.normalized == Vector3.zero)
		{
			targetMesh.Clear();
			if (!camera) Debug.LogError("FrustumPlaneIntersection: Camera is null");
			else Debug.LogWarning("FrustumPlaneIntersection: Invalid plane normal (zero vector)");
			return false;
		}

		var plane = new Plane(planeNormal.normalized, planeNormal.normalized * planeOffset);
		float near = -camera.nearClipPlane, far = -camera.farClipPlane, aspect = camera.aspect;
		var nearCorners = new Vector3[4];
		var farCorners = new Vector3[4];

		if (camera.orthographic)
		{
			float h = camera.orthographicSize, w = h * aspect;
			nearCorners[0] = new Vector3(-w, h, near);
			nearCorners[1] = new Vector3(w, h, near);
			nearCorners[2] = new Vector3(w, -h, near);
			nearCorners[3] = new Vector3(-w, -h, near);
			farCorners[0] = new Vector3(-w, h, far);
			farCorners[1] = new Vector3(w, h, far);
			farCorners[2] = new Vector3(w, -h, far);
			farCorners[3] = new Vector3(-w, -h, far);
		}
		else
		{
			float fovRad = camera.fieldOfView * Mathf.Deg2Rad, halfFovTan = Mathf.Tan(fovRad * 0.5f);
			nearCorners[0] = new Vector3(-halfFovTan * aspect * near, halfFovTan * near, near);
			nearCorners[1] = new Vector3(halfFovTan * aspect * near, halfFovTan * near, near);
			nearCorners[2] = new Vector3(halfFovTan * aspect * near, -halfFovTan * near, near);
			nearCorners[3] = new Vector3(-halfFovTan * aspect * near, -halfFovTan * near, near);
			farCorners[0] = new Vector3(-halfFovTan * aspect * far, halfFovTan * far, far);
			farCorners[1] = new Vector3(halfFovTan * aspect * far, halfFovTan * far, far);
			farCorners[2] = new Vector3(halfFovTan * aspect * far, -halfFovTan * far, far);
			farCorners[3] = new Vector3(-halfFovTan * aspect * far, -halfFovTan * far, far);
		}

		var viewToWorld = camera.cameraToWorldMatrix;
		for (int i = 0; i < 4; i++)
		{
			nearCorners[i] = viewToWorld.MultiplyPoint(nearCorners[i]);
			farCorners[i] = viewToWorld.MultiplyPoint(farCorners[i]);
		}

		var points = new Vector3[6];
		var uvs = new Vector2[6];
		int pointCount = 0;
		for (int i = 0; i < 4 && pointCount < 6; i++)
		{
			if (AddIntersection(plane, nearCorners[i], farCorners[i], out var point))
			{
				points[pointCount] = point;
				uvs[pointCount] = GetUVFromPosition(point, planeNormal);
				pointCount++;
			}
			if (AddIntersection(plane, nearCorners[i], nearCorners[(i + 1) % 4], out point))
			{
				points[pointCount] = point;
				uvs[pointCount] = GetUVFromPosition(point, planeNormal);
				pointCount++;
			}
			if (AddIntersection(plane, farCorners[i], farCorners[(i + 1) % 4], out point))
			{
				points[pointCount] = point;
				uvs[pointCount] = GetUVFromPosition(point, planeNormal);
				pointCount++;
			}
		}

		if (pointCount < 3)
		{
			targetMesh.Clear();
			Debug.LogWarning($"FrustumPlaneIntersection: Too few intersection points ({pointCount}) to create mesh");
			return false;
		}

		targetMesh.Clear();
		var vertices = new Vector3[pointCount];
		var finalUVs = new Vector2[pointCount];
		System.Array.Copy(points, vertices, pointCount);
		System.Array.Copy(uvs, finalUVs, pointCount);
		targetMesh.vertices = vertices;
		targetMesh.uv = finalUVs;
		var tris = new int[(pointCount - 2) * 3];
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
			var dir = (end - start).normalized;
			if (plane.Raycast(new Ray(start, dir), out var t) && t >= 0 && t <= Vector3.Distance(start, end))
			{
				point = start + dir * t;
				return true;
			}
			return false;
		}

		Vector2 GetUVFromPosition(Vector3 position, Vector3 planeNormal)
		{
			// Project world position onto a 2D plane based on planeNormal
			Vector3 absNormal = new Vector3(Mathf.Abs(planeNormal.x), Mathf.Abs(planeNormal.y), Mathf.Abs(planeNormal.z));
			if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
			{
				// Y-dominant (e.g., ground plane): use XZ for UVs
				return new Vector2(position.x, position.z) * 0.1f; // Scale for noise texture
			}
			else if (absNormal.x > absNormal.z)
			{
				// X-dominant: use YZ for UVs
				return new Vector2(position.y, position.z) * 0.1f;
			}
			else
			{
				// Z-dominant: use XY for UVs
				return new Vector2(position.x, position.y) * 0.1f;
			}
		}
	}
}