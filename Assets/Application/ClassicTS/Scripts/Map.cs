using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public interface IMapData
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] Indices { get; }
	}

	public interface IMap : IMapData
	{
		bool IsValidTile(int _);
		Tile GetTile(int _);
		int WorldToMapIndex(Vector3 _);
		Vector3 TileWorldPosition(int _);

		string GetDefinitionAtIndex(int mapIndex);

		int GetStartTile();
		int GetEndTile();
		int FindAdjacentConsole(int nTile);

		int GetWaypoint(int _);

		Action<Map, bool, Vector3> OnMapEdited { get; set; }
		void RefreshAttachmentInstance(MapAttachment _);

		int CameraHitTile(Camera camera, Vector3 position);
		int[] Waypoints { get; set; }
		MapAttachment [] Attachments { get; set; }
		MapAttachment[] GetAllAttachments();
		Waypoint[] waypointAttachments { get; }
		void AddAttachment(MapAttachment _);
		bool RemoveAttachment(MapAttachment _);
		bool RemoveAttachments(MapAttachment[] _);

		View GetView(int tile);

		Bounds GetTileGeometryBounds(int tileIndex);
		bool UpdateTileAt(int x, int z, string id, bool expand = true);

		public string Music { get; }
	}

	[Serializable]
	public class Map : IMap
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

		// ─────────────────────────────────────────────
		// Tile table — now the ONLY source of truth (hashes)
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 20)] public string[] table;

		[JsonProperty(Order = 21)] public int[] tiles;
		[JsonProperty(Order = 22)] public int[] solve;
		[JsonProperty(Order = 23)] public int[] waypoints;

		[JsonProperty(Order = 30)] public MapAttachment[] attachments;

		// ATOMIC-ONLY FIELDS
		[JsonProperty(Order = 100)] public Definition[] definitions;
		[JsonProperty(Order = 101)] public TextureSequence[] textures;
		[JsonProperty(Order = 102)] public string version = "1.0";
		[JsonProperty(Order = 103)] public string author = "Player";
		[JsonProperty(Order = 104)] public string exportedFrom = "ClassicTilestorm";

		// Conditional serialization
		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		public bool IsValidTile(int index) => index >= 0 && index < width * height;

		public Action<Map, bool, Vector3> OnMapEdited { get; set; }
		public static Transform parentTransform;

		public int Width => width;
		public int Height => height;
		public int Count => Width * Height;
		public int[] Indices => indices;
		public int[] Waypoints { get => waypoints; set => waypoints = value; }
		public MapAttachment[] Attachments { get => attachments; set => attachments = value; }
		public string Music => music;

		public const int MAP_MAX_SIZE = 64;

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

		// ─────────────────────────────────────────────
		// Runtime tile instances (not serialized)
		// ─────────────────────────────────────────────

		[JsonIgnore, NonSerialized]
		public Tile[] runtimeTiles;

		[JsonIgnore]
		public IReadOnlyList<Tile> RuntimeTiles => runtimeTiles ?? Array.Empty<Tile>();

		[JsonIgnore]
		public int RuntimeTileCount => runtimeTiles?.Length ?? 0;

		[NonSerialized]
		public int[] indices;           // runtime permutation, never serialized

		[JsonIgnore]
		public bool IsScrambled => indices != null && !IsIdentity(indices);

		private static bool IsIdentity(int[] arr)
		{
			if (arr == null) return true;
			for (int i = 0; i < arr.Length; i++)
				if (arr[i] != i) return false;
			return true;
		}

		public Tile[] CreateOrGetRuntimeTiles(Transform parent = null)
		{
			if (runtimeTiles != null)
				return runtimeTiles;

			if (tiles == null || tiles.Length != width * height)
			{
				DebugUtil.LogError($"Invalid tile map data! length={(tiles?.Length ?? -1)}, expected={width * height}");
				return null;
			}

			runtimeTiles = new Tile[width * height];

			string mapName = name ?? "Unnamed map";

			for (int n = 0; n < tiles.Length; n++)
			{
				int idx = tiles[n];
				string hashId = null;

				if (idx >= 0 && table != null && idx < table.Length)
					hashId = table[idx];
				else if (idx != -1)
					DebugUtil.LogWarning($"Out-of-range table index {idx} at tile {n} (map: {mapName})");

				var def = ResourceManager.ResolveDefinition(hashId, out bool hadError);

				if (hadError)
					Debug.LogWarning($"Failed to resolve tile definition at tile {n} (hash: '{hashId ?? "<null>"}') — using default");

				runtimeTiles[n] = new Tile(def, parent ?? parentTransform, TileWorldPosition(n));
			}

			return runtimeTiles;
		}

		public void DestroyAllTiles()
		{
			if (runtimeTiles == null)
				return;

			foreach (var tile in runtimeTiles)
				tile.Destroy();

			runtimeTiles = null;
		}

		private Tile GetSeedTile(int seedIndex)
		{
			if (runtimeTiles == null || seedIndex < 0 || seedIndex >= runtimeTiles.Length)
				return default;
			return runtimeTiles[seedIndex];
		}

		public Tile GetTile(int index)
		{
			if (index < 0 || index >= width * height || width <= 0)
				return default;

			int dataIndex = indices?[index] ?? index;

			return GetSeedTile(dataIndex);
		}

		public int GetWaypoint(int index)
		{
			if (index < 0 || waypoints == null) return -1;
			return index < waypoints.Length ? waypoints[index] : -1;
		}

		public string GetDefinitionAtIndex(int mapIndex)
		{
			if (mapIndex < 0 || mapIndex >= width * height)
				return null;

			return GetSeedTile(mapIndex).definitionId;
		}

		// ─────────────────────────────────────────────
		// Attachment runtime state (unchanged)
		// ─────────────────────────────────────────────

		[NonSerialized] private readonly Dictionary<MapAttachment, GameObject> attachmentGameObjects = new();

		public void RefreshAllAttachmentInstances()
		{
			foreach (var att in attachments ?? Array.Empty<MapAttachment>())
				RefreshAttachmentInstance(att);
		}

		public void RefreshAttachmentsOnTile(int tileIndex)
		{
			if (attachments == null) return;
			foreach (var att in attachments)
				if (att?.tile == tileIndex)
					RefreshAttachmentInstance(att);
		}

		public void RefreshAttachmentInstance(MapAttachment attachment)
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

			go = Assets.PrefabAssets.Instantiate(prefabName, worldPos, rotation, parentTransform);
			go.name = $"{attachment.TypeName}_{prefabName}_tile{attachment.tile}";
			attachmentGameObjects[attachment] = go;
		}

		public void DestroyAttachmentInstance(MapAttachment attachment)
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

		public void CleanupAttachmentInstances()
		{
			foreach (var att in attachmentGameObjects.Keys.ToList())
				DestroyAttachmentInstance(att);
			attachmentGameObjects.Clear();
		}

		// Inside class Map

		public void AddAttachment(MapAttachment attachment)
		{
			if (attachment == null) return;

			// ── existing mutation logic ────────────────────────────────
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

			// ── side effects ───────────────────────────────────────────
			RefreshAttachmentInstance(attachment);
			OnMapEdited?.Invoke(this, false, Vector3.zero);
		}

		public bool RemoveAttachment(MapAttachment attachment)
		{
			if (attachment == null) return false;

			bool removed = false;

			// ── existing mutation logic ────────────────────────────────
			if (attachment is Waypoint wp)
			{
				if (waypoints == null || wp.waypointIndex < 0 || wp.waypointIndex >= waypoints.Length)
					return false;

				// Renumber higher waypoint indices
				if (attachments != null)
				{
					foreach (var att in attachments)
					{
						if (att is Waypoint other && other.waypointIndex > wp.waypointIndex)
							other.waypointIndex--;
					}
				}

				var newWaypoints = new List<int>();
				for (int i = 0; i < waypoints.Length; i++)
					if (i != wp.waypointIndex)
						newWaypoints.Add(waypoints[i]);

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
				// ── side effects ───────────────────────────────────────
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

			foreach (var att in attachmentArray)
			{
				if (RemoveAttachment(att))      // ← reuses the single-remove logic (including side effects)
					anyRemoved = true;
			}

			return anyRemoved;
		}


		public void RemoveAllAttachmentsOnTile(int tileIndex)
		{
			if (attachments == null) return;

			var toRemove = attachments
				.Where(a => a?.tile == tileIndex)
				.ToArray();

			foreach (var att in toRemove)
				RemoveAttachment(att);          // ← again, reuses single-remove (side effects included)
		}

		[JsonIgnore] public Waypoint[] waypointAttachments => GetWaypointAttachments() ?? Array.Empty<Waypoint>();

		public Waypoint[] GetWaypointAttachments()
		{
			if (waypoints == null || waypoints.Length == 0) return Array.Empty<Waypoint>();
			var result = new Waypoint[waypoints.Length];
			for (int i = 0; i < waypoints.Length; i++)
				result[i] = new Waypoint(i, waypoints[i]);
			return result;
		}

		public MapAttachment[] GetAllAttachments()
		{
			var real = attachments ?? Array.Empty<MapAttachment>();
			return real.Concat(GetWaypointAttachments()).ToArray();
		}

		public void SetAllAttachments(MapAttachment[] value)
		{
			if (value == null)
			{
				attachments = null;
				waypoints = null;
				return;
			}

			var realAttachments = new List<MapAttachment>();
			var waypointsList = new List<(int index, int tile)>();

			foreach (var att in value)
			{
				if (att is Waypoint wp)
					waypointsList.Add((wp.waypointIndex, wp.tile));
				else if (att != null)
					realAttachments.Add(att);
			}

			attachments = realAttachments.Count > 0 ? realAttachments.ToArray() : null;

			if (waypointsList.Count == 0)
			{
				waypoints = null;
			}
			else
			{
				int maxIndex = waypointsList.Max(x => x.index);
				var arr = new int[maxIndex + 1];
				foreach (var (idx, tile) in waypointsList)
					if (idx >= 0 && idx < arr.Length)
						arr[idx] = tile;
				waypoints = arr;
			}
		}

		// ─────────────────────────────────────────────
		// Original methods (unchanged)
		// ─────────────────────────────────────────────

		public Definition ResolveDefinition(string id, int? tileIndexForLogging = null)
		{
			if (string.IsNullOrEmpty(id))
			{
				Debug.LogError("attempting to load null tile def!!");
				return ResourceManager.FindOrCreateDefaultTile();
			}

			var def = ResourceManager.GetDefinition(id);
			if (def != null)
			{
				return def;
			}

			string context = tileIndexForLogging.HasValue
				? $"at visual tile {tileIndexForLogging.Value}"
				: "during map load";

			Debug.LogWarning($"Missing or invalid definition for hash '{id}' {context} → falling back to default tile");

			return ResourceManager.FindOrCreateDefaultTile();
		}

		public bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0) return false;

			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			var defaultHash = defaultDef.hashid;

			var mapDefinitions = tiles.Select(idx =>
				(idx >= 0 && idx < table.Length) ? table[idx] : null
			).ToArray();

			for (int i = 0; i < mapDefinitions.Length; i++)
			{
				if (mapDefinitions[i] == null)
					mapDefinitions[i] = defaultHash;
			}

			var newFrequencyTable = mapDefinitions.ToFrequencySortedTable();

			bool changed = !table.SequenceEqual(newFrequencyTable);

			if (changed)
			{
				table = newFrequencyTable;
			}

			if (changed)
			{
				tiles = mapDefinitions.Select(hash =>
					Array.IndexOf(table, hash)
				).ToArray();
			}

			if (changed) Debug.Log($"{name} consolidated (table updated)");
			return changed;
		}

		private bool RepositionAndResize(int newWidth, int newHeight, int offsetX, int offsetZ)
		{
			if (newWidth <= 0 || newHeight <= 0) return false;

			int oldWidth = width;
			int oldHeight = height;
			int newSize = newWidth * newHeight;

			var defaultDef = ResourceManager.FindOrCreateDefaultTile();

			int defaultIndex = -1;
			for (int i = 0; i < table.Length; i++)
			{
				if (table[i] == defaultDef.hashid)
				{
					defaultIndex = i;
					break;
				}
			}

			if (defaultIndex == -1)
			{
				var list = table.ToList();
				list.Add(defaultDef.hashid);
				table = list.ToArray();
				defaultIndex = table.Length - 1;
			}

			var newTiles = new int[newSize];
			Array.Fill(newTiles, defaultIndex);

			for (int z = 0; z < oldHeight; z++)
				for (int x = 0; x < oldWidth; x++)
				{
					int oldIdx = z * oldWidth + x;
					if (oldIdx >= tiles.Length) continue;

					int nx = x + offsetX;
					int nz = z + offsetZ;

					if (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight)
						newTiles[nz * newWidth + nx] = tiles[oldIdx];
				}

			var newSolve = new int[newSize];
			if (solve != null && solve.Length == oldWidth * oldHeight)
			{
				for (int z = 0; z < oldHeight; z++)
					for (int x = 0; x < oldWidth; x++)
					{
						int oldIdx = z * oldWidth + x;
						int delta = solve[oldIdx];
						if (delta == 0) continue;

						int srcIdx = oldIdx + delta;
						if (srcIdx < 0 || srcIdx >= solve.Length) continue;

						int srcX = srcIdx % oldWidth;
						int srcZ = srcIdx / oldWidth;

						int nx = x + offsetX;
						int nz = z + offsetZ;
						int nsx = srcX + offsetX;
						int nsz = srcZ + offsetZ;

						if (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight &&
							nsx >= 0 && nsx < newWidth && nsz >= 0 && nsz < newHeight)
						{
							int newPos = nz * newWidth + nx;
							int newSrc = nsz * newWidth + nsx;
							newSolve[newPos] = newSrc - newPos;
						}
					}
			}

			int Remap(int idx)
			{
				if (idx < 0) return idx;
				int x = idx % oldWidth;
				int z = idx / oldWidth;
				int nx = x + offsetX;
				int nz = z + offsetZ;
				return (nx >= 0 && nx < newWidth && nz >= 0 && nz < newHeight) ? nz * newWidth + nx : -1;
			}

			if (waypoints != null)
				for (int n = 0; n < waypoints.Length; n++) waypoints[n] = Remap(waypoints[n]);

			if (attachments != null)
				foreach (var a in attachments) a.tile = Remap(a.tile);

			width = newWidth;
			height = newHeight;
			tiles = newTiles;
			solve = newSolve;

			return true;
		}

		private bool CropToContent()
		{
			var (minX, minZ, maxX, maxZ) = GetContentBounds();
			if (maxX < 0) return false;

			int w = maxX - minX + 1;
			int h = maxZ - minZ + 1;

			bool success = RepositionAndResize(w, h, -minX, -minZ);

			if (success) Consolidate();

			return success;
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
				if (t < 0)
					continue;

				string tileName = (t < table.Length) ? table[t] : null;
				if (string.IsNullOrEmpty(tileName))
					continue;

				var def = ResourceManager.GetDefinition(tileName);
				if (def == null || def.IsDefault())
					continue;

				int x = i % width;
				int z = i / width;

				minX = Math.Min(minX, x);
				maxX = Math.Max(maxX, x);
				minZ = Math.Min(minZ, z);
				maxZ = Math.Max(maxZ, z);
			}

			return maxX >= 0 ? (minX, minZ, maxX, maxZ) : (0, 0, -1, -1);
		}

		public Map CreateCroppedCopy()
		{
			var copy = new Map
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
				table = table != null ? (string[])table.Clone() : Array.Empty<string>()
			};

			bool cropped = copy.CropToContent();

			if (cropped)
				Debug.Log($"[Export] Map '{copy.name}' auto-cropped to {copy.width}x{copy.height}");

			return copy;
		}

		public View GetView(int tile)
		{
			if (attachments == null || tile < 0 || tile >= tiles.Length)
				return null;

			foreach (var att in attachments)
			{
				if (att is View view && att.tile == tile)
					return view;
			}
			return null;
		}

		public int CameraHitTile(Camera camera, Vector3 position) => WorldToMapIndex(Map.ScreenToWorld(camera, position));

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
			// Invalid index → safe empty bounds at tile position
			if (tileIndex < 0 || tileIndex >= width * height)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center, Vector3.zero);
			}

			// Resolve runtime tile (respects scrambling / indices)
			var tile = GetTile(tileIndex);

			// No tile or no geometry
			if (tile.gameObject == null)
			{
				Vector3 center = TileWorldPosition(tileIndex);
				return new Bounds(center, Vector3.zero);
			}

			// Delegate to Tile geometry logic
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
			if (waypoints?.Length > 0) return waypoints[0];

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

		private void UpdateTileObjectNamesAndPositions()
		{
			var perm = indices;
			if (perm == null || RuntimeTileCount != perm.Length)
			{
				Debug.Assert(false, "mismatched indices and runtime tiles");
				return;
			}

			for (int n = 0; n < perm.Length; ++n)
			{
				var mapTile = GetSeedTile(perm[n]);
				var go = mapTile.gameObject;
				if (go == null) continue;

				var position = TileWorldPosition(n);
				go.transform.position = position;

#if DEBUG
				position -= tile_origin;
				var id = string.IsNullOrEmpty(mapTile.definitionId) ? "Empty" : mapTile.definitionId;
				var def = ResourceManager.GetDefinition(mapTile.definitionId);
				go.name = $"{def?.id ?? "??"} ({position.x},{position.z})";
#endif
			}
		}

		public void RefreshGeometry()
		{
			DestroyAllTiles();

			CreateOrGetRuntimeTiles(parentTransform);

			if (RuntimeTileCount == 0)
			{
				Debug.LogError("RefreshGeometry failed — could not recreate tiles.");
				return;
			}

			RefreshAllAttachmentInstances();
		}

		public bool UpdateTileAt(int x, int z, string id, bool expand = true) => expand ? UpdateTileAtSmart(x, z, id) : UpdateTileAtRestricted(x, z, id);

		private bool UpdateTileAtRestricted(int x, int z, string id)
		{
			if (x < 0 || x >= width || z < 0 || z >= height)
			{
				Debug.LogError($"Invalid coordinates: ({x}, {z}) outside map bounds ({width}x{height})");
				return false;
			}

			int index = z * width + x;

			var oldTile = GetSeedTile(index);
			oldTile.Destroy();

			var def = ResolveDefinition(id, index);

			// We can't directly set runtimeTiles[index] here anymore — force full refresh
			tiles[index] = GetOrAddTableIndex(id);

			RefreshAttachmentsOnTile(index);
			Consolidate();

			RefreshGeometry();  // rebuilds runtime tiles

			OnMapEdited?.Invoke(this, false, Vector3.zero);
			return true;
		}

		private bool UpdateTileAtSmart(int x, int z, string id, Action<bool, Vector3> onEdited = null)
		{
			int oldWidth = width;
			int oldHeight = height;

			var oldBounds = GetContentBounds();

			Vector3 originDelta = Vector3.zero;
			bool sizeChanged = false;

			if (x < 0 || x >= width || z < 0 || z >= height)
			{
				int minX = Mathf.Min(0, x);
				int minZ = Mathf.Min(0, z);
				int maxX = Mathf.Max(width - 1, x);
				int maxZ = Mathf.Max(height - 1, z);

				int newWidth = maxX - minX + 1;
				int newHeight = maxZ - minZ + 1;

				if (newWidth > MAP_MAX_SIZE || newHeight > MAP_MAX_SIZE)
				{
					Debug.LogWarning($"Map placement rejected: would exceed max size ({MAP_MAX_SIZE}x{MAP_MAX_SIZE})");
					onEdited?.Invoke(false, Vector3.zero);
					return false;
				}

				int offsetX = -minX;
				int offsetZ = -minZ;

				RepositionAndResize(newWidth, newHeight, offsetX, offsetZ);

				if (x < 0) originDelta.x = offsetX;
				if (z < 0) originDelta.z = offsetZ;

				x += offsetX;
				z += offsetZ;

				sizeChanged = true;
			}

			int index = z * width + x;

			tiles[index] = GetOrAddTableIndex(id);

			var defForCrop = ResourceManager.GetDefinition(id);
			bool isDefaultTile = defForCrop?.IsDefault() ?? false;

			if (isDefaultTile || sizeChanged)
			{
				var newBounds = GetContentBounds();

				if (CropToContent())
				{
					originDelta += new Vector3(oldBounds.minX - newBounds.minX,0,oldBounds.minZ - newBounds.minZ);
					sizeChanged = true;
				}
			}

			bool boundsChanged = sizeChanged || width != oldWidth || height != oldHeight;

			if (boundsChanged)
			{
				DestroyAllTiles();
				CreateOrGetRuntimeTiles(parentTransform);
				RefreshAllAttachmentInstances();
			}
			else
			{
				var oldTile = GetSeedTile(index);
				oldTile.Destroy();

				var def = ResolveDefinition(id, index);

				// Force single tile recreation
				runtimeTiles[index] = new Tile(def, parentTransform, TileWorldPosition(index));

				RefreshAttachmentsOnTile(index);
			}

			Consolidate();

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);
			return true;
		}

		private int GetOrAddTableIndex(string id)
		{
			if (table == null || !Array.Exists(table, s => s == id))
			{
				var list = table?.ToList() ?? new List<string>();
				list.Add(id);
				table = list.ToArray();
				return table.Length - 1;
			}
			return Array.IndexOf(table, id);
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

			for (int n = 0; n < RuntimeTileCount; ++n)
			{
				var go = GetSeedTile(n).gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? parentTransform.gameObject.AddComponent<WindController>();
				windController.AddSway(sway, this.TileWorldPosition(n));
			}

			if (windController != null)
				Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		public void Initialise()
		{
			OnMapEdited = null;//clear all delegates

			CleanupAttachmentInstances();
			DestroyAllTiles();

			CreateOrGetRuntimeTiles(parentTransform);

			if (RuntimeTileCount == 0)
			{
				Debug.LogError("Failed to create runtime tiles — map data invalid.");
				return;
			}

			if (ApplicationSettings.Scrambled) Preset();
			else Solve();

			InitializeWindController();

			RefreshAllAttachmentInstances();

			SetupWaypoints();
		}

		public void Preset()
		{
			indices = Enumerable.Range(0, width * height).ToArray();
			UpdateTileObjectNamesAndPositions();
		}

		public void Scramble()
		{
			if (indices == null)
				indices = Enumerable.Range(0, width * height).ToArray();

			const int iterations = 1;
			for (var n = 0; n < indices.Length * iterations; ++n)
			{
				var stride = (UnityEngine.Random.value > 0.5f ? width : 1) * (UnityEngine.Random.value > 0.5f ? 1 : -1);

				var tileStrip = TileStripHelper.GetTileStrip(this, n % indices.Length, stride, true);
				TileStripHelper.RollStrip(this, tileStrip);
			}
			UpdateTileObjectNamesAndPositions();
		}

		public void Solve()
		{
			indices = Enumerable.Range(0, width * height) .Select(n => n + (solve?[n] ?? 0)) .ToArray();

			UpdateTileObjectNamesAndPositions();
		}
	}
}