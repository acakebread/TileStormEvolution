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

			var validDefs = definitionsToRender.ToList(); // .Where(d => d != null && !string.IsNullOrEmpty(d.model)) .ToList();

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

			// Initialize entire atlas with transparent black
			var blank = new Color32[width * height];
			Array.Fill(blank, new Color32(0, 0, 0, 0));
			Texture.SetPixels32(blank);

			// ─── Use the reusable renderer for the whole atlas ────────────────────────────────
			using (var renderer = new ReusableIconRenderer(
				size: cellSize,
				background: background ?? new Color(0, 0, 0, 0),
				includeGround: includeGround,
				initialYaw: yaw,
				initialPitch: pitch))
			{
				for (int i = 0; i < validDefs.Count; i++)
				{
					var def = validDefs[i];

					Texture2D icon = null;
					try
					{
						icon = renderer.RenderIcon(def);
						// If you ever need per-icon rotation overrides, you can do:
						// icon = renderer.RenderIcon(def, yaw: 45f, pitch: 20f);
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"Failed to render icon for {def?.name ?? "unknown"}: {ex.Message}");
					}

					int col = i % Columns;
					int row = i / Columns;

					int x = col * cellSize;
					int y = (Rows - 1 - row) * cellSize;  // bottom-up packing (Unity texture coords)

					if (icon != null)
					{
						Texture.SetPixels32(x, y, cellSize, cellSize, icon.GetPixels32());
						UnityEngine.Object.DestroyImmediate(icon);
					}
					else
					{
						// Fallback placeholder frame when render fails
						Color32[] pixels = new Color32[cellSize * cellSize];

						int outerMargin = cellSize / 4;
						int frameThickness = cellSize / 16;
						Color32 frameColor = new Color(0.2f, 0.5f, 1.0f, 1.0f); // visible blue

						for (int py = 0; py < cellSize; py++)
						{
							for (int px = 0; px < cellSize; px++)
							{
								bool inFrameHoriz = px >= outerMargin && px < cellSize - outerMargin;
								bool inFrameVert = py >= outerMargin && py < cellSize - outerMargin;

								if (inFrameHoriz && inFrameVert)
								{
									bool onTop = py >= outerMargin && py < outerMargin + frameThickness;
									bool onBottom = py >= cellSize - outerMargin - frameThickness && py < cellSize - outerMargin;
									bool onLeft = px >= outerMargin && px < outerMargin + frameThickness;
									bool onRight = px >= cellSize - outerMargin - frameThickness && px < cellSize - outerMargin;

									if (onTop || onBottom || onLeft || onRight)
									{
										pixels[py * cellSize + px] = frameColor;
									}
								}
							}
						}

						Texture.SetPixels32(x, y, cellSize, cellSize, pixels);
					}

					// Always store the atlas entry — even for failed renders
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