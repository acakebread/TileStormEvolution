using UnityEngine;

namespace MassiveHadronLtd
{
	public static class AtlasCubemapUtility
	{
		/// <summary>
		/// Returns a standard flattened cross-layout Texture2D from a Cubemap (raw data).
		/// Layout:
		///          +Y     ← Sky (should appear at top after flip)
		///    -X    +Z    +X    -Z
		///          -Y     ← Ground (should appear at bottom after flip)
		/// </summary>
		public static Texture2D FlattenCubemap(Cubemap cubemap, int faceSize = 128)
		{
			if (cubemap == null || !cubemap.isReadable)
				return null;

			int finalFaceSize = Mathf.Min(cubemap.width, faceSize);
			int texWidth = finalFaceSize * 4;
			int texHeight = finalFaceSize * 3;

			Texture2D result = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
			result.name = $"{cubemap.name}_Flattened";

			// Fill entire texture with black first (prevents grey bleed)
			Color[] clearPixels = new Color[texWidth * texHeight];
			for (int i = 0; i < clearPixels.Length; i++)
				clearPixels[i] = Color.black;

			result.SetPixels(clearPixels);

			void CopyFace(CubemapFace face, int offsetX, int offsetY)
			{
				Color[] srcPixels = cubemap.GetPixels(face);

				if (cubemap.width == finalFaceSize)
				{
					result.SetPixels(offsetX, offsetY, finalFaceSize, finalFaceSize, srcPixels);
					return;
				}

				// Downsample
				Color[] dest = new Color[finalFaceSize * finalFaceSize];
				float scale = (float)cubemap.width / finalFaceSize;

				for (int y = 0; y < finalFaceSize; y++)
				{
					for (int x = 0; x < finalFaceSize; x++)
					{
						int srcX = Mathf.FloorToInt(x * scale);
						int srcY = Mathf.FloorToInt(y * scale);
						dest[y * finalFaceSize + x] = srcPixels[srcY * cubemap.width + srcX];
					}
				}

				result.SetPixels(offsetX, offsetY, finalFaceSize, finalFaceSize, dest);
			}

			// Correct face placement: +Y in top row, -Y in bottom row
			CopyFace(CubemapFace.PositiveY, finalFaceSize * 1, finalFaceSize * 0); // +Y top
			CopyFace(CubemapFace.NegativeX, finalFaceSize * 0, finalFaceSize * 1); // -X
			CopyFace(CubemapFace.PositiveZ, finalFaceSize * 1, finalFaceSize * 1); // +Z center
			CopyFace(CubemapFace.PositiveX, finalFaceSize * 2, finalFaceSize * 1); // +X
			CopyFace(CubemapFace.NegativeZ, finalFaceSize * 3, finalFaceSize * 1); // -Z
			CopyFace(CubemapFace.NegativeY, finalFaceSize * 1, finalFaceSize * 2); // -Y bottom

			result.Apply();
			return result.FlipVertically();
		}

		private struct Candidate
		{
			public float luminance;
			public Vector2 uv;
			public CubemapFace face;
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
	}
}
