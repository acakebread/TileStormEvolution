using UnityEngine;

public static class WangTileGenerator
{
	/// <summary>
	/// Generate (or update) a 4x4 Wang tile atlas.
	/// Pass in an existing texture to reuse it and avoid leaks/allocations.
	/// </summary>
	public static Texture2D GenerateWangTileAtlas(Texture2D existingAtlas = null,
		int tileSize = 64, int tilesPerRow = 4,
		float minBright = 0f, float maxBright = 1f, int deterministicSeed = 12345)
	{
		int atlasSize = tileSize * tilesPerRow;

		Texture2D atlas = existingAtlas;

		if (atlas == null || atlas.width != atlasSize || atlas.height != atlasSize)
		{
			if (atlas != null)
				Object.DestroyImmediate(atlas);

			atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.HideAndDontSave,
				name = "WangTileAtlas"
			};
		}

		var rnd = new System.Random(deterministicSeed);

		float[] GenerateEdge(int size)
		{
			float[] arr = new float[size];
			for (int i = 0; i < size; i++)
				arr[i] = (float)rnd.NextDouble();

			for (int i = 1; i < size - 1; i++)
				arr[i] = (arr[i - 1] + arr[i] + arr[i + 1]) / 3f;
			return arr;
		}

		float[][] verticalEdges = { GenerateEdge(tileSize), GenerateEdge(tileSize) };
		float[][] horizontalEdges = { GenerateEdge(tileSize), GenerateEdge(tileSize) };

		float SampleEdge(float[] arr, float t)
		{
			t = Mathf.Clamp01(t);
			float pos = t * (arr.Length - 1);
			int i0 = Mathf.FloorToInt(pos);
			int i1 = Mathf.Min(i0 + 1, arr.Length - 1);
			return Mathf.Lerp(arr[i0], arr[i1], pos - i0);
		}

		// Reuse a single interior texture instead of creating 16 new ones!
		Texture2D interior = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false); // temporary

		for (int tileIndex = 0; tileIndex < 16; tileIndex++)
		{
			int tileX = tileIndex % tilesPerRow;
			int tileY = tileIndex / tilesPerRow;

			int leftBit = (tileIndex >> 0) & 1;
			int rightBit = (tileIndex >> 1) & 1;
			int topBit = (tileIndex >> 2) & 1;
			int bottomBit = (tileIndex >> 3) & 1;

			// Fill interior (reused)
			FillSeamlessValueNoise(interior, minBright, maxBright, deterministicSeed + tileIndex);

			int borderBlend = Mathf.Max(1, tileSize / 10);

			for (int py = 0; py < tileSize; py++)
			{
				float fy = (float)py / (tileSize - 1f);
				for (int px = 0; px < tileSize; px++)
				{
					float fx = (float)px / (tileSize - 1f);

					float leftVal = SampleEdge(verticalEdges[leftBit], fy);
					float rightVal = SampleEdge(verticalEdges[rightBit], fy);
					float topVal = SampleEdge(horizontalEdges[topBit], fx);
					float bottomVal = SampleEdge(horizontalEdges[bottomBit], fx);

					float distLeft = Mathf.Clamp01((borderBlend - px) / borderBlend);
					float distRight = Mathf.Clamp01((px - (tileSize - borderBlend - 1)) / borderBlend);
					float distTop = Mathf.Clamp01((py - (tileSize - borderBlend - 1)) / borderBlend);
					float distBottom = Mathf.Clamp01((borderBlend - py) / borderBlend);

					float edgeWeight = Mathf.Max(distLeft, distRight, distTop, distBottom);

					float nearestEdge = leftVal * distLeft + rightVal * distRight +
										topVal * distTop + bottomVal * distBottom;
					if (edgeWeight > 0) nearestEdge /= edgeWeight;

					float interiorVal = interior.GetPixel(px, py).r;

					float finalVal = Mathf.Lerp(interiorVal, nearestEdge, edgeWeight);

					Color c = new Color(finalVal, finalVal, finalVal, 1f);
					atlas.SetPixel(tileX * tileSize + px, tileY * tileSize + py, c);
				}
			}
		}

		Object.DestroyImmediate(interior);   // ← critical: clean up the temporary interior

		atlas.Apply();
		return atlas;
	}

	/// <summary>
	/// Fills an existing texture with seamless value noise (no allocation)
	/// </summary>
	private static void FillSeamlessValueNoise(Texture2D tex, float min, float max, int seed)
	{
		int size = tex.width;
		System.Random rnd = new System.Random(seed);
		float[,] noise = new float[size, size];

		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				noise[x, y] = (float)rnd.NextDouble();

		for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
			{
				int x1 = (x + 1) % size;
				int y1 = (y + 1) % size;
				float v = (noise[x, y] + noise[x1, y] + noise[x, y1] + noise[x1, y1]) / 4f;
				float val = Mathf.Lerp(min, max, v);
				tex.SetPixel(x, y, new Color(val, val, val, 1f));
			}

		tex.Apply();
	}
}