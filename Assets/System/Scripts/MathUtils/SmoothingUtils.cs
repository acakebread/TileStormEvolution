using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// A utility class providing smoothing and easing functions for interpolating values in Unity.
	/// Includes time-based exponential moving average (EMA) smoothing and cosine-based easing methods.
	/// </summary>
	public static class SmoothingUtils
	{
		/// <summary>
		/// Smooths a value toward a target using an exponential moving average (EMA).
		/// Converts a frame-based EMA (value = (value * N + target) / (N + 1)) to a time-based EMA
		/// to ensure consistent smoothing regardless of frame rate. Matches the original game's
		/// camera smoothing behavior at the specified frame rate (default 60 FPS).
		/// </summary>
		/// <param name="current">The current value to smooth (e.g., m_fWobble, m_fRate).</param>
		/// <param name="target">The target value to approach (e.g., 0.125f for 1/8 smoothing).</param>
		/// <param name="n">The smoothing factor N from the original EMA formula (e.g., 63 for 1/64 smoothing).</param>
		/// <param name="deltaTime">Time since the last frame, typically Time.deltaTime.</param>
		/// <param name="fps">The target frame rate for smoothing calibration (default 60 FPS, matching original camera system).</param>
		/// <returns>The smoothed value, interpolated between current and target.</returns>
		public static float Smooth(float current, float target, float n, float deltaTime, float fps = 60f)
		{
			if (n <= 0f)
			{
				Debug.LogWarning($"SmoothingUtils.Smooth: Invalid smoothing factor N={n}. Must be greater than 0. Returning current value.");
				return current;
			}

			// Calculate the smoothing rate (k) based on the frame-based EMA formula
			float alpha = 1f / (n + 1f);
			float k = -Mathf.Log(1f - alpha) * fps;

			// Apply time-based EMA using deltaTime for frame-rate independence
			float timeAlpha = Mathf.Clamp01(k * deltaTime);
			return current * (1f - timeAlpha) + target * timeAlpha;
		}

		public static Vector3 SmoothVector(Vector3 src, Vector3 dst, float n, float deltaTime, float fps = 60f) => new(Smooth(src.x, dst.x, n, deltaTime, fps), Smooth(src.y, dst.y, n, deltaTime, fps), Smooth(src.z, dst.z, n, deltaTime, fps));

		/// <summary>
		/// Applies a cosine-based easing function to a value, producing a smooth curve from 0 to 1.
		/// Useful for interpolating animations or transitions with a natural, eased feel.
		/// Maps input t (typically 0 to 1) to a value between 0 and 1 using a cosine curve.
		/// </summary>
		/// <param name="t">The input value, typically between 0 and 1, representing progress.</param>
		/// <returns>The eased value between 0 and 1, following a cosine curve.</returns>
		public static float Ease(float t) => (1f - Mathf.Cos(t * Mathf.PI)) / 2f;

		/// <summary>
		/// Applies a cosine-based ping-pong easing function, producing a smooth oscillating curve.
		/// Maps input t (typically 0 to 1) to a value between 0 and 1 that rises and falls,
		/// ideal for looping or back-and-forth animations like FOV pulsing or wobble effects.
		/// </summary>
		/// <param name="t">The input value, typically between 0 and 1, representing progress.</param>
		/// <returns>The eased value between 0 and 1, following a ping-pong cosine curve.</returns>
		public static float EasePingPong(float t) => (1f - Mathf.Cos(t * Mathf.PI * 2f)) / 2f;
	}
}