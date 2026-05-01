using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;

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

		public static TileStrip GetTileStrip(IMapPlay map, int startIndex, int stride, bool difficult = false)
		{
			var strip = new TileStrip { First = -1, Count = 0, Stride = 0 };
			var tile = map.GetTile(startIndex);
			if (!tile.IsDrag) return strip;
			strip.First = startIndex;
			strip.Count = 1;

			if (0 == stride)
				return strip;//return invalid strip as 'fail' condition

			var lastIndex = startIndex;
			while (true)
			{
				if (!ValidNextTile(lastIndex, stride)) break;
				tile = map.GetTile(lastIndex + stride);
				if (!tile.IsDrag) break;//skip all movable tiles
				lastIndex += stride;
			}

			while (difficult)
			{
				if (!ValidNextTile(lastIndex, stride)) break;
				tile = map.GetTile(lastIndex + stride);
				if (!(tile.IsDrag | tile.IsRoll)) break;
				lastIndex += stride;
			}

			while (true)
			{
				if (!ValidNextTile(lastIndex, stride)) break;
				tile = map.GetTile(lastIndex + stride);
				if (!(tile.IsFold | tile.IsRoll)) break;
				lastIndex += stride;
			}

			var lastTile = map.GetTile(lastIndex);
			if (!(lastTile.IsFold | lastTile.IsRoll))
				return strip;//return invalid strip as 'fail' condition

			while (true)
			{
				if (!ValidNextTile(strip.First, -stride)) break;
				tile = map.GetTile(strip.First - stride);
				if (!(tile.IsFold | tile.IsRoll)) break;
				strip.First -= stride;
			}

			var testRoll = difficult && map.GetTile(lastIndex).IsRoll;
			while (testRoll)
			{
				if (!ValidNextTile(strip.First, -stride)) break;
				tile = map.GetTile(strip.First - stride);
				if (!(tile.IsDrag | tile.IsFold | tile.IsRoll)) break;
				strip.First -= stride;
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip;//return draggable strip

			bool ValidNextTile(int index, int delta)
			{
				var x = (index % map.Width) + (delta % map.Width);
				var y = (index / map.Width) + (delta / map.Width);
				return x >= 0 && x < map.Width && y >= 0 && y < map.Height;
			}
		}

		public static void ResetStrip(IMapPlay map, in TileStrip strip)
		{
			if (null == strip.Indices) return;
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTile(index).gameObject;
				if (null != gameObject)
					gameObject.transform.position = map.TileRenderPosition(index);
			}
			if (null != SpareTile)
				SpareTile.SetActive(false);
		}

		public static bool RollStrip(IMapPlay map, TileStrip strip, int adjust = 1)
		{
			if (strip.Count <= 1 || null == strip.Indices || null == map.State)
				return false;

			ArrayExtensions.RollArray(map.State, strip.First, strip.Count, adjust, strip.Stride);

			ResetStrip(map, strip);

			return true;
		}

		public static void TranslateStrip(IMapPlay map, in TileStrip strip, in Vector3 delta)
		{
			if (null == strip.Indices) return;

			UpdateSpareTile(map, strip, delta, delta != Vector3.zero);
			foreach (var index in strip.Indices)
			{
				var gameObject = map.GetTile(index).gameObject;
				if (null != gameObject)
					gameObject.transform.position += delta;
			}

			static void UpdateSpareTile(IMapPlay map, in TileStrip strip, in Vector3 delta, bool active)
			{
				if (!active && null != SpareTile) { SpareTile.SetActive(false); return; }
				if (strip.Count <= 1) return;

				var leadingTileIndex = strip.Indices.Last();
				var leadingTile = map.GetTile(leadingTileIndex).gameObject;
				if (null == leadingTile) { if (null != SpareTile) SpareTile.SetActive(false); return; }

				var trailingTileIndex = strip.Indices.First() - strip.Stride;
				var trailingPosition = map.TileRenderPosition(trailingTileIndex);

				if (null == SpareTile) SpareTile = GeometryFactory.CreateSpareTile(leadingTile, leadingTile.transform.parent, trailingPosition + delta);
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
