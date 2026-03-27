using UnityEngine;

namespace MassiveHadronLtd
{
	public static class EquirectangularCubemapUtility
	{
		public static Texture2D Create(Cubemap cubemap, int width = 512, int height = 256)
		{
			if (cubemap == null || !cubemap.isReadable)
			{
				Debug.LogError("Cubemap is null or not readable.");
				return null;
			}

			Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
			tex.name = cubemap.name + "_Equirect";

			for (int y = 0; y < height; y++)
			{
				float v = (float)y / (height - 1);

				// Latitude: -π/2 → +π/2
				float latitude = (v - 0.5f) * Mathf.PI;
				float sinLat = Mathf.Sin(latitude);
				float cosLat = Mathf.Cos(latitude);

				for (int x = 0; x < width; x++)
				{
					float u = (float)x / (width - 1);

					// Longitude: -π → +π (NO offsets, NO hacks)
					float longitude = (u - 0.5f) * Mathf.PI * 2f;

					float sinLon = Mathf.Sin(longitude);
					float cosLon = Mathf.Cos(longitude);

					// Unity-aligned direction
					Vector3 dir = new Vector3(
						cosLat * sinLon,
						sinLat,
						cosLat * cosLon
					);

					Color col = SampleCubemap(cubemap, dir);
					tex.SetPixel(x, y, col);
				}
			}

			tex.Apply();
			return tex;

			static Color SampleCubemap(Cubemap cubemap, Vector3 dir)
			{
				dir.Normalize();

				float absX = Mathf.Abs(dir.x);
				float absY = Mathf.Abs(dir.y);
				float absZ = Mathf.Abs(dir.z);

				CubemapFace face;
				float u, v;

				if (absX >= absY && absX >= absZ)
				{
					// ±X faces
					if (dir.x > 0)
					{
						face = CubemapFace.PositiveX;
						u = -dir.z / absX;
						v = -dir.y / absX;
					}
					else
					{
						face = CubemapFace.NegativeX;
						u = dir.z / absX;
						v = -dir.y / absX;
					}
				}
				else if (absY >= absX && absY >= absZ)
				{
					// ±Y faces (different orientation!)
					if (dir.y > 0)
					{
						face = CubemapFace.PositiveY;
						u = dir.x / absY;
						v = dir.z / absY;
					}
					else
					{
						face = CubemapFace.NegativeY;
						u = dir.x / absY;
						v = -dir.z / absY;
					}
				}
				else
				{
					// ±Z faces
					if (dir.z > 0)
					{
						face = CubemapFace.PositiveZ;
						u = dir.x / absZ;
						v = -dir.y / absZ;
					}
					else
					{
						face = CubemapFace.NegativeZ;
						u = -dir.x / absZ;
						v = -dir.y / absZ;
					}
				}

				// Map [-1,1] → [0,1]
				u = 0.5f * (u + 1f);
				v = 0.5f * (v + 1f);

				int size = cubemap.width;
				int px = Mathf.Clamp((int)(u * (size - 1)), 0, size - 1);
				int py = Mathf.Clamp((int)(v * (size - 1)), 0, size - 1);

				return cubemap.GetPixel(face, px, py);
			}
		}

		public static Vector3 FindSunDirection(Cubemap cubemap, float highLumThreshold = 0.85f)
		{
			var w = 512;
			var h = 256;

			var equirectangular = Create(cubemap, w, h);
			var uv = ImageProcessing.FindSunUV(equirectangular, highLumThreshold);
			return UVToDirection(uv, w, h);
		}

		public static Vector3 UVToDirection(Vector2 uv, int width, int height)
		{
			float u = uv.x / (width - 1);
			float v = uv.y / (height - 1);

			float latitude = (v - 0.5f) * Mathf.PI;
			float longitude = (u - 0.5f) * Mathf.PI * 2f;

			float cosLat = Mathf.Cos(latitude);

			return new Vector3(
				cosLat * Mathf.Sin(longitude),
				Mathf.Sin(latitude),
				cosLat * Mathf.Cos(longitude)
			);
		}
	}
}