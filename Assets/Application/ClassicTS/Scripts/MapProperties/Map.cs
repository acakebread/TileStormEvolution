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
		public HashId hash;
		public Vector3 delta;           // local position offset (usually small x/z values)
		public float angle;             // degrees, usually 0/90/180/270

		// ─── constructors (unchanged) ────────────────────────────────────────
		public Variant(HashId h) : this(h, Vector3.zero, 0f) { }
		public Variant(HashId h, Vector3 offset, float rotationDegrees)
		{
			hash = h;
			delta = offset;
			angle = rotationDegrees;
		}

		public static implicit operator HashId(Variant v) => v.hash;

		public readonly Definition definition => ResourceManager.GetDefinition(hash);

		public readonly bool IsDefaultEquivalent => definition != null && definition.IsDefaultEquivalent();
		public readonly bool HasNav => definition != null && definition.Nav != 0;
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
		int UpdateTileAt(int x, int z, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f, bool allowResize = true);
		int UpdateTileAt(Vector3 pos, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f, bool allowResize = true);
		int UpdateTileAt(Vector3 pos, Variant variant, bool allowResize = true);
		Vector3 ResizeMap(Rect extents);
		Vector3 ResizeMap(RectInt extents);
		bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null);

		bool RemoveTileAt(int x, int z);//does not affect bounds
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

			int tableIdx = tiles[mapIndex];
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

			for (int visualIndex = 0; visualIndex < state.Length; ++visualIndex)
			{
				int logicalIndex = state[visualIndex];
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

		public static bool ValidExtents(Rect extents) => (Mathf.FloorToInt(extents.xMax) - Mathf.FloorToInt(extents.xMin)) < MAP_MAX_SIZE && (Mathf.FloorToInt(extents.yMax) - Mathf.FloorToInt(extents.yMin)) < MAP_MAX_SIZE;
		public static bool ValidExtents(RectInt extents) => extents.width <= MAP_MAX_SIZE && extents.height <= MAP_MAX_SIZE;

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
				for (int i = 0; i < waypoints.Length; i++)
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

		public Bounds GetTileGeometryBounds(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= width * height)
			{
				Vector3 center = TileRenderPosition(tileIndex);
				return new Bounds(center, Vector3.zero);
			}

			var tile = GetTile(tileIndex);

			if (tile.gameObject == null)
			{
				Vector3 center = TileRenderPosition(tileIndex);
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

		public int UpdateTileAt(Vector3 pos, Variant variant, bool allowResize = true)
		{
			var snapped = variant.HasNav ? FullFloorVec(pos) : HalfFloorVec(pos);
			var delta = new Vector3(((snapped.x % 1f) + 1f) % 1f, snapped.y, ((snapped.z % 1f) + 1f) % 1f);//make sure valid delta for variant
			return UpdateTileAt(pos, variant.hash, delta, variant.angle, allowResize);
		}

		public int UpdateTileAt(Vector3 pos, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f, bool allowResize = true) => UpdateTileAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z), hashId, delta, angle, allowResize);

		public int UpdateTileAt(int x, int z, HashId hashId, Vector3 delta = new Vector3(), float angle = 0f, bool allowResize = true)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot update tile: map has no tiles array");
				return -1;
			}

			delta = new Vector3(((delta.x % 1f) + 1f) % 1f, delta.y, ((delta.z % 1f) + 1f) % 1f);//make sure valid delta for variant

			int oldWidth = width;
			int oldHeight = height;
			(int minX, int minZ, int maxX, int maxZ) oldBounds = new(0, 0, width, height);

			Vector3 originDelta = Vector3.zero;
			bool sizeChanged = false;

			if (allowResize)
			{
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
						return -1;
					}
				}
			}

			int index = z * width + x;

			// ────────────────────────────────────────────────────────────────
			// Find or create variant with exact hash + angle + delta
			// ────────────────────────────────────────────────────────────────
			int tableIndex = this.GetOrCreateVariantIndex(hashId, delta, angle);
			tiles[index] = tableIndex;

			// ────────────────────────────────────────────────────────────────
			// Rest of the method unchanged
			// ────────────────────────────────────────────────────────────────
			bool cropped = false;

			var def = ResourceManager.GetDefinition(hashId);
			bool isDefaultTile = def?.IsDefault() ?? false;

			if (allowResize)
			{
				//if (isDefaultTile || sizeChanged)//for now always try to crop the map because it may not currently be cropped due to RemoveTileAt 
				{
					var (minX, minZ, maxX, maxZ) = MapUtils.GetContentBounds(this);
					cropped = CropToContent();

					if (cropped)
					{
						var dx = oldBounds.minX - minX;
						var dz = oldBounds.minZ - minZ;
						originDelta += new Vector3(dx, 0, dz);
						x += dx;
						z += dz;
						sizeChanged = true;
					}
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
				graph[index] = CreateTile(variants[tableIndex], parent, TileRenderPosition(index));
				RefreshAttachments(GetAttachments(tileIndex: index));
			}

			OnMapEdited?.Invoke(this, boundsChanged, originDelta);
			return z * width + x;//recalculate index
		}

		public bool RemoveTileAt(Vector3 pos) => RemoveTileAt(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z));
		public bool RemoveTileAt(int x, int z)
		{
			if (tiles == null || tiles.Length == 0)
			{
				Debug.LogError("Cannot update tile: map has no tiles array");
				return false;
			}

			var index = z * width + x;
			int tableIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);//find table index of empty tile
			tiles[index] = tableIndex;
			GetGraphTile(index).Destroy();
			graph[index] = CreateTile(variants[tableIndex], parent, TileRenderPosition(index));
			RefreshAttachments(GetAttachments(tileIndex: index));

			return true;
		}

		// ─────────────────────────────────────────────
		// Original methods — adapted to variants
		// ─────────────────────────────────────────────

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
			int oldWidth = width;
			int oldHeight = height;
			int newSize = targetWidth * targetHeight;

			int defaultIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);

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
								newSolve[newPos] = newSrcPos - newPos;
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
				return (nx >= 0 && nx < targetWidth && nz >= 0 && nz < targetHeight) ? nz * newW + nx : -1;
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

		public bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null)
		{
			bool resized = RepositionAndResize();

			bool consolidated = false;
			if (consolidate)
				consolidated = this.Optimise();

			return resized || consolidated;
		}

		public Vector3 ResizeMap(Rect extents) => ResizeMap(new RectInt(Mathf.FloorToInt(extents.xMin), Mathf.FloorToInt(extents.yMin), Mathf.FloorToInt(extents.xMax) - Mathf.FloorToInt(extents.xMin), Mathf.FloorToInt(extents.yMax) - Mathf.FloorToInt(extents.yMin)));

		public Vector3 ResizeMap(RectInt extents)
		{
			if (tiles == null || variants == null || width <= 0 || height <= 0)
			{
				Debug.LogWarning("Cannot resize map: invalid or empty map data");
				return Vector3.zero;
			}

			int desiredMinX = extents.xMin;
			int desiredMinZ = extents.yMin;
			int desiredMaxX = extents.xMax;
			int desiredMaxZ = extents.yMax;

			int targetWidth = desiredMaxX - desiredMinX + 1;
			int targetHeight = desiredMaxZ - desiredMinZ + 1;

			if (targetWidth <= 0 || targetHeight <= 0)
			{
				Debug.LogWarning($"Invalid resize bounds: {extents} → size {targetWidth}×{targetHeight}");
				return Vector3.zero;
			}

			if (targetWidth > MAP_MAX_SIZE || targetHeight > MAP_MAX_SIZE)
			{
				Debug.LogWarning($"Requested map size {targetWidth}×{targetHeight} exceeds maximum {MAP_MAX_SIZE}");
				return Vector3.zero;
			}

			// ────────────────────────────────────────────────────────────────
			// The offset is how much we need to shift existing content so that
			// the point that was at (desiredMinX, desiredMinZ) becomes new (0,0)
			// ────────────────────────────────────────────────────────────────
			int offsetX = -desiredMinX;
			int offsetZ = -desiredMinZ;

			// No-op check — exact same size and position
			if (targetWidth == width && targetHeight == height && offsetX == 0 && offsetZ == 0)
				return Vector3.zero;

			// The amount the old (0,0) moves in world space
			// (this is what UpdateTileAt accumulates in originDelta)
			Vector3 originDeltaWorld = new Vector3(offsetX, 0f, offsetZ);

			// ────────────────────────────────────────────────────────────────
			// Prepare new grid
			// ────────────────────────────────────────────────────────────────
			int oldWidth = width;
			int oldHeight = height;
			int newSize = targetWidth * targetHeight;

			var newTiles = new int[newSize];
			var newSolve = new int[newSize];

			int defaultIndex = this.GetOrCreateVariantIndex(ResourceManager.DefaultHash);//find table index of empty tile
			Array.Fill(newTiles, defaultIndex);
			Array.Fill(newSolve, 0);

			// ────────────────────────────────────────────────────────────────
			// Copy old content with offset (same logic as RepositionAndResize)
			// ────────────────────────────────────────────────────────────────
			for (int oldZ = 0; oldZ < oldHeight; oldZ++)
			{
				for (int oldX = 0; oldX < oldWidth; oldX++)
				{
					int oldIndex = oldZ * oldWidth + oldX;
					if (oldIndex >= tiles.Length) continue;

					int newX = oldX + offsetX;
					int newZ = oldZ + offsetZ;

					if (newX >= 0 && newX < targetWidth && newZ >= 0 && newZ < targetHeight)
					{
						int newIndex = newZ * targetWidth + newX;
						newTiles[newIndex] = tiles[oldIndex];

						// Remap solve delta — exact same as RepositionAndResize
						if (solve != null && oldIndex < solve.Length)
						{
							int delta = solve[oldIndex];
							if (delta != 0)
							{
								int oldSrcIdx = oldIndex + delta;
								if (oldSrcIdx >= 0 && oldSrcIdx < oldWidth * oldHeight)
								{
									int oldSrcX = oldSrcIdx % oldWidth;
									int oldSrcZ = oldSrcIdx / oldWidth;

									int newSrcX = oldSrcX + offsetX;
									int newSrcZ = oldSrcZ + offsetZ;

									if (newSrcX >= 0 && newSrcX < targetWidth &&
										newSrcZ >= 0 && newSrcZ < targetHeight)
									{
										int newSrcIdx = newSrcZ * targetWidth + newSrcX;
										newSolve[newIndex] = newSrcIdx - newIndex;
									}
								}
							}
						}
					}
				}
			}

			// ────────────────────────────────────────────────────────────────
			// Remap waypoints and attachments — identical to RepositionAndResize
			// ────────────────────────────────────────────────────────────────
			if (waypoints != null)
			{
				for (int i = 0; i < waypoints.Length; i++)
				{
					int oldTile = waypoints[i];
					if (oldTile < 0) continue;

					int ox = oldTile % oldWidth;
					int oz = oldTile / oldWidth;

					int nx = ox + offsetX;
					int nz = oz + offsetZ;

					waypoints[i] = (nx >= 0 && nx < targetWidth && nz >= 0 && nz < targetHeight) ? nz * targetWidth + nx : -1;
				}
			}

			if (attachments != null)
			{
				foreach (var att in attachments)
				{
					if (att.tile < 0) continue;

					int ox = att.tile % oldWidth;
					int oz = att.tile / oldWidth;

					int nx = ox + offsetX;
					int nz = oz + offsetZ;

					att.tile = (nx >= 0 && nx < targetWidth && nz >= 0 && nz < targetHeight) ? nz * targetWidth + nx : -1;
				}
			}

			// ────────────────────────────────────────────────────────────────
			// Apply
			// ────────────────────────────────────────────────────────────────
			width = targetWidth;
			height = targetHeight;
			tiles = newTiles;
			solve = newSolve;   // we keep it even if all zero — simpler downstream
			state = Enumerable.Range(0, newSize).ToArray();

			RecreateTiles();
			RefreshAttachments(GetAttachments());

			OnMapEdited?.Invoke(this, true, originDeltaWorld);

#if VERBOSE
			Debug.Log($"ResizeMap({extents}) → {width}×{height}  | origin delta {originDeltaWorld}");
#endif

			return originDeltaWorld;
		}
	}
}