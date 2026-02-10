using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	[Serializable]
	public class IconAtlas : MassiveHadronLtd.IGridIconAtlas, IDisposable
	{
		public Texture2D Texture { get; private set; }

		public int CellSize { get; private set; }           // iconSize e.g. 64 or 128
		public int Columns { get; private set; }
		public int Rows { get; private set; }
		public int IconCount => _entries.Count;

		// Optional: if you later want non-square cells
		// public Vector2Int CellSize { get; private set; }

		// Core mapping: index (from ResourceManager.Definitions) → UV rect
		// Or use HashId if you prefer
		private readonly List<AtlasEntry> _entries = new List<AtlasEntry>();

		private struct AtlasEntry
		{
			public int Index;           // original list index in ResourceManager.Definitions
			public Rect RectNormalized; // UV rect [0,1]×[0,1] in atlas
										// public HashId Hash;      // if you want to key by hash instead
		}

		public IconAtlas(
			int cellSize,
			int columns,
			IEnumerable<Definition> definitionsToRender,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			CellSize = cellSize;
			Columns = columns;

			var validDefs = definitionsToRender.ToList();//.Where(d => d != null && !string.IsNullOrEmpty(d.model)) .ToList();

			if (validDefs.Count == 0)
			{
				Debug.LogWarning("No valid definitions to render into atlas.");
				return;
			}

			Rows = Mathf.CeilToInt((float)validDefs.Count / columns);

			int width = columns * cellSize;
			int height = Rows * cellSize;

			Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,  // crisp icons preferred
				wrapMode = TextureWrapMode.Clamp,
				name = "DefinitionIconAtlas"
			};

			// Initialize transparent
			var blank = new Color[width * height];
			Array.Fill(blank, new Color(0, 0, 0, 0));
			Texture.SetPixels(blank);

			for (int i = 0; i < validDefs.Count; i++)
			{
				var def = validDefs[i];

				Texture2D icon = null;
				try
				{
					icon = DefinitionIconRenderUtil.GenerateIcon(
						def,
						size: cellSize,
						background: background,
						yaw: yaw,
						pitch: pitch,
						includeGround: includeGround);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"Failed to generate icon for {def?.name ?? "unknown"}: {ex.Message}");
				}

				int col = i % Columns;
				int row = i / Columns;

				int x = col * cellSize;
				int y = (Rows - 1 - row) * cellSize;  // bottom-up packing

				if (icon != null)
				{
					Texture.SetPixels(x, y, cellSize, cellSize, icon.GetPixels());
					UnityEngine.Object.DestroyImmediate(icon);
				}
				// else → leave the pixels as they are (transparent from initial fill)

				else
				{
					Color[] pixels = new Color[cellSize * cellSize];

					// All transparent by default

					int outerMargin = cellSize / 4;          // 25% inset from edge → frame starts here
					int frameThickness = cellSize / 16;       // ~12.5% thick frame (adjust as needed)

					int innerStart = outerMargin + frameThickness;
					int innerEnd = cellSize - outerMargin - frameThickness;

					Color frameColor = new Color(0.2f, 0.5f, 1.0f, 1.0f);  // visible blue

					for (int py = 0; py < cellSize; py++)
					{
						for (int px = 0; px < cellSize; px++)
						{
							// Check if pixel is inside the frame area (between outerMargin and cellSize-outerMargin)
							bool inFrameHoriz = px >= outerMargin && px < cellSize - outerMargin;
							bool inFrameVert = py >= outerMargin && py < cellSize - outerMargin;

							if (inFrameHoriz && inFrameVert)
							{
								// Now check if it's on the frame border (not in the hollow center)
								bool onTop = py >= outerMargin && py < outerMargin + frameThickness;
								bool onBottom = py >= cellSize - outerMargin - frameThickness && py < cellSize - outerMargin;
								bool onLeft = px >= outerMargin && px < outerMargin + frameThickness;
								bool onRight = px >= cellSize - outerMargin - frameThickness && px < cellSize - outerMargin;

								if (onTop || onBottom || onLeft || onRight)
								{
									pixels[py * cellSize + px] = frameColor;
								}
								// else: inside hollow center → remains transparent
							}
							// else: outside the inset area → remains transparent
						}
					}

					Texture.SetPixels(x, y, cellSize, cellSize, pixels);
				}

				// Always store the entry — even for failed renders
				var uvRect = new Rect(
					(float)x / Texture.width,
					(float)y / Texture.height,
					(float)cellSize / Texture.width,
					(float)cellSize / Texture.height);

				_entries.Add(new AtlasEntry
				{
					Index = ResourceManager.Definitions.IndexOf(def),
					RectNormalized = uvRect
				});
			}

			Texture.Apply(true, false);
			Debug.Log($"IconAtlas created: {width}×{height}, {validDefs.Count} icons placed");
		}

		public bool TryGetUVRect(int definitionIndex, out Rect uvRect)
		{
			uvRect = default;
			var match = _entries.FirstOrDefault(e => e.Index == definitionIndex);
			if (match.Index == definitionIndex) // default struct has Index=0, so check properly
			{
				uvRect = match.RectNormalized;
				return true;
			}
			return false;
		}

		public bool TryGetIndex(Vector2 normalizedUV, out int index)
		{
			index = -1;

			float atlasY = 1f - normalizedUV.y;

			if (normalizedUV.x < 0f || normalizedUV.x > 1f || atlasY < 0f || atlasY > 1f)
				return false;

			float colF = normalizedUV.x * Columns;
			float rowF = atlasY * Rows;

			int col = Mathf.Clamp(Mathf.FloorToInt(colF), 0, Columns - 1);
			int row = Mathf.Clamp(Mathf.FloorToInt(rowF), 0, Rows - 1);

			// Search for entry at this grid position
			foreach (var entry in _entries)
			{
				int entryCol = entry.Index % Columns;
				int entryRow = entry.Index / Columns;

				if (entryCol == col && entryRow == row)
				{
					index = entry.Index;
					return true;
				}
			}

			return false;
		}

		///// <summary>
		///// Attempts to find the original definition index from a normalized UV coordinate [0..1, 0..1].
		///// Returns false if the point is outside any cell or no matching entry exists.
		///// </summary>
		///// <param name="normalizedUV">UV in atlas space (bottom-left = 0,0; top-right = 1,1)</param>
		///// <param name="index">The original index in ResourceManager.Definitions (or -1 if not found)</param>
		///// <returns>true if a valid cell was hit</returns>
		//public bool TryGetIndex(Vector2 normalizedUV, out int index)
		//{
		//	index = -1;

		//	if (normalizedUV.x < 0f || normalizedUV.x > 1f ||
		//		normalizedUV.y < 0f || normalizedUV.y > 1f)
		//	{
		//		return false;
		//	}

		//	// Convert normalized UV → column / row
		//	// Note: atlas UVs are usually bottom-left origin (y=0 at bottom)
		//	float colF = normalizedUV.x * Columns;
		//	float rowF = normalizedUV.y * Rows;          // y increases upwards

		//	int col = Mathf.FloorToInt(colF);
		//	int row = Mathf.FloorToInt(rowF);

		//	// Clamp (in case of floating-point edge cases)
		//	if (col < 0 || col >= Columns || row < 0 || row >= Rows)
		//	{
		//		return false;
		//	}

		//	int candidateIndex = row * Columns + col;

		//	// Now check if this slot actually has an entry (some slots might be empty if defs.Count % Columns != 0)
		//	var entry = _entries.FirstOrDefault(e => GetRowColFromFlatIndex(e.Index) == (row, col));

		//	if (entry.Index >= 0) // found a real entry
		//	{
		//		index = entry.Index;
		//		return true;
		//	}

		//	return false;
		//}

		//private (int row, int col) GetRowColFromFlatIndex(int flatIndex)
		//{
		//	return (flatIndex / Columns, flatIndex % Columns);
		//}

		// Variant: by HashId
		// public bool TryGetUVRect(HashId hash, out Rect uvRect) { ... }

		public void Dispose()
		{
			if (Texture != null)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(Texture);
				else
					UnityEngine.Object.DestroyImmediate(Texture);

				Texture = null;
			}
			_entries.Clear();
		}
	}
}