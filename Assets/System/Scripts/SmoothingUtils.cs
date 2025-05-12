using UnityEngine;

public static class SmoothingUtils
{
	/// <summary>
	/// Smooths a value toward a target using an exponential moving average, remapping
	/// a frame-based EMA (value = (value * N + target) / (N + 1)) to a time-based EMA.
	/// Matches the original 30 FPS behavior at any frame rate.
	/// </summary>
	/// <param name="current">Current value (e.g., m_fWobble, m_fRate).</param>
	/// <param name="target">Target value (e.g., 0.02f, fRate * timeScale).</param>
	/// <param name="n">Smoothing factor N from the original EMA (e.g., 99, 63).</param>
	/// <param name="deltaTime">Time since last frame (e.g., Time.deltaTime).</param>
	/// <returns>Smoothed value.</returns>
	public static float Smooth(float current, float target, float n, float deltaTime)
	{
		if (n <= 0f)
		{
			Debug.LogWarning($"SmoothingUtils: Invalid N={n}, must be greater than 0. Returning current value.");
			return current;
		}

		// Compute smoothing rate k for 30 FPS
		float alpha = 1f / (n + 1f);
		float k = -Mathf.Log(1f - alpha) * 30f;

		// Apply time-based EMA
		float timeAlpha = Mathf.Clamp01(k * deltaTime);
		return current * (1f - timeAlpha) + target * timeAlpha;
	}
}