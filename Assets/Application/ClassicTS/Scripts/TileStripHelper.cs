using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class TileStripHelper
	{
		public static GameObject SpareTile; // Public static spare tile

		public struct TileStrip
		{
			public int First;
			public int Count;
			public int Stride;

			public readonly int Last => First + Stride * (Count - 1);

			private List<int> indices;
			public List<int> Indices
			{
				get
				{
					if (indices == null && Stride != 0)
					{
						indices = new();
						for (var i = 0; i < Count; ++i) indices.Add(First + Stride * i);
					}
					return indices;
				}
			}
		}

		public static TileStrip GetTileStrip(IMap map, int startIndex, int directionFlag)
		{
			var strip = new TileStrip { First = -1, Count = 0, Stride = 0 };

			var startProps = map.GetTileProperties(startIndex);
			if (startProps == null || !startProps.Interactive)
				return strip;

			strip.First = startIndex;
			strip.Count = 1;

			if (directionFlag == 0)
				return strip;

			var stride = 0;
			var (dx, dz) = TileProperties.GetDirectionOffset(directionFlag);
			if (dx != 0) stride = dx;
			else if (dz != 0) stride = dz * map.Width;

			var lastIndex = startIndex;
			while (true)
			{
				var nextProps = map.GetTileProperties(lastIndex + stride);
				if (nextProps == null || !nextProps.IsSlide || nextProps.IsDock) break;
				lastIndex += stride;
			}

			while (true)
			{
				var nextProps = map.GetTileProperties(lastIndex + stride);
				if (nextProps == null || !nextProps.IsDock) break;
				lastIndex += stride;
			}

			while (true)
			{
				var nextProps = map.GetTileProperties(lastIndex + stride);
				if (nextProps == null || !nextProps.IsRoll) break;
				lastIndex += stride;
			}

			if (!map.GetTileProperties(lastIndex).IsRoll)
				return strip;

			if (map.GetTileProperties(lastIndex).IsDock)
			{
				while (true)
				{
					var nextProps = map.GetTileProperties(strip.First - stride);
					if (nextProps == null || !nextProps.IsDock) break;
					strip.First -= stride;
				}
			}
			else
			{
				while (true)
				{
					var nextProps = map.GetTileProperties(strip.First - stride);
					if (nextProps == null || !nextProps.IsSlide) break;
					strip.First -= stride;
				}
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip;
		}

		public static bool RollStrip(IMap map, TileStrip strip, int adjust = 1)
		{
			if (strip.Count <= 1 || strip.Indices == null)
				return false;

			var tiles = map.GetTiles();
			if (tiles == null)
				return false;

			ArrayExtensions.RollArray(tiles, strip.First, strip.Count, adjust, strip.Stride);

			ResetStrip(map, strip);

			return true;
		}

		public static void TranslateStrip(IMap map, in TileStrip strip, in Vector3 delta)
		{
			if (strip.Indices == null) return;

			UpdateSpareTile(map, strip, delta, delta != Vector3.zero);
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTileGameObject(index);
				if (gameObject != null)
					gameObject.transform.position += delta;
			}
		}

		public static void ResetStrip(IMap map, in TileStrip strip)
		{
			if (strip.Indices == null) return;
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTileGameObject(index);
				if (gameObject != null)
					gameObject.transform.position = new Vector3(index % map.Width, 0f, index / map.Width);
			}
		}

		private static void UpdateSpareTile(IMap map, in TileStrip strip, in Vector3 delta, bool active)
		{
			if (!active)
			{
				if (SpareTile != null)
					SpareTile.SetActive(false);
				return;
			}

			if (strip.Count <= 1)
				return;

			var leadingTileIndex = strip.Indices.Last();
			var leadingTile = map.GetTileGameObject(leadingTileIndex);

			var trailingTileIndex = strip.Indices.First() - strip.Stride;
			var trailingPosition = map.GetTileCoordinates(trailingTileIndex).ToPosition();

			if (SpareTile == null)
			{
				SpareTile = new GameObject("SpareTile");
				SpareTile.transform.SetParent(map.GetMapRoot().transform, false);
				SpareTile.AddComponent<MeshFilter>();
				SpareTile.AddComponent<MeshRenderer>();
			}

			var leadingRenderer = leadingTile?.GetComponentInChildren<MeshRenderer>();
			var leadingFilter = leadingTile?.GetComponentInChildren<MeshFilter>();
			var spareRenderer = SpareTile?.GetComponent<MeshRenderer>();
			var spareFilter = SpareTile?.GetComponent<MeshFilter>();

			if (leadingRenderer != null && leadingFilter != null && spareRenderer != null && spareFilter != null)
			{
				spareFilter.sharedMesh = leadingFilter.sharedMesh;
				spareRenderer.material = leadingRenderer.material;
				spareRenderer.transform.rotation = leadingRenderer.transform.rotation;
				spareRenderer.transform.localScale = leadingRenderer.transform.localScale;
			}
			else
			{
				SpareTile.SetActive(false);
				return;
			}

			foreach (var collider in SpareTile.GetComponentsInChildren<Collider>()) Object.Destroy(collider);

			SpareTile.transform.position = trailingPosition + delta;
			SpareTile.SetActive(true);
		}
	}
}