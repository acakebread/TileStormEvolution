using UnityEngine;

public static class MatrixUtils
{
	/// <summary>
	/// Computes a reflection matrix for a plane defined by a normal and offset from the origin.
	/// </summary>
	/// <param name="planeNormal">The plane's normal vector (will be normalized).</param>
	/// <param name="planeOffset">The distance from the origin along the normal to the plane.</param>
	/// <returns>A Matrix4x4 representing the reflection transformation. Returns identity if the normal is zero.</returns>
	public static Matrix4x4 GetReflectionMatrix(Vector3 planeNormal, float planeOffset)
	{
		var n = planeNormal.normalized;
		if (n == Vector3.zero)
		{
			Debug.LogWarning("MatrixUtils: Invalid plane normal (zero vector), returning identity matrix");
			return Matrix4x4.identity;
		}

		var p = n * planeOffset;
		var m = Matrix4x4.identity;
		m[0, 0] = 1 - 2 * n.x * n.x;
		m[0, 1] = -2 * n.x * n.y;
		m[0, 2] = -2 * n.x * n.z;
		m[1, 0] = -2 * n.y * n.x;
		m[1, 1] = 1 - 2 * n.y * n.y;
		m[1, 2] = -2 * n.y * n.z;
		m[2, 0] = -2 * n.z * n.x;
		m[2, 1] = -2 * n.z * n.y;
		m[2, 2] = 1 - 2 * n.z * n.z;

		return Matrix4x4.Translate(p) * m * Matrix4x4.Translate(-p);
	}
}