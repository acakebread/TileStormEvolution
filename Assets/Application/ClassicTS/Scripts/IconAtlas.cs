using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	[Serializable]
	public class IconAtlas : MassiveHadronLtd.GridIconAtlas, IDisposable
	{
		// Core mapping: index (from ResourceManager.Definitions) → UV rect
		public IconAtlas(
			int cellSize,
			int columns,
			IEnumerable<Definition> filteredDefs,
			bool includeGround = false,
			Color? background = null,
			float yaw = 35f,
			float pitch = 30f)
		{
			CellSize = cellSize;
			Columns = columns;

			var defintionList = filteredDefs.ToList();

			if (defintionList.Count == 0)
			{
				Debug.LogWarning("No valid definitions to render into atlas.");
				return;
			}

			Rows = Mathf.CeilToInt((float)defintionList.Count / columns);

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
				for (int i = 0; i < defintionList.Count; i++)
				{
					var def = defintionList[i];

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
						RectNormalized = uvRect
						//Index = ResourceManager.Definitions.IndexOf(def),
					});
				}
			}

			Texture.Apply(true, false);
			Debug.Log($"IconAtlas created: {width}×{height}, {defintionList.Count} icons placed");
		}
	}
}