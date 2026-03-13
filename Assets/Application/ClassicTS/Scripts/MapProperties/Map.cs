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

		int VectorToIndex(Vector3 _);
		Vector3 TileRenderPosition(int _);

		Tile GetTile(int _);
		Tile GetTile(Vector3 _);

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

		Vector3 IndexToVector(int index);
		HashId GetTileID(int _);

		int UpdateTileAt(Vector3 pos, Variant variant);
		int InsertTileAt(Vector3 pos, Variant variant);

		Vector3 ResizeMap(RectInt extents, bool cropToContent = true);
		bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null);

		bool RemoveTileAt(Vector3 pos);//does not affect bounds
		Variant GetVariantAt(int mapIndex);
		Variant GetVariantAt(Vector3 pos);
		void AddAttachment(MapAttachment _);
		bool RemoveAttachment(MapAttachment _);
		bool RemoveAttachments(MapAttachment[] _);
		void RefreshAttachment(MapAttachment _);
		void RefreshAttachments(MapAttachment[] _);

		int CameraHitTile(Camera camera, Vector3 position);
		Variant CameraHitVariant(Camera camera, Vector3 position);
		Definition CameraHitDefinition(Camera camera, Vector3 position);
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
		[JsonIgnore] public Transform parent { get; set; }//had to make public for now for preview creation in maputil

		[JsonIgnore] public int Width => width;
		[JsonIgnore] public int Height => height;
		[JsonIgnore] public int Count => Width * Height;
		[JsonIgnore] public int[] State { get => state = state?.Length == width * height ? state : Enumerable.Range(0, width * height).ToArray(); }//set => state = value; 
		[JsonIgnore] public string Music { get => music; set => music = value; }
		[JsonIgnore] public string Skybox { get => skybox; set => skybox = value; }
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

			var tableIdx = tiles[mapIndex];
			if (tableIdx >= 0 && tableIdx < variants.Length)
				return variants[tableIdx];

			return new Variant(0);
		}

		public Variant GetVariantAt(Vector3 pos) => GetVariantAt(VectorToIndex(pos));
		public Variant GetVariantAt(int mapIndex)
		{
			if (state == null || mapIndex < 0 || mapIndex >= state.Length)
				return new Variant(0);

			// Apply current permutation (scrambled/solved state)
			var logicalIndex = state[mapIndex];

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

#if VERBOSE
				Debug.Log($"Rebuilding graph | state first 8: [{string.Join(", ", tiles.Take(8))}]");                                                                                                    
#endif

				for (int visualIndex = 0; visualIndex < _graph.Length; visualIndex++)
				{
					Debug.Assert(tiles[visualIndex] >= 0 && tiles[visualIndex] < variants.Length, "variant index out of bounds");
					_graph[visualIndex] = CreateTile(variants[tiles[visualIndex]], parent, TileRenderPosition(visualIndex));
#if DEBUG
					UpdateGraphTileInfo(visualIndex);
#endif
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

			for (var visualIndex = 0; visualIndex < state.Length; ++visualIndex)
			{
				var logicalIndex = state[visualIndex];
				if (logicalIndex < 0 || logicalIndex >= _graph.Length) continue;

				var mapTile = _graph[logicalIndex];
				var go = mapTile.gameObject;
				if (go == null) continue;

				Debug.Assert(tiles[visualIndex] >= 0 && tiles[visualIndex] < variants.Length, "variant index out of bounds");
				go.transform.position = TileRenderPosition(visualIndex) + variants[tiles[visualIndex]].delta;//reset position
#if DEBUG
				UpdateGraphTileInfo(State[visualIndex]);
#endif
			}
		}

		internal bool InitialiseGraph()
		{
			var _ = graph;// force tile creation on clone
			return _graph != null && 0 != _graph.Length;
		}

		internal void InvalidateGraphCache()
		{
			_graph = null;
		}

		private void UpdateGraphTileInfo(int index)
		{
			var go = _graph[index].gameObject;
			if (null == go) return;
			var variant = GetVariantForIndex(index);
			var def = ResourceManager.GetDefinition(variant.hash);
			go.name = $"{def?.name ?? "??"} ({go.transform.position.x:F1},{go.transform.position.z:F1})+{variant.delta:F2}@{variant.angle:F1}°";
		}

		private void SetupWaypoints()
		{
			if (waypoints != null && waypoints.Length > 0)
				return;

			var generated = new List<int>();
			var start = GetStartTile();
			var end = GetEndTile();

			if (start == -1 || end == -1)
			{
				waypoints = generated.ToArray();
				return;
			}

			generated.Add(start);

			var cur = start;
			var dir = Navigation.NavToDest(this, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (FindAdjacentConsole(cur) != -1)
						generated.Add(cur);

					var next = Navigation.GetAdjacentTile(this, cur, dir);
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

			for (var n = 0; n < graphCount; ++n)
			{
				var go = GetGraphTile(n).gameObject;
				if (go == null) continue;

				var sway = go.GetComponent<MorphGeomSway>();
				if (sway == null) continue;

				windController = windController ?? parent.gameObject.AddComponent<WindController>();
				windController.AddSway(sway, TileRenderPosition(n));
			}
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
			var tileCount = width * height;
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
		public static Map CreateEmpty(int width = 16, int height = 16, string name = null) => new Map(width, height, name ?? $"Map {width}×{height}");

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

		[JsonIgnore] public UnityRenderSettings RenderSettings => new(
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
				PreviewRenderLayers.RemovePreviewLayers(light);

			PreviewRenderLayers.RemovePreviewLayersFromChildren(parent);

			SetupWaypoints();
		}

		private Tile CreateTile(Variant variant, Transform parent, Vector3 renderPosition) => new Tile(variant, parent, renderPosition);

		public static Vector3 FullFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x), vec.y, Mathf.FloorToInt(vec.z));
		public static Vector3 HalfFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x * 2f) * 0.5f, vec.y, Mathf.FloorToInt(vec.z * 2f) * 0.5f);

		private static readonly Vector3 HALF_TILE = new(0.5f, 0f, 0.5f);
#if UNITY_EDITOR
		public static Vector3 WorldToRender(Vector3 value) => value + HALF_TILE;
		public static Vector3 RenderToWorld(Vector3 value) => value - HALF_TILE;
#else
		public static Vector3 WorldToRender(Vector3 value) => value;
		public static Vector3 RenderToWorld(Vector3 value) => value;
#endif

		public static bool RayToWorld(Ray ray, out Vector3 point)
		{
			point = Vector3.zero;
			var plane = new Plane(Vector3.up, Vector3.zero);
			if (plane.Raycast(ray, out float d))
			{
				point = RenderToWorld(ray.GetPoint(d) + HALF_TILE);
				return true;
			}
			return false;
		}

		public static bool RayToPlane(Ray ray, Plane plane, out Vector3 point)
		{
			point = Vector3.zero;
			if (plane.Raycast(ray, out float d))
			{
				point = RenderToWorld(ray.GetPoint(d) + HALF_TILE);
				return true;
			}
			return false;
		}

		public static Vector3 ScreenToWorld(Camera camera, Vector3 screenPos, float offset = 0f)
		{
			if (null == camera) return Vector3.negativeInfinity;
			var plane = new Plane(Vector3.up, Vector3.up * offset);
			RayToPlane(camera.ScreenPointToRay(screenPos), plane, out Vector3 result);
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

		public static bool ValidExtents(RectInt extents) => extents.width < MAP_MAX_SIZE && extents.height < MAP_MAX_SIZE;

		public int VectorToIndex(Vector3 vec) => vec.x < 0 || vec.x >= width || vec.z < 0 || vec.z >= height ? -1 : Mathf.FloorToInt(vec.z) * width + Mathf.FloorToInt(vec.x);
		public Vector3 IndexToVector(int index) => new(index % width, 0f, index / width);
		public Vector3 TileRenderPosition(int index) => WorldToRender(IndexToVector(index));

		public int CameraHitTile(Camera camera, Vector3 position) => VectorToIndex(ScreenToWorld(camera, position));
		public Variant CameraHitVariant(Camera camera, Vector3 position) => GetVariantAt(CameraHitTile(camera, InputX.mousePosition));
		public Definition CameraHitDefinition(Camera camera, Vector3 position) => GetDefinitionAt(CameraHitTile(camera, InputX.mousePosition));

		public Quaternion LocalRotation(int tileIndex, Quaternion worldRotation) => worldRotation;
		public Quaternion WorldRotation(int tileIndex, Quaternion localRotation) => localRotation;

		public Vector3 LocalPosition(int tileIndex, Vector3 worldPosition) => tileIndex < 0 ? worldPosition : worldPosition - TileRenderPosition(tileIndex);
		public Vector3 WorldPosition(int tileIndex, Vector3 localPosition) => tileIndex < 0 ? localPosition : localPosition + TileRenderPosition(tileIndex);

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
		public Tile GetTile(Vector3 pos) => GetTile(VectorToIndex(pos));

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
				for (var i = 0; i < waypoints.Length; i++)
					waypointWrappers[i] = new Waypoint(i, waypoints[i]);
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

			Vector3 worldPos = TileRenderPosition(attachment.tile) + localPos;

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

				for (var i = 0; i < waypoints.Length; i++)
				{
					if (i != wp.waypointIndex)
						newWaypoints.Add(waypoints[i]);
				}

				waypoints = newWaypoints.Count > 0 ? newWaypoints.ToArray() : null;

				removed = true;
			}
			else if (attachments != null)
			{
				var idx = Array.IndexOf(attachments, attachment);
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

			var anyRemoved = false;

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

		public int GetStartTile()
		{
			if (waypoints?.Length > 0) return waypoints[0];

			for (var i = 0; i < width * height; ++i)
				if (GetTile(i).IsStart) return i;

			Debug.LogWarning("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			if (waypoints?.Length > 0) return waypoints[waypoints.Length - 1];

			for (var i = 0; i < width * height; ++i)
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
				var consoleIndex = Navigation.GetAdjacentTile(this, nTile, dirBit);
				if (consoleIndex == -1) continue;

				var consoleTile = GetTile(consoleIndex);
				if (consoleTile.IsConsole && dirBit == Navigation.GetOppositeDirection(consoleTile.Nav))
					return consoleIndex;
			}
			return -1;
		}

		public bool RemoveTileAt(Vector3 pos) => UpdateTileAt(pos, new Variant(ResourceManager.DefaultHash)) != -1;

		/// <summary>
		/// Updates a tile at the given grid position. Position must be within current map bounds.
		/// Does NOT perform automatic resizing or cropping.
		/// </summary>
		/// <returns>Final tile index if successful, -1 if out of bounds or other failure</returns>
		public int UpdateTileAt(Vector3 pos, Variant variant)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot update tile: map has no tiles array");
				return -1;
			}

			// Strict bounds check — no resize allowed in this version
			var index = VectorToIndex(pos);
			if (-1 == index)
			{
				Debug.LogWarning($"Cannot update tile at ({pos}) — position out of bounds and resizing is not allowed in UpdateTileAt");
				return -1;
			}

			// Normalize delta to [0,1) range
			variant.delta = new Vector3(Mathf.Repeat(pos.x, 1f), pos.y, Mathf.Repeat(pos.z, 1f));// delta is +ve modulo
			
			// ────────────────────────────────────────────────────────────────
			// Update rendering / graph (single tile update path)
			// ────────────────────────────────────────────────────────────────

			// No resize/crop in this version — just update the single tile
			var def = ResourceManager.GetDefinition(variant.hash);
			var tableIndex = this.GetOrCreateVariantIndex(variant.hash, variant.delta, variant.angle);// Find or create variant entry
			tiles[index] = tableIndex;
			GetGraphTile(index).Destroy();
			graph[index] = CreateTile(variants[tableIndex], parent, TileRenderPosition(index));
			RefreshAttachments(GetAttachments(tileIndex: index));

			// No bounds change possible in this version
			OnMapEdited?.Invoke(this, false, Vector3.zero);

			return index;
		}

		public int InsertTileAt(Vector3 pos, Variant variant)
		{
			var extents = GeomUtils.PointArrayBoundsInt(new[] { new Vector2Int(0, 0), new Vector2Int(width - 1, height - 1), new Vector2Int(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z)) });
			if(!ValidExtents(extents)) return -1;
			var originDelta = ResizeMap(extents, false);
			pos += originDelta;
			if (-1 == UpdateTileAt(pos, variant)) return -1;
			pos += ResizeMap(extents, true);
			return VectorToIndex(pos);
		}

		private bool RepositionAndResize(int expandToX = 0, int expandToZ = 0, int expandToXnax = 0, int expandToZnax = 0)
		{
			if (tiles == null || tiles.Length == 0) return false;

			int targetWidth, targetHeight, offsetX, offsetZ;

			if (expandToX != 0 || expandToZ != 0 || expandToXnax != 0 || expandToZnax != 0)
			{
				var minX = expandToX;
				var minZ = expandToZ;
				if (expandToXnax == 0) minX = Mathf.Min(0, expandToX);
				if (expandToZnax == 0) minZ = Mathf.Min(0, expandToZ);

				if (expandToXnax == 0) expandToXnax = Mathf.Max(width - 1, expandToXnax);
				if (expandToZnax == 0) expandToZnax = Mathf.Max(height - 1, expandToZnax);
				expandToXnax = Mathf.Max(expandToX, expandToXnax);
				expandToZnax = Mathf.Max(expandToZ, expandToZnax);

				var maxX = expandToXnax;
				var maxZ = expandToZnax;

				targetWidth = maxX - minX + 1;
				targetHeight = maxZ - minZ + 1;
				offsetX = -minX;
				offsetZ = -minZ;
			}
			else
			{
				var (minX, minZ, maxX, maxZ) = MapUtils.GetContentBounds(this);
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
			var oldWidth = width;
			var oldHeight = height;
			var newSize = targetWidth * targetHeight;

			var defaultIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);

			var newTiles = new int[newSize];
			Array.Fill(newTiles, defaultIndex);

			var newSolve = new int[newSize];

			for (var oldIdx = 0; oldIdx < oldWidth * oldHeight; oldIdx++)
			{
				if (oldIdx >= tiles.Length) continue;

				var newPos = Remap(oldIdx, oldWidth, targetWidth, offsetX, offsetZ);
				if (newPos < 0) continue;

				newTiles[newPos] = tiles[oldIdx];

				if (solve != null && oldIdx < solve.Length)
				{
					var delta = solve[oldIdx];
					if (delta != 0)
					{
						var oldSrcIdx = oldIdx + delta;
						if (oldSrcIdx >= 0 && oldSrcIdx < solve.Length)
						{
							var newSrcPos = Remap(oldSrcIdx, oldWidth, targetWidth, offsetX, offsetZ);
							if (newSrcPos >= 0)
								newSolve[newPos] = newSrcPos - newPos;
						}
					}
				}
			}

			int Remap(int idx, int oldW, int newW, int offX, int offZ)
			{
				if (idx < 0) return idx;
				var px = idx % oldW;
				var pz = idx / oldW;
				var nx = px + offX;
				var nz = pz + offZ;
				return (nx >= 0 && nx < targetWidth && nz >= 0 && nz < targetHeight) ? nz * newW + nx : -1;
			}

			if (waypoints != null)
				for (var n = 0; n < waypoints.Length; n++)
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

		public bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null)
		{
			var resized = RepositionAndResize();
			var optimised = false;
			if (consolidate) optimised = this.Optimise();
			return resized || optimised;
		}

		public Vector3 ResizeMap(RectInt extents, bool cropToContent = true)
		{
			var (minX, minZ, maxX, maxZ) = MapUtils.GetContentBounds(this);
			if (cropToContent)
				extents = new RectInt(minX, minZ, maxX - minX, maxZ - minZ);

			if (RepositionAndResize(extents.xMin, extents.yMin, extents.xMax, extents.yMax))
			{
				RecreateTiles();
				RefreshAttachments(GetAttachments());
				var originDelta = new Vector3(Mathf.Max(0, -extents.xMin) - minX, 0f, Mathf.Max(0, -extents.yMin) - minZ);
				OnMapEdited?.Invoke(this, true, originDelta);
#if VERBOSE
				Debug.Log($"ResizeMap({extents}) → {width}×{height}  | origin delta {originDeltaWorld}");
#endif
				return originDelta;
			}
			return Vector3.zero;
		}
	}
}