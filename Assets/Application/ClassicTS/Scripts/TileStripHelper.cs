using UnityEngine;
using System.Linq;
using System.Collections.Generic;

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
					if (null == indices && 0 != Stride)
					{
						indices = new();
						for (var i = 0; i < Count; ++i) indices.Add(First + Stride * i);
					}
					return indices;
				}
			}
		}

		public static TileStrip GetTileStrip(IMap map, int startIndex, int stride, bool difficult = false)
		{
			var strip = new TileStrip { First = -1, Count = 0, Stride = 0 };

			var props = map.GetTileProperties(startIndex);
			if (null == props || !props.Interactive)
				return strip;

			strip.First = startIndex;
			strip.Count = 1;

			if (0 == stride)
				return strip;

			var lastIndex = startIndex;
			while (true)
			{
				props = map.GetTileProperties(lastIndex + stride);
				if (null == props || !props.IsSlide || props.IsDock || (!difficult && props.IsRoll)) break;
				lastIndex += stride;
			}

			while (true)
			{
				props = map.GetTileProperties(lastIndex + stride);
				if (null == props || !props.IsRoll) break;
				lastIndex += stride;
			}

			if (!map.GetTileProperties(lastIndex).IsRoll)
				return strip;

			var testDock = difficult && map.GetTileProperties(lastIndex).IsDock;

			while (true)
			{
				props = map.GetTileProperties(strip.First - stride);
				if (null == props) break;
				if (testDock)
				{
					if (!props.IsDock) break;
				}
				else
				{
					if (!props.IsSlide) break;
					if (!difficult && !props.IsDock && !props.IsRoll) break;
				}
				strip.First -= stride;
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip;
		}

		public static void ResetStrip(IMap map, in TileStrip strip)
		{
			if (strip.Indices == null) return;
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTileGameObject(index);
				if (null != gameObject)
					gameObject.transform.position = new Vector3(index % map.Width, 0f, index / map.Width);
			}
			if (null != SpareTile)
				SpareTile.SetActive(false);
		}

		public static bool RollStrip(IMap map, TileStrip strip, int adjust = 1)
		{
			if (strip.Count <= 1 || strip.Indices == null)
				return false;

			var tiles = map.GetTileIndexes();
			if (tiles == null)
				return false;

			ArrayExtensions.RollArray(tiles, strip.First, strip.Count, adjust, strip.Stride);

			ResetStrip(map, strip);

			return true;
		}

		public static void TranslateStrip(IMap map, in TileStrip strip, in Vector3 delta)
		{
			if (null == strip.Indices) return;

			UpdateSpareTile(map, strip, delta, delta != Vector3.zero);
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTileGameObject(index);
				if (null != gameObject)
					gameObject.transform.position += delta;
			}

			static void UpdateSpareTile(IMap map, in TileStrip strip, in Vector3 delta, bool active)
			{
				if (!active && null != SpareTile) { SpareTile.SetActive(false); return; }
				if (strip.Count <= 1) return;

				var leadingTileIndex = strip.Indices.Last();
				var leadingTile = map.GetTileGameObject(leadingTileIndex);
				if (null == leadingTile) return;

				var trailingTileIndex = strip.Indices.First() - strip.Stride;
				var trailingPosition = new Vector3(trailingTileIndex % map.Width, 0f, trailingTileIndex / map.Width);

				if (null == SpareTile) SpareTile = GeometryManager.CreateSpareTile(leadingTile, leadingTile.transform.parent, trailingPosition + delta);
				if (null == SpareTile) return;

				var spareRenderer = SpareTile.GetComponent<MeshRenderer>();
				var spareFilter = SpareTile.GetComponent<MeshFilter>();
				var leadingRenderer = leadingTile?.GetComponentInChildren<MeshRenderer>();
				var leadingFilter = leadingTile?.GetComponentInChildren<MeshFilter>();

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
