using UnityEngine;

namespace MassiveHadronLtd
{
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

		public static Matrix4x4 GetReflectedMatrixNonInverted(this Matrix4x4 matrix, Vector3 planeNormal, float planeOffset)
		{
			var n = planeNormal.normalized;
			if (n == Vector3.zero)
			{
				UnityEngine.Debug.LogWarning("Matrix4x4Extensions: Invalid plane normal, returning original matrix");
				return matrix;
			}

			// Reflect position
			Matrix4x4 reflectionMatrix = MatrixUtils.GetReflectionMatrix(planeNormal, planeOffset);
			Vector3 position = matrix.GetColumn(3);
			Vector3 reflectedPosition = reflectionMatrix.MultiplyPoint(position);

			// Compute reflected coordinate system
			Matrix4x4 reflectedCameraMatrix = reflectionMatrix * matrix;
			Vector3 xAxis = reflectedCameraMatrix.GetColumn(0);
			Vector3 zAxis = reflectedCameraMatrix.GetColumn(2);
			Vector3 upVector = Vector3.Cross(xAxis, zAxis).normalized;
			if (upVector.sqrMagnitude < 0.0001f)
			{
				UnityEngine.Debug.LogWarning("Matrix4x4Extensions: Invalid up vector, returning original matrix");
				return matrix;
			}

			Vector3 rightVector = Vector3.Cross(-upVector, zAxis).normalized;
			if (rightVector.sqrMagnitude < 0.0001f)
			{
				UnityEngine.Debug.LogWarning("Matrix4x4Extensions: Invalid right vector, returning original matrix");
				return matrix;
			}

			// Build result matrix
			Matrix4x4 result = Matrix4x4.identity;
			result.SetColumn(0, rightVector);
			result.SetColumn(1, upVector);
			result.SetColumn(2, zAxis);
			result.SetColumn(3, new Vector4(reflectedPosition.x, reflectedPosition.y, reflectedPosition.z, 1));
			return result;
		}

		public static bool IsFinite(this Matrix4x4 matrix)
		{
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					float val = matrix[i, j];
					if (float.IsNaN(val) || float.IsInfinity(val))
					{
						return false;
					}
				}
			}
			return true;
		}
	}
}