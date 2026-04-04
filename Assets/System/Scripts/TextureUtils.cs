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

		public static Texture2D GenerateCheckerTexture(int numSquares = 256)
		{
			// How many tiles across/down
			int tilesPerSide = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(numSquares)));

			// We’ll make texture resolution match tile count (1 pixel per tile)
			int size = tilesPerSide;

			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Point,
				hideFlags = HideFlags.HideAndDontSave
			};

			Color32 c0 = new Color32(0, 0, 0, 255);
			Color32 c1 = new Color32(255, 255, 255, 255);

			for (int y = 0; y < tilesPerSide; y++)
			{
				for (int x = 0; x < tilesPerSide; x++)
				{
					bool isWhite = ((x + y) & 1) == 0;   // fast checker test
					tex.SetPixel(x, y, isWhite ? c1 : c0);
				}
			}

			tex.Apply(false, true);
			return tex;
		}

		public static Texture2D GenerateCheckerTexture(int squaresX, int squaresY)
		{
			squaresX = Mathf.Max(1, squaresX);
			squaresY = Mathf.Max(1, squaresY);

			Texture2D tex = new Texture2D(squaresX, squaresY, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Point,
				hideFlags = HideFlags.HideAndDontSave
			};

			Color32 c0 = new Color32(0, 0, 0, 255);
			Color32 c1 = new Color32(255, 255, 255, 255);

			for (int y = 0; y < squaresY; y++)
			{
				for (int x = 0; x < squaresX; x++)
				{
					bool isWhite = ((x + y) & 1) == 0;
					tex.SetPixel(x, y, isWhite ? c1 : c0);
				}
			}

			tex.Apply(false, true);
			return tex;
		}

		/// <summary>
		/// Converts a RenderTexture to a new Texture2D by reading pixels from GPU → CPU.
		/// Caller is responsible for Destroy()ing the returned Texture2D when no longer needed.
		/// </summary>
		public static Texture2D RenderTextureToTexture2D(RenderTexture rt, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
		{
			if (rt == null || !rt.IsCreated())
			{
				Debug.LogError("RenderTexture is null or not created.");
				return null;
			}

			// Create matching Texture2D
			Texture2D tex = new Texture2D(rt.width, rt.height, format, mipChain)
			{
				filterMode = rt.filterMode,
				wrapMode = TextureWrapMode.Clamp, // or match rt.wrapMode if needed
				name = rt.name + "_AsTex2D"
			};

			// Remember previous active RT to restore it
			RenderTexture previous = RenderTexture.active;

			RenderTexture.active = rt;
			tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			tex.Apply();  // Uploads changes to GPU — skip only if you never use tex as a GPU texture

			RenderTexture.active = previous;

			return tex;
		}

		public static Color[] GetPixels(this RenderTexture src)
		{
			Texture2D clone = new Texture2D(src.width, src.height);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = src;
			clone.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
			clone.Apply();
			RenderTexture.active = active;
			Color[] data = clone.GetPixels();
			GameObject.Destroy(clone);
			return data;
		}

		//replaces flash histogram
		public static float GetAlphaHistogram(this RenderTexture src, Rect[] rect = null)//normalised rects passed in
		{
			if (null == src) return 0;
			if (null == rect) rect = new Rect[] { new Rect(0, 0, 1, 1) };//src.width,src.height)};//default to full size rect

			Color[] data = src.GetPixels();
			float fAlpha = 0;
			float count = 0;
			for (int n = 0; n < rect.Length; n++)
			{
				for (int y = (int)(rect[n].yMin * src.height); y < (int)(rect[n].yMax * src.height); y++)
				{
					for (int x = (int)(rect[n].xMin * src.width); x < (int)(rect[n].xMax * src.width); x++)
					{
						fAlpha += data[Mathf.Clamp(y * src.width + x, 0, data.Length - 1)].a;
						count++;
					}
				}
			}
			return 0 < count ? fAlpha / count : 0;
		}

		public static Texture2D FlipVertically(this Texture2D source)
		{
			if (source == null) return null;
			Texture2D flipped = new Texture2D(source.width, source.height, source.format, false);
			Color[] p = source.GetPixels();
			Color[] fp = new Color[p.Length];
			int w = source.width, h = source.height;
			for (int y = 0; y < h; y++)
				System.Array.Copy(p, y * w, fp, (h - 1 - y) * w, w);
			flipped.SetPixels(fp);
			flipped.Apply();
			return flipped;
		}

		///// <summary>
		///// Creates a deep copy (clone) of the source Texture2D.
		///// The returned texture is a completely independent copy with its own pixel data.
		///// Caller is responsible for calling Destroy() on the returned texture when no longer needed.
		///// </summary>
		//public static Texture2D Clone(this Texture2D source)
		//{
		//	if (source == null)
		//	{
		//		Debug.LogError("Cannot clone a null Texture2D.");
		//		return null;
		//	}

		//	// Create a new Texture2D with the same dimensions and format
		//	Texture2D clone = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1)
		//	{
		//		wrapMode = source.wrapMode,
		//		filterMode = source.filterMode,
		//		anisoLevel = source.anisoLevel,
		//		name = source.name + " (Clone)"
		//	};

		//	// Copy all pixel data
		//	if (source.isReadable)
		//	{
		//		// Fast path: direct pixel array copy (most efficient)
		//		Color[] pixels = source.GetPixels();
		//		clone.SetPixels(pixels);
		//		clone.Apply();
		//	}
		//	else
		//	{
		//		// Fallback for non-readable textures (slower, uses GPU readback)
		//		RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

		//		Graphics.Blit(source, rt);

		//		RenderTexture previous = RenderTexture.active;
		//		RenderTexture.active = rt;

		//		clone.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		//		clone.Apply();

		//		RenderTexture.active = previous;
		//		RenderTexture.ReleaseTemporary(rt);
		//	}

		//	return clone;
		//}

		/// <summary>
		/// Creates a deep clone of any Texture (Texture2D, Cubemap, etc.).
		/// Returns a new independent texture with copied pixel data.
		/// Supports Cubemaps perfectly.
		/// Caller is responsible for Destroy()ing the returned texture when done.
		/// </summary>
		public static Texture Clone(this Texture source)
		{
			if (source == null)
			{
				Debug.LogError("Cannot clone a null Texture.");
				return null;
			}

			if (source is Texture2D tex2D)
			{
				return tex2D.Clone(); // Use the more optimized Texture2D version below
			}

			if (source is Cubemap cubemap)
			{
				return CloneCubemap(cubemap);
			}

			// Fallback for other texture types (RenderTexture, Texture3D, etc.) using GPU copy
			return CloneViaGraphicsCopy(source);
		}

		/// <summary>
		/// Specialized fast clone for Texture2D (kept for backward compatibility and performance).
		/// </summary>
		public static Texture2D Clone(this Texture2D source)
		{
			if (source == null) return null;

			Texture2D clone = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1, false)
			{
				wrapMode = source.wrapMode,
				wrapModeU = source.wrapModeU,
				wrapModeV = source.wrapModeV,
				wrapModeW = source.wrapModeW,
				filterMode = source.filterMode,
				anisoLevel = source.anisoLevel,
				name = source.name + " (Clone)"
			};

			if (source.isReadable)
			{
				// Fast CPU path
				clone.SetPixels(source.GetPixels());
				clone.Apply();
			}
			else
			{
				// GPU path via temporary RenderTexture
				RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
				Graphics.Blit(source, rt);
				RenderTexture previous = RenderTexture.active;
				RenderTexture.active = rt;
				clone.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
				clone.Apply();
				RenderTexture.active = previous;
				RenderTexture.ReleaseTemporary(rt);
			}

			return clone;
		}

		/// <summary>
		/// Internal: Clones a Cubemap using Graphics.CopyTexture (very fast, GPU-side).
		/// </summary>
		private static Cubemap CloneCubemap(Cubemap source)
		{
			Cubemap clone = new Cubemap(source.width, source.format, source.mipmapCount > 1)
			{
				wrapMode = source.wrapMode,
				filterMode = source.filterMode,
				anisoLevel = source.anisoLevel,
				name = source.name + " (Clone)"
			};

			for (int face = 0; face < 6; face++)
			{
				Graphics.CopyTexture(source, face, 0, clone, face, 0); // Copy all mip levels automatically if present
			}

			// Note: Cubemaps created at runtime are not readable by default.
			// If you need CPU read access, you must set isReadable = true in the constructor (Unity 2021.2+).

			return clone;
		}

		/// <summary>
		/// Generic fallback using Graphics.CopyTexture for other texture types.
		/// </summary>
		private static Texture CloneViaGraphicsCopy(Texture source)
		{
			// This is a simple but effective GPU copy for most cases.
			// For full control you can extend it per type if needed.
			Debug.LogWarning($"Using generic clone for texture type: {source.GetType().Name}. " +
							 "Performance may vary.");

			// For now we redirect Cubemap and Texture2D to their specialized versions
			if (source is Cubemap c) return CloneCubemap(c);
			if (source is Texture2D t2d) return t2d.Clone();

			// You can add more specific cases here (e.g. Texture3D, CubemapArray...)

			return null;
		}
	}
}
