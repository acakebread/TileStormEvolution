using System;
using System.Linq;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public partial class Map
	{
		public bool RemoveTileAt(Vector3 pos) => UpdateTileAt(pos, new Variant(ResourceManager.DefaultHash)) != -1;

		public int UpdateTileAt(Vector3 pos, Variant variant)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot update tile: map has no tiles array");
				return -1;
			}

			var index = VectorToIndex(pos);
			if (-1 == index)
			{
				Debug.LogWarning($"Cannot update tile at ({pos}) - position out of bounds");
				return -1;
			}

			variant.delta = new Vector3(Mathf.Repeat(pos.x, 1f), pos.y, Mathf.Repeat(pos.z, 1f));

			var tableIndex = this.GetOrCreateVariantIndex(variant.hash, variant.delta, variant.angle);
			if (tiles[index] == tableIndex) return index;

			tiles[index] = tableIndex;
			SyncDoorWaypoints();
			var _graph = graph;
			_graph[index].Dispose();
			//_graph[index] = CreateTile(variants[tableIndex], parent, TileRenderPosition(index));
			_graph[index] = CreateTile(variants[tableIndex], parent, index);
			RefreshAttachments(GetAttachments(tileIndex: index));

			OnMapEdited?.Invoke(this, false, Vector3.zero);

			return index;
		}

		public int InsertTileAt(Vector3 pos, Variant variant)
		{
			var extents = GeomUtils.GetBoundingRect(new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z)), new RectInt(0, 0, width, height));
			if (!ValidExtents(extents)) return -1;
			ResizeMap(extents);
			pos -= new Vector3(extents.x, 0f, extents.y);
			if (-1 == UpdateTileAt(pos, variant)) return -1;
			extents = this.GetContentBounds();
			ResizeMap(extents);
			pos -= new Vector3(extents.x, 0f, extents.y);
			return VectorToIndex(pos);
		}

		public int MoveTile(Vector3 fromPos, Vector3 toPos, Variant variant)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot move tile: map has no tiles array");
				return -1;
			}

			var fromIndex = VectorToIndex(fromPos);
			var toIndex = VectorToIndex(toPos);
			if (fromIndex == -1 || toIndex == -1)
			{
				Debug.LogWarning($"Cannot move tile from ({fromPos}) to ({toPos}) - position out of bounds");
				return -1;
			}

			if (fromIndex == toIndex)
				return UpdateTileAt(toPos, variant);

			variant.delta = new Vector3(Mathf.Repeat(toPos.x, 1f), toPos.y, Mathf.Repeat(toPos.z, 1f));
			var tableIndex = this.GetOrCreateVariantIndex(variant.hash, variant.delta, variant.angle);
			var defaultIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);

			tiles[fromIndex] = defaultIndex;
			tiles[toIndex] = tableIndex;

			RemapWaypointTile(fromIndex, toIndex);
			SyncDoorWaypoints();

			var _graph = graph;
			_graph[fromIndex].Dispose();
			_graph[fromIndex] = CreateTile(variants[defaultIndex], parent, fromIndex);
			_graph[toIndex].Dispose();
			_graph[toIndex] = CreateTile(variants[tableIndex], parent, toIndex);

			RefreshAttachments(GetAttachments(tileIndex: fromIndex));
			RefreshAttachments(GetAttachments(tileIndex: toIndex));

			OnMapEdited?.Invoke(this, false, Vector3.zero);

			return toIndex;
		}

		private bool RepositionAndResize(RectInt extents)
		{
			if (extents.width == width && extents.height == height && extents.x == 0 && extents.y == 0)
				return false;

			if (extents.width > MAP_MAX_SIZE || extents.height > MAP_MAX_SIZE)
				return false;

			var newSize = extents.width * extents.height;

			var defaultIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);

			var newTiles = new int[newSize];
			Array.Fill(newTiles, defaultIndex);

			for (var oldIdx = 0; oldIdx < width * height && oldIdx < tiles.Length; oldIdx++)
			{
				var newPos = Remap(oldIdx);
				if (newPos < 0) continue;

				newTiles[newPos] = tiles[oldIdx];
			}

			if (waypoints != null)
				for (var n = 0; n < waypoints.Length; n++)
					waypoints[n] = Remap(waypoints[n]);

			if (attachments != null)
				foreach (var a in attachments)
					a.tile = Remap(a.tile);

			width = extents.width;
			height = extents.height;
			tiles = newTiles;
			state = Enumerable.Range(0, width * height).ToArray();
			SyncDoorWaypoints();

			return true;

			int Remap(int idx)
			{
				if (idx < 0) return idx;

				var x = idx % width - extents.x;
				var y = idx / width - extents.y;

				return ((uint)x >= extents.width || (uint)y >= extents.height) ? -1 : y * extents.width + x;
			}
		}

		public bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null)
		{
			var resized = RepositionAndResize(this.GetContentBounds());
			var optimised = false;
			if (consolidate) optimised = this.Optimise();
			return resized || optimised;
		}

		public RectInt ContentBounds() => this.GetContentBounds();

		public RectInt MapExtents() => new(0, 0, width - 1, height - 1);

		public RectInt ResizeMap(RectInt extents)
		{
			var w = width;
			var h = height;
			if (RepositionAndResize(extents))
			{
				RecreateTiles();
				RefreshAttachments(GetAttachments());
				OnMapEdited?.Invoke(this, true, new Vector3(-extents.x, 0f, -extents.y));
				return new RectInt(Mathf.FloorToInt(-extents.x), Mathf.FloorToInt(-extents.y), width - w, height - h);
			}
			return RectInt.zero;
		}
	}
}
