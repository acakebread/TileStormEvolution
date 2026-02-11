using UnityEngine;
using System;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface IGridIconAtlas
	{
		Texture2D Texture { get; }
		int CellSize { get; }
		int Columns { get; }
		int Rows { get; }

		// If you need to highlight / pick icons later:
		// bool TryGetUVRect(int index, out Rect uvRect);
		// or bool TryGetUVRect(object key, out Rect uvRect);  // if you use HashId or something
	}

	[Serializable]
	public class GridIconAtlas : IGridIconAtlas, IDisposable
	{
		public Texture2D Texture { get; protected set; }

		public int CellSize { get; protected set; }           // iconSize e.g. 64 or 128
		public int Columns { get; protected set; }
		public int Rows { get; protected set; }
		public int IconCount => _entries.Count;

		// Optional: if you later want non-square cells
		// public Vector2Int CellSize { get; private set; }

		// Core mapping: index (from ResourceManager.Definitions) → UV rect
		protected readonly List<AtlasEntry> _entries = new();

		protected struct AtlasEntry
		{
			public Rect RectNormalized; // UV rect [0,1]×[0,1] in atlas
			//public int Index;//no real need for this
		}

		public GridIconAtlas() { }

		public bool TryGetIndex(Vector2 normalizedUV, out int index)
		{
			index = -1;

			float atlasY = 1f - normalizedUV.y;

			if (normalizedUV.x < 0f || normalizedUV.x >= 1f || atlasY < 0f || atlasY >= 1f)
				return false;

			float colF = normalizedUV.x * Columns;
			float rowF = atlasY * Rows;

			int col = Mathf.FloorToInt(colF);
			int row = Mathf.FloorToInt(rowF);

			if (col < 0 || col >= Columns || row < 0 || row >= Rows || row * Columns + col > IconCount)
				return false;

			index = row * Columns + col;
			return true;
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