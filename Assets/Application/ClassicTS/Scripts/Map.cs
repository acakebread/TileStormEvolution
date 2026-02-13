//#define VERBOSE//for debug logging
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
		public Vector3 delta;           // local position offset

		public Variant(HashId h) : this(h, Vector3.zero, 0f) { }
		public Variant(HashId h, Vector3 offset, float rotationDegrees)
		{
			hash = h;
			angle = rotationDegrees;
			delta = offset;
		}

		public static implicit operator HashId(Variant v) => v.hash;
	}

	public interface IMapData
	{
		int Width { get; }
		int Height { get; }
		int Count { get; }
		int[] State { get; }
	}

	public interface IMapPlay : IMapData
	{
		string Music { get; set; }
		string Skybox { get; set; }

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
		bool UpdateTileAt(int x, int z, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f);
		bool UpdateTileAt(Vector3 pos, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f);
		Variant GetVariantAt(int mapIndex);
		void AddAttachment(MapAttachment _);
		bool RemoveAttachment(MapAttachment _);
		bool RemoveAttachments(MapAttachment[] _);
		void RefreshAttachment(MapAttachment _);

		int CameraHitTile(Camera camera, Vector3 position);
		Variant CameraHitVariant(Camera camera, Vector3 position);
		Definition CameraHitDefinition(Camera camera, Vector3 position);
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
		[JsonProperty(Order = 4)] public string light;
		[JsonProperty(Order = 5)] public string skybox;
		[JsonProperty(Order = 6)] public string effect;
		[JsonProperty(Order = 7)] public string button;

		// ─────────────────────────────────────────────
		// Dimensions
		// ─────────────────────────────────────────────
		private const int MAP_MAX_SIZE = 64;
		[JsonProperty(Order = 10)] public int width;
		[JsonProperty(Order = 11)] public int height;

		[JsonProperty(Order = 21)] public int[] tiles;     // seed indices
		[JsonProperty(Order = 22)] public int[] solve;     // delta
		[JsonProperty(Order = 23)] public int[] waypoints;

		[JsonProperty(Order = 30)] public MapAttachment[] attachments;

		// Conditional serialization
		public bool ShouldSerializelight() => !string.IsNullOrEmpty(light);
		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializeeffect() => !string.IsNullOrEmpty(effect);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;

		public Action<Map, bool, Vector3> OnMapEdited { get; set; }
		[JsonIgnore] private Transform parent;

		[JsonIgnore] public int Width => width;
		[JsonIgnore] public int Height => height;
		[JsonIgnore] public int Count => Width * Height;
		[JsonIgnore] public int[] State { get => state = state?.Length == width * height ? state : Enumerable.Range(0, width * height).ToArray(); }//set => state = value; 
		[JsonIgnore] public string Music { get => music; set => music = value; }
		[JsonIgnore] public string Skybox { get => skybox; set => skybox = value; }
		//[JsonIgnore] public string Effect { get => string.IsNullOrEmpty(effect) ? "Water" : effect; set => effect = value; }
		[JsonIgnore] public ReflectionEffectCamera.EffectMode Effect 
		{ 
			get => ReflectionEffectCamera.ParseEffectMode(string.IsNullOrEmpty(effect) ? "Water" : effect);
			set => effect = ReflectionEffectCamera.EffectModeToString(value);
		}

		[JsonIgnore] public Color Light
		{
			get => StringUtil.FromHexString(light, defaultColor: Color.white);
			set => light = value.ToHexString(includeAlpha: true);
		}

		[JsonIgnore] public Material SkyboxMaterial => SkyboxUtility.GetSkyboxMaterialForName(Skybox);

		// ─────────────────────────────────────────────
		// Tile data — variants is now the only source of truth
		// ─────────────────────────────────────────────

		[JsonIgnore] public Variant[] variants = Array.Empty<Variant>();

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

		// In Map class — public implementation
		public Variant GetVariantAt(int mapIndex)
		{
			if (state == null || mapIndex < 0 || mapIndex >= state.Length)
				return new Variant(0);

			// Apply current permutation (scrambled/solved state)
			int logicalIndex = state[mapIndex];

			return GetVariantForIndex(logicalIndex);
		}

		public Definition GetDefinitionAt(int mapIndex)
		{
			if (state == null || mapIndex < 0 || mapIndex >= state.Length)
				return null;

			// Apply current permutation (scrambled/solved state)
			int logicalIndex = state[mapIndex];

			return ResourceManager.GetDefinition(GetVariantForIndex(logicalIndex).hash);
		}

		// ─────────────────────────────────────────────
		// Runtime tile (graph) instances (lazy / just-in-time)
		// ─────────────────────────────────────────────

		[JsonIgnore] private int[] state; // runtime permutation, never serialized

		[JsonIgnore] private int graphCount => graph.Length;
		[JsonIgnore] private Tile[] _graph; // private backing field (never serialized)
		[JsonIgnore] private Tile[] graph
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

				//Debug.Log($"Rebuilding graph | state first 8: [{string.Join(", ", tiles.Take(8))}]");

				for (int visualIndex = 0; visualIndex < _graph.Length; visualIndex++)
				{
					UpdateGraphTile(visualIndex, allocate: true);
				}

				return _graph;
			}
		}

		private void UpdateGraph()
		{
			if (state == null || graphCount != state.Length)
			{
				Debug.Assert(false, "mismatched indices and runtime tiles");
				return;
			}

			for (int visualIndex = 0; visualIndex < state.Length; ++visualIndex)
			{
				int logicalIndex = state[visualIndex];
				if (logicalIndex < 0 || logicalIndex >= _graph.Length) continue;

				var mapTile = _graph[logicalIndex];
				var go = mapTile.gameObject;
				if (go == null) continue;

				UpdateGraphTile(visualIndex, allocate: false);
			}
		}

		private void UpdateGraphTile(int visualIndex, bool allocate = true)
		{
			var variant = GetVariantForIndex(visualIndex);  // ← visualIndex is correct for variant lookup? Wait — no!
															// FIX: variant is based on logical position, not visual

			// Correct: lookup logical first
			int logicalIndex = allocate ? visualIndex : State[visualIndex];  // during creation: visual == logical
			variant = GetVariantForIndex(logicalIndex);

			var position = TileWorldPosition(visualIndex);

			GameObject go;

			if (allocate)
			{
				_graph[visualIndex] = new Tile(variant, parent, position);
				go = _graph[visualIndex].gameObject;
			}
			else
			{
				go = _graph[State[visualIndex]].gameObject;  // existing tile at logical position
			}

			if (go != null)
			{
				var finalPos = position + variant.delta;
				go.transform.position = finalPos;

#if DEBUG
				var displayPos = finalPos - tile_origin;
				var def = ResourceManager.GetDefinition(variant.hash);
				go.name = $"{def?.name ?? "??"} ({displayPos.x:F1},{displayPos.z:F1})+{variant.delta:F2}@{variant.angle:F1}°";
#endif
			}
		}

		public Vector3 TileNormalisedWorldPosition(int index) => new(index % width, 0f, index / width);
		public static Vector3 TileNormalisedSnappedMapPosition(Vector3 vec) => new (Mathf.FloorToInt(vec.x), 0f, Mathf.FloorToInt(vec.z));

#if UNITY_EDITOR
		private static readonly Vector3 tile_origin = new(0.5f, 0f, 0.5f);
		public Vector3 TileWorldPosition(int index) => TileNormalisedWorldPosition(index) + tile_origin;
		public int WorldToMapIndex(Vector3 vec) => vec.x >= 0 && vec.x < width && vec.z >= 0 && vec.z < height ? (int)vec.z * width + (int)vec.x : -1;
		public static Vector3 SnappedMapPosition(Vector3 vec) => TileNormalisedSnappedMapPosition(vec) + tile_origin;
#else
        private static readonly Vector3 tile_origin = Vector3.zero;
        public Vector3 TileWorldPosition(int index) => TileNormalisedWorldPosition(index);
        public int WorldToMapIndex(Vector3 vec) { vec += new Vector3(0.5f, 0f, 0.5f); return vec.x >= 0 && vec.x < width && vec.z >= 0 && vec.z < height ? (int)vec.z * width + (int)vec.x : -1; }
        public static Vector3 SnappedMapPosition(Vector3 vec) => new Vector3(Mathf.FloorToInt(vec.x + 0.5f), 0f, Mathf.FloorToInt(vec.z + 0.5f));
#endif

		public static Vector3 ScreenToWorldSnapped(Camera camera, Vector3 screenPos) => SnappedMapPosition(ScreenToWorld(camera, Input.mousePosition));
		public Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => tileIndex < 0 ? worldPosition : worldPosition - TileWorldPosition(tileIndex);
		public Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => tileIndex < 0 ? localPosition : localPosition + TileWorldPosition(tileIndex);

		public HashId GetTileID(int index)
		{
			if (tiles == null || index < 0 || index >= tiles.Length)
				return 0;

			var tableIdx = tiles[index];
			if (tableIdx >= 0 && tableIdx < variants.Length)
				return variants[tableIdx].hash;

			return 0;
		}

		public Tile GetTile(int index) => state == null || index < 0 || index >= state.Length ? default : GetGraphTile(state[index]);
		private Tile GetGraphTile(int graphIndex) => _graph == null || graphIndex < 0 || graphIndex >= _graph.Length ? default : _graph[graphIndex];

		private void DestroyAllTiles()
		{
			if (_graph == null)
				return;

			foreach (var tile in _graph)
				tile.Destroy();

			_graph = null;
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

		private void RecreateTiles()
		{
			DestroyAllTiles();
			var _ = graph;
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

		public bool IsDefinitionUsed(HashId hashId)
		{
			if (hashId == 0) return false;
			return variants?.Any(v => v.hash == hashId) == true;
		}

		public int DefinitionUsageCount(HashId hashId)
		{
			if (hashId == 0) return 0;
			return variants?.Count(v => v.hash == hashId) ?? 0;
		}

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
		// Original methods — adapted to variants
		// ─────────────────────────────────────────────

		private bool Consolidate()
		{
			if (tiles == null || tiles.Length == 0 || variants == null || variants.Length == 0)
				return false;

			// Group by full Variant identity (hash + angle + delta)
			var grouped = tiles
				.Select(idx => idx >= 0 && idx < variants.Length ? variants[idx] : new Variant(0))
				.GroupBy(v => (v.hash, v.angle, v.delta))   // tuple key = full identity
				.Select(g => new
				{
					Variant = g.Key,
					Count = g.Count(),
					// OriginalVariants = g.ToList()   // removed because it was never used
				})
				.OrderByDescending(g => g.Count)
				.ThenBy(g => g.Variant.hash)// stable secondary sort
				.ThenBy(g => g.Variant.angle)
				.ThenBy(g => g.Variant.delta, Vector3LexComparer.Instance)
				.ToList();

			var newVariants = grouped
				.Select(g => new Variant(g.Variant.hash, g.Variant.delta, g.Variant.angle))
				.ToArray();

			// Build lookup: old key → new index
			var oldToNew = new Dictionary<(HashId, float, Vector3), int>(grouped.Count);
			for (int i = 0; i < newVariants.Length; i++)
			{
				var v = newVariants[i];
				oldToNew[(v.hash, v.angle, v.delta)] = i;
			}

			// Remap tiles to new indices
			var newTiles = new int[tiles.Length];
			for (int i = 0; i < tiles.Length; i++)
			{
				int oldIdx = tiles[i];
				var oldVariant = oldIdx >= 0 && oldIdx < variants.Length ? variants[oldIdx] : new Variant(0);
				var key = (oldVariant.hash, oldVariant.angle, oldVariant.delta);
				newTiles[i] = oldToNew[key];
			}

			// Detect what actually changed
			bool sizeChanged = newVariants.Length != variants.Length;

			bool orderChanged = !sizeChanged &&
				!variants.Select(v => (v.hash, v.angle, v.delta))
						 .SequenceEqual(newVariants.Select(v => (v.hash, v.angle, v.delta)));

			bool anythingChanged = sizeChanged || orderChanged;

			if (anythingChanged)
			{
				variants = newVariants;
				tiles = newTiles;

				if (sizeChanged)
				{
					string direction = newVariants.Length > variants.Length ? "increased" : "reduced";
					Debug.Log($"{name} consolidated: table size {direction} from {variants.Length} → {newVariants.Length}");
				}
				else if (orderChanged)
				{
					Debug.Log($"{name} consolidated: table order changed (size remains {newVariants.Length})");
				}

				// Invalidate caches
				_graph = null;
			}

			return anythingChanged;
		}

		private bool RepositionAndResize(int expandToX = 0, int expandToZ = 0)
		{
			if (tiles == null || tiles.Length == 0) return false;

			int targetWidth, targetHeight, offsetX, offsetZ;

			if (expandToX != 0 || expandToZ != 0)
			{
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
				var (minX, minZ, maxX, maxZ) = GetContentBounds();
				if (maxX < 0) return false;

				targetWidth = maxX - minX + 1;
				targetHeight = maxZ - minZ + 1;
				offsetX = -minX;
				offsetZ = -minZ;
			}

			if (targetWidth == width && targetHeight == height && offsetX == 0 && offsetZ == 0)
				return false;

			if (targetWidth > MAP_MAX_SIZE || targetHeight > MAP_MAX_SIZE)
			{
#if VERBOSE
				Debug.LogWarning($"Resize rejected: would exceed max size ({MAP_MAX_SIZE}x{MAP_MAX_SIZE})");
#endif
				return false;
			}
#if VERBOSE
			Debug.Log($"Resize Map '{name}' to {targetWidth}x{targetHeight}");
#endif
			int oldWidth = width;
			int oldHeight = height;
			int newSize = targetWidth * targetHeight;

			int defaultIndex = -1;

			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			var defaultHash = defaultDef.HashID;

			for (int i = 0; i < variants.Length; i++)
			{
				var def = ResourceManager.GetDefinition(variants[i].hash);
				if (def != null && def.IsDefaultEquivalent())
				{
					defaultIndex = i;
					break;
				}
			}

			if (defaultIndex == -1)
			{
				variants = variants.Concat(new[] { new Variant(defaultHash) }).ToArray();
				defaultIndex = variants.Length - 1;
			}

			var newTiles = new int[newSize];
			Array.Fill(newTiles, defaultIndex);

			var newSolve = new int[newSize];

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
			bool resized = RepositionAndResize();

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

				int hash = (t < variants.Length) ? variants[t].hash : 0;
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
			light = light,
			skybox = skybox,
			effect = effect,
			button = button,
			width = width,
			height = height,

			waypoints = waypoints != null ? (int[])waypoints.Clone() : null,
			tiles = tiles != null ? (int[])tiles.Clone() : null,
			solve = solve != null ? (int[])solve.Clone() : null,

			attachments = attachments != null ? attachments.Select(a => a.ShallowClone()).ToArray() : Array.Empty<MapAttachment>(),
			variants = variants != null ? variants.Select(v => new Variant(v.hash, v.delta, v.angle)).ToArray() : Array.Empty<Variant>()
		};

		public int CameraHitTile(Camera camera, Vector3 position) => WorldToMapIndex(ScreenToWorld(camera, position));

		public Variant CameraHitVariant(Camera camera, Vector3 position) => GetVariantAt(CameraHitTile(camera, Input.mousePosition));

		public Definition CameraHitDefinition(Camera camera, Vector3 position) => GetDefinitionAt(CameraHitTile(camera, Input.mousePosition));

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
			if (default == direction) direction = camera.transform.forward;
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

			Debug.LogWarning("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (waypoints?.Length > 0) return waypoints[waypoints.Length - 1];

			for (int i = 0; i < width * height; ++i)
				if (GetTile(i).IsEnd) return i;

			Debug.LogWarning("No end tile found!");
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

		public bool UpdateTileAt(Vector3 pos, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f) => UpdateTileAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), hashId, delta, angle);

		public bool UpdateTileAt(int x, int z, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f)
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

			// ────────────────────────────────────────────────────────────────
			// Find or create variant with exact hash + angle + delta
			// ────────────────────────────────────────────────────────────────
			int tableIndex = -1;

			// First: look for exact match (hash + angle + delta)
			for (int i = 0; i < variants.Length; i++)
			{
				var v = variants[i];
				if (v.hash == hashId &&
					Mathf.Approximately(v.angle, angle) &&
					Vector3LexComparer.ApproximatelyEqual(v.delta, delta))
				{
					tableIndex = i;
					break;
				}
			}

			// If no exact match → create new variant
			if (tableIndex == -1)
			{
				var newVariant = new Variant(hashId, delta, angle);

				variants = variants != null
					? variants.Concat(new[] { newVariant }).ToArray()
					: new[] { newVariant };

				tableIndex = variants.Length - 1;
			}

			tiles[index] = tableIndex;

			// ────────────────────────────────────────────────────────────────
			// Rest of the method unchanged
			// ────────────────────────────────────────────────────────────────
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
				//Debug.Log($"Using {waypoints.Length} predefined waypoints.");
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

			//if (windController != null)
			//	Debug.Log($"WindController initialized with {windController.SwayComponents.Count} sway components.");
		}

		public Map()
		{
			// Optionally initialize minimal safe state here
			variants = Array.Empty<Variant>();
			tiles = Array.Empty<int>();
			// state, attachments, etc. can stay null or empty
		}

		public Map(int width = 16, int height = 16, string mapName = "New Map")
		{
			if (width <= 0 || height <= 0)
				throw new ArgumentException("Map dimensions must be positive");

			this.name = mapName;
			this.width = width;
			this.height = height;

			// Get (or create) the canonical default tile definition
			var defaultDef = ResourceManager.FindOrCreateDefaultTile();
			var defaultHash = defaultDef.HashID;

			// Minimal variant table: just the default tile (angle=0, delta=0)
			variants = new Variant[] { new Variant(defaultHash, Vector3.zero, 0f) };

			// Every position in the grid points to variant index 0
			int tileCount = width * height;
			tiles = new int[tileCount];
			Array.Fill(tiles, 0);           // 0 = default variant index

			// Optional: initialize solve array if you want it from the beginning
			solve = new int[tileCount];     // all zeros = identity permutation

			// Runtime state starts as identity
			state = Enumerable.Range(0, tileCount).ToArray();

			// Clear / null out optional arrays
			attachments = null;
			waypoints = null;
			music = null;
			skybox = null;
			character = null;
			button = null;
		}

		// Alternative: static factory method (sometimes cleaner to call)
		public static Map CreateEmpty(int width = 16, int height = 16, string name = null)
		{
			return new Map(width, height, name ?? $"Map {width}×{height}");
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

		public GameObject BuildPreviewGeometry(Transform previewParent, int layer)
		{
			if (width <= 0 || height <= 0 || tiles == null || variants == null)
				return null;

			// CRITICAL: Work on a CLONE so we don't corrupt the original map's runtime state
			var previewMap = this.Clone();

			var previewRoot = new GameObject($"Preview_{name ?? "Map"}");
			previewRoot.transform.SetParent(previewParent, false);
			previewRoot.transform.localPosition = Vector3.zero;

			var originalParent = previewMap.parent;
			previewMap.parent = previewRoot.transform;

			try
			{
				previewMap.Preset();                // identity state on clone
				var _ = previewMap.graph;           // force tile creation on clone

				if (previewMap._graph == null || previewMap._graph.Length == 0)
				{
					Debug.LogWarning("Preview graph creation failed on clone");
					UnityEngine.Object.DestroyImmediate(previewRoot);
					return null;
				}

				previewMap.RefreshAttachments(previewMap.GetAttachments());

				// Apply layer recursively
				//previewRoot.transform.SetLayer(layer, true);

				PreviewRenderLayers.SetLayerRecursively(previewRoot, PreviewRenderLayers.LAYER_PREVIEW);
				PreviewRenderLayers.SetPreviewLayersToChildren(previewRoot.transform);

				var particleControllers = previewRoot.GetComponentsInChildren<ParticleController>(true);
				foreach (var particleController in particleControllers)
					particleController.gameObject.layer = PreviewRenderLayers.previewTransparentLayer;

				var lights = previewRoot.GetComponentsInChildren<Light>(true);
				foreach (var light in lights)
					PreviewRenderLayers.SetPreviewLayers(light, false); // Preview only //light.cullingMask = 1 << LayerMask.NameToLayer("Preview");

				//// Optional: disable unwanted scripts/components
				//foreach (var tile in previewMap._graph)
				//{
				//	if (tile.gameObject == null) continue;

				//	//// Disable things that shouldn't run in editor preview
				//	//foreach (var mb in tile.gameObject.GetComponentsInChildren<MonoBehaviour>(true))
				//	//{
				//	//	if (mb is MorphGeomSway ||
				//	//		mb is WindController)           // optional
				//	//	{
				//	//		mb.enabled = false;
				//	//	}
				//	//}

				//	//// Optional: turn off shadows for performance
				//	//foreach (var mr in tile.gameObject.GetComponentsInChildren<Renderer>(true))
				//	//{
				//	//	mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				//	//	mr.receiveShadows = false;
				//	//}
				//}


				return previewRoot;
			}
			catch (Exception e)
			{
				Debug.LogError($"Preview build failed: {e.Message}");
				UnityEngine.Object.DestroyImmediate(previewRoot);
				return null;
			}
			finally
			{
				// Restore original parent on clone (not needed, but clean)
				previewMap.parent = originalParent;

				// IMPORTANT: clean up the clone's runtime tiles
				// (prevents memory leak from dangling GameObjects)
				//previewMap.DestroyAllTiles();

				// Clone itself can be GC'd — no need to destroy it explicitly
			}
		}

		[JsonIgnore] public UnityRenderSettings RenderSettings => new (
			ambientMode: UnityEngine.Rendering.AmbientMode.Flat,
			ambientLight: Light,
			ambientIntensity: 1f,
			skybox: SkyboxMaterial,
			ambientProbe: default,
			subtractiveShadowColor: UnityEngine.RenderSettings.subtractiveShadowColor);

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

			var lights = parent.GetComponentsInChildren<Light>(true);
			foreach (var light in lights)
				PreviewRenderLayers.RemovePreviewLayers(light); //light.cullingMask &= ~(1 << LayerMask.NameToLayer("Preview"));

			PreviewRenderLayers.RemovePreviewLayersFromChildren(parent);

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