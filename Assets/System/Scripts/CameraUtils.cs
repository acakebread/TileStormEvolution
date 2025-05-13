using UnityEngine;
using static ClassicTilestorm.DatabaseLoader;

public static class CameraUtils
{
	private const float MaxVectorValue = 100f;

	public static bool IsValidVector(VectorData vector)
	{
		if (vector == null)
			return false;

		bool valid = !float.IsNaN(vector.fX) && !float.IsInfinity(vector.fX) && Mathf.Abs(vector.fX) < MaxVectorValue &&
					 !float.IsNaN(vector.fY) && !float.IsInfinity(vector.fY) && Mathf.Abs(vector.fY) < MaxVectorValue &&
					 !float.IsNaN(vector.fZ) && !float.IsInfinity(vector.fZ) && Mathf.Abs(vector.fZ) < MaxVectorValue;

		if (!valid)
			Debug.LogWarning($"Invalid vector: fX={vector?.fX}, fY={vector?.fY}, fZ={vector?.fZ}");

		return valid;
	}

	public static void LookAtTarget(Transform cameraTransform, Vector3 target)
	{
		Vector3 direction = target - cameraTransform.position;
		if (direction.sqrMagnitude < 0.01f)
		{
			Debug.LogWarning("LookAtTarget: Target too close to camera, skipping orientation.");
			return;
		}

		if (target.y > cameraTransform.position.y - 0.5f)
		{
			target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
			direction = target - cameraTransform.position;
			Debug.Log($"LookAtTarget: Adjusted target to ensure downward tilt: new target={target}");
		}

		cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
	}

	public static Vector3 ScreenToPlaneXZ(Camera camera, Vector3 fallbackPosition, bool debugLogging = false)
	{
		Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
		Plane xzPlane = new Plane(Vector3.up, Vector3.zero); // y=0 plane
		if (xzPlane.Raycast(ray, out float distance))
		{
			Vector3 point = ray.GetPoint(distance);
			return new Vector3(point.x, 0, point.z);
		}

		Vector3 fallback = new Vector3(fallbackPosition.x, 0, fallbackPosition.z);
		if (debugLogging)
			Debug.LogWarning($"ScreenToPlaneXZ failed, using fallback: {fallback}");
		return fallback;
	}
}