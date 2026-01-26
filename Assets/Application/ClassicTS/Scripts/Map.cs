using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[Serializable]
	public struct Variant
	{
		public HashId hash;           // the core tile definition ID
		public float angle;           // degrees, usually 0/90/180/270
		public float delta;           // local position offset

		// Optional: constructor helpers
		public Variant(HashId h) : this(h, 0f, 0f) { }
		public Variant(HashId h, float rotationDegrees, float offset)
		{
			hash = h;
			angle = rotationDegrees;
			delta = offset;
		}

		// Helper to make migration easier
		public static implicit operator HashId(Variant v) => v.hash;
	}

	public interface IMapData
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] Indices { get; }
	}

	public interface IMapPlay : IMapData
	{
		public string Music { get; set; }
		public string Skybox { get; set; }

		int WorldToMapIndex(Vector3 _);
		Vector3 TileWorldPosition(int _);

		Tile GetTile(int _);

		int GetStartTile();
		int GetEndTile();
		int FindAdjacentConsole(int _);

		MapAttachment[] GetAttachments(int? tileIndex = null, Type[] filterTypes = null);
	}

	public interface IMapEdit : IMapPlay
	{
		Action<Map, bool, Vector3> OnMapEdited { get; set; }

		Quaternion LocalRotation(int tileIndex, Quaternion worldRotation);
		Quaternion WorldRotation(int tileIndex, Quaternion localRotation);

		Vector3 LocalPosition(int tileIndex, Vector3 worldPosition);
		Vector3 WorldPosition(int tileIndex, Vector3 localPosition);

		HashId GetTileID(int _);
		bool UpdateTileAt(int x, int z, HashId hashId);

		void AddAttachment(MapAttachment _);
		bool RemoveAttachment(MapAttachment _);
		bool RemoveAttachments(MapAttachment[] _);
		void RefreshAttachment(MapAttachment _);

		int CameraHitTile(Camera camera, Vector3 position);
		Bounds GetTileGeometryBounds(int _);
	}

	[Serializable]
	public class Map : IMapEdit, Map.IVariantAccess
	{
		// ─────────────────────────────────────────────
		// Core identity
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 1)] public string name;
		[JsonProperty(Order = 2)] public string character;
		[JsonProperty(Order = 3)] public string music;
		[JsonProperty(Order = 4)] public string skybox;
		[JsonProperty(Order = 5)] public string button;

		// ─────────────────────────────────────────────
		// Dimensions
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 10)] public int width;
		[JsonProperty(Order = 11)] public int height;

		[JsonProperty(Order = 21)] public int[] tiles;// seed indices
		[JsonProperty(Order = 22)] public int[] solve;// delta
		[JsonProperty(Order = 23)] public int[] waypoints;

		[JsonProperty(Order = 30)] public MapAttachment[] attachments;

		// Conditional serialization
		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		public Action<Map, bool, Vector3> OnMapEdited { get; set; }
		[JsonIgnore] private Transform parent;

		[JsonIgnore] public int Width => width;
		[JsonIgnore] public int Height => height;
		[JsonIgnore] public int Count => Width * Height;
		[JsonIgnore] public int[] Indices => state;
		[JsonIgnore] public string Music { get => music; set => music = value; }
		[JsonIgnore] public string Skybox { get => skybox; set => skybox = value; }

		// ─────────────────────────────────────────────
		// Tile hashes — for definitions
		// ─────────────────────────────────────────────

		//[JsonIgnore] private HashId[] hashes;// runtime int copy of table, never serialized
		//internal interface IHashAccess { HashId[] Hashes { get; set; } }
		//HashId[] IHashAccess.Hashes { get => hashes; set => hashes = value; }

		[JsonIgnore]
		public Variant[] variants = Array.Empty<Variant>();

		// Compatibility layer - exact same name, now a property
		[JsonIgnore]
		private HashId[] hashes
		{
			get
			{
				if (variants == null || variants.Length == 0)
					return Array.Empty<HashId>();

				var arr = new HashId[variants.Length];
				for (int i = 0; i < variants.Length; i++)
					arr[i] = variants[i].hash;
				return arr;
			}

			// ← This is the missing piece
			set
			{
				if (value == null)
				{
					variants = Array.Empty<Variant>();
				}
				else
				{
					variants = new Variant[value.Length];
					for (int i = 0; i < value.Length; i++)
					{
						variants[i] = new Variant(value[i]);   // angle=0, delta=zero
					}
				}

				// Invalidate anything derived from the tile list
				_graph = null;
				// If you have other caches depending on hashes/variants → clear them here too
			}
		}

		// ─────────────────────────────────────────────
		// Recommended clean access for future code
		// ─────────────────────────────────────────────
		internal interface IVariantAccess
		{
			Variant[] Variants { get; set; }
		}

		Variant[] IVariantAccess.Variants
		{
			get => variants;
			set
			{
				variants = value ?? Array.Empty<Variant>();
				_graph = null;
			}
		}

		private Variant GetVariantForIndex(int mapIndex)
		{
			if (tiles == null || variants == null || mapIndex < 0 || mapIndex >= tiles.Length)
				return new Variant(0);

			int tableIdx = tiles[mapIndex];
			if (tableIdx >= 0 && tableIdx < variants.Length)
				return variants[tableIdx];

			return new Variant(0);
		}

		// ─────────────────────────────────────────────
		// Runtime tile (graph) instances (lazy / just-in-time)
		// ─────────────────────────────────────────────

		[JsonIgnore] private int[] state; // runtime permutation, never serialized
		[JsonIgnore] private Tile[] _graph; // private backing field (never serialized)
											//[JsonIgnore]
											//private Tile[] graph
											//{
											//	get
											//	{
											//		if (_graph != null)
											//			return _graph;

		//		if (tiles == null || tiles.Length != width * height)
		//		{
		//			Debug.LogError($"Invalid tile map data! length={(tiles?.Length ?? -1)}, expected={width * height}");
		//			return Array.Empty<Tile>();
		//		}

		//		_graph = new Tile[width * height];

		//		string mapName = name ?? "Unnamed map";

		//		for (int index = 0; index < tiles.Length; index++)
		//		{
		//			int idx = tiles[index];
		//			Variant variant = default;

		//			if (idx >= 0 && idx < variants.Length)
		//			{
		//				variant = variants[idx];  // ← Get the full Variant, not just hash
		//			}
		//			else if (idx != -1)
		//			{
		//				Debug.LogWarning($"Out-of-range table index {idx} at tile {index} (map: {mapName})");
		//				variant = new Variant(0);  // fallback to empty
		//			}

		//			// ── Pass the full Variant to Tile constructor ──────────────────
		//			_graph[index] = new Tile(variant, parent, TileWorldPosition(index));
		//		}

		//		return _graph;
		//	}
		//}

		//[JsonIgnore]
		//private Tile[] graph
		//{
		//	get
		//	{
		//		if (_graph != null)
		//			return _graph;

		//		if (tiles == null || tiles.Length != width * height)
		//		{
		//			Debug.LogError($"Invalid tile map data! length={(tiles?.Length ?? -1)}, expected={width * height}");
		//			return Array.Empty<Tile>();
		//		}

		//		_graph = new Tile[width * height];

		//		string mapName = name ?? "Unnamed map";

		//		for (int visualIndex = 0; visualIndex < tiles.Length; visualIndex++)
		//		{
		//			// Apply current permutation (state)
		//			int logicalIndex = (state != null && visualIndex < state.Length)
		//				? state[visualIndex]
		//				: visualIndex;

		//			int tableIdx = tiles[logicalIndex];
		//			Variant variant = default;

		//			if (tableIdx >= 0 && tableIdx < variants.Length)
		//			{
		//				variant = variants[tableIdx];
		//			}
		//			else if (tableIdx != -1)
		//			{
		//				Debug.LogWarning($"Out-of-range table index {tableIdx} at visual pos {visualIndex} (map: {mapName})");
		//				variant = new Variant(0);
		//			}

		//			_graph[visualIndex] = new Tile(variant, parent, TileWorldPosition(visualIndex));
		//		}

		//		return _graph;
		//	}
		//}

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

				string mapName = name ?? "Unnamed map";

				// Use current state for permutation, fallback to identity if missing
				int[] perm = state != null && state.Length == width * height
					? state
					: Enumerable.Range(0, width * height).ToArray();

				for (int visualIndex = 0; visualIndex < _graph.Length; visualIndex++)
				{
					int logicalIndex = perm[visualIndex];

					int tableIdx = tiles[logicalIndex];
					Variant variant = GetVariantForIndex(logicalIndex);

					_graph[visualIndex] = new Tile(variant, parent, TileWorldPosition(visualIndex));

#if DEBUG
					// Rich debug name — applied right after creation
					var go = _graph[visualIndex].gameObject;
					if (go != null)
					{
						Vector3 basePos = TileWorldPosition(visualIndex);
						Vector3 displayPos = basePos + new Vector3(0f, variant.delta, 0f);
						displayPos -= tile_origin;

						var def = ResourceManager.GetDefinition(variant.hash);
						go.name = $"{def?.name ?? "??"} ({displayPos.x},{displayPos.z})+{variant.delta:F2}@{variant.angle:F1}°";
					}
#endif
				}

				return _graph;
			}
		}

		[JsonIgnore] private int graphCount => graph.Length;

		private const int MAP_MAX_SIZE = 64;

#if UNITY_EDITOR
		public static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => new Vector3(index % width, 0f, index / width) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < width && vec.z >= 0 && vec.z < height ? (int)vec.z * width + (int)vec.x : -1;
		public static Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x), 0f, Mathf.FloorToInt(vec.z)) + tile_origin;
#else
        public static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => new(index % width, 0f, index / width);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < width && vec.z >= 0 && vec.z < height ? (int)vec.z * width + (int)vec.x : -1; }
        public static Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x + 0.5f), 0f, Mathf.FloorToInt(vec.z + 0.5f));
#endif

		public static Vector3 ScreenToWorldSnapped(Camera camera, Vector3 screenPos) => SnappedMapPosition(ScreenToWorld(camera, Input.mousePosition));
		public Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => tileIndex < 0 ? worldPosition : worldPosition - TileWorldPosition(tileIndex);
		public Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => tileIndex < 0 ? localPosition : localPosition + TileWorldPosition(tileIndex);

		// ─────────────────────────────────────────────
		// Runtime integer hash cache (non-serialized, mirrors table)
		// ─────────────────────────────────────────────

		//public HashId GetTileID(int index) => tiles == null || state == null || index < 0 || index >= tiles.Length || index >= state.Length ? 0 : hashes[tiles[state[index]]];
		public HashId GetTileID(int index) => tiles == null || index < 0 || index >= tiles.Length ? 0 : hashes[tiles[index]];//I think it's this - we can only edit unscrambled maps so need to ensure this

		public Tile GetTile(int index) => null == state || index < 0 || index >= state.Length ? default : GetGraphTile(state[index]);
		private Tile GetGraphTile(int graphIndex) => _graph == null || graphIndex < 0 || graphIndex >= _graph.Length ? default : _graph[graphIndex];

		private void DestroyAllTiles()
		{
			if (_graph == null)
				return;

			foreach (var tile in _graph)
				tile.Destroy();

			_graph = null;
		}

		/// <summary>
		/// Returns true if this map uses the given hash ID at least once.
		/// </summary>
		public bool IsDefinitionUsed(HashId hashId)
		{
			if (hashId == 0) return false;
			return hashes?.Contains(hashId) == true;
		}

		/// <summary>
		/// Returns how many times this map uses the given hash ID in its tile table.
		/// </summary>
		public int DefinitionUsageCount(HashId hashId)
		{
			if (hashId == 0) return 0;
			return hashes?.Count(h => h == hashId) ?? 0;
		}

		// ─────────────────────────────────────────────
		// Attachment runtime state
		// ─────────────────────────────────────────────

		public MapAttachment[] GetAttachments(int? tileIndex = null, Type[] filterTypes = null)
		{
			var real = attachments ?? Array.Empty<MapAttachment>();

			MapAttachment[] waypointWrappers = Array.Empty<MapAttachment>();
			if (waypoints != null && waypoints.Length > 0)
			{
				waypointWrappers = new MapAttachment[waypoints.Length];
				for (int i = 0; i < waypoints.Length; i++)
				{
					waypointWrappers[i] = new Waypoint(i, waypoints[i]);
				}
			}

			var source = real.Concat(waypointWrappers).AsEnumerable();

			source = source.Where(a => a != null && a.tile >= 0);

			if (tileIndex.HasValue)
				source = source.Where(a => a.tile == tileIndex.Value);

			if (filterTypes != null && filterTypes.Length > 0)
			{
				var typeSet = new HashSet<Type>(filterTypes);
				source = source.Where(a => typeSet.Contains(a.GetType()));
			}

			return source.ToArray();
		}

		[NonSerialized] private readonly Dictionary<MapAttachment, GameObject> attachmentGameObjects = new();

		public void RefreshAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachment is Waypoint wp)
			{
				if (waypoints != null && wp.waypointIndex >= 0 && wp.waypointIndex < waypoints.Length)
					waypoints[wp.waypointIndex] = wp.tile;
			}

			string prefabName = attachment switch
			{
				Waypoint => null,
				Emitter e => e.variant switch
				{
					"flame" => "flame",
					"spark" => "spark",
					_ => null
				},
				Pickup => null,
				View => null,
				_ => null
			};

			if (string.IsNullOrEmpty(prefabName))
			{
				DestroyAttachmentInstance(attachment);
				return;
			}

			Vector3 localPos = attachment switch
			{
				Waypoint w => Vector3.zero,
				Emitter e => e.Position,
				Pickup => Vector3.up * 0.5f,
				View v => v.Position,
				_ => Vector3.zero
			};

			Quaternion rotation = attachment switch
			{
				Waypoint w => Quaternion.identity,
				Emitter e => e.Rotation,
				Pickup => Quaternion.identity,
				View v => v.Rotation,
				_ => Quaternion.identity
			};

			Vector3 worldPos = TileWorldPosition(attachment.tile) + localPos;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				go.transform.position = worldPos;
				go.transform.rotation = rotation;
				return;
			}

			go = Assets.PrefabAssets.Instantiate(prefabName, worldPos, rotation, parent);
			go.name = $"{attachment.TypeName}_{prefabName}_tile{attachment.tile}";
			attachmentGameObjects[attachment] = go;
		}

		public void AddAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachment is Waypoint wp)
			{
				var list = waypoints?.ToList() ?? new List<int>();
				while (list.Count <= wp.waypointIndex)
					list.Add(-1);
				list[wp.waypointIndex] = wp.tile;
				waypoints = list.ToArray();
			}
			else
			{
				var list = attachments?.ToList() ?? new List<MapAttachment>();
				list.Add(attachment);
				attachments = list.ToArray();
			}

			RefreshAttachment(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			if (attachment == null) return false;

			bool removed = false;

			if (attachment is Waypoint wp)
			{
				if (waypoints == null || wp.waypointIndex < 0 || wp.waypointIndex >= waypoints.Length)
					return false;

				var newWaypoints = new List<int>(waypoints.Length - 1);

				for (int i = 0; i < waypoints.Length; i++)
				{
					if (i != wp.waypointIndex)
						newWaypoints.Add(waypoints[i]);
				}

				waypoints = newWaypoints.Count > 0 ? newWaypoints.ToArray() : null;

				removed = true;
			}
			else if (attachments != null)
			{
				int idx = Array.IndexOf(attachments, attachment);
				if (idx >= 0)
				{
					var list = attachments.ToList();
					list.RemoveAt(idx);
					attachments = list.Count > 0 ? list.ToArray() : null;
					removed = true;
				}
			}

			if (removed)
			{
				DestroyAttachmentInstance(attachment);
				OnMapEdited?.Invoke(this, false, Vector3.zero);
			}

			return removed;
		}

		public bool RemoveAttachments(MapAttachment[] attachmentArray)
		{
			if (attachmentArray == null || attachmentArray.Length == 0)
				return false;

			bool anyRemoved = false;

			var waypointsToRemove = attachmentArray.OfType<Waypoint>().ToList();
			var others = attachmentArray.Where(a => a is not Waypoint).ToArray();

			foreach (var att in others)
			{
				if (RemoveAttachment(att))
					anyRemoved = true;
			}

			var sortedWaypoints = waypointsToRemove.OrderByDescending(wp => wp.waypointIndex).ToList();

			foreach (var wp in sortedWaypoints)
			{
				if (RemoveAttachment(wp))
					anyRemoved = true;
			}

			return anyRemoved;
		}

		public void RefreshAttachments(MapAttachment[] attachmentsToRefresh)
		{
			if (attachmentsToRefresh == null || attachmentsToRefresh.Length == 0)
				return;

			foreach (var att in attachmentsToRefresh)
				RefreshAttachment(att);
		}

		private void DestroyAttachmentInstance(MapAttachment attachment)
		{
			if (attachment == null) return;

			if (attachmentGameObjects.TryGetValue(attachment, out GameObject go) && go != null)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(go);
				else
					UnityEngine.Object.DestroyImmediate(go);
			}

			attachmentGameObjects.Remove(attachment);
		}

		private void CleanupAttachmentInstances()
		{
			foreach (var att in attachmentGameObjects.Keys.ToList())
				DestroyAttachmentInstance(att);

			attachmentGameObjects.Clear();
		}

		// ─────────────────────────────────────────────
		// Original methods
		// ─────────────────────────────────────────────

		private bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0)
				return false;

			var defaultHash = ResourceManager.FindOrCreateDefaultTile().HashID;

			var currentHashes = tiles.Select(idx =>
				idx >= 0 && idx < hashes.Length ? hashes[idx] : defaultHash
			).ToArray();

			var newTable = currentHashes.ToFrequencySortedDistinct(); // assuming this returns int[]

			HashId[] originalTable = hashes;//needs a copy here
			int originalSize = originalTable?.Length ?? 0;
			int newSize = newTable.Length;

			bool sizeChanged = newSize != originalSize;
			bool orderChanged = !sizeChanged && !originalTable.SequenceEqual(newTable);

			bool anythingChanged = sizeChanged || orderChanged;

			if (anythingChanged)
			{
				tiles = currentHashes.Select(h => Array.IndexOf(newTable, h)).ToArray();

				hashes = newTable; // keep in sync

				if (sizeChanged)
				{
					string direction = newSize > originalSize ? "increased" : "reduced";
					Debug.Log($"{name} consolidated: table size {direction} {originalSize} → {newSize}");
				}
				else if (orderChanged)
				{
					Debug.Log($"{name} consolidated: table order changed (same size: {newSize})");
				}
			}

			return anythingChanged;
		}

		private bool RepositionAndResize(int expandToX = 0, int expandToZ = 0)
		{
			if (tiles == null || tiles.Length == 0) return false;

			int targetWidth, targetHeight, offsetX, offsetZ;

			if (0 != expandToX || 0 != expandToZ)
			{
				// Expand mode: include the target point
				int minX = Mathf.Min(0, expandToX);
				int minZ = Mathf.Min(0, expandToZ);
				int maxX = Mathf.Max(width - 1, expandToX);
				int maxZ = Mathf.Max(height - 1, expandToZ);

				targetWidth = maxX - minX + 1;
				targetHeight = maxZ - minZ + 1;
				offsetX = -minX;
				offsetZ = -minZ;
			}
			else
			{
				// Crop mode: tight bounds around non-default content
				var (minX, minZ, maxX, maxZ) = GetContentBounds();
				if (maxX < 0) return false; // no content → no-op

				targetWidth = maxX - minX + 1;
				targetHeight = maxZ - minZ + 1;
				offsetX = -minX;
				offsetZ = -minZ;
			}

			// Early exit: nothing would change
			if (targetWidth == width && targetHeight == height && offsetX == 0 && offsetZ == 0)
				return false;

			// Reject oversized maps (only really applies to expand mode)
			if (targetWidth > MAP_MAX_SIZE || targetHeight > MAP_MAX_SIZE)
			{
				Debug.LogWarning($"Resize rejected: would exceed max size ({MAP_MAX_SIZE}x{MAP_MAX_SIZE})");
				return false;
			}

			Debug.Log($"Resize Map '{name}' to {targetWidth}x{targetHeight}");

			// ─────────────────────────────────────────────
			// Perform the actual resize / reposition
			// ─────────────────────────────────────────────

			int oldWidth = width;
			int oldHeight = height;
			int newSize = targetWidth * targetHeight;

			int defaultIndex = -1;

			// Prefer any existing empty-like tile
			for (int i = 0; i < hashes.Length; i++)
			{
				var def = ResourceManager.GetDefinition(hashes[i]);
				if (def != null && def.IsDefaultEquivalent())
				{
					defaultIndex = i;
					break;
				}
			}

			// Fallback: append canonical default
			if (defaultIndex == -1)
			{
				var defaultDef = ResourceManager.FindOrCreateDefaultTile();
				var defaultHash = defaultDef.HashID;

				hashes = hashes.Concat(new[] { defaultHash }).ToArray();
				defaultIndex = hashes.Length - 1;
			}

			var newTiles = new int[newSize];
			Array.Fill(newTiles, defaultIndex);

			var newSolve = new int[newSize]; // zero-filled

			for (int oldIdx = 0; oldIdx < oldWidth * oldHeight; oldIdx++)
			{
				if (oldIdx >= tiles.Length) continue;

				int newPos = Remap(oldIdx, oldWidth, targetWidth, offsetX, offsetZ);
				if (newPos < 0) continue;

				newTiles[newPos] = tiles[oldIdx];

				if (solve != null && oldIdx < solve.Length)
				{
					int delta = solve[oldIdx];
					if (delta != 0)
					{
						int oldSrcIdx = oldIdx + delta;
						if (oldSrcIdx >= 0 && oldSrcIdx < solve.Length)
						{
							int newSrcPos = Remap(oldSrcIdx, oldWidth, targetWidth, offsetX, offsetZ);
							if (newSrcPos >= 0)
							{
								newSolve[newPos] = newSrcPos - newPos;
							}
						}
					}
				}
			}

			// Helper Remap (updated to take parameters so it can be called locally)
			int Remap(int idx, int oldW, int newW, int offX, int offZ)
			{
				if (idx < 0) return idx;
				int px = idx % oldW;
				int pz = idx / oldW;
				int nx = px + offX;
				int nz = pz + offZ;
				return (nx >= 0 && nx < targetWidth && nz >= 0 && nz < targetHeight)
					? nz * newW + nx
					: -1;
			}

			if (waypoints != null)
				for (int n = 0; n < waypoints.Length; n++)
					waypoints[n] = Remap(waypoints[n], oldWidth, targetWidth, offsetX, offsetZ);

			if (attachments != null)
				foreach (var a in attachments)
					a.tile = Remap(a.tile, oldWidth, targetWidth, offsetX, offsetZ);

			width = targetWidth;
			height = targetHeight;
			tiles = newTiles;
			solve = newSolve;
			state = Enumerable.Range(0, width * height).ToArray();

			return true;
		}

		public bool CropToContent(bool consolidate = false)
		{
			bool resized = RepositionAndResize(); // no args = crop mode

			bool consolidated = false;
			if (consolidate)
				consolidated = Consolidate();

			return resized || consolidated;
		}

		private (int minX, int minZ, int maxX, int maxZ) GetContentBounds()
		{
			if (tiles == null || tiles.Length == 0 || width <= 0 || height <= 0)
				return (0, 0, -1, -1);

			int minX = width;
			int minZ = height;
			int maxX = -1;
			int maxZ = -1;

			for (int i = 0; i < tiles.Length; i++)
			{
				int t = tiles[i];
				if (t < 0) continue;

				int hash = (t < hashes.Length) ? hashes[t] : 0;
				if (hash == 0) continue;

				var def = ResourceManager.GetDefinition(hash);
				if (def == null || def.IsDefault()) continue;

				int x = i % width;
				int z = i / width;

				minX = Math.Min(minX, x);
				maxX = Math.Max(maxX, x);
				minZ = Math.Min(minZ, z);
				maxZ = Math.Max(maxZ, z);
			}

			return maxX >= 0 ? (minX, minZ, maxX, maxZ) : (0, 0, -1, -1);
		}

		public Map Clone() => new()
		{
			name = name,
			character = character,
			music = music,
			button = button,
			width = width,
			height = height,

			waypoints = waypoints != null ? (int[])waypoints.Clone() : null,
			tiles = tiles != null ? (int[])tiles.Clone() : null,
			solve = solve != null ? (int[])solve.Clone() : null,

			attachments = attachments != null ? attachments.Select(a => a.ShallowClone()).ToArray() : Array.Empty<MapAttachment>(),
			//hashes = null != hashes ? (HashId[])hashes.Clone() : Array.Empty<HashId>()

			// ── Fix: copy variants directly ─────────────────────────────────────
			variants = variants != null ? variants.Select(v => new Variant(v.hash, v.angle, v.delta)).ToArray() : Array.Empty<Variant>()
			// No need to set hashes — the property getter will rebuild it from variants
		};

		public int CameraHitTile(Camera camera, Vector3 position) => WorldToMapIndex(ScreenToWorld(camera, position));

		public static bool RayToWorld(Ray ray, out Vector3 point)
		{
			point = Vector3.zero;
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (plane.Raycast(ray, out float d))
			{
				point = ray.GetPoint(d);
				return true;
			}
			return false;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos)
		{
			if (null == camera) return Vector3.negativeInfinity;
			RayToWorld(camera.ScreenPointToRay(screenPos), out Vector3 result);
			return result;
		}

		public static Vector3 CameraToWorld(Camera camera, Vector3 direction = default)
		{
			if (null == camera) return Vector3.negativeInfinity;
			if (default == direction) direction = Camera.main.transform.forward;
			var ray = new Ray(camera.transform.position, direction);
			RayToWorld(ray, out Vector3 result);
			return result;
		}

		public Bounds GetTileGeometryBounds(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= width * height)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center, Vector3.zero);
			}

			var tile = GetTile(tileIndex);

			if (tile.gameObject == null)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center, Vector3.zero);
			}

			return tile.GetGeometryBounds();
		}

		public int GetStartTile()
		{
			if (waypoints?.Length > 0) return waypoints[0];

			for (int i = 0; i < width * height; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (waypoints?.Length > 0) return waypoints[waypoints.Length - 1];

			for (int i = 0; i < width * height; ++i)
				if (GetTile(i).IsEnd) return i;

			Debug.LogError("No end tile found!");
			return -1;
		}

		public int FindAdjacentConsole(int nTile)
		{
			var tile = GetTile(nTile);
			if (tile.Nav == 0) return -1;

			foreach (var dirBit in Navigation.Directions)
			{
				int consoleIndex = Navigation.GetAdjacentTile(this, nTile, dirBit);
				if (consoleIndex == -1) continue;

				var consoleTile = GetTile(consoleIndex);
				if (consoleTile.IsConsole && dirBit == Navigation.GetOppositeDirection(consoleTile.Nav))
					return consoleIndex;
			}
			return -1;
		}

//		private void UpdateTileObjectNamesAndPositions()
//		{
//			var perm = state;
//			if (perm == null || graphCount != perm.Length)
//			{
//				Debug.Assert(false, "mismatched indices and runtime tiles");
//				return;
//			}

//			for (int n = 0; n < perm.Length; ++n)
//			{
//				int tableIndex = perm[n];
//				if (tableIndex < 0 || tableIndex >= variants.Length)
//					continue;

//				var variant = variants[tableIndex];
//				var mapTile = GetGraphTile(tableIndex);
//				var go = mapTile.gameObject;
//				if (go == null) continue;

//				// Base world position (grid center)
//				Vector3 basePosition = TileWorldPosition(n);

//				// Apply delta (Y-axis offset)
//				Vector3 finalPosition = basePosition + new Vector3(0f, variant.delta, 0f);

//				// Apply rotation (Y-axis)
//				Quaternion finalRotation = Quaternion.Euler(0f, variant.angle, 0f);

//				go.transform.position = finalPosition;
//				go.transform.rotation = finalRotation;

//#if DEBUG
//				var displayPos = finalPosition;
//				displayPos -= tile_origin;
//				var id = variant.hash;
//				var def = ResourceManager.GetDefinition(id);
//				go.name = $"{def?.name ?? "??"} ({displayPos.x:F1},{displayPos.z:F1})+{variant.delta:F2}@{variant.angle:F1}°";
//#endif
//			}
//		}

		private void RecreateTiles()
		{
			DestroyAllTiles();
			var _ = graph; // force lazy creation
		}

		public void RefreshGeometry()
		{
			RecreateTiles();

			if (graphCount == 0)
			{
				Debug.LogError("RefreshGeometry failed — could not recreate tiles.");
				return;
			}

			RefreshAttachments(GetAttachments());
		}

		//public bool UpdateTileAt(int x, int z, HashId hashId)
		//{
		//	if (tiles == null || tiles.Length == 0)
		//	{
		//		Debug.LogError("Cannot update tile: map has no tiles array");
		//		return false;
		//	}

		//	int oldWidth = width;
		//	int oldHeight = height;
		//	var oldBounds = GetContentBounds();

		//	Vector3 originDelta = Vector3.zero;
		//	bool sizeChanged = false;

		//	// If coordinate out of bounds → expand automatically
		//	if (x < 0 || x >= width || z < 0 || z >= height)
		//	{
		//		bool didResize = RepositionAndResize(x, z);

		//		if (didResize)
		//		{
		//			// Recompute the actual shift that occurred
		//			int minX = Mathf.Min(0, x);
		//			int minZ = Mathf.Min(0, z);
		//			int offsetX = -minX;
		//			int offsetZ = -minZ;

		//			if (x < 0) originDelta.x = offsetX;
		//			if (z < 0) originDelta.z = offsetZ;

		//			// Adjust local x/z to the new valid position after shift
		//			x += offsetX;
		//			z += offsetZ;

		//			sizeChanged = true;
		//		}
		//		else
		//		{
		//			Debug.LogWarning($"Cannot place tile at ({x},{z}) — map resize failed (too large?)");
		//			return false;
		//		}
		//	}

		//	// Now coordinates are guaranteed in bounds
		//	int index = z * width + x;

		//	// Update tile definition index
		//	if (hashes == null || !hashes.Contains(hashId))
		//	{
		//		hashes = hashes.Concat(new[] { hashId }).ToArray();
		//		tiles[index] = hashes.Length - 1;
		//	}
		//	else
		//	{
		//		tiles[index] = Array.IndexOf(hashes, hashId);
		//	}

		//	bool cropped = false;

		//	var def = ResourceManager.GetDefinition(hashId);
		//	bool isDefaultTile = def?.IsDefault() ?? false;

		//	// If default tile placed or size changed → try to crop
		//	if (isDefaultTile || sizeChanged)
		//	{
		//		var newBounds = GetContentBounds();
		//		cropped = CropToContent();

		//		if (cropped)
		//		{
		//			originDelta += new Vector3(
		//				oldBounds.minX - newBounds.minX,
		//				0,
		//				oldBounds.minZ - newBounds.minZ
		//			);
		//			sizeChanged = true;
		//		}
		//	}

		//	bool boundsChanged = sizeChanged || width != oldWidth || height != oldHeight || cropped;

		//	if (boundsChanged)
		//	{
		//		RecreateTiles();
		//		RefreshAttachments(GetAttachments());
		//	}
		//	else
		//	{
		//		GetGraphTile(index).Destroy();

		//		//graph[index] = new Tile(hashId, parent, TileWorldPosition(index));

		//		var variant = new Variant(hashId, 0f, 0f);

		//		graph[index] = new Tile(variant, parent, TileWorldPosition(index));

		//		RefreshAttachments(GetAttachments(tileIndex: index));
		//	}

		//	OnMapEdited?.Invoke(this, boundsChanged, originDelta);

		//	return true;
		//}

		public bool UpdateTileAt(int x, int z, HashId hashId)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot update tile: map has no tiles array");
				return false;
			}

			int oldWidth = width;
			int oldHeight = height;
			var oldBounds = GetContentBounds();

			Vector3 originDelta = Vector3.zero;
			bool sizeChanged = false;

			// If coordinate out of bounds → expand automatically
			if (x < 0 || x >= width || z < 0 || z >= height)
			{
				bool didResize = RepositionAndResize(x, z);

				if (didResize)
				{
					int minX = Mathf.Min(0, x);
					int minZ = Mathf.Min(0, z);
					int offsetX = -minX;
					int offsetZ = -minZ;

					if (x < 0) originDelta.x = offsetX;
					if (z < 0) originDelta.z = offsetZ;

					x += offsetX;
					z += offsetZ;

					sizeChanged = true;
				}
				else
				{
					Debug.LogWarning($"Cannot place tile at ({x},{z}) — map resize failed (too large?)");
					return false;
				}
			}

			int index = z * width + x;

			// ── Manage variants directly ────────────────────────────────────────
			int tableIndex = -1;

			if (variants == null || !variants.Any(v => v.hash == hashId))
			{
				var newVariant = new Variant(hashId, 0f, 0f);

				variants = variants != null
					? variants.Concat(new[] { newVariant }).ToArray()
					: new[] { newVariant };

				tableIndex = variants.Length - 1;
			}
			else
			{
				tableIndex = Array.FindIndex(variants, v => v.hash == hashId);
			}

			tiles[index] = tableIndex;

			// ── Rest unchanged ──────────────────────────────────────────────────
			bool cropped = false;

			var def = ResourceManager.GetDefinition(hashId);
			bool isDefaultTile = def?.IsDefault() ?? false;

			if (isDefaultTile || sizeChanged)
			{
				var newBounds = GetContentBounds();
				cropped = CropToContent();

				if (cropped)
				{
					originDelta += new Vector3(
						oldBounds.minX - newBounds.minX,
						0,
						oldBounds.minZ - newBounds.minZ
					);
					sizeChanged = true;
				}
			}

			bool boundsChanged = sizeChanged || width != oldWidth || height != oldHeight || cropped;

			// Refresh attachments appropriately
			if (boundsChanged)
			{
				RecreateTiles();
				RefreshAttachments(GetAttachments());
			}
			else
			{
				GetGraphTile(index).Destroy();
				graph[index] = new Tile(variants[tableIndex], parent, TileWorldPosition(index));
				RefreshAttachments(GetAttachments(tileIndex: index));
			}

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);

			return true;
		}

		private void SetupWaypoints()
		{
			if (waypoints != null && waypoints.Length > 0)
			{
				Debug.Log($"Using {waypoints.Length} predefined waypoints.");
				return;
			}

			var generated = new List<int>();
			int start = GetStartTile();
			int end = GetEndTile();

			if (start == -1 || end == -1)
			{
				waypoints = generated.ToArray();
				return;
			}

			generated.Add(start);

			int cur = start;
			int dir = Navigation.NavToDest(this, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (FindAdjacentConsole(cur) != -1)
						generated.Add(cur);

					int next = Navigation.GetAdjacentTile(this, cur, dir);
					if (next == -1 || next == start) break;

					var nextTile = GetTile(next);
					if (nextTile.Nav == 0) break;

					dir = Navigation.CalculateNav(dir, nextTile.Nav);
					if (dir == 0) break;

					cur = next;
				}
			}

			generated.Add(end);

			waypoints = generated.ToArray();

			Debug.Log($"Generated {waypoints.Length} waypoints.");
		}

		private void InitializeWindController()
		{
			WindController windController = null;

			for (int n = 0; n < graphCount; ++n)
			{
				var go = GetGraphTile(n).gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? parent.gameObject.AddComponent<WindController>();
				windController.AddSway(sway, TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		public void Preset()
		{
			state = Enumerable.Range(0, width * height).ToArray();
			RecreateTiles(); //UpdateTileObjectNamesAndPositions();
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
			RecreateTiles(); //UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			state = Enumerable.Range(0, width * height).Select(n => n + (solve?[n] ?? 0)).ToArray();

			RecreateTiles(); //UpdateTileObjectNamesAndPositions();
		}

		public void Destroy()
		{
			OnMapEdited = null;

			DestroyAllTiles();

			CleanupAttachmentInstances();

			if (parent != null)
			{
				var wind = parent.GetComponent<WindController>();
				if (wind != null)
				{
					if (Application.isPlaying)
						UnityEngine.Object.Destroy(wind);
					else
						UnityEngine.Object.DestroyImmediate(wind);
				}
			}

			state = null;

			if (parent != null)
				parent = null;
		}

		public void Initialise(Transform parent = null)
		{
			this.parent = parent;

			if (graphCount == 0)
			{
				Debug.LogError("Failed to create runtime tiles — map data invalid.");
				return;
			}

			if (ApplicationSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();

			RefreshAttachments(GetAttachments());

			SetupWaypoints();
		}
	}

	public static class MapExtensions
	{
		public static T GetAttachmentOfType<T>(this IMapPlay map, int tile) where T : MapAttachment
		{
			return map.GetAttachments(tileIndex: tile, filterTypes: new[] { typeof(T) })
					  .OfType<T>()
					  .FirstOrDefault();
		}

		public static bool HasAttachmentOfType<T>(this IMapPlay map, int tile) where T : MapAttachment
		{
			return map.GetAttachments(tileIndex: tile, filterTypes: new[] { typeof(T) })
					  .Length > 0;
		}

		public static Waypoint GetWaypoint(this IMapPlay map, int waypointIndex)
		{
			return map.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
					  .Cast<Waypoint>()
					  .FirstOrDefault(w => w.waypointIndex == waypointIndex);
		}

		public static Waypoint[] GetWaypoints(this IMapPlay map)
		{
			return map.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
					  .Cast<Waypoint>()
					  .OrderBy(w => w.waypointIndex)
					  .ToArray();
		}
	}
}