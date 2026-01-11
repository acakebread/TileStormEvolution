using UnityEngine;

namespace MassiveHadronLtd
{
	public static class TextureUtils
	{
		/// <summary>
		/// Drop-in seamless replacement for GeneratePerlinNoiseTexture.
		/// Ignores Perlin limitations and produces a tileable noise texture
		/// using the seamless value noise generator.
		/// </summary>
		public static Texture2D GeneratePerlinNoiseTexture(int width = 256, int height = 256, float scale = 1f)
		{
			// NOTE:
			// We ignore scale (Perlin’s frequency) because seamless value noise
			// handles frequency differently. Instead we map scale roughly to
			// noise “detail”.
			// Higher scale → more noisy texture.

			int size = Mathf.Max(width, height);

			// Map "scale" to value-noise sampling density (empirical)
			float brightnessMin = 0f;
			float brightnessMax = 1f;

			// To emulate Perlin's "scale" idea:
			// - low scale = broad gradients (blurrier)
			// - high scale = sharp/more random
			// We achieve this by sampling a larger/smaller underlying grid.
			int effectiveSize = Mathf.Clamp(Mathf.RoundToInt(size * (scale / 10f)), 32, 1024);

			Texture2D baseTex = GenerateSeamlessValueNoise(effectiveSize, brightnessMin, brightnessMax);

			// Now resize to requested width/height if needed
			if (width != effectiveSize || height != effectiveSize)
			{
				Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, false)
				{
					wrapMode = TextureWrapMode.Repeat,
					filterMode = FilterMode.Bilinear
				};

				for (int y = 0; y < height; y++)
				{
					float v = (float)y / height;
					for (int x = 0; x < width; x++)
					{
						float u = (float)x / width;
						resized.SetPixel(x, y, baseTex.GetPixelBilinear(u, v));
					}
				}

				resized.Apply();
				return resized;
			}

			return baseTex;
		}


		// Helper to create a texture for the tile selector background
		public static Texture2D MakeTex(int width, int height, Color col)
		{
			Color[] pix = new Color[width * height];
			for (int i = 0; i < pix.Length; i++)
				pix[i] = col;
			Texture2D result = new Texture2D(width, height);
			result.SetPixels(pix);
			result.Apply();
			return result;
		}

		public static Texture2D GenerateSeamlessValueNoise(int size, float minBright = 0f, float maxBright = 1f)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear
			};

			// Generate a grid of random values INCLUDING the final wrap row/column
			float[,] grid = new float[size + 1, size + 1];
			for (int y = 0; y <= size; y++)
			{
				for (int x = 0; x <= size; x++)
				{
					// wrap edges: copy from opposite side
					int rx = (x == size) ? 0 : x;
					int ry = (y == size) ? 0 : y;

					grid[x, y] = UnityEngine.Random.value;
				}
			}

			// Now fill the texture using bilinear-interpolated value noise
			for (int y = 0; y < size; y++)
			{
				float fy = (float)y / size;
				int iy = Mathf.FloorToInt(fy * size);
				float ty = fy * size - iy;

				for (int x = 0; x < size; x++)
				{
					float fx = (float)x / size;
					int ix = Mathf.FloorToInt(fx * size);
					float tx = fx * size - ix;

					float v00 = grid[ix, iy];
					float v10 = grid[ix + 1, iy];
					float v01 = grid[ix, iy + 1];
					float v11 = grid[ix + 1, iy + 1];

					// smooth interpolation
					float sx = tx * tx * (3 - 2 * tx);
					float sy = ty * ty * (3 - 2 * ty);

					float nx0 = Mathf.Lerp(v00, v10, sx);
					float nx1 = Mathf.Lerp(v01, v11, sx);
					float v = Mathf.Lerp(nx0, nx1, sy);

					v = Mathf.Lerp(minBright, maxBright, v);

					tex.SetPixel(x, y, new Color(v, v, v, 1));
				}
			}

			tex.Apply();
			return tex;
		}

		/// <summary>
		/// Generate a 4x4 Wang tile atlas (16 tiles) using a 2-colour edge Wang set.
		/// Each tile is tileSize x tileSize pixels. The atlas is returned as a square texture:
		/// atlas size = tileSize * tilesPerRow (tilesPerRow default 4).
		/// The tiles' edges are guaranteed to match for the same edge-bit values (0/1).
		/// </summary>
		public static Texture2D GenerateWangTileAtlas(int tileSize = 64, int tilesPerRow = 4, float minBright = 0f, float maxBright = 1f, int deterministicSeed = 12345)
		{
			if (tilesPerRow <= 0) tilesPerRow = 4;
			int tileCount = tilesPerRow * tilesPerRow;
			int atlasSize = tileSize * tilesPerRow;

			var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.HideAndDontSave
			};

			// Deterministic random
			var rnd = new System.Random(deterministicSeed);

			// Create global edge profiles for the two edge states (0 and 1)
			// Vertical edges array: for each state we have tileSize samples along Y (0..tileSize-1)
			float[][] verticalEdges = new float[2][];
			float[][] horizontalEdges = new float[2][];

			for (int s = 0; s < 2; s++)
			{
				verticalEdges[s] = new float[tileSize + 1];   // +1 to include wrap sample
				horizontalEdges[s] = new float[tileSize + 1];

				// Use per-row/per-column value noise generated deterministically
				// Create a small 1D noise using seeded values and smooth them
				for (int i = 0; i <= tileSize; i++)
				{
					// base random plus a smoothed neighbour blend to avoid harsh jumps
					float v = (float)rnd.NextDouble();
					// small smoothing using a simple triangular kernel
					float v2 = (float)rnd.NextDouble();
					float blended = Mathf.Lerp(v, v2, 0.45f);
					verticalEdges[s][i] = blended;
					horizontalEdges[s][i] = (float)rnd.NextDouble();
				}
			}

			// Helper to get edge sample at fractional coordinate with simple linear interpolation
			System.Func<float[], float, float> SampleEdge = (arr, t) =>
			{
				if (t <= 0) return arr[0];
				if (t >= 1f) return arr[tileSize];
				float pos = t * tileSize;
				int i0 = Mathf.FloorToInt(pos);
				int i1 = Mathf.Min(i0 + 1, tileSize);
				float f = pos - i0;
				return Mathf.Lerp(arr[i0], arr[i1], f);
			};

			// For each tile index (0..tileCount-1) build the tile and paste into atlas.
			// We'll use the 4-bit tile index mapping: bit0=left, bit1=right, bit2=top, bit3=bottom
			// This gives 16 possible combinations (2^4).
			for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
			{
				int tileX = tileIndex % tilesPerRow;
				int tileY = tileIndex / tilesPerRow;

				int leftBit = (tileIndex >> 0) & 1;
				int rightBit = (tileIndex >> 1) & 1;
				int topBit = (tileIndex >> 2) & 1;
				int bottomBit = (tileIndex >> 3) & 1;

				// Start with an interior seamless noise tile for visual richness
				Texture2D interior = GenerateSeamlessValueNoise(tileSize, minBright, maxBright);

				// Compose tile pixels
				for (int py = 0; py < tileSize; py++)
				{
					float fy = (float)py / (tileSize - 1f); // 0..1
					for (int px = 0; px < tileSize; px++)
					{
						float fx = (float)px / (tileSize - 1f); // 0..1

						// edge samples (normalized 0..1)
						float leftVal = SampleEdge(verticalEdges[leftBit], fy);
						float rightVal = SampleEdge(verticalEdges[rightBit], fy);
						float topVal = SampleEdge(horizontalEdges[topBit], fx);
						float bottomVal = SampleEdge(horizontalEdges[bottomBit], fx);

						// interior value from generated noise
						Color interiorCol = interior.GetPixel(px, py);
						float interiorVal = interiorCol.r;

						// Blend edges into interior with a small falloff to avoid visible seams.
						// borderBlend controls how many pixels are blended from edge (in [0..1] fraction)
						float borderBlend = Mathf.Clamp01(6f / tileSize); // ~6 pixel blend default
																		  // compute proximity to each edge (0 at center to 1 at edge)
						float proximityLeft = Mathf.Clamp01(1f - (fx));
						float proximityRight = Mathf.Clamp01(fx);
						float proximityTop = Mathf.Clamp01(fy);
						float proximityBottom = Mathf.Clamp01(1f - fy);

						// Edge influence weight: stronger at edge, fade into interior
						float wLeft = Mathf.SmoothStep(0f, 1f, proximityLeft / borderBlend);
						float wRight = Mathf.SmoothStep(0f, 1f, proximityRight / borderBlend);
						float wTop = Mathf.SmoothStep(0f, 1f, proximityTop / borderBlend);
						float wBottom = Mathf.SmoothStep(0f, 1f, proximityBottom / borderBlend);

						// Combine horizontal interpolation and vertical interpolation anchored at edges
						float horizEdge = Mathf.Lerp(leftVal, rightVal, fx);
						float vertEdge = Mathf.Lerp(bottomVal, topVal, fy);

						// Weighted mix: prefer interior, but near edges mix towards exact edge interpolation
						float edgeMixWeight = Mathf.Clamp01(Mathf.Max(wLeft, wRight, wTop, wBottom));
						float mixedEdge = Mathf.Lerp(horizEdge, vertEdge, 0.5f);

						// final value = interior blended with mixedEdge based on edge influence
						float finalVal = Mathf.Lerp(interiorVal, mixedEdge, edgeMixWeight);

						// small per-pixel deterministic jitter to break repeating structure (use tile-specific RNG)
						// deterministic: seed depends on tileIndex and px/py
						int jseed = (tileIndex + 1) * (px + 1) * (py + 1);
						float jitter = (new System.Random(jseed)).NextDouble() < 0.002 ? 0.02f * (float)(new System.Random(jseed + 7)).NextDouble() : 0f;
						finalVal = Mathf.Clamp01(finalVal + jitter);

						Color c = new Color(finalVal, finalVal, finalVal, 1f);

						atlas.SetPixel(tileX * tileSize + px, tileY * tileSize + py, c);
					}
				}
			}

			atlas.Apply();
			return atlas;
		}

		/// <summary>
		/// Generates a classic 256x256 XOR RGB test texture.
		/// R = x
		/// G = y
		/// B = x XOR y
		/// This produces all possible 8-bit combinations and a strong diagnostic pattern.
		/// </summary>
		public static Texture2D GenerateXorTexture256()
		{
			const int size = 256;

			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Point,
				hideFlags = HideFlags.HideAndDontSave
			};

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					byte r = (byte)x;           // 0..255 across X
					byte g = (byte)y;           // 0..255 across Y
					byte b = (byte)(x ^ y);     // XOR pattern

					tex.SetPixel(x, y, new Color32(r, g, b, 255));
				}
			}

			tex.Apply(false, true);
			return tex;
		}
	}
}
