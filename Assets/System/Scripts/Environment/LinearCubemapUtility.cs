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
		/// Converts UV from the Linear texture to world direction.
		/// Sky is at the top of the texture.
		/// This version matches your working code but is cleaner.
		/// </summary>
		public static Vector3 UVToDirection(Vector2 uv)
		{
			// vx: -1 = left, +1 = right
			float vx = uv.x * 2f - 1f;

			// vy: top of texture (uv.y = 1) → vy = +1 (sky)
			// bottom of texture (uv.y = 0) → vy = -1 (ground)
			float vy = uv.y * 2f - 1f;

			float theta = vy * Mathf.PI * 0.5f;
			float cosTheta = Mathf.Cos(theta);
			float scalar = (cosTheta > 0.0001f) ? 1f / Mathf.Sqrt(cosTheta) : 1000f;

			// Positive pitch for sky at top
			float pitchRadians = vy * Mathf.PI * 0.5f;

			// Yaw
			float yawRadians = vx * scalar * Mathf.PI;

			return Quaternion.Euler(pitchRadians * Mathf.Rad2Deg, yawRadians * Mathf.Rad2Deg, 0f)
				   * Vector3.forward;
		}

		/// <summary>
		/// Converts a normalized world direction back to UV [0,1] in the Linear projection.
		/// Matches the custom mapping used in Create() and UVToDirection().
		/// </summary>
		public static Vector2 DirectionToUV(Vector3 dir)
		{
			dir = dir.normalized;

			// Recover vy from the Y component (vy = +1 at sky/top, vy = -1 at ground/bottom)
			float pitchRadians = Mathf.Asin(dir.y);
			float vy = pitchRadians / (Mathf.PI * -0.5f);

			float theta = vy * Mathf.PI * 0.5f;
			float cosTheta = Mathf.Cos(theta);
			float scalar = (cosTheta > 0.0001f) ? 1f / Mathf.Sqrt(cosTheta) : 1000f;

			// Yaw recovery
			float yawRadians = Mathf.Atan2(dir.x, dir.z);

			// Recover vx using the inverse of the scalar compensation
			float vx = yawRadians / (scalar * Mathf.PI);

			// Convert to UV [0,1]
			// vy = +1 (sky) → v = 0 (top of texture)
			// vy = -1 (ground) → v = 1 (bottom of texture)
			float u = vx * 0.5f + 0.5f;
			float v = vy * 0.5f + 0.5f;

			return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
		}

		/// <summary>
		/// Finds the direction of the brightest area (e.g. sun) in the cubemap using
		/// the equirectangular projection as an intermediate step.
		/// </summary>
		public static Vector3 FindLightDirection(Cubemap cubemap)
		{
			const int w = 256;
			const int h = 128;

			var linearrect = Create(cubemap, w, h);
			if (linearrect == null)
				return Vector3.up;

			var uv = ImageProcessing.FindSunUV(linearrect, scanAboveHorizonOnly: true);
			return -UVToDirection(uv);

			////test
			//var dir = UVToDirection(uv);
			//var uv2 = DirectionToUV(dir);
			//return -UVToDirection(uv2);
		}
	}
}

