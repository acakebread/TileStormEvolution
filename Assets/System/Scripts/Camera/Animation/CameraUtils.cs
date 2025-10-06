using UnityEngine;

namespace MassiveHadronLtd
{
	public static class CameraUtils
	{
		private static float seed = Random.value * 100f; // Seed for Perlin noise

		// Camera shake constants
		private const float ShakePositionAmplitude = 0.02f; // Max position offset (±0.02 units)
		private const float ShakeRotationAmplitude = 2.4f; // Max rotation offset (±2.4 degrees)
		private const float ShakeFrequency = 0.85f; // Frequency of shake (Hz)
		private const float InstabilityDuration = 1.4f; // Duration of instability phase (0.7s up, 0.7s down)
		private const float MinInstabilityInterval = 3f; // Min interval between instability phases
		private const float MaxInstabilityInterval = 5f; // Max interval

		// Instability state
		private static float modulationTimer;
		private static float nextInstabilityTime;
		private static float instabilityStart;
		private static float instabilityEnd;
		private static float modulationSeed;
		public static bool unstable = false; // Flag for testing
		private static bool enableInstability = true; // Toggle for instability effect

		private static float effectTime = 0;

		public static void ApplyCameraShake(Camera camera, float amplitude = 1f)
		{
			if (0f == amplitude) return;
			if (camera == null)
			{
				// Reset instability state
				modulationTimer = 0f;
				nextInstabilityTime = 0f;
				unstable = false;
				return;
			}

			// Initialize modulation seed
			if (modulationSeed == 0f)
			{
				if (0f == seed) seed = Random.value * 100f;
				modulationSeed = seed + 100f; // Offset from shake seed
				nextInstabilityTime = Random.Range(MinInstabilityInterval, MaxInstabilityInterval);
			}

			float timeMultiplier = 1f; // Baseline multiplier
			float scaleMultiplier = 1f; // Baseline multiplier

			#region Instability
			if (enableInstability)
			{
				// Update modulation timer
				modulationTimer += Time.deltaTime;

				// Reset timer to prevent accumulation
				float cycleDuration = InstabilityDuration + MaxInstabilityInterval;
				if (modulationTimer >= cycleDuration)
				{
					modulationTimer %= cycleDuration;
				}

				// Check for new instability phase
				if (modulationTimer >= nextInstabilityTime)
				{
					// Start new instability phase
					float noiseTime = (modulationTimer % 10f) * 0.02f + modulationSeed;
					instabilityStart = modulationTimer;
					instabilityEnd = modulationTimer + InstabilityDuration;
					nextInstabilityTime = modulationTimer + InstabilityDuration + Random.Range(MinInstabilityInterval, MaxInstabilityInterval);
				}

				// Apply instability multiplier
				unstable = false;
				if (modulationTimer >= instabilityStart && modulationTimer < instabilityEnd)
				{
					unstable = true;
					// Calculate progress (0 to 1.4s)
					float t = (modulationTimer - instabilityStart) / InstabilityDuration; // 0 to 1
					float ease = (1f - Mathf.Cos(t * Mathf.PI * 2f)) / 2f; // EasePingPong: 0 to 1 to 0
					const float timeScalar = 2f; // Unstable time scalar max
					timeMultiplier = 1f + ease * (timeScalar - 1f); // 1x to 2x
					const float scaleScalar = 1.3f; // Unstable scale scalar max
					scaleMultiplier = 1f + ease * (scaleScalar - 1f); // 1x to 1.3x
				}
			}
			#endregion

			// Apply shake with time multiplier
			effectTime += Time.deltaTime * timeMultiplier;
			float time = effectTime * ShakeFrequency;

			// Position shake in camera space (circular distribution)
			// Generate 2D Perlin noise for circular motion
			float noiseTimeX = time * 1.1f + seed;
			float noiseTimeY = time * 1.3f + seed + 10f; // Offset to decorrelate
			float noiseX = (Mathf.PerlinNoise(noiseTimeX, 0f) - 0.5f) * 2f; // -1 to 1
			float noiseY = (Mathf.PerlinNoise(noiseTimeY, 0f) - 0.5f) * 2f; // -1 to 1

			// Map to circular domain (normalize to unit circle)
			Vector2 noiseVec = new Vector2(noiseX, noiseY);
			float magnitude = noiseVec.magnitude;
			if (magnitude > 1f) noiseVec /= magnitude; // Clamp to unit circle
			else noiseVec *= Mathf.Sqrt(magnitude); // Smooth falloff near center
			noiseVec *= ShakePositionAmplitude * scaleMultiplier; // Apply amplitude and instability scale

			// Transform to 3D camera space (apply in camera's local XZ plane)
			Vector3 shakePosOffset = camera.transform.right * noiseVec.x +
									 camera.transform.up * noiseVec.y;
			camera.transform.position += shakePosOffset;

			// Rotation shake (pitch and yaw)
			float pitchShake = (Mathf.PerlinNoise(time * 1.07f + seed, 0f) - 0.5f) * 2f * ShakeRotationAmplitude * scaleMultiplier;
			float yawShake = (Mathf.PerlinNoise(time * 1.13f + seed, 0f) - 0.5f) * 2f * ShakeRotationAmplitude * scaleMultiplier;
			Quaternion shakeRot = Quaternion.Euler(pitchShake * amplitude, yawShake * amplitude, 0f); // No roll
			camera.transform.rotation = camera.transform.rotation * shakeRot;
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
}