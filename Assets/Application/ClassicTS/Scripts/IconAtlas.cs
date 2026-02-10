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

			var validDefs = definitionsToRender
				.Where(d => d != null && !string.IsNullOrEmpty(d.model))
				.ToList();

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

				var icon = DefinitionIconRenderUtil.GenerateIcon(
					def,
					size: cellSize,
					background: background,
					yaw: yaw,
					pitch: pitch,
					includeGround: includeGround);

				if (icon == null) continue;

				int col = i % columns;
				int row = i / columns;

				// Texture coords: bottom-left = (0,0), flip row
				int x = col * cellSize;
				int y = (Rows - 1 - row) * cellSize;

				Texture.SetPixels(x, y, cellSize, cellSize, icon.GetPixels());

				// Store metadata
				var uvRect = new Rect(
					(float)x / width,
					(float)y / height,
					(float)cellSize / width,
					(float)cellSize / height);

				_entries.Add(new AtlasEntry
				{
					Index = ResourceManager.Definitions.IndexOf(def), // or keep your own index
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