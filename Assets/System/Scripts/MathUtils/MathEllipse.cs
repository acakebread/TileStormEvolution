using UnityEngine;

public static class MathEllipse
{
	public static Vector2 WorldToEllipse(Vector2 worldXZ, Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular)
	{
		Vector2 relativeXZ = worldXZ - midPointXZ;
		Vector2 pathDirXZ = new Vector2(pathDir.x, pathDir.z).normalized;
		Vector2 perpXZ = new Vector2(perpendicular.x, perpendicular.z).normalized;
		return new Vector2(
			Vector2.Dot(relativeXZ, pathDirXZ), // x0 along pathDir (major)
			Vector2.Dot(relativeXZ, perpXZ) // y0 along perpendicular (minor)
		);
	}

	public static Vector2 EllipseToWorld(Vector2 ellipseCoords, Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular)
	{
		Vector2 pathDirXZ = new Vector2(pathDir.x, pathDir.z).normalized;
		Vector2 perpXZ = new Vector2(perpendicular.x, perpendicular.z).normalized;
		// Map x (major) to pathDirXZ, y (minor) to perpXZ
		return midPointXZ + ellipseCoords.x * pathDirXZ + ellipseCoords.y * perpXZ;
	}

	public static Vector2 GetEllipsePoint(float t, float a, float b, Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular)
	{
		float x = a * Mathf.Cos(t); // Major axis
		float y = b * Mathf.Sin(t); // Minor axis
		return EllipseToWorld(new Vector2(x, y), midPointXZ, pathDir, perpendicular);
	}

	public static (Vector3 tangent1, Vector3 tangent2) ComputeTangentAtPoint(float t, float a, float b, Vector3 pathDir, Vector3 perpendicular)
	{
		Vector3 tangent = (-a * Mathf.Sin(t) * pathDir + b * Mathf.Cos(t) * perpendicular).normalized;
		return (tangent, -tangent);
	}

	public static (Vector3? tangentPoint1, Vector3? tangentPoint2) ComputeTangentPoints(Vector3 point, Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float lozengeMajor, float lozengeMinor)
	{
		Vector2 pointXZ = new Vector2(point.x, point.z);
		Vector2 ellipseCoords = WorldToEllipse(pointXZ, midPointXZ, pathDir, perpendicular);
		float x0 = ellipseCoords.x;
		float y0 = ellipseCoords.y;
		float a = lozengeMajor;
		float b = lozengeMinor;

		float A = x0 / a;
		float B = y0 / b;
		float quadA = A + 1;
		float quadB = -2 * B;
		float quadC = 1 - A;
		float discriminant = quadB * quadB - 4 * quadA * quadC;

		if (discriminant < 0 || Mathf.Abs(quadA) < 0.0001f)
		{
			Debug.LogWarning($"No real tangent points for point: {pointXZ}, discriminant: {discriminant}, quadA: {quadA}, x0: {x0}, y0: {y0}");
			return (null, null);
		}

		float sqrtDisc = Mathf.Sqrt(discriminant);
		float u1 = (-quadB + sqrtDisc) / (2 * quadA);
		float u2 = (-quadB - sqrtDisc) / (2 * quadA);

		Vector3? tangentPoint1 = ComputeTangentPoint(u1, a, b, midPointXZ, pathDir, perpendicular, point.y);
		Vector3? tangentPoint2 = ComputeTangentPoint(u2, a, b, midPointXZ, pathDir, perpendicular, point.y);

		if (tangentPoint1.HasValue)
		{
			Vector2 tp1XZ = new Vector2(tangentPoint1.Value.x, tangentPoint1.Value.z);
			Vector2 tp1Ellipse = WorldToEllipse(tp1XZ, midPointXZ, pathDir, perpendicular);
			float ellipseValue = (tp1Ellipse.x / a) * (tp1Ellipse.x / a) + (tp1Ellipse.y / b) * (tp1Ellipse.y / b);
			if (Mathf.Abs(ellipseValue - 1) > 0.01f)
			{
				Debug.LogWarning($"TangentPoint1 {tangentPoint1.Value} not on ellipse, value: {ellipseValue}");
			}
			else
			{
				float theta = Mathf.Atan2(tp1Ellipse.y / b, tp1Ellipse.x / a);
				var (ellipseTangent1, _) = ComputeTangentAtPoint(theta, a, b, pathDir, perpendicular);
				Vector2 lineDir = (tp1XZ - pointXZ).normalized;
				Vector2 ellipseTangentXZ = new Vector2(ellipseTangent1.x, ellipseTangent1.z).normalized;
				if (Vector2.Dot(lineDir, ellipseTangentXZ) < 0) ellipseTangentXZ = -ellipseTangentXZ;
				float dot = Vector2.Dot(lineDir, ellipseTangentXZ);
				if (Mathf.Abs(dot) < 0.99f)
				{
					Debug.LogWarning($"Line from {pointXZ} to {tp1XZ} not tangent, dot: {dot}");
				}
			}
		}
		if (tangentPoint2.HasValue)
		{
			Vector2 tp2XZ = new Vector2(tangentPoint2.Value.x, tangentPoint2.Value.z);
			Vector2 tp2Ellipse = WorldToEllipse(tp2XZ, midPointXZ, pathDir, perpendicular);
			float ellipseValue = (tp2Ellipse.x / a) * (tp2Ellipse.x / a) + (tp2Ellipse.y / b) * (tp2Ellipse.y / b);
			if (Mathf.Abs(ellipseValue - 1) > 0.01f)
			{
				Debug.LogWarning($"TangentPoint2 {tangentPoint2.Value} not on ellipse, value: {ellipseValue}");
			}
			else
			{
				float theta = Mathf.Atan2(tp2Ellipse.y / b, tp2Ellipse.x / a);
				var (ellipseTangent1, _) = ComputeTangentAtPoint(theta, a, b, pathDir, perpendicular);
				Vector2 lineDir = (tp2XZ - pointXZ).normalized;
				Vector2 ellipseTangentXZ = new Vector2(ellipseTangent1.x, ellipseTangent1.z).normalized;
				if (Vector2.Dot(lineDir, ellipseTangentXZ) < 0) ellipseTangentXZ = -ellipseTangentXZ;
				float dot = Vector2.Dot(lineDir, ellipseTangentXZ);
				if (Mathf.Abs(dot) < 0.99f)
				{
					Debug.LogWarning($"Line from {pointXZ} to {tp2XZ} not tangent, dot: {dot}");
				}
			}
		}

		return (tangentPoint1, tangentPoint2);
	}

	private static Vector3? ComputeTangentPoint(float u, float a, float b, Vector2 midPointXZ, Vector3 pathDir, Vector3 perpendicular, float y)
	{
		float uSquared = u * u;
		if (Mathf.Abs(1 + uSquared) < 0.0001f) return null;
		float cosT = (1 - uSquared) / (1 + uSquared);
		float sinT = (2 * u) / (1 + uSquared);

		Vector2 ellipseCoords = new Vector2(a * cosT, b * sinT);
		Vector2 worldXZ = EllipseToWorld(ellipseCoords, midPointXZ, pathDir, perpendicular);
		return new Vector3(worldXZ.x, y, worldXZ.y);
	}
}