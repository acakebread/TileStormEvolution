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
		/// Finds the direction of the brightest area (e.g. sun) in the cubemap using
		/// the equirectangular projection as an intermediate step.
		/// </summary>
		public static Vector3 FindSunDirection(Cubemap cubemap, float highLumThreshold = 0.85f)
		{
			const int w = 512;
			const int h = 256;

			var equirect = Create(cubemap, w, h);
			if (equirect == null)
				return Vector3.up;

			var uv = ImageProcessing.FindSunUV(equirect, highLumThreshold);
			return UVToDirection(uv, w, h);
		}

		/// <summary>
		/// Converts UV coordinates (pixel space) in the equirectangular texture 
		/// back to a normalized world direction.
		/// </summary>
		public static Vector3 UVToDirection(Vector2 uv, int width, int height)
		{
			var u = uv.x / (width - 1f);
			var v = uv.y / (height - 1f);

			var longitude = (u - 0.5f) * Mathf.PI * 2f;
			var latitude = (v - 0.5f) * Mathf.PI;

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
		public static Vector2 DirectionToUV(Vector3 dir, int width, int height)
		{
			dir = dir.normalized;

			// Longitude using Atan2 (matches the forward mapping: +Z = 0°, increasing toward +X)
			var longitude = Mathf.Atan2(dir.x, dir.z);

			// Latitude using Asin (most stable for Y-up convention)
			var latitude = Mathf.Asin(dir.y);

			// Map angles to [0,1] range
			var u = (longitude / (Mathf.PI * 2f)) + 0.5f;
			var v = (latitude / Mathf.PI) + 0.5f;

			// Convert to pixel coordinates (width-1 and height-1 for exact edge mapping)
			var x = u * (width - 1f);
			var y = v * (height - 1f);

			return new Vector2(x, y);
		}
	}
}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class EquirectangularCubemapUtility
//	{
//		public static Texture2D Create(Cubemap cubemap, int width = 512, int height = 256)
//		{
//			if (cubemap == null || !cubemap.isReadable)
//			{
//				Debug.LogError("Cubemap is null or not readable.");
//				return null;
//			}

//			var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
//			tex.name = cubemap.name + "_Equirect";

//			for (var y = 0; y < height; y++)
//			{
//				var v = (float)y / (height - 1);

//				// Latitude: -π/2 → +π/2
//				var latitude = (v - 0.5f) * Mathf.PI;
//				var sinLat = Mathf.Sin(latitude);
//				var cosLat = Mathf.Cos(latitude);

//				for (var x = 0; x < width; x++)
//				{
//					var u = (float)x / (width - 1);

//					// Longitude: -π → +π (NO offsets, NO hacks)
//					var longitude = (u - 0.5f) * Mathf.PI * 2f;

//					var sinLon = Mathf.Sin(longitude);
//					var cosLon = Mathf.Cos(longitude);

//					// Unity-aligned direction
//					var dir = new Vector3(cosLat * sinLon, sinLat, cosLat * cosLon);

//					Color col = CubemapUtility.SampleCubemap(cubemap, dir);
//					tex.SetPixel(x, y, col);
//				}
//			}

//			tex.Apply();
//			return tex;
//		}

//		public static Vector3 FindSunDirection(Cubemap cubemap, float highLumThreshold = 0.85f)
//		{
//			const int w = 512;
//			const int h = 256;

//			var equirect = Create(cubemap, w, h);
//			if (equirect == null) return Vector3.up;

//			var uv = ImageProcessing.FindSunUV(equirect, highLumThreshold);
//			return UVToDirection(uv, w, h);
//		}

//		public static Vector3 UVToDirection(Vector2 uv, int width, int height)
//		{
//			var u = uv.x / (width - 1f);   // better to use -1f for exact edge mapping
//			var v = uv.y / (height - 1f);

//			var longitude = (u - 0.5f) * Mathf.PI * 2f;
//			var latitude = (v - 0.5f) * Mathf.PI;   // -π/2 at bottom, +π/2 at top

//			var cosLat = Mathf.Cos(latitude);

//			return new Vector3(
//				cosLat * Mathf.Sin(longitude),  // X
//				Mathf.Sin(latitude),            // Y (up)
//				cosLat * Mathf.Cos(longitude)   // Z
//			).normalized;   // always safe
//		}

//		public static Vector2 DirectionToUV(Vector3 dir, int width, int height)
//		{
//			dir = dir.normalized;

//			// Longitude: atan2(x, z) because +Z = 0°, increasing toward +X
//			var longitude = Mathf.Atan2(dir.x, dir.z);   // range: -π ... +π

//			// Latitude: asin(y)  (this is the cleanest and most stable)
//			var latitude = Mathf.Asin(dir.y);            // range: -π/2 ... +π/2

//			// Map to [0,1]
//			var u = (longitude / (Mathf.PI * 2f)) + 0.5f;
//			var v = (latitude / Mathf.PI) + 0.5f;

//			// Convert to pixel coordinates
//			var x = u * (width - 1f);
//			var y = v * (height - 1f);

//			return new Vector2(x, y);
//		}
//	}
//}