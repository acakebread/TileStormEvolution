using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class CubemapUtility
	{
		public static Color ComputeBrightRegionColor(Cubemap cubemap, float thresholdRatio = 0.85f)
		{
			if (cubemap == null)
				return Color.white;

			int originalSize = cubemap.width;
			int faceSize = Mathf.Max(32, originalSize / 16);

			// Cache all six faces
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegY = cubemap.GetPixels(CubemapFace.NegativeY);
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			float maxLum = 0f;
			var brightColors = new List<Color>(faceSize * faceSize * 6);
			var brightLums = new List<float>(faceSize * faceSize * 6);

			// Process every face with identical simple downsampling - no flips, no atlas
			ProcessFace(pxPosZ);
			ProcessFace(pxPosX);
			ProcessFace(pxNegX);
			ProcessFace(pxPosY);
			ProcessFace(pxNegY);
			ProcessFace(pxNegZ);

			if (maxLum <= 0f || brightColors.Count == 0)
				return Color.white;

			float threshold = maxLum * thresholdRatio;

			Color sum = Color.black;
			float weightSum = 0f;

			for (int i = 0; i < brightColors.Count; i++)
			{
				if (brightLums[i] >= threshold)
				{
					float weight = brightLums[i];
					sum += brightColors[i] * weight;
					weightSum += weight;
				}
			}

			return weightSum > 0f ? (sum / weightSum) : Color.white;

			void ProcessFace(Color[] pixels)
			{
				float scale = (float)originalSize / faceSize;

				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);
						int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);

						Color col = pixels[srcY * originalSize + srcX];
						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						brightColors.Add(col);
						brightLums.Add(lum);

						if (lum > maxLum)
							maxLum = lum;
					}
				}
			}
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
				int bestSrcY = 0;   // we track source Y because of the flip

				int yStart = (face == CubemapFace.PositiveY) ? 0 : targetSize / 2;

				for (int y = yStart; y < targetSize; y++)
				{
					for (int x = 0; x < targetSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);

						// Important: match the vertical flip that DrawFaceFast applied
						int srcY = Mathf.Clamp(Mathf.FloorToInt(((targetSize - 1 - y) + 0.5f) * scale), 0, originalSize - 1);

						Color col = fullFacePixels[srcY * originalSize + srcX];
						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						if (lum > localMaxLum)
						{
							localMaxLum = lum;
							bestSrcX = x;
							bestSrcY = y;   // this is the y in the *flipped* coordinate space
						}
					}
				}

				if (localMaxLum > globalBest.luminance)
				{
					globalBest.luminance = localMaxLum;
					globalBest.uv = new Vector2((float)bestSrcX / targetSize, (float)bestSrcY / targetSize);
					globalBest.face = face;
				}
			}
		}

		private static Vector3 PixelToDirection(CubemapFace face, float u, float v)
		{
			float x = u * 2f - 1f;
			float y = v * 2f - 1f;

			switch (face)
			{
				case CubemapFace.PositiveZ: return new Vector3(x, y, 1f).normalized;
				case CubemapFace.NegativeZ: return new Vector3(-x, y, -1f).normalized;
				case CubemapFace.PositiveX: return new Vector3(1f, y, -x).normalized;
				case CubemapFace.NegativeX: return new Vector3(-1f, y, x).normalized;
				case CubemapFace.PositiveY: return new Vector3(x, 1f, -y).normalized;
				default: return Vector3.up;
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