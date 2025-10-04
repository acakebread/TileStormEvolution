using UnityEngine;
using System.Collections.Generic;

public static class FrustumPlaneIntersection
{
	public static bool GenerateFrustumPlaneIntersectionMesh(Camera camera, Vector3 planeNormal, float planeOffset, Mesh targetMesh)
	{
		if (!camera || planeNormal == Vector3.zero)
		{
			targetMesh.Clear();
			if (!camera) Debug.LogError("FrustumPlaneIntersection: Camera is null");
			else Debug.LogWarning("FrustumPlaneIntersection: Invalid plane normal (zero vector)");
			return false;
		}

		planeNormal = planeNormal.normalized;
		var plane = new Plane(planeNormal, planeNormal * planeOffset);
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

		// Collect intersections (possible duplicates) into a list, then dedupe.
		var rawPoints = new List<Vector3>(8);

		bool AddIntersectionRaw(Plane pl, Vector3 a, Vector3 b)
		{
			if (TryIntersectSegmentWithPlane(pl, a, b, out var p))
			{
				rawPoints.Add(p);
				return true;
			}
			return false;
		}

		for (int i = 0; i < 4; i++)
		{
			AddIntersectionRaw(plane, nearCorners[i], farCorners[i]);
			AddIntersectionRaw(plane, nearCorners[i], nearCorners[(i + 1) % 4]);
			AddIntersectionRaw(plane, farCorners[i], farCorners[(i + 1) % 4]);
		}

		// Deduplicate points (within epsilon)
		const float EPS = 1e-5f;
		var points = new List<Vector3>();
		foreach (var p in rawPoints)
		{
			bool exists = false;
			for (int i = 0; i < points.Count; i++)
			{
				if ((points[i] - p).sqrMagnitude <= EPS * EPS)
				{
					exists = true;
					break;
				}
			}
			if (!exists) points.Add(p);
		}

		int pointCount = points.Count;
		if (pointCount < 3)
		{
			targetMesh.Clear();
			//Debug.LogWarning($"FrustumPlaneIntersection: Too few intersection points ({pointCount}) to create mesh");
			return false;
		}

		// Compute centroid
		Vector3 centroid = Vector3.zero;
		for (int i = 0; i < pointCount; i++) centroid += points[i];
		centroid /= pointCount;

		// Build a basis on the plane to compute angles
		Vector3 tangent;
		// Choose a tangent that's not parallel to the normal
		if (Mathf.Abs(Vector3.Dot(planeNormal, Vector3.up)) < 0.999f)
			tangent = Vector3.Cross(planeNormal, Vector3.up).normalized;
		else
			tangent = Vector3.Cross(planeNormal, Vector3.right).normalized;
		Vector3 bitangent = Vector3.Cross(planeNormal, tangent);

		// Pair points with angle around centroid
		var anglePairs = new List<(float angle, Vector3 point)>(pointCount);
		for (int i = 0; i < pointCount; i++)
		{
			Vector3 d = points[i] - centroid;
			// project onto plane basis
			float x = Vector3.Dot(d, tangent);
			float y = Vector3.Dot(d, bitangent);
			float angle = Mathf.Atan2(y, x); // -PI..PI
			anglePairs.Add((angle, points[i]));
		}

		// Sort by angle (ascending)
		anglePairs.Sort((a, b) => a.angle.CompareTo(b.angle));

		// Create ordered vertex list and uvs
		var vertices = new Vector3[pointCount];
		var uvs = new Vector2[pointCount];
		for (int i = 0; i < pointCount; i++)
		{
			vertices[i] = anglePairs[i].point;
			uvs[i] = GetUVFromPosition(vertices[i], planeNormal);
		}

		// Ensure winding matches planeNormal (we want triangles to face the planeNormal direction)
		// Compute polygon normal from ordered vertices and compare with planeNormal.
		Vector3 polyNormal = ComputePolygonNormal(vertices);
		if (Vector3.Dot(polyNormal, planeNormal) < 0f)
		{
			// Reverse order to match planeNormal
			System.Array.Reverse(vertices);
			System.Array.Reverse(uvs);
		}

		// Build triangles as a fan from 0
		var tris = new int[(pointCount - 2) * 3];
		for (int i = 0, t = 0; i < pointCount - 2; i++, t += 3)
		{
			tris[t] = 0;
			tris[t + 1] = i + 1;
			tris[t + 2] = i + 2;
		}

		targetMesh.Clear();
		targetMesh.vertices = vertices;
		targetMesh.uv = uvs;
		targetMesh.triangles = tris;
		targetMesh.RecalculateBounds();
		targetMesh.RecalculateNormals();

		return true;
	}

	// Helper: robust segment-plane intersection (handles segment, not infinite ray)
	private static bool TryIntersectSegmentWithPlane(Plane plane, Vector3 a, Vector3 b, out Vector3 point)
	{
		point = Vector3.zero;
		Vector3 ab = b - a;
		float denom = Vector3.Dot(plane.normal, ab);
		// If denom == 0 -> segment parallel; no intersection unless the endpoints lie on plane (rare)
		if (Mathf.Abs(denom) < 1e-9f)
		{
			// Check if a is on plane
			if (Mathf.Abs(plane.GetDistanceToPoint(a)) < 1e-6f)
			{
				point = a;
				return true;
			}
			return false;
		}
		// t along segment where intersection occurs
		float t = -(Vector3.Dot(plane.normal, a) + plane.distance) / denom;
		if (t >= 0f - 1e-6f && t <= 1f + 1e-6f)
		{
			point = a + ab * t;
			return true;
		}
		return false;
	}

	// Estimate polygon normal via Newell's method (robust for non-self-intersecting polygons)
	private static Vector3 ComputePolygonNormal(Vector3[] verts)
	{
		Vector3 n = Vector3.zero;
		int count = verts.Length;
		for (int i = 0; i < count; i++)
		{
			Vector3 current = verts[i];
			Vector3 next = verts[(i + 1) % count];
			n.x += (current.y - next.y) * (current.z + next.z);
			n.y += (current.z - next.z) * (current.x + next.x);
			n.z += (current.x - next.x) * (current.y + next.y);
		}
		return n.normalized;
	}

	private static Vector2 GetUVFromPosition(Vector3 position, Vector3 planeNormal)
	{
		// Project world position onto a 2D plane based on planeNormal
		Vector3 absNormal = new Vector3(Mathf.Abs(planeNormal.x), Mathf.Abs(planeNormal.y), Mathf.Abs(planeNormal.z));
		if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
		{
			// Y-dominant (e.g., ground plane): use XZ for UVs
			return new Vector2(position.x, position.z) * 1f; // Scale for noise texture
		}
		else if (absNormal.x > absNormal.z)
		{
			// X-dominant: use YZ for UVs
			return new Vector2(position.y, position.z) * 1f;
		}
		else
		{
			// Z-dominant: use XY for UVs
			return new Vector2(position.x, position.y) * 1f;
		}
	}
}
