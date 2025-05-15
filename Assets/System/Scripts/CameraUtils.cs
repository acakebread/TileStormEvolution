using UnityEngine;

public static class CameraUtils
{
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

	//public static void LookAtTarget(Transform cameraTransform, Vector3 target)
	//{
	//    Vector3 direction = target - cameraTransform.position;
	//    if (direction.sqrMagnitude < 0.01f)
	//    {
	//        Debug.LogWarning("LookAtTarget: Target too close to camera, skipping orientation.");
	//        return;
	//    }
	//
	//    if (target.y > cameraTransform.position.y - 0.5f)
	//    {
	//        target.y = Mathf.Min(target.y, cameraTransform.position.y - 1f);
	//        direction = target - cameraTransform.position;
	//        Debug.Log($"LookAtTarget: Adjusted target to ensure downward tilt: new target={target}");
	//    }
	//
	//    cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
	//}

	// Camera shake constants
	private const float ShakePositionAmplitude = 0.02f; // Max position offset (±0.02 units in camera space)
	private const float ShakeRotationAmplitude = 2.4f; // Max rotation offset (±2.4 degrees)
	private const float ShakeFrequency = 0.85f; // Frequency of shake (Hz)

	public static void ApplyCameraShake(Camera camera, bool isEnabled, float seed)
	{
		if (!isEnabled || camera == null)
			return;

		float time = Time.time * ShakeFrequency;
		// Position shake in camera space (X, Y for screen wobble, small Z for depth)
		Vector3 shakePosOffset = new Vector3(
			(Mathf.PerlinNoise(time + seed + 0f, 0f) - 0.5f) * 2f * ShakePositionAmplitude,
			(Mathf.PerlinNoise(time + seed + 1f, 0f) - 0.5f) * 2f * ShakePositionAmplitude,
			(Mathf.PerlinNoise(time + seed + 2f, 0f) - 0.5f) * 2f * ShakePositionAmplitude * 0.5f // Reduced Z shake
		);
		// Transform to world space using camera's local axes
		shakePosOffset = camera.transform.right * shakePosOffset.x +
						 camera.transform.up * shakePosOffset.y +
						 camera.transform.forward * shakePosOffset.z;
		camera.transform.position += shakePosOffset;

		// Rotation shake (pitch and yaw)
		float pitchShake = (Mathf.PerlinNoise(time + seed + 10f, 0f) - 0.5f) * 2f * ShakeRotationAmplitude;
		float yawShake = (Mathf.PerlinNoise(time + seed + 11f, 0f) - 0.5f) * 2f * ShakeRotationAmplitude;
		Quaternion shakeRot = Quaternion.Euler(pitchShake, yawShake, 0f); // No roll for natural feel
		camera.transform.rotation = camera.transform.rotation * shakeRot;
	}
}