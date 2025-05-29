using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
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
				if (null == indices && 0 != Stride)
				{
					indices = new();
					for (var i = 0; i < Count; ++i) indices.Add(First + Stride * i);
				}
				return indices;
			}
		}
	}

	public static class TileStripHelper
	{
		public static GameObject SpareTile; // Public static spare tile

		public static TileStrip GetTileStrip(IMapManager map, int startIndex, int stride, bool difficult = false)
		{
			var strip = new TileStrip { First = -1, Count = 0, Stride = 0 };
			var tile = MapManager.GetTile(map, startIndex);
			if (!tile.Interactive) return strip;
			strip.First = startIndex;
			strip.Count = 1;

			if (0 == stride)
				return strip;

			var lastIndex = startIndex;
			while (true)
			{
				tile = MapManager.GetTile(map, lastIndex + stride);
				if (!tile.IsSlide || tile.IsDock || (!difficult && tile.IsRoll)) break;
				lastIndex += stride;
			}

			while (true)
			{
				tile = MapManager.GetTile(map, lastIndex + stride);
				if (!tile.IsRoll) break;
				lastIndex += stride;
			}

			if (!MapManager.GetTile(map, lastIndex).IsRoll)
				return strip;

			var testDock = difficult && MapManager.GetTile(map, lastIndex).IsDock;

			while (true)
			{
				tile = MapManager.GetTile(map, strip.First - stride);
				if (testDock)
				{
					if (!tile.IsDock) break;
				}
				else
				{
					if (!tile.IsSlide) break;
					if (!difficult && !tile.IsDock && !tile.IsRoll) break;
				}
				strip.First -= stride;
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip;
		}

		public static void ResetStrip(IMapManager map, in TileStrip strip)
		{
			if (null == strip.Indices) return;
			foreach (var index in strip.Indices)
			{
				var gameObject = MapManager.GetTile(map, index).GameObject;
				if (null != gameObject)
					gameObject.transform.position = MapManager.TileWorldPosition(map, index);
			}
			if (null != SpareTile)
				SpareTile.SetActive(false);
		}

		public static bool RollStrip(IMapManager map, TileStrip strip, int adjust = 1)
		{
			if (strip.Count <= 1 || null == strip.Indices || null == map.Indices)
				return false;

			ArrayExtensions.RollArray(map.Indices, strip.First, strip.Count, adjust, strip.Stride);

			ResetStrip(map, strip);

			return true;
		}

		public static void TranslateStrip(IMapManager map, in TileStrip strip, in Vector3 delta)
		{
			if (null == strip.Indices) return;

			UpdateSpareTile(map, strip, delta, delta != Vector3.zero);
			foreach (var index in strip.Indices)
			{
				var gameObject = MapManager.GetTile(map, index).GameObject;
				if (null != gameObject)
					gameObject.transform.position += delta;
			}

			static void UpdateSpareTile(IMapManager map, in TileStrip strip, in Vector3 delta, bool active)
			{
				if (!active && null != SpareTile) { SpareTile.SetActive(false); return; }
				if (strip.Count <= 1) return;

				var leadingTileIndex = strip.Indices.Last();
				var leadingTile = MapManager.GetTile(map, leadingTileIndex).GameObject;
				if (null == leadingTile) { if (null != SpareTile) SpareTile.SetActive(false); return; }

				var trailingTileIndex = strip.Indices.First() - strip.Stride;
				var trailingPosition = MapManager.TileWorldPosition(map, trailingTileIndex);

				if (null == SpareTile) SpareTile = GeometryManager.CreateSpareTile(leadingTile, leadingTile.transform.parent, trailingPosition + delta);
				if (null == SpareTile) return;

				var spareRenderer = SpareTile.GetComponent<MeshRenderer>();
				var spareFilter = SpareTile.GetComponent<MeshFilter>();
				var leadingRenderer = leadingTile.GetComponentInChildren<MeshRenderer>();
				var leadingFilter = leadingTile.GetComponentInChildren<MeshFilter>();

				if (leadingRenderer == null || leadingFilter == null || spareRenderer == null || spareFilter == null)
				{
					SpareTile.SetActive(false);
					return;
				}

				spareFilter.sharedMesh = leadingFilter.sharedMesh;
				spareRenderer.material = leadingRenderer.material;
				spareRenderer.transform.rotation = leadingRenderer.transform.rotation;
				spareRenderer.transform.localScale = leadingRenderer.transform.localScale;

				foreach (var collider in SpareTile.GetComponentsInChildren<Collider>())
					Object.Destroy(collider);

				SpareTile.transform.position = trailingPosition + delta;
				SpareTile.SetActive(true);
			}
		}
	}
}
