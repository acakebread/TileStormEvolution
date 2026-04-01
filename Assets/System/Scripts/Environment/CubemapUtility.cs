using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class CubemapUtility
	{
		public static Color ComputeBrightColor(Cubemap cubemap, float threshold = 0.85f)
		{
			if (cubemap == null)
				return Color.white;

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
				return Color.white;
			}

			// === Build one large array containing ALL pixels from all 6 faces ===
			var faceSize = cubemap.width;
			var pixels = new Color[faceSize * faceSize * 6];//totalPixels

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				System.Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
			}

			return ColourUtils.ThresholdColour(pixels, threshold);
		}

		/// <summary>
		/// Computes a good ambient colour from the skybox cubemap using luminance-weighted averaging.
		/// Brighter areas (clouds, sun, sky) contribute much more than dark areas.
		/// </summary>
		public static Color ComputeAmbientColor(Cubemap cubemap, float power = 1f)
		{
			if (cubemap == null)
				return new Color(0.25f, 0.25f, 0.35f);

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
				return new Color(0.25f, 0.25f, 0.35f);
			}

			var pixels = GetAllPixels(cubemap);   // reuse the helper above

			if (pixels.Length == 0)
				return new Color(0.25f, 0.25f, 0.35f);

			return ColourUtils.ComputeAmbientColor(pixels, power);
		}

		//possibly better version to be tested
		public static Color SampleCubemap(Cubemap cubemap, Vector3 dir)
		{
			dir.Normalize();   // safe, in case of floating point drift

			float absX = Mathf.Abs(dir.x);
			float absY = Mathf.Abs(dir.y);
			float absZ = Mathf.Abs(dir.z);

			CubemapFace face;
			float u, v;

			if (absX >= absY && absX >= absZ)
			{
				face = dir.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
				u = dir.x > 0 ? -dir.z : dir.z;
				v = -dir.y;
				float abs = absX;
				u /= abs; v /= abs;
			}
			else if (absY >= absX && absY >= absZ)
			{
				face = dir.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
				u = dir.x;
				v = dir.y > 0 ? dir.z : -dir.z;
				float abs = absY;
				u /= abs; v /= abs;
			}
			else
			{
				face = dir.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
				u = dir.z > 0 ? dir.x : -dir.x;
				v = -dir.y;
				float abs = absZ;
				u /= abs; v /= abs;
			}

			u = 0.5f * (u + 1f);
			v = 0.5f * (v + 1f);

			int size = cubemap.width;
			int px = Mathf.Clamp((int)(u * (size - 1)), 0, size - 1);
			int py = Mathf.Clamp((int)(v * (size - 1)), 0, size - 1);

			return cubemap.GetPixel(face, px, py);
		}

		/// <summary>
		/// Returns a single flat array containing ALL pixels from all 6 faces of the cubemap.
		/// Useful for full-image processing like ambient colour calculation.
		/// </summary>
		public static Color[] GetAllPixels(Cubemap cubemap)
		{
			if (cubemap == null)
				return Array.Empty<Color>();

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable. Cannot extract pixels.");
				return Array.Empty<Color>();
			}

			var faceSize = cubemap.width;
			var totalPixels = faceSize * faceSize * 6;
			var pixels = new Color[totalPixels];

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
			}

			return pixels;
		}
	}
}

