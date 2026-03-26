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
			var allPixels = new Color[faceSize * faceSize * 6];//totalPixels
			var offset = 0;

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				System.Array.Copy(face, 0, allPixels, offset, face.Length);
				offset += face.Length;
			}

			return ColourUtils.ThresholdColour(allPixels, threshold);
		}

		public static Vector3 FindLightDirection(Cubemap cubemap, int downscaleFactor = 16)
		{
			if (cubemap == null)
				return Vector3.down;

			int originalSize = cubemap.width;
			int faceSize = Mathf.Max(64, originalSize / downscaleFactor);

			Candidate best = new Candidate
			{
				luminance = -1f,
				face = CubemapFace.Unknown
			};

			// Cache full face pixels once (same as original)
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Process each face with the same sampling logic as the original
			ProcessFace(CubemapFace.PositiveY, pxPosY, faceSize, ref best);
			ProcessFace(CubemapFace.PositiveX, pxPosX, faceSize, ref best);
			ProcessFace(CubemapFace.NegativeX, pxNegX, faceSize, ref best);
			ProcessFace(CubemapFace.PositiveZ, pxPosZ, faceSize, ref best);
			ProcessFace(CubemapFace.NegativeZ, pxNegZ, faceSize, ref best);

			if (best.luminance <= 0f || best.face == CubemapFace.Unknown)
				return Vector3.down;

			Vector3 dir = PixelToDirection(best.face, best.uv.x, best.uv.y);
			return -dir.normalized;

			// ====================== Local Helper ======================
			void ProcessFace(CubemapFace face, Color[] fullFacePixels, int targetSize, ref Candidate globalBest)
			{
				float scale = (float)originalSize / targetSize;

				float localMaxLum = -1f;
				int bestSrcX = 0;
				int bestSrcY = 0;

				int yCount = (face == CubemapFace.PositiveY) ? targetSize : targetSize / 2;
				for (int y = 0; y < yCount; y++)
				{
					int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);
					for (int x = 0; x < targetSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);

						Color col = fullFacePixels[srcY * originalSize + srcX];
						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						if (lum > localMaxLum)
						{
							localMaxLum = lum;
							bestSrcX = x;
							bestSrcY = y;
						}
					}
				}

				if (localMaxLum > globalBest.luminance)
				{
					globalBest.luminance = localMaxLum;
					globalBest.uv = new Vector2((float)bestSrcX / targetSize, 1f - (float)bestSrcY / targetSize);
					globalBest.face = face;
				}
			}

			static Vector3 PixelToDirection(CubemapFace face, float u, float v)
			{
				float x = u * 2f - 1f;
				float y = v * 2f - 1f;

				return face switch
				{
					CubemapFace.PositiveZ => new Vector3(x, y, 1f).normalized,
					CubemapFace.NegativeZ => new Vector3(-x, y, -1f).normalized,
					CubemapFace.PositiveX => new Vector3(1f, y, -x).normalized,
					CubemapFace.NegativeX => new Vector3(-1f, y, x).normalized,
					CubemapFace.PositiveY => new Vector3(x, 1f, -y).normalized,
					_ => Vector3.up,
				};
			}
		}

		private struct Candidate
		{
			public float luminance;
			public Vector2 uv;
			public CubemapFace face;
		}
	}
}