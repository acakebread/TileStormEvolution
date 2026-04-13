using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public interface IGridAtlas
	{
		Texture2D Texture { get; }
		int CellSize { get; }
		int Columns { get; }
		int Rows { get; }
		bool IsBuildComplete { get; }
	}

	[Serializable]
	public abstract class GridAtlas : IGridAtlas, IDisposable
	{
		public Texture2D Texture { get; protected set; }
		public int CellSize { get; protected set; }
		public int Columns { get; protected set; }
		public int Rows { get; protected set; }
		public int IconCount => _entries.Count;
		public bool IsBuildComplete => _nextIndex >= _pendingItems?.Count && !_isBuilding;

		protected readonly List<AtlasEntry> _entries = new List<AtlasEntry>();

		private List<object> _pendingItems;
		private IDisposable _rendererInstance;
		private int _nextIndex;
		private bool _isBuilding;

		protected struct AtlasEntry
		{
			public Rect RectNormalized;
		}

		protected GridAtlas() { }

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

			// Create texture immediately (transparent)
			Texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "GridIconAtlas"
			};

			var blank = new Color32[w * h];
			Array.Fill(blank, new Color32(0, 0, 0, 0));
			Texture.SetPixels32(blank);
			Texture.Apply();                     // ready for UI instantly

			// Pre-calculate ALL UV rects so TryGetUVRect / TryGetIndex work immediately
			_entries.Clear();
			for (int i = 0; i < itemList.Count; i++)
			{
				int col = i % columns;
				int rowFromTop = i / columns;
				int y = (Rows - 1 - rowFromTop) * cellSize;
				int x = col * cellSize;

				float normX = (float)x / w;
				float normY = (float)y / h;
				float normW = (float)cellSize / w;
				float normH = (float)cellSize / h;

				_entries.Add(new AtlasEntry { RectNormalized = new Rect(normX, normY, normW, normH) });
			}

			// Store data for progressive build
			_pendingItems = itemList;
			_rendererInstance = CreateRenderer(cellSize, background ?? new Color(0, 0, 0, 0));
			_nextIndex = 0;
			_isBuilding = true;

			Debug.Log($"Atlas created instantly: {w}×{h} with {itemList.Count} slots. Progressive build started.");
		}

		/// <summary>
		/// Call this with StartCoroutine right after creating the atlas.
		/// Fills icons gradually without freezing WebGL.
		/// </summary>
		public IEnumerator BuildIconsCoroutine(int iconsPerFrame = 2)
		{
			if (_pendingItems == null || !_isBuilding)
				yield break;

			while (_nextIndex < _pendingItems.Count)
			{
				int batch = Mathf.Min(iconsPerFrame, _pendingItems.Count - _nextIndex);

				for (int b = 0; b < batch; b++)
				{
					int i = _nextIndex;
					Texture2D icon = null;

					try
					{
						icon = GenerateIcon(_rendererInstance, _pendingItems[i], i);
					}
					catch (Exception ex)
					{
						Debug.LogWarning($"Icon {i} failed: {ex.Message}");
					}

					if (icon != null)
					{
						int col = i % Columns;
						int rowFromTop = i / Columns;
						int y = (Rows - 1 - rowFromTop) * CellSize;
						int x = col * CellSize;

						Texture.SetPixels32(x, y, CellSize, CellSize, icon.GetPixels32());
						UnityEngine.Object.DestroyImmediate(icon);
					}

					_nextIndex++;
				}

				Texture.Apply(false, false);   // update GPU
				//yield return null;             // wait one frame → smooth in WebGL
				yield return null;           // one frame per batch
				yield return new WaitForEndOfFrame();  // ← add this if still hitching
			}

			// Final cleanup
			Texture.Apply(false, false);
			_isBuilding = false;

			if (_rendererInstance != null)
			{
				_rendererInstance.Dispose();
				_rendererInstance = null;
			}
			_pendingItems = null;

			Debug.Log("Atlas build completed progressively.");
		}

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
			if (_rendererInstance != null)
			{
				_rendererInstance.Dispose();
				_rendererInstance = null;
			}

			if (Texture != null)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(Texture);
				else
					UnityEngine.Object.DestroyImmediate(Texture);
				Texture = null;
			}

			_entries.Clear();
			_pendingItems = null;
			_nextIndex = 0;
			_isBuilding = false;
		}
	}
}