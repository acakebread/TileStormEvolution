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
			if (!tile.IsDrag)
				return strip;

			strip.First = startIndex;
			strip.Count = 1;

			if (stride == 0)
				return strip; // invalid strip as 'fail' condition

			int lastIndex = startIndex;

			// In easy mode, first add all consecutive roll tiles
			if (!difficult && tile.IsRoll)
			{
				while (map.TryGetNextTile(lastIndex, stride, out var nextTile) && nextTile.IsRoll)
					lastIndex += stride;
			}

			// Main drag loop
			while (map.TryGetNextTile(lastIndex, stride, out tile))
			{
				if (!tile.IsDrag || (!difficult && tile.IsRoll)) break;
				lastIndex += stride;
			}

			// Difficult (Roll) extension
			while (difficult)
			{
				if (!map.TryGetNextTile(lastIndex, stride, out tile)) break;
				if (!(tile.IsDrag | tile.IsRoll)) break;
				lastIndex += stride;
			}

			// Fold / Roll extension
			while (map.TryGetNextTile(lastIndex, stride, out tile))
			{
				if (!(tile.IsFold | tile.IsRoll)) break;
				lastIndex += stride;
			}

			var lastTile = map.GetTile(lastIndex);

			// In easy mode, if a roll-led strip has collected a non-roll tail,
			// trim that tail back to the last roll tile before validating.
			if (!difficult && map.GetTile(startIndex).IsRoll)
			{
				while (lastIndex != strip.First && !lastTile.IsRoll)
				{
					lastIndex -= stride;
					lastTile = map.GetTile(lastIndex);
				}
			}

			// Validate ending condition
			if (!(lastTile.IsFold | lastTile.IsRoll))
				return strip; // return invalid strip as 'fail' condition

			// If easy mode and the resolved strip is entirely roll tiles,
			// include the first plain drag tile behind that roll run.
			if (!difficult)
			{
				var allRoll = true;
				for (var index = strip.First; allRoll; index += stride)
				{
					if (!map.GetTile(index).IsRoll)
						allRoll = false;

					if (index == lastIndex)
						break;
				}

				if (allRoll)
				{
					while (map.TryGetNextTile(strip.First, -stride, out tile))
					{
						if (tile.IsRoll)
						{
							strip.First -= stride;
							continue;
						}

						if (tile.IsDrag)
							strip.First -= stride;

						break;
					}
				}
			}

			// Extend backwards from the start
			while (map.TryGetNextTile(strip.First, -stride, out tile))
			{
				if (!(tile.IsFold | tile.IsRoll)) break;
				strip.First -= stride;
			}

			// Extend backwards from the start when difficult
			var testHard = difficult && !lastTile.IsFold;
			while (testHard)
			{
				if (!map.TryGetNextTile(strip.First, -stride, out tile)) break;
				if (tile.IsBake) break;
				strip.First -= stride;
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip; // return draggable strip
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
