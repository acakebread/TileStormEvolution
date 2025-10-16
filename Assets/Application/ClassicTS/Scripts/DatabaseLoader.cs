//using System;
//using System.Linq;
//using System.Collections.Generic;
//using UnityEngine;

//namespace ClassicTilestorm
//{
//	public static class DatabaseLoader
//	{
//		[Serializable]
//		public class DatabaseData
//		{
//			public Map[] maps;
//			public Theme[] themes;
//			public TileDef[] tiledefs;
//			public Button[] buttons;
//			public TextureSet[] texture_set;
//		}

//		[Serializable]
//		public class Map
//		{
//			public string name;
//			public bool bLightTiles;
//			public MapTileDef[] defs;
//			public Waypoint[] waypoints;
//			public Tiles tiles;
//			public Tiles mixed;
//			public Pickups Pickups;
//			public string szEggbotCostume;
//			public string szButtonID;
//			public string szMusic;
//		}

//		[Serializable]
//		public class MapTileDef
//		{
//			public string szTheme;
//			public string szType;
//		}

//		[Serializable]
//		public class TileDef
//		{
//			public string szTheme;
//			public string szType;
//			public string szGeom;
//			public bool bSlide;
//			public bool bRoll;
//			public bool bDock;
//			public bool bConsole;
//			public bool bDoor;
//			public bool bStart;
//			public bool bEnd;
//			public int nPickup;
//			public bool bPuzzleBlock;
//			public bool bNorth;
//			public bool bSouth;
//			public bool bEast;
//			public bool bWest;
//		}

//		[Serializable]
//		public class Tiles
//		{
//			public int nWidth;
//			public int nHeight;
//			public TileArray TileData;
//		}

//		[Serializable]
//		public class TileArray
//		{
//			public int nReserved;
//			public int nUncompressedLength;
//			public int nCompression;
//			public int nCompressedLength;
//			public int nAdjust;
//			public string[] compressed_bytes;
//			public int[] bytes;
//		}

//		[Serializable]
//		public class Waypoint
//		{
//			public string name;
//			public int nTile;
//			public bool bCamera;
//			public VectorData vSrc;
//			public VectorData vDst;
//		}

//		[Serializable]
//		public class VectorData
//		{
//			public float fX;
//			public float fY;
//			public float fZ;

//			public Vector3 ToVector3() => IsValidVector() ? new Vector3(fX, fY, fZ) : Vector3.zero;

//			public bool IsValidVector()
//			{
//				var valid = !float.IsNaN(fX) && !float.IsInfinity(fX) && !float.IsNaN(fY) && !float.IsInfinity(fY) && !float.IsNaN(fZ) && !float.IsInfinity(fZ);
//				if (!valid) Debug.LogWarning($"Invalid vector: fX={fX}, fY={fY}, fZ={fZ}");
//				return valid;
//			}
//		}

//		[Serializable]
//		public class Pickups
//		{
//			public int nPickupCount;
//		}

//		[Serializable]
//		public class Theme
//		{
//			public string name;
//			public string szTileTextureSet;
//		}

//		[Serializable]
//		public class Button
//		{
//			public string name;
//			public string szTexture;
//			public string szButtonText;
//			public float fWidth;
//			public float fHeight;
//			public float fUVupX;
//			public float fUVupY;
//			public float fUVupW;
//			public float fUVupH;
//			public float fUVdownX;
//			public float fUVdownY;
//			public float fUVdownW;
//			public float fUVdownH;
//		}

//		[Serializable]
//		public class TextureSet
//		{
//			public string name;
//			public bool bAlphaTest;
//			public TextureFrame[] frames;
//		}

//		[Serializable]
//		public class TextureFrame
//		{
//			public string name;
//			public string szTexture;
//			public float fDuration;
//		}

//		private static TextAsset databaseJsonFile;
//		private static DatabaseData data;
//		private static bool isLoaded;
//		private static readonly object lockObject = new object();
//		public static event Action OnDatabaseLoaded;

//		// Static accessors with lazy loading
//		public static IReadOnlyList<Map> Maps => LoadAndGetData()?.maps ?? Array.Empty<Map>();
//		public static IReadOnlyList<Theme> Themes => LoadAndGetData()?.themes ?? Array.Empty<Theme>();
//		public static IReadOnlyList<TileDef> TileDefs => LoadAndGetData()?.tiledefs ?? Array.Empty<TileDef>();
//		public static IReadOnlyList<Button> Buttons => LoadAndGetData()?.buttons ?? Array.Empty<Button>();
//		public static IReadOnlyList<TextureSet> TextureSets => LoadAndGetData()?.texture_set ?? Array.Empty<TextureSet>();

//		// Initialize or reset with a new TextAsset
//		public static void Init(TextAsset jsonFile)
//		{
//			lock (lockObject)
//			{
//				databaseJsonFile = jsonFile ?? throw new ArgumentNullException(nameof(jsonFile), "TextAsset cannot be null.");
//				data = null; // Clear existing data
//				isLoaded = false; // Reset loaded flag
//				Debug.Log($"DatabaseLoader initialized with new TextAsset: Maps.Count={Maps.Count}");
//			}
//		}

//		private static DatabaseData LoadAndGetData()
//		{
//			lock (lockObject)
//			{
//				if (isLoaded && data != null)
//				{
//					return data;
//				}

//				if (databaseJsonFile == null)
//				{
//					Debug.LogError("DatabaseLoader: TextAsset is not assigned. Call Init() with a valid TextAsset.");
//					return null;
//				}

//				try
//				{
//					data = JsonUtility.FromJson<DatabaseData>(databaseJsonFile.text);
//					if (data == null)
//					{
//						Debug.LogError("Failed to parse JSON: DatabaseData is null!");
//						return null;
//					}

//					isLoaded = true;

//					// Log loaded data counts
//					Debug.Log($"Loaded {data.maps?.Length ?? 0} maps: {string.Join(", ", (data.maps ?? Array.Empty<Map>()).Select(m => m.name))}");
//					Debug.Log($"Loaded {data.themes?.Length ?? 0} themes: {string.Join(", ", (data.themes ?? Array.Empty<Theme>()).Select(t => t.name))}");
//					Debug.Log($"Loaded {data.tiledefs?.Length ?? 0} tiledefs: {string.Join(", ", (data.tiledefs ?? Array.Empty<TileDef>()).Take(5).Select(td => td.szType))}");
//					Debug.Log($"Loaded {data.buttons?.Length ?? 0} buttons: {string.Join(", ", (data.buttons ?? Array.Empty<Button>()).Select(b => b.name))}");
//					Debug.Log($"Loaded {data.texture_set?.Length ?? 0} texture sets: {string.Join(", ", (data.texture_set ?? Array.Empty<TextureSet>()).Take(5).Select(ts => ts.name))}");

//					VerifyData();

//					OnDatabaseLoaded?.Invoke();

//					return data;
//				}
//				catch (Exception ex)
//				{
//					Debug.LogError($"JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
//					isLoaded = false;
//					data = null;
//					return null;
//				}
//			}
//		}

//		private static void VerifyData()
//		{
//			Debug.Log("Verifying data...");

//			if (data?.maps?.Length > 0)
//			{
//				var sampleMap = data.maps[0];
//				Debug.Log($"Sample map: name={sampleMap.name}, bLightTiles={sampleMap.bLightTiles}, defsCount={(sampleMap.defs?.Length ?? 0)}, " +
//						  $"waypointsCount={(sampleMap.waypoints?.Length ?? 0)}, tiles={sampleMap.tiles?.nWidth}x{sampleMap.tiles?.nHeight}");
//			}

//			if (data?.themes?.Length > 0)
//			{
//				Debug.Log($"Sample theme: name={data.themes[0].name}, szTileTextureSet={data.themes[0].szTileTextureSet ?? "null"}");
//			}

//			if (data?.tiledefs?.Length > 0)
//			{
//				var sampleTileDef = data.tiledefs[0];
//				Debug.Log($"Sample tiledef: szType={sampleTileDef.szType}, szTheme={sampleTileDef.szTheme}, szGeom={sampleTileDef.szGeom}, " +
//						  $"bSlide={sampleTileDef.bSlide}, bNorth={sampleTileDef.bNorth}");
//			}

//			if (data?.buttons?.Length > 0)
//			{
//				var sampleButton = data.buttons[0];
//				Debug.Log($"Sample button: name={sampleButton.name}, szTexture={sampleButton.szTexture}, szButtonText={sampleButton.szButtonText}");
//			}

//			if (data?.texture_set?.Length > 0)
//			{
//				var sampleTextureSet = data.texture_set[0];
//				var sampleFrame = sampleTextureSet.frames?.FirstOrDefault();
//				Debug.Log($"Sample texture_set: name={sampleTextureSet.name}, bAlphaTest={sampleTextureSet.bAlphaTest}, " +
//						  $"frameName={sampleFrame?.name ?? "null"}, frameTexture={sampleFrame?.szTexture ?? "null"}, " +
//						  $"frameDuration={sampleFrame?.fDuration}");
//			}

//			Debug.Log($"Verification complete: {data?.maps?.Length ?? 0} maps, {data?.themes?.Length ?? 0} themes, " +
//					  $"{data?.tiledefs?.Length ?? 0} tiledefs, {data?.buttons?.Length ?? 0} buttons, {data?.texture_set?.Length ?? 0} texture sets");
//		}
//	}
//}
