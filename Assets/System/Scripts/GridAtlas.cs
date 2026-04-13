using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public interface IGridAtlas
	{
		Texture2D Texture { get; }
		int CellSize { get; }
		int Columns { get; }
		int Rows { get; }
	}

	[Serializable]
	public abstract class GridAtlas : IGridAtlas, IDisposable
	{
		// All the properties & fields as before...
		public Texture2D Texture { get; protected set; }
		public int CellSize { get; protected set; }
		public int Columns { get; protected set; }
		public int Rows { get; protected set; }
		public int IconCount => _entries.Count;

		protected readonly List<AtlasEntry> _entries = new();

		protected struct AtlasEntry { public Rect RectNormalized; }

		// Parameterless — derived classes will call Initialize themselves
		protected GridAtlas() { }

		/// <summary>
		/// This constructor is not usable because GridIconAtlas is abstract.
		/// Use a concrete derived class (IconAtlas, etc.).
		/// </summary>
		[Obsolete("Use a concrete derived implementation")]
		protected GridAtlas(int cellSize, int columns, IEnumerable<object> items, Color? background = null)
		{
			throw new NotSupportedException("Cannot instantiate abstract GridIconAtlas directly");
		}

		protected void Initialize(
			int cellSize,
			int columns,
			IEnumerable<object> itemsToRender,
			Color? background = null)
		{
			if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));

			CellSize = cellSize;
			Columns = columns;

			var itemList = itemsToRender?.ToList() ?? new List<object>();
			if (itemList.Count == 0)
			{
				Debug.LogWarning("No items to render.");
				return;
			}

			Rows = Mathf.CeilToInt((float)itemList.Count / columns);

			int w = columns * cellSize;
			int h = Rows * cellSize;

			Texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "GridIconAtlas"
			};

			var blank = new Color32[w * h];
			Array.Fill(blank, new Color32(0, 0, 0, 0));
			Texture.SetPixels32(blank);

			using var renderer = CreateRenderer(cellSize, background ?? new Color(0, 0, 0, 0));

			for (var i = 0; i < itemList.Count; i++)
			{
				Texture2D icon = null;
				try
				{
					icon = GenerateIcon(renderer, itemList[i], i);
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"Icon {i} failed: {ex.Message}");
				}

				var col = i % columns;
				var rowFromTop = i / columns;
				var y = (Rows - 1 - rowFromTop) * cellSize;
				var x = col * cellSize;

				if (icon != null)
				{
					Texture.SetPixels32(x, y, cellSize, cellSize, icon.GetPixels32());
					UnityEngine.Object.DestroyImmediate(icon);
				}
				else
				{
					Debug.LogError("no icon to copy");
				}

				_entries.Add(new AtlasEntry { RectNormalized = new((float)x / w, (float)y / h, (float)cellSize / w, (float)cellSize / h) });
			}

			Texture.Apply(true, false);
			Debug.Log($"Atlas built: {w}×{h}, {itemList.Count} slots");
		}

		// Must be implemented by derived class
		protected abstract IDisposable CreateRenderer(int cellSize, Color background);

		protected abstract Texture2D GenerateIcon(IDisposable renderer, object item, int index);

		public bool TryGetUVRect(int index, out Rect uvRect)
		{
			uvRect = default;
			if (index < 0 || index >= _entries.Count) return false;
			uvRect = _entries[index].RectNormalized;
			return true;
		}

		public bool TryGetIndex(Vector2 normalizedUV, out int index)
		{
			index = -1;
			float ay = 1f - normalizedUV.y;
			if (normalizedUV.x < 0f || normalizedUV.x >= 1f || ay < 0f || ay >= 1f)
				return false;

			int col = Mathf.FloorToInt(normalizedUV.x * Columns);
			int row = Mathf.FloorToInt(ay * Rows);

			if (col < 0 || col >= Columns || row < 0 || row >= Rows) return false;

			index = row * Columns + col;
			return index < IconCount;
		}

		public virtual void Dispose()
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
