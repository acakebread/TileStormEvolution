//#define VERBOSE
using System;
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

	public partial class Map : IMapEdit, Map.IVariantAccess
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

		private Cubemap tintedCubemap = null;
		[JsonIgnore]
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

		[JsonIgnore]
		public UnityRenderSettings RenderSettings => new(
			ambientMode: UnityEngine.Rendering.AmbientMode.Flat,
			ambientLight: AmbientRGB,
			ambientIntensity: 1f,
			skybox: SkyboxMaterial,
			ambientProbe: default,
			subtractiveShadowColor: UnityEngine.RenderSettings.subtractiveShadowColor);
	}
}