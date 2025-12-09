using UnityEngine;
using System;

public static class WangTileGenerator
{
	/// <summary>
	/// Generate a 4x4 Wang tile atlas (16 tiles) using a 2-color edge Wang set.
	/// Each tile is tileSize x tileSize pixels. Edges are guaranteed to match for the same edge-bit values (0/1).
	/// </summary>
	public static Texture2D GenerateWangTileAtlas(int tileSize = 64, int tilesPerRow = 4, float minBright = 0f, float maxBright = 1f, int deterministicSeed = 12345)
	{
		int tileCount = tilesPerRow * tilesPerRow;
		int atlasSize = tileSize * tilesPerRow;

		var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Repeat,
			filterMode = FilterMode.Bilinear,
			hideFlags = HideFlags.HideAndDontSave
		};

		var rnd = new System.Random(deterministicSeed);

		// --- Generate smooth 1D edge noise for state 0 and 1 ---
		float[] GenerateEdge(int size)
		{
			float[] arr = new float[size];
			for (int i = 0; i < size; i++)
				arr[i] = (float)rnd.NextDouble();
			// Simple smoothing
			for (int i = 1; i < size - 1; i++)
				arr[i] = (arr[i - 1] + arr[i] + arr[i + 1]) / 3f;
			return arr;
		}

		float[][] verticalEdges = { GenerateEdge(tileSize), GenerateEdge(tileSize) };
		float[][] horizontalEdges = { GenerateEdge(tileSize), GenerateEdge(tileSize) };

		// --- Sample edge with linear interpolation ---
		float SampleEdge(float[] arr, float t)
		{
			t = Mathf.Clamp01(t);
			float pos = t * (arr.Length - 1);
			int i0 = Mathf.FloorToInt(pos);
			int i1 = Mathf.Min(i0 + 1, arr.Length - 1);
			return Mathf.Lerp(arr[i0], arr[i1], pos - i0);
		}

		// --- Generate each tile ---
		for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
		{
			int tileX = tileIndex % tilesPerRow;
			int tileY = tileIndex / tilesPerRow;

			int leftBit = (tileIndex >> 0) & 1;
			int rightBit = (tileIndex >> 1) & 1;
			int topBit = (tileIndex >> 2) & 1;
			int bottomBit = (tileIndex >> 3) & 1;

			// Interior 2D seamless noise
			Texture2D interior = GenerateSeamlessValueNoise(tileSize, minBright, maxBright, deterministicSeed + tileIndex);

			// Blend width in pixels
			int borderBlend = Mathf.Max(1, tileSize / 10); // ~10% of tile size

			for (int py = 0; py < tileSize; py++)
			{
				float fy = (float)py / (tileSize - 1f);

				for (int px = 0; px < tileSize; px++)
				{
					float fx = (float)px / (tileSize - 1f);

					// Sample edges
					float leftVal = SampleEdge(verticalEdges[leftBit], fy);
					float rightVal = SampleEdge(verticalEdges[rightBit], fy);
					float topVal = SampleEdge(horizontalEdges[topBit], fx);
					float bottomVal = SampleEdge(horizontalEdges[bottomBit], fx);

					// Compute distance to each edge (0=center, 1=edge)
					float distLeft = Mathf.Clamp01((borderBlend - px) / borderBlend);
					float distRight = Mathf.Clamp01((px - (tileSize - borderBlend - 1)) / borderBlend);
					float distTop = Mathf.Clamp01((py - (tileSize - borderBlend - 1)) / borderBlend);
					float distBottom = Mathf.Clamp01((borderBlend - py) / borderBlend);

					// Compute total edge influence weight
					float edgeWeight = Mathf.Max(distLeft, distRight, distTop, distBottom);

					// Select nearest edge value based on distance
					float nearestEdge = leftVal * distLeft + rightVal * distRight + topVal * distTop + bottomVal * distBottom;
					if (edgeWeight > 0) nearestEdge /= edgeWeight; // normalize

					// Interior
					float interiorVal = interior.GetPixel(px, py).r;

					// Blend
					float finalVal = Mathf.Lerp(interiorVal, nearestEdge, edgeWeight);

					Color c = new Color(finalVal, finalVal, finalVal, 1f);
					atlas.SetPixel(tileX * tileSize + px, tileY * tileSize + py, c);
				}
			}
		}

		atlas.Apply();
		return atlas;
	}

	/// <summary>
	/// Generates seamless value noise for the interior of a tile
	/// </summary>
	public static Texture2D GenerateSeamlessValueNoise(int tileSize, float min, float max, int seed = 12345)
	{
		Texture2D tex = new Texture2D(tileSize, tileSize, TextureFormat.RGBA32, false);
		System.Random rnd = new System.Random(seed);

		float[,] noise = new float[tileSize, tileSize];

		// Fill random
		for (int y = 0; y < tileSize; y++)
			for (int x = 0; x < tileSize; x++)
				noise[x, y] = (float)rnd.NextDouble();

		// Make seamless by averaging across edges
		for (int y = 0; y < tileSize; y++)
			for (int x = 0; x < tileSize; x++)
			{
				int x1 = (x + 1) % tileSize;
				int y1 = (y + 1) % tileSize;
				float v = (noise[x, y] + noise[x1, y] + noise[x, y1] + noise[x1, y1]) / 4f;
				float val = Mathf.Lerp(min, max, v);
				tex.SetPixel(x, y, new Color(val, val, val, 1f));
			}

		tex.Apply();
		return tex;
	}
}
