using System;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace ClassicTilestorm
{
	public partial class Map
	{
		[JsonIgnore] private int[] state;

		[JsonIgnore] private int graphCount => graph.Length;
		[JsonIgnore] private Tile[] _graph;
		[JsonIgnore]
		private Tile[] graph
		{
			get
			{
				if (_graph != null)
					return _graph;

				if (tiles == null || tiles.Length != width * height)
				{
					Debug.LogError($"Invalid tile map data! length={(tiles?.Length ?? -1)}, expected={width * height}");
					return Array.Empty<Tile>();
				}

				_graph = new Tile[width * height];

				for (var visualIndex = 0; visualIndex < _graph.Length; visualIndex++)
				{
					_graph[visualIndex] = CreateTile(variants[tiles[visualIndex]], parent, visualIndex);
#if DEBUG
					UpdateGraphTileInfo(visualIndex);
#endif
				}

				return _graph;
			}

			set
			{
				if (null != _graph) foreach (var iter in _graph) iter.Dispose();
				_graph = value;
			}
		}

		private void UpdateGraph()
		{
			if (state == null || graphCount != state.Length)
				return;

			for (var visualIndex = 0; visualIndex < state.Length; ++visualIndex)
			{
				var logicalIndex = state[visualIndex];
				if (logicalIndex < 0 || logicalIndex >= _graph.Length) continue;

				var mapTile = _graph[logicalIndex];
				var go = mapTile.gameObject;
				if (go == null) continue;

				go.transform.position = TileRenderPosition(visualIndex) + variants[tiles[visualIndex]].delta;
#if DEBUG
				UpdateGraphTileInfo(State[visualIndex]);
#endif
			}
		}

		internal bool InitialiseGraph() => graph?.Length > 0;

		internal void InvalidateGraphCache() => DestroyAllGraphTiles();

		private Tile GetGraphTile(int graphIndex) => _graph == null || graphIndex < 0 || graphIndex >= _graph.Length ? default : _graph[graphIndex];
		private void DestroyAllGraphTiles() => graph = null;

		private void UpdateGraphTileInfo(int index)
		{
			var go = _graph[index].gameObject;
			if (null == go) return;
			var variant = GetVariantForIndex(index);
			var def = ResourceManager.GetDefinition(variant.hash);
			go.name = $"{def?.name ?? "??"} ({go.transform.position.x:F1},{go.transform.position.z:F1})+{variant.delta:F2}@{variant.angle:F1}deg";
		}

		private Tile CreateTile(Variant variant, Transform parent, int visualIndex)
		{
			Vector3 renderPosition = TileRenderPosition(visualIndex);

			var tile = new Tile(variant, parent, renderPosition);

			if (tile.gameObject != null)
				AttachPickColliders(tile.gameObject, mapRoot: parent != null ? parent.GetComponent<MapRoot>()?.Map : null, visualIndex);

			return tile;
		}

		internal static void AttachPickColliders(GameObject root, Map mapRoot, int visualIndex = -1, int logicalIndexOverride = -1)
		{
			if (root == null) return;
			if (mapRoot == null)
			{
				Debug.LogWarning("Tile created without MapRoot component on parent.");
				return;
			}

			// Add collider info
			var info = root.GetComponent<TileColliderInfo>() ?? root.AddComponent<TileColliderInfo>();
			info.Map = mapRoot;
			info.VisualIndex = visualIndex;
			info.LogicalIndexOverride = logicalIndexOverride;

			var attachedAny = false;

			foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
			{
				attachedAny |= EnsurePickCollider(meshFilter.gameObject, meshFilter.sharedMesh);
			}

			foreach (var skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				attachedAny |= EnsurePickCollider(skinnedRenderer.gameObject, skinnedRenderer.sharedMesh, skinnedRenderer.localBounds);
			}

			if (!attachedAny)
				Debug.LogWarning($"No mesh found on tile at visualIndex {visualIndex} - skipping collider.");
		}

		private static bool EnsurePickCollider(GameObject target, Mesh mesh, Bounds? fallbackBounds = null)
		{
			if (target == null) return false;

			// If the prefab already supplies a collider, keep using it.
			if (target.GetComponent<Collider>() != null)
				return true;

			if (mesh != null && mesh.isReadable)
			{
				var meshCollider = target.GetComponent<MeshCollider>();
				if (meshCollider == null)
					meshCollider = target.AddComponent<MeshCollider>();

				meshCollider.sharedMesh = mesh;
				meshCollider.convex = false;
				return true;
			}

			if (mesh == null && fallbackBounds == null)
				return false;

			var bounds = fallbackBounds ?? mesh.bounds;
			var boxCollider = target.GetComponent<BoxCollider>();
			if (boxCollider == null)
				boxCollider = target.AddComponent<BoxCollider>();

			boxCollider.center = bounds.center;
			boxCollider.size = bounds.size;
			return true;
		}

		private void RecreateTiles()
		{
			DestroyAllGraphTiles();
			InitialiseGraph();
		}

		public void RefreshGeometry()
		{
			RecreateTiles();

			if (graphCount == 0)
			{
				Debug.LogError("RefreshGeometry failed - could not recreate tiles.");
				return;
			}

			RefreshAttachments(GetAttachments());
		}

		public void Preset()
		{
			state = Enumerable.Range(0, width * height).ToArray();
			UpdateGraph();
		}

		public void Scramble()
		{
			if (state == null)
				state = Enumerable.Range(0, width * height).ToArray();

			const int iterations = 1;
			for (var n = 0; n < state.Length * iterations; ++n)
			{
				var stride = (UnityEngine.Random.value > 0.5f ? width : 1) * (UnityEngine.Random.value > 0.5f ? 1 : -1);

				var tileStrip = TileStripHelper.GetTileStrip(this, n % state.Length, stride, true);
				TileStripHelper.RollStrip(this, tileStrip);
			}
			UpdateGraph();
		}

		public void Solve()
		{
			state = Enumerable.Range(0, width * height).Select(n => n + (solve?[n] ?? 0)).ToArray();
			UpdateGraph();
		}

		public Tile GetTile(Vector3 pos, bool logicalIndex = true) => GetTile(VectorToIndex(pos), logicalIndex);
		public Tile GetTile(int index, bool logicalIndex = true) => state == null || index < 0 || index >= state.Length ? default : GetGraphTile(logicalIndex ? state[index] : index);
	}
}
