using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Utility for converting a Cubemap to a 2D Texture2D using a custom "Linear" projection.
	/// 
	/// This projection aims for approximately equal solid-angle sampling across the sphere,
	/// which is particularly useful for analyzing bright areas (e.g. sun detection, light sources)
	/// without the severe polar oversampling that plagues standard equirectangular maps.
	/// 
	/// Key characteristics:
	/// - Vertical coordinate (vy) is linear in [-1, 1], roughly corresponding to a linear change
	///   in the projected height (proportional to sin(latitude) behavior).
	/// - Horizontal angle (yaw) is dynamically compensated by a factor of ~1/sqrt(cos(theta))
	///   to counteract the natural shrinking of latitude circles near the poles.
	/// 
	/// Result: Each pixel covers a much more consistent portion of the sphere's solid angle
	/// compared to equirectangular. Bright features therefore have area proportions that
	/// better match their true angular size in 3D space. The map appears somewhat "sheared"
	/// away from the center (especially near poles), but pixel counts for features remain
	/// coherent with real-world solid angles.
	/// 
	/// This is NOT a strict mathematically equal-area projection (like Lambert cylindrical
	/// equal-area or Lambert azimuthal equal-area), but a practical approximation tailored
	/// for analysis tasks.
	/// </summary>
	public static class LinearCubemapUtility
	{
		/// <summary>
		/// Creates a 2D texture from a cubemap using the linear/compensated projection.
		/// The output is square and contains a circular mapping inside (pixels outside
		/// the unit disk are set to black).
		/// </summary>
		public static Texture2D Create(Cubemap cubemap, int width = 256, int height = 256)
		{
			if (cubemap == null || !cubemap.isReadable)
			{
				Debug.LogError("Cubemap is null or not readable.");
				return null;
			}

			var result = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				name = cubemap.name + "_Linear"
			};

			var pixels = new Color[width * height];

			for (var y = 0; y < height; y++)
			{
				var vy = (float)y / (height - 1) * 2f - 1f;           // Linear vertical [-1..1]

				var theta = vy * Mathf.PI * 0.5f;
				var cosTheta = Mathf.Cos(theta);
				var scalar = (cosTheta > 0.0001f) ? 1f / Mathf.Sqrt(cosTheta) : 1000f;

				var pitchRadians = vy * Mathf.PI * -0.5f;

				for (var x = 0; x < width; x++)
				{
					var vx = (float)x / (width - 1) * 2f - 1f;

					if (vx * vx + vy * vy > 1f)
					{
						pixels[y * width + x] = Color.black;
						continue;
					}

					var yawRadians = vx * scalar * Mathf.PI;

					var dir = Quaternion.Euler(pitchRadians * Mathf.Rad2Deg, yawRadians * Mathf.Rad2Deg, 0f)
							  * Vector3.forward;

					pixels[y * width + x] = CubemapUtility.SampleCubemap(cubemap, dir);
				}
			}

			result.SetPixels(pixels);
			result.Apply();
			return result;
		}

		// ===================================================================
		// UV <-> Direction helpers (to be added here for FindSun-style functions)
		// ===================================================================

		/// <summary>
		/// Converts UV coordinates in the Linear projection back to a world direction.
		/// Matches the mapping used in Create().
		/// </summary>
		public static Vector3 UVToDirection(Vector2 uv, int width, int height)
		{
			var vx = uv.x / (width - 1f) * 2f - 1f;
			var vy = uv.y / (height - 1f) * 2f - 1f;

			if (vx * vx + vy * vy > 1f)
				return Vector3.up;

			var theta = vy * Mathf.PI * 0.5f;
			var cosTheta = Mathf.Cos(theta);
			var scalar = (cosTheta > 0.0001f) ? 1f / Mathf.Sqrt(cosTheta) : 1000f;

			var pitchRadians = vy * Mathf.PI * -0.5f;
			var yawRadians = vx * scalar * Mathf.PI;

			return Quaternion.Euler(pitchRadians * Mathf.Rad2Deg, yawRadians * Mathf.Rad2Deg, 0f)
				   * Vector3.forward;
		}

		/// <summary>
		/// Converts a normalized direction back to UV coordinates in the Linear projection.
		/// (Inverse mapping — more involved; requires solving for vx/vy given the compensated angles)
		/// </summary>
		public static Vector2 DirectionToUV(Vector3 dir, int width, int height)
		{
			dir = dir.normalized;

			// Extract pitch from the vertical component (matching the forward mapping)
			var pitchRadians = Mathf.Asin(-dir.y); // Adjust sign based on your quaternion convention

			var vy = pitchRadians / (Mathf.PI * -0.5f);

			// Reconstruct yaw and then vx using the inverse scalar
			// This requires computing the effective yaw from the direction and dividing by scalar
			// (Implementation note: you may need to tune this for perfect round-tripping)

			var theta = vy * Mathf.PI * 0.5f;
			var cosTheta = Mathf.Cos(theta);
			var scalar = (cosTheta > 0.0001f) ? 1f / Mathf.Sqrt(cosTheta) : 1000f;

			var yawRadians = Mathf.Atan2(dir.x, dir.z);

			var vx = yawRadians / (scalar * Mathf.PI);

			// Clamp to valid range if needed
			var u = (vx * 0.5f + 0.5f) * (width - 1f);
			var v = (vy * 0.5f + 0.5f) * (height - 1f);

			return new Vector2(u, v);
		}
	}
}