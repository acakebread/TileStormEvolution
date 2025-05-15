using UnityEngine;

public static class SmoothingUtils
{
	/// <summary>
	/// Smooths a value toward a target using an exponential moving average (EMA), converting
	/// a frame-based EMA (value = (value * N + target) / (N + 1)) to a time-based EMA.
	/// Matches the original game's smoothing behavior at the specified frame rate (default 60 FPS).
	/// </summary>
	/// <param name="current">Current value (e.g., m_fWobble, m_fRate).</param>
	/// <param name="target">Target value (e.g., 0.125f for 1/8 smoothing).</param>
	/// <param name="n">Smoothing factor N from the original EMA (e.g., 63 for 1/64).</param>
	/// <param name="deltaTime">Time since last frame (e.g., Time.deltaTime).</param>
	/// <param name="fps">Target frame rate for smoothing (default 60 FPS, matching original camera system).</param>
	/// <returns>Smoothed value.</returns>
	public static float Smooth(float current, float target, float n, float deltaTime, float fps = 60f)
	{
		if (n <= 0f)
		{
			Debug.LogWarning($"SmoothingUtils: Invalid N={n}, must be greater than 0. Returning current value.");
			return current;
		}

		// Compute smoothing rate k for default 60 FPS
		float alpha = 1f / (n + 1f);
		float k = -Mathf.Log(1f - alpha) * fps;

		// Apply time-based EMA
		float timeAlpha = Mathf.Clamp01(k * deltaTime);
		return current * (1f - timeAlpha) + target * timeAlpha;
	}

	public static float Ease(float t) => (1f - Mathf.Cos(t * Mathf.PI)) / 2f;
	public static float EasePingPong(float t) => (1f - Mathf.Cos(t * Mathf.PI * 2f)) / 2f;
}