using UnityEngine;

namespace MassiveHadronLtd
{
	/// <summary>
	/// Utility for converting a Cubemap to a standard Equirectangular (latitude-longitude) projection.
	/// 
	/// This is the classic 2:1 aspect ratio panorama format (width = 2 × height).
	/// Note: Equirectangular mapping has strong oversampling near the poles, which can cause
	/// false positives when detecting bright features (e.g. sun, light sources). 
	/// For better area distribution, consider using LinearCubemapUtility instead.
	/// </summary>
	public static class EquirectangularCubemapUtility
	{
		/// <summary>
		/// Creates a 2D equirectangular texture from a cubemap.
		/// Default resolution is 512×256 (standard 2:1 aspect ratio).
		/// </summary>
		public static Texture2D Create(Cubemap cubemap, int width = 512, int height = 256)
		{
			if (cubemap == null || !cubemap.isReadable)
			{
				Debug.LogError("Cubemap is null or not readable.");
				return null;
			}

			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				name = cubemap.name + "_Equirect"
			};

			var pixels = new Color[width * height];

			for (var y = 0; y < height; y++)
			{
				var v = (float)y / (height - 1);

				var latitude = (v - 0.5f) * Mathf.PI;
				var sinLat = Mathf.Sin(latitude);
				var cosLat = Mathf.Cos(latitude);

				for (var x = 0; x < width; x++)
				{
					var u = (float)x / (width - 1);

					var longitude = (u - 0.5f) * Mathf.PI * 2f;

					var sinLon = Mathf.Sin(longitude);
					var cosLon = Mathf.Cos(longitude);

					var dir = new Vector3(cosLat * sinLon, sinLat, cosLat * cosLon);

					pixels[y * width + x] = CubemapUtility.SampleCubemap(cubemap, dir);
				}
			}

			tex.SetPixels(pixels);
			tex.Apply();
			return tex;
		}

		/// <summary>
		/// Converts UV coordinates (pixel space) in the equirectangular texture 
		/// back to a normalized world direction.
		/// </summary>
		public static Vector3 UVToDirection(Vector2 uv)
		{
			var u = uv.x;
			var v = uv.y;

			var longitude = (u - 0.5f) * Mathf.PI * 2f;
			var latitude = (v - 0.5f) * Mathf.PI * 1f;

			var cosLat = Mathf.Cos(latitude);

			return new Vector3(
				cosLat * Mathf.Sin(longitude),
				Mathf.Sin(latitude),
				cosLat * Mathf.Cos(longitude)
			).normalized;
		}

		/// <summary>
		/// Converts a normalized direction vector to UV pixel coordinates 
		/// in the equirectangular projection.
		/// </summary>
		public static Vector2 DirectionToUV(Vector3 dir)
		{
			dir = dir.normalized;

			// Longitude using Atan2 (matches the forward mapping: +Z = 0°, increasing toward +X)
			var longitude = Mathf.Atan2(dir.x, dir.z);

			// Latitude using Asin (most stable for Y-up convention)
			var latitude = Mathf.Asin(dir.y);

			// Map angles to [0,1] range
			var u = (longitude / (Mathf.PI * 2f)) + 0.5f;
			var v = (latitude / Mathf.PI * 1f) + 0.5f;

			return new Vector2(u, v);
		}

		/// <summary>
		/// Finds the uv of the brightest area (e.g. sun) in the cubemap using
		/// the equirectangular projection as an intermediate step.
		/// </summary>
		public static Vector3 FindLightUV(Cubemap cubemap, int w = 512, int h = 256, bool scanAboveHorizonOnly = true)
		{
			var equirect = Create(cubemap, w, h);
			if (equirect == null)
				return Vector3.up;

			return ImageProcessing.FindSunUV(equirect, scanAboveHorizonOnly: scanAboveHorizonOnly);
		}

		/// <summary>
		/// Finds the direction of the brightest area (e.g. sun) in the cubemap using
		/// the equirectangular projection as an intermediate step.
		/// </summary>
		public static Vector3 FindLightDirection(Cubemap cubemap, int w = 512, int h = 256, bool scanAboveHorizonOnly = true)
		{
			var uv = FindLightUV(cubemap, w, h, scanAboveHorizonOnly);
			return -UVToDirection(uv);

			////test
			//var dir = UVToDirection(uv);
			//var uv2 = DirectionToUV(dir);
			//return -UVToDirection(uv2);
		}
	}
}
