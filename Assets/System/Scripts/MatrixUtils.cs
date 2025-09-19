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
		var normalizedNormal = planeNormal.normalized;
		if (normalizedNormal == Vector3.zero)
		{
			Debug.LogWarning("MatrixUtils: Invalid plane normal (zero vector), returning identity matrix");
			return Matrix4x4.identity;
		}

		var pointOnPlane = normalizedNormal * planeOffset;

		var reflectionMat = Matrix4x4.identity;
		reflectionMat[0, 0] = 1 - 2 * normalizedNormal.x * normalizedNormal.x;
		reflectionMat[0, 1] = -2 * normalizedNormal.x * normalizedNormal.y;
		reflectionMat[0, 2] = -2 * normalizedNormal.x * normalizedNormal.z;
		reflectionMat[1, 0] = -2 * normalizedNormal.y * normalizedNormal.x;
		reflectionMat[1, 1] = 1 - 2 * normalizedNormal.y * normalizedNormal.y;
		reflectionMat[1, 2] = -2 * normalizedNormal.y * normalizedNormal.z;
		reflectionMat[2, 0] = -2 * normalizedNormal.z * normalizedNormal.x;
		reflectionMat[2, 1] = -2 * normalizedNormal.z * normalizedNormal.y;
		reflectionMat[2, 2] = 1 - 2 * normalizedNormal.z * normalizedNormal.z;

		var translateToOrigin = Matrix4x4.Translate(-pointOnPlane);
		var translateBack = Matrix4x4.Translate(pointOnPlane);
		return translateBack * reflectionMat * translateToOrigin;
	}
}