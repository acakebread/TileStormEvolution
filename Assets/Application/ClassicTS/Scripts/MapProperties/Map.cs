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

		Tile GetTile(int _, bool active = true);
		Tile GetTile(Vector3 _, bool active = true);

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

		RectInt ContentBounds();
		RectInt ResizeMap(RectInt extents);
		bool CropToContent(bool consolidate = false, Action<Vector2Int> onOriginDelta = null);

		bool RemoveTileAt(Vector3 pos);
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

		Material SkyboxMaterial { get; }
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
		[JsonProperty(Order = 4)] public string effect;
		[JsonProperty(Order = 5)] public string skybox;
		[JsonProperty(Order = 6)] public string button;

		// ─────────────────────────────────────────────
		// Dimensions and Tile Data
		// ─────────────────────────────────────────────
		private const int MAP_MAX_SIZE = 64;
		[JsonProperty(Order = 10)] public int width;
		[JsonProperty(Order = 11)] public int height;
		[JsonProperty(Order = 12)] public int[] tiles;
		[JsonProperty(Order = 13)] public int[] solve;
		public const int TableJsonOrder = 20;

		// ─────────────────────────────────────────────
		// Properties
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 30)] public int[] waypoints;
		[JsonProperty(Order = 31)] public MapAttachment[] attachments;

		// ─────────────────────────────────────────────
		// Light
		// ─────────────────────────────────────────────
		[JsonProperty(Order = 40)] public string ambient;
		[JsonProperty(Order = 41)] public string skyrgb;
		[JsonProperty(Order = 42)] public float[] skyvec;

		// Conditional serialization
		public bool ShouldSerializeeffect() => !string.IsNullOrEmpty(effect);
		public bool ShouldSerializesolve() => solve != null && solve.Length > 0;
		public bool ShouldSerializewaypoints() => waypoints != null && waypoints.Length > 0;
		public bool ShouldSerializeattachments() => attachments != null && attachments.Length > 0;
		public bool ShouldSerializeambient() => !string.IsNullOrEmpty(ambient);
		public bool ShouldSerializeskybox() => !string.IsNullOrEmpty(skybox);
		public bool ShouldSerializeskyvec() => null != skyvec;

		public Action<Map, bool, Vector3> OnMapEdited { get; set; }
		[JsonIgnore] public Transform parent { get; set; }

		[JsonIgnore] public int Width => width;
		[JsonIgnore] public int Height => height;
		[JsonIgnore] public int Count => Width * Height;
		[JsonIgnore] public int[] State { get => state = state?.Length == width * height ? state : Enumerable.Range(0, width * height).ToArray(); }

		[JsonIgnore] public string Music { get => music; set => music = value; }

		// Skybox setter - clean, no ref stuff
		[JsonIgnore]
		public string Skybox
		{
			get => skybox;
			set
			{
				if (skybox == value)
					return;
				skybox = value;
				TintedCubemap = null;
			}
		}

		[JsonIgnore]
		public ReflectionEffectCamera.EffectMode Effect
		{
			get => ReflectionEffectCamera.ParseEffectMode(string.IsNullOrEmpty(effect) ? "Water" : effect);
			set => effect = ReflectionEffectCamera.EffectModeToString(value);
		}

		public Action<ReflectionEffectCamera.EffectMode> OnEffectChanged;

		[JsonIgnore] private Color ambientRGB;
		[JsonIgnore]
		public Color AmbientRGB
		{
			get => null != ambient ? StringUtil.FromHexString(ambient, defaultColor: Color.white) : ambientRGB;
			set => ambient = value.ToHexString(includeAlpha: true);
		}

		[JsonIgnore] private Color skyRBG;
		[JsonIgnore]
		public Color SkyRGB
		{
			get => null != skyrgb ? StringUtil.FromHexString(skyrgb, defaultColor: Color.white) : skyRBG;
			set => skyrgb = value.ToHexString(includeAlpha: true);
		}

		[JsonIgnore]
		public Vector3 SkyVec
		{
			get => null != skyvec ? new(skyvec[0], skyvec[1], skyvec[2]) : Vector3.zero;
			set => skyvec = new float[] { value.x, value.y, value.z };
		}

		[JsonIgnore] private DirectionalLightUtility directionalLight;

		[JsonIgnore] public Material SkyboxMaterial => SkyboxUtility.GetSkyboxMaterialForName(Skybox);

		// ─────────────────────────────────────────────
		// Tile data — variants is now the only source of truth
		// ─────────────────────────────────────────────
		[JsonIgnore] public Variant[] variants = Array.Empty<Variant>();

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
				DestroyAllGraphTiles();
			}
		}

		private Variant GetVariantForIndex(int mapIndex)
		{
			if (tiles == null || variants == null || mapIndex < 0 || mapIndex >= tiles.Length)
				return new Variant(0);

			var tableIdx = tiles[mapIndex];
			return tableIdx < 0 || tableIdx >= variants.Length ? new Variant(0) : variants[tableIdx];
		}

		public Variant GetVariantAt(Vector3 pos) => GetVariantAt(VectorToIndex(pos));
		public Variant GetVariantAt(int mapIndex)
		{
			if (state == null || mapIndex < 0 || mapIndex >= state.Length)
				return new Variant(0);

			return GetVariantForIndex(state[mapIndex]);
		}

		public Definition GetDefinitionAt(int mapIndex)
		{
			if (state == null || mapIndex < 0 || mapIndex >= state.Length)
				return null;

			return ResourceManager.GetDefinition(GetVariantForIndex(state[mapIndex]).hash);
		}

		// ─────────────────────────────────────────────
		// Runtime tile (graph) instances
		// ─────────────────────────────────────────────
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
					_graph[visualIndex] = CreateTile(variants[tiles[visualIndex]], parent, TileRenderPosition(visualIndex));
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
			go.name = $"{def?.name ?? "??"} ({go.transform.position.x:F1},{go.transform.position.z:F1})+{variant.delta:F2}@{variant.angle:F1}°";
		}

		[JsonIgnore]
		public UnityRenderSettings RenderSettings => new(
			ambientMode: UnityEngine.Rendering.AmbientMode.Flat,
			ambientLight: AmbientRGB,
			ambientIntensity: 1f,
			skybox: SkyboxMaterial,
			ambientProbe: default,
			subtractiveShadowColor: UnityEngine.RenderSettings.subtractiveShadowColor);

		public Map() { }

		public Map(int width = 16, int height = 16, string mapName = "New Map")
		{
			if (width <= 0 || height <= 0)
				throw new ArgumentException("Map dimensions must be positive");

			this.name = mapName;
			this.width = width;
			this.height = height;

			var defaultDef = ResourceManager.FindOrCreateDefaultDefinition();
			var defaultHash = defaultDef.HashID;

			variants = new Variant[] { new(defaultHash) };

			var tileCount = width * height;
			tiles = new int[tileCount];
			Array.Fill(tiles, 0);

			solve = new int[tileCount];
			state = Enumerable.Range(0, tileCount).ToArray();

			attachments = null;
			waypoints = null;
			music = null;
			skybox = null;
			character = null;
			button = null;
		}

		public static Map CreateEmpty(int width = 16, int height = 16, string name = null) => new(width, height, name ?? $"Map {width}×{height}");

		public Map Clone() => new()
		{
			name = name,
			character = character,
			music = music,
			ambient = ambient,
			skyrgb = skyrgb,
			skybox = skybox,
			skyvec = skyvec,
			effect = effect,
			button = button,
			width = width,
			height = height,

			waypoints = waypoints != null ? (int[])waypoints.Clone() : null,
			tiles = tiles != null ? (int[])tiles.Clone() : null,
			solve = solve != null ? (int[])solve.Clone() : null,

			attachments = attachments != null ? attachments.Select(a => a.ShallowClone()).ToArray() : Array.Empty<MapAttachment>(),
			variants = variants != null ? variants.Select(v => new Variant(v.hash, v.delta, v.angle)).ToArray() : Array.Empty<Variant>(),
		};

		public bool Initialise(Transform parent = null, bool solved = false)
		{
			this.parent = parent;
			if (!InitialiseGraph())
			{
				Debug.LogWarning("Failed to create runtime tiles — map data invalid.");
				return false;
			}
			UpdateLighting();

			if (solved) Solve();
			else Preset();
			RefreshAttachments(GetAttachments());

			PreviewRenderLayers.RemovePreviewLayersFromChildLights(parent);

			if (null == waypoints || 0 == waypoints.Length)
				waypoints = this.GenerateWaypoints();
			parent.gameObject.GetOrAddComponent<WindController>();

			return true;
		}

		public void Destroy()
		{
			OnMapEdited = null;

			DestroyAllGraphTiles();
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

			if (null != directionalLight)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(directionalLight);
				else
					UnityEngine.Object.DestroyImmediate(directionalLight);
				directionalLight = null;
			}

			if (null != tintedCubemap)
				UnityEngine.Object.DestroyImmediate(tintedCubemap);

			state = null;

			if (parent != null)
				parent = null;
		}

		private Tile CreateTile(Variant variant, Transform parent, Vector3 renderPosition) => new Tile(variant, parent, renderPosition);

		public static Vector3 FullFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x), vec.y, Mathf.FloorToInt(vec.z));
		public static Vector3 HalfFloorVec(Vector3 vec) => new(Mathf.FloorToInt(vec.x * 2f) * 0.5f, vec.y, Mathf.FloorToInt(vec.z * 2f) * 0.5f);

		private static readonly Vector3 HALF_TILE = new(0.5f, 0f, 0.5f);
#if UNITY_EDITOR
		private static readonly Vector3 OFFSET = HALF_TILE;
#else
        private static readonly Vector3 OFFSET = Vector3.zero;
#endif
		public static readonly Vector3 ORIGIN = OFFSET - HALF_TILE;

		public static Vector3 WorldToRender(Vector3 value) => value + OFFSET;
		public static Vector3 RenderToWorld(Vector3 value) => value - OFFSET;

		public static bool RayToWorld(Ray ray, out Vector3 point)
		{
			point = Vector3.zero;
			if (RayToPlane(ray, new Plane(Vector3.up, Vector3.zero), out Vector3 result))
			{
				point = result;
				return true;
			}
			return false;
		}

		public static bool RayToPlane(Ray ray, Plane plane, out Vector3 point)
		{
			point = Vector3.zero;
			if (plane.Raycast(ray, out float d))
			{
				point = (ray.GetPoint(d) - ORIGIN).Rounded(2);
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

		public static bool ValidExtents(RectInt extents) => extents.width <= MAP_MAX_SIZE && extents.height <= MAP_MAX_SIZE;

		public int VectorToIndex(Vector3 vec) => vec.x < 0 || vec.x >= width || vec.z < 0 || vec.z >= height ? width > 0 ? -1 : 0 : Mathf.FloorToInt(vec.z) * width + Mathf.FloorToInt(vec.x);
		public Vector3 IndexToVector(int index) => width > 0 ? new(index % width, 0f, index / width) : Vector3.zero;
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

		public Tile GetTile(Vector3 pos, bool logicalIndex = true) => GetTile(VectorToIndex(pos), logicalIndex);
		public Tile GetTile(int index, bool logicalIndex = true) => state == null || index < 0 || index >= state.Length ? default : GetGraphTile(logicalIndex ? state[index] : index);

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
			DestroyAllGraphTiles();
			InitialiseGraph();
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
				Debug.LogWarning($"Cannot update tile at ({pos}) — position out of bounds");
				return -1;
			}

			variant.delta = new Vector3(Mathf.Repeat(pos.x, 1f), pos.y, Mathf.Repeat(pos.z, 1f));

			var def = ResourceManager.GetDefinition(variant.hash);
			var tableIndex = this.GetOrCreateVariantIndex(variant.hash, variant.delta, variant.angle);
			if (tiles[index] == tableIndex) return index;

			tiles[index] = tableIndex;
			var _graph = graph;
			_graph[index].Dispose();
			_graph[index] = CreateTile(variants[tableIndex], parent, TileRenderPosition(index));
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

			var newSolve = new int[newSize];

			for (var oldIdx = 0; oldIdx < width * height && oldIdx < tiles.Length; oldIdx++)
			{
				var newPos = Remap(oldIdx);
				if (newPos < 0) continue;

				newTiles[newPos] = tiles[oldIdx];

				if (solve != null && oldIdx < solve.Length)
				{
					var delta = solve[oldIdx];
					if (delta != 0)
					{
						var oldSrcIdx = oldIdx + delta;
						if ((uint)oldSrcIdx < solve.Length)
						{
							var newSrcPos = Remap(oldSrcIdx);
							if (newSrcPos >= 0)
								newSolve[newPos] = newSrcPos - newPos;
						}
					}
				}
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
			solve = newSolve;
			state = Enumerable.Range(0, width * height).ToArray();

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

		[JsonIgnore]
		private Cubemap tintedCubemap = null;
		public Cubemap TintedCubemap
		{
			get => tintedCubemap = null == tintedCubemap ? CubemapUtility.GetTintedCubemap(SkyboxMaterial) : tintedCubemap;
			set
			{
				if (null != tintedCubemap)
					UnityEngine.Object.DestroyImmediate(tintedCubemap);
				tintedCubemap = value;
			}
		}

		public void UpdateLighting(Material skymat = null)
		{
			if (null == ambient)
				ambientRGB = CubemapUtility.ComputeAmbientColor(TintedCubemap, 2f);

			if (null == directionalLight)
				directionalLight = DirectionalLightUtility.Instantiate(parent);
			if (null == directionalLight) return;

			if (null == skyrgb || null == skyvec)
				skyRBG = directionalLight.UpdateFromTintendCubemap(TintedCubemap);

			if (null != SkyRGB)
				directionalLight.UpdateColour(SkyRGB);

			if (null != skyvec)
				directionalLight.UpdateDirection(new Vector3(skyvec[0], skyvec[1], skyvec[2]));
		}

		public void CopyFrom(Map other)
		{
			if (null == other)
				return;

			skyrgb = other.skyrgb;
			skyRBG = other.skyRBG;
			directionalLight.CopyFrom(other.directionalLight);

			var updateRenderSettings = ambient != other.ambient || skybox != other.skybox;
			ambient = other.ambient;
			ambientRGB = other.ambientRGB;

			skybox = other.skybox;
			skyvec = other.skyvec;

			if (updateRenderSettings)
			{
				OnRenderSettingsChanged?.Invoke(RenderSettings);
			}

			effect = other.effect;
			OnEffectChanged?.Invoke(Effect);
		}

		public Action<UnityRenderSettings> OnRenderSettingsChanged;
	}
}