using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class CubemapUtility
	{
		public static Color ComputeBrightRegionColor(Cubemap cubemap, float thresholdRatio = 0.85f)
		{
			if (cubemap == null) return Color.white;

			// Use same downscale resolution as FindLightDirection for consistency
			int originalSize = cubemap.width;
			int downscaleFactor = 16; // ← you can expose this as parameter if needed
			int faceSize = Mathf.Max(32, originalSize / downscaleFactor);

			Texture2D sampleCubemap = new Texture2D(faceSize * 4, faceSize * 3, TextureFormat.RGBA32, false);

			// Cache face pixels once (same speedup as in direction function)
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegY = cubemap.GetPixels(CubemapFace.NegativeY); // we need -Y now
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Same fast drawing function (nearest with centering)
			void DrawFaceFast(CubemapFace face, int xOffset, int yOffset)
			{
				Color[] pixels;
				switch (face)
				{
					case CubemapFace.PositiveZ: pixels = pxPosZ; break;
					case CubemapFace.PositiveX: pixels = pxPosX; break;
					case CubemapFace.NegativeX: pixels = pxNegX; break;
					case CubemapFace.PositiveY: pixels = pxPosY; break;
					case CubemapFace.NegativeY: pixels = pxNegY; break;
					case CubemapFace.NegativeZ: pixels = pxNegZ; break;
					default: return;
				}

				float scale = (float)originalSize / faceSize;

				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);
						int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);

						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + (faceSize - 1 - y);

						sampleCubemap.SetPixel(px, py, pixels[srcY * originalSize + srcX]);
					}
				}
			}

			// Draw all six faces — same layout as your original ComputeBrightRegionColor
			DrawFaceFast(CubemapFace.PositiveZ, 1, 1);
			DrawFaceFast(CubemapFace.PositiveX, 2, 1);
			DrawFaceFast(CubemapFace.NegativeX, 0, 1);
			DrawFaceFast(CubemapFace.PositiveY, 1, 2);
			DrawFaceFast(CubemapFace.NegativeY, 1, 0);
			DrawFaceFast(CubemapFace.NegativeZ, 3, 1);

			sampleCubemap.Apply();

			// ──────────────────────────────────────────────────────────────
			// Now sample only the downscaled pixels — same logic, much fewer iterations
			// ──────────────────────────────────────────────────────────────

			float maxLum = 0f;

			List<Color> cols = new List<Color>();
			List<float> lums = new List<float>();

			void SampleFace(int xOffset, int yOffset)
			{
				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + y;

						Color col = sampleCubemap.GetPixel(px, py);

						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						cols.Add(col);
						lums.Add(lum);

						if (lum > maxLum) maxLum = lum;
					}
				}
			}

			SampleFace(1, 1);
			SampleFace(2, 1);
			SampleFace(0, 1);
			SampleFace(1, 2);
			SampleFace(1, 0);
			SampleFace(3, 1);

			Object.DestroyImmediate(sampleCubemap);

			if (maxLum <= 0f || cols.Count == 0)
				return Color.white;

			float threshold = maxLum * thresholdRatio;

			Color sum = Color.black;
			float weightSum = 0f;

			for (int i = 0; i < cols.Count; i++)
			{
				if (lums[i] >= threshold)
				{
					float weight = lums[i];
					sum += cols[i] * weight;
					weightSum += weight;
				}
			}

			if (weightSum <= 0f)
				return Color.white;

			return sum / weightSum;
		}

		public static Vector3 __FindLightDirection(Cubemap cubemap, int downscaleFactor = 16)
		{
			if (cubemap == null) return Vector3.down;

			int originalSize = cubemap.width;
			int faceSize = Mathf.Max(64, originalSize / downscaleFactor);

			// Use Texture2D atlas — same as working version
			Texture2D sampleCubemap = new Texture2D(faceSize * 4, faceSize * 3, TextureFormat.RGBA32, false);

			// Cache full face pixels once — huge speedup
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Fast nearest-neighbor downsample — very close to bilinear for this purpose, much faster
			void DrawFaceFast(CubemapFace face, int xOffset, int yOffset)
			{
				Color[] pixels;
				switch (face)
				{
					case CubemapFace.PositiveZ: pixels = pxPosZ; break;
					case CubemapFace.PositiveX: pixels = pxPosX; break;
					case CubemapFace.NegativeX: pixels = pxNegX; break;
					case CubemapFace.PositiveY: pixels = pxPosY; break;
					case CubemapFace.NegativeZ: pixels = pxNegZ; break;
					default: return;
				}

				float scale = (float)originalSize / faceSize;

				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);
						int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);

						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + (faceSize - 1 - y);

						sampleCubemap.SetPixel(px, py, pixels[srcY * originalSize + srcX]);
					}
				}
			}

			// Draw the faces — fast path
			DrawFaceFast(CubemapFace.PositiveZ, 1, 1);
			DrawFaceFast(CubemapFace.PositiveX, 2, 1);
			DrawFaceFast(CubemapFace.NegativeX, 0, 1);
			DrawFaceFast(CubemapFace.PositiveY, 1, 2);
			DrawFaceFast(CubemapFace.NegativeZ, 3, 1);

			sampleCubemap.Apply();

			// ──────────────────────────────────────────────────────────────
			// Identical brightest pixel search as your working version
			// ──────────────────────────────────────────────────────────────

			float maxLum = -1f;
			Vector2 brightestUV = Vector2.zero;
			CubemapFace brightestFace = CubemapFace.Unknown;

			var facesToCheck = new[]
			{
				CubemapFace.PositiveY,
				CubemapFace.PositiveZ,
				CubemapFace.NegativeZ,
				CubemapFace.PositiveX,
				CubemapFace.NegativeX
			};

			foreach (var face in facesToCheck)
			{
				int xOff = 0, yOff = 0;
				switch (face)
				{
					case CubemapFace.PositiveY: xOff = 1; yOff = 2; break;
					case CubemapFace.PositiveZ: xOff = 1; yOff = 1; break;
					case CubemapFace.NegativeZ: xOff = 3; yOff = 1; break;
					case CubemapFace.PositiveX: xOff = 2; yOff = 1; break;
					case CubemapFace.NegativeX: xOff = 0; yOff = 1; break;
				}

				int yStart = (face == CubemapFace.PositiveY) ? 0 : faceSize / 2;

				for (int y = yStart; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int px = xOff * faceSize + x;
						int py = yOff * faceSize + y;

						Color col = sampleCubemap.GetPixel(px, py);
						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						if (lum > maxLum)
						{
							maxLum = lum;
							brightestUV = new Vector2((float)x / faceSize, (float)y / faceSize);
							brightestFace = face;
						}
					}
				}
			}

			Object.DestroyImmediate(sampleCubemap);

			if (maxLum <= 0f || brightestFace == CubemapFace.Unknown)
				return Vector3.down;

			Vector3 dir = PixelToDirection(brightestFace, brightestUV.x, brightestUV.y);
			return -dir.normalized;

			Vector3 PixelToDirection(CubemapFace face, float u, float v)
			{
				// Adjust +Y UV to compensate for common 90°/180° rotation in top face
				// Your "rotate 180° works" → try flipping both axes (equivalent to 180°)
				if (face == CubemapFace.PositiveY)
				{
					//u = 1f - u;  // horizontal flip
					v = 1f - v;  // vertical flip
				}

				float x = u * 2f - 1f;
				float y = v * 2f - 1f;

				switch (face)
				{
					// Adjusted mappings — matches most Unity cubemaps / RenderToCubemap
					case CubemapFace.PositiveZ: return new Vector3(x, y, 1f).normalized;   // front
					case CubemapFace.NegativeZ: return new Vector3(-x, y, -1f).normalized;   // back
					case CubemapFace.PositiveX: return new Vector3(1f, y, -x).normalized;    // right
					case CubemapFace.NegativeX: return new Vector3(-1f, y, x).normalized;    // left
					case CubemapFace.PositiveY: return new Vector3(x, 1f, y).normalized;    // up (after UV flip)
					default: return Vector3.up;
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
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Process each face with the same sampling logic as the original
			ProcessFace(CubemapFace.PositiveY, pxPosY, faceSize, ref best);
			ProcessFace(CubemapFace.PositiveZ, pxPosZ, faceSize, ref best);
			ProcessFace(CubemapFace.PositiveX, pxPosX, faceSize, ref best);
			ProcessFace(CubemapFace.NegativeX, pxNegX, faceSize, ref best);
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
			// Exactly the same +Y adjustment as your original working version
			if (face == CubemapFace.PositiveY)
			{
				//u = 1f - u;        // commented out (as in your code)
				v = 1f - v;           // vertical flip — this was active
			}

			float x = u * 2f - 1f;
			float y = v * 2f - 1f;

			switch (face)
			{
				case CubemapFace.PositiveZ: return new Vector3(x, y, 1f).normalized;
				case CubemapFace.NegativeZ: return new Vector3(-x, y, -1f).normalized;
				case CubemapFace.PositiveX: return new Vector3(1f, y, -x).normalized;
				case CubemapFace.NegativeX: return new Vector3(-1f, y, x).normalized;
				case CubemapFace.PositiveY: return new Vector3(x, 1f, y).normalized;
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