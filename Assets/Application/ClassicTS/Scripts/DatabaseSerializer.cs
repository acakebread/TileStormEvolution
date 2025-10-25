using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace ClassicTilestorm
{
	public static class DatabaseSerializer
	{
		[Serializable]
		public class DatabaseData
		{
			public Map[] maps;
			public Theme[] themes;
			public TileDef[] tiledefs;
			public Button[] buttons;
			public TextureSet[] texture_set;
		}

		[Serializable]
		public class Map
		{
			public string name;
			public bool bLightTiles;
			public string[] defs;
			public Waypoint[] waypoints;
			public int nWidth; // Moved to Map level
			public int nHeight; // Moved to Map level
			public int[] tiles; // Direct int array
			public int[] mixed; // Direct int array
			public Pickups Pickups;
			public string szEggbotCostume;
			public string szButtonID;
			public string szMusic;
		}

		[Serializable]
		private class LegacyDatabaseData
		{
			public LegacyMap[] maps;
			public Theme[] themes;
			public TileDef[] tiledefs;
			public Button[] buttons;
			public TextureSet[] texture_set;
		}

		[Serializable]
		private class LegacyMap
		{
			public string name;
			public bool bLightTiles;
			public string[] defs;
			public Waypoint[] waypoints;
			public LegacyTiles tiles;
			public LegacyTiles mixed;
			public Pickups Pickups;
			public string szEggbotCostume;
			public string szButtonID;
			public string szMusic;
		}

		[Serializable]
		private class LegacyTiles
		{
			public int nWidth;
			public int nHeight;
			public LegacyTileArray TileData;
		}

		[Serializable]
		private class LegacyTileArray
		{
			public int[] bytes;
		}

		[Serializable]
		public class TileDef
		{
			public string szTheme;
			public string szType;
			public string szGeom;
			public bool bSlide;
			public bool bRoll;
			public bool bDock;
			public bool bConsole;
			public bool bDoor;
			public bool bStart;
			public bool bEnd;
			public int nPickup;
			public bool bPuzzleBlock;
			public bool bNorth;
			public bool bSouth;
			public bool bEast;
			public bool bWest;
		}

		[Serializable]
		public class Waypoint
		{
			public string name;
			public int nTile;
			public bool bCamera;
			public VectorData vSrc;
			public VectorData vDst;
		}

		[Serializable]
		public class VectorData
		{
			public float fX;
			public float fY;
			public float fZ;

			public Vector3 ToVector3() => IsValidVector() ? new Vector3(fX, fY, fZ) : Vector3.zero;

			public bool IsValidVector()
			{
				var valid = !float.IsNaN(fX) && !float.IsInfinity(fX) && !float.IsNaN(fY) && !float.IsInfinity(fY) && !float.IsNaN(fZ) && !float.IsInfinity(fZ);
				if (!valid) Debug.LogWarning($"Invalid vector: fX={fX}, fY={fY}, fZ={fZ}");
				return valid;
			}
		}

		[Serializable]
		public class Pickups
		{
			public int nPickupCount;
		}

		[Serializable]
		public class Theme
		{
			public string name;
			public string szTileTextureSet;
		}

		[Serializable]
		public class Button
		{
			public string name;
			public string szTexture;
			public string szButtonText;
			public float fWidth;
			public float fHeight;
			public float fUVupX;
			public float fUVupY;
			public float fUVupW;
			public float fUVupH;
			public float fUVdownX;
			public float fUVdownY;
			public float fUVdownW;
			public float fUVdownH;
		}

		[Serializable]
		public class TextureSet
		{
			public string name;
			public bool bAlphaTest;
			public TextureFrame[] frames;
		}

		[Serializable]
		public class TextureFrame
		{
			public string name;
			public string szTexture;
			public float fDuration;
		}

		private static TextAsset databaseJsonFile;
		private static Action<TextAsset> saveDelegate;
		private static DatabaseData data;
		private static bool isLoaded;
		private static readonly object lockObject = new object();
		public static event Action OnDatabaseLoaded;

		public static IReadOnlyList<Map> Maps => LoadAndGetData()?.maps ?? Array.Empty<Map>();
		public static IReadOnlyList<Theme> Themes => LoadAndGetData()?.themes ?? Array.Empty<Theme>();
		public static IReadOnlyList<TileDef> TileDefs => LoadAndGetData()?.tiledefs ?? Array.Empty<TileDef>();
		public static IReadOnlyList<Button> Buttons => LoadAndGetData()?.buttons ?? Array.Empty<Button>();
		public static IReadOnlyList<TextureSet> TextureSets => LoadAndGetData()?.texture_set ?? Array.Empty<TextureSet>();

		public static void Init(TextAsset jsonFile, Action<TextAsset> saveAction = null)
		{
			lock (lockObject)
			{
				if (jsonFile == null)
				{
					throw new ArgumentNullException(nameof(jsonFile), "TextAsset cannot be null.");
				}
				if (saveAction == null)
				{
					Debug.LogWarning($"Save delegate is null. Saving disabled: {nameof(saveAction)}");
				}

				databaseJsonFile = jsonFile;
				saveDelegate = saveAction;
				data = null;
				isLoaded = false;
				Debug.Log($"DatabaseSerializer initialized with TextAsset: {jsonFile.name}");
			}
		}

		private static DatabaseData LoadAndGetData()
		{
			lock (lockObject)
			{
				if (isLoaded && data != null)
				{
					return data;
				}

				if (databaseJsonFile == null)
				{
					Debug.LogError("DatabaseSerializer: Not initialized. Call Init() with a valid TextAsset and save delegate.");
					return null;
				}

				string jsonContent = databaseJsonFile.text;
				if (string.IsNullOrEmpty(jsonContent))
				{
					Debug.LogError("DatabaseSerializer: TextAsset content is null or empty.");
					return null;
				}

				try
				{
					// Try deserializing with new format
					data = JsonUtility.FromJson<DatabaseData>(jsonContent);
					bool needsConversion = false;

					// Validate maps, defs, tiles, and mixed
					if (data != null && data.maps != null)
					{
						foreach (var map in data.maps)
						{
							if (map == null)
							{
								Debug.LogWarning($"Null map detected in DatabaseData.maps");
								needsConversion = true;
								break;
							}
							if (map.defs == null)
							{
								Debug.LogWarning($"Map {map.name}: defs array is null, setting to empty array");
								map.defs = new string[0];
								needsConversion = true;
							}
							else if (map.defs.Any(d => string.IsNullOrEmpty(d)))
							{
								Debug.LogWarning($"Map {map.name}: defs array contains null or empty entries: [{string.Join(", ", map.defs.Select(d => d ?? "null"))}]");
								needsConversion = true;
							}
							if (map.nWidth <= 0 || map.nHeight <= 0)
							{
								Debug.LogWarning($"Map {map.name}: Invalid dimensions (nWidth={map.nWidth}, nHeight={map.nHeight})");
								needsConversion = true;
							}
							if (map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight)
							{
								Debug.LogWarning($"Map {map.name}: tiles array is invalid (null={map.tiles == null}, length={map.tiles?.Length}, expected={map.nWidth * map.nHeight})");
								needsConversion = true;
							}
							if (map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight)
							{
								Debug.LogWarning($"Map {map.name}: mixed array is invalid (null={map.mixed == null}, length={map.mixed?.Length}, expected={map.nWidth * map.nHeight})");
								needsConversion = true;
							}
						}
					}
					else
					{
						Debug.LogWarning("DatabaseData or maps is null");
						needsConversion = true;
					}

					if (needsConversion || data == null)
					{
						// Deserialize with legacy format
						var legacyData = JsonUtility.FromJson<LegacyDatabaseData>(jsonContent);
						if (legacyData == null || legacyData.maps == null)
						{
							Debug.LogError("DatabaseSerializer: Failed to parse JSON with both new and legacy formats.");
							return null;
						}

						// Convert legacy format to new format
						data = new DatabaseData
						{
							maps = legacyData.maps.Select(lm =>
							{
								if (lm == null)
								{
									Debug.LogWarning($"Null legacy map detected, skipping");
									return null;
								}
								if (lm.tiles == null || lm.mixed == null || lm.tiles.nWidth != lm.mixed.nWidth || lm.tiles.nHeight != lm.mixed.nHeight)
								{
									Debug.LogWarning($"Map {lm.name}: Invalid or mismatched tiles/mixed dimensions (tiles={lm.tiles?.nWidth}x{lm.tiles?.nHeight}, mixed={lm.mixed?.nWidth}x{lm.mixed?.nHeight})");
									return null;
								}
								if (lm.tiles?.TileData?.bytes == null || lm.mixed?.TileData?.bytes == null)
								{
									Debug.LogWarning($"Map {lm.name}: Null tiles or mixed bytes array");
									return null;
								}
								return new Map
								{
									name = lm.name,
									bLightTiles = lm.bLightTiles,
									defs = lm.defs ?? new string[0],
									waypoints = lm.waypoints,
									nWidth = lm.tiles.nWidth,
									nHeight = lm.tiles.nHeight,
									tiles = lm.tiles.TileData.bytes,
									mixed = lm.mixed.TileData.bytes,
									Pickups = lm.Pickups,
									szEggbotCostume = lm.szEggbotCostume,
									szButtonID = lm.szButtonID,
									szMusic = lm.szMusic
								};
							}).Where(m => m != null).ToArray(),
							themes = legacyData.themes,
							tiledefs = legacyData.tiledefs,
							buttons = legacyData.buttons,
							texture_set = legacyData.texture_set
						};

						Debug.Log("Converted legacy format to new format (tiles and mixed)");
						SaveDatabase(data); // Save converted data to persistent data path
					}

					// Validate TileDefs
					if (data.tiledefs == null || data.tiledefs.Any(td => string.IsNullOrEmpty(td?.szType)))
					{
						Debug.LogError($"TileDefs contains null or invalid entries: [{string.Join(", ", (data.tiledefs ?? Array.Empty<TileDef>()).Select(td => td?.szType ?? "null"))}]");
						return null;
					}

					isLoaded = true;

					Debug.Log($"DatabaseSerializer: Loaded {data.maps?.Length ?? 0} maps: {string.Join(", ", (data.maps ?? Array.Empty<Map>()).Select(m => m.name ?? "null"))}");
					Debug.Log($"DatabaseSerializer: Loaded {data.themes?.Length ?? 0} themes: {string.Join(", ", (data.themes ?? Array.Empty<Theme>()).Select(t => t.name ?? "null"))}");
					Debug.Log($"DatabaseSerializer: Loaded {data.tiledefs?.Length ?? 0} tiledefs: {string.Join(", ", (data.tiledefs ?? Array.Empty<TileDef>()).Take(5).Select(td => td?.szType ?? "null"))}");
					Debug.Log($"DatabaseSerializer: Loaded {data.buttons?.Length ?? 0} buttons: {string.Join(", ", (data.buttons ?? Array.Empty<Button>()).Select(b => b.name ?? "null"))}");
					Debug.Log($"DatabaseSerializer: Loaded {data.texture_set?.Length ?? 0} texture sets: {string.Join(", ", (data.texture_set ?? Array.Empty<TextureSet>()).Take(5).Select(ts => ts.name ?? "null"))}");

					foreach (var map in data.maps)
					{
						Debug.Log($"Map {map.name}: defs=[{string.Join(", ", map.defs.Select(d => d ?? "null"))}], tiles=[{string.Join(", ", map.tiles.Take(5))}], mixed=[{string.Join(", ", map.mixed.Take(5))}]");
					}

					VerifyData();

					OnDatabaseLoaded?.Invoke();

					return data;
				}
				catch (Exception ex)
				{
					Debug.LogError($"DatabaseSerializer: JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
					isLoaded = false;
					data = null;
					return null;
				}
			}
		}

		public static void SaveDatabase(DatabaseData newData)
		{
			if (newData == null)
			{
				Debug.LogError("DatabaseSerializer: Cannot save null DatabaseData.");
				return;
			}

			lock (lockObject)
			{
				if (databaseJsonFile == null || saveDelegate == null)
				{
					Debug.LogError("DatabaseSerializer: Not initialized. Call Init() with a valid TextAsset and save delegate.");
					return;
				}

				try
				{
					// Validate before saving
					foreach (var map in newData.maps)
					{
						if (map == null)
						{
							Debug.LogError($"Null map detected in DatabaseData.maps");
							return;
						}
						if (map.defs == null || map.defs.Any(d => string.IsNullOrEmpty(d)))
						{
							Debug.LogError($"Map {map.name}: Cannot save, defs contains null or empty entries: [{string.Join(", ", map.defs.Select(d => d ?? "null"))}]");
							return;
						}
						if (map.nWidth <= 0 || map.nHeight <= 0)
						{
							Debug.LogError($"Map {map.name}: Cannot save, invalid dimensions (nWidth={map.nWidth}, nHeight={map.nHeight})");
							return;
						}
						if (map.tiles == null || map.tiles.Length != map.nWidth * map.nHeight)
						{
							Debug.LogError($"Map {map.name}: Cannot save, tiles array is invalid (null={map.tiles == null}, length={map.tiles?.Length}, expected={map.nWidth * map.nHeight})");
							return;
						}
						if (map.mixed == null || map.mixed.Length != map.nWidth * map.nHeight)
						{
							Debug.LogError($"Map {map.name}: Cannot save, mixed array is invalid (null={map.mixed == null}, length={map.mixed?.Length}, expected={map.nWidth * map.nHeight})");
							return;
						}
					}

					string outputDir = Path.Combine(Application.persistentDataPath, "Data");
					if (!Directory.Exists(outputDir))
					{
						Directory.CreateDirectory(outputDir);
						Debug.Log($"Created directory: {outputDir}");
					}

					string outputPath = Path.Combine(outputDir, "database.json");
					string jsonContent = JsonUtility.ToJson(newData, true);
					File.WriteAllText(outputPath, jsonContent);
					Debug.Log($"Database saved to {outputPath}");

					data = newData;
					isLoaded = true;
					VerifyData();
					OnDatabaseLoaded?.Invoke();
				}
				catch (Exception ex)
				{
					Debug.LogError($"DatabaseSerializer: Failed to save DatabaseData: {ex.Message}\nStackTrace: {ex.StackTrace}");
				}
			}
		}

		private static void VerifyData()
		{
			Debug.Log("DatabaseSerializer: Verifying data...");

			if (data?.maps?.Length > 0)
			{
				var sampleMap = data.maps[0];
				Debug.Log($"Sample map: name={sampleMap.name}, bLightTiles={sampleMap.bLightTiles}, defsCount={(sampleMap.defs?.Length ?? 0)}, " +
						  $"waypointsCount={(sampleMap.waypoints?.Length ?? 0)}, dimensions={sampleMap.nWidth}x{sampleMap.nHeight}");
			}

			if (data?.themes?.Length > 0)
			{
				Debug.Log($"Sample theme: name={data.themes[0].name}, szTileTextureSet={data.themes[0].szTileTextureSet ?? "null"}");
			}

			if (data?.tiledefs?.Length > 0)
			{
				var sampleTileDef = data.tiledefs[0];
				Debug.Log($"Sample tiledef: szType={sampleTileDef.szType}, szTheme={sampleTileDef.szTheme}, szGeom={sampleTileDef.szGeom}, " +
						  $"bSlide={sampleTileDef.bSlide}, bNorth={sampleTileDef.bNorth}");
			}

			if (data?.buttons?.Length > 0)
			{
				var sampleButton = data.buttons[0];
				Debug.Log($"Sample button: name={sampleButton.name}, szTexture={sampleButton.szTexture}, szButtonText={sampleButton.szButtonText}");
			}

			if (data?.texture_set?.Length > 0)
			{
				var sampleTextureSet = data.texture_set[0];
				var sampleFrame = sampleTextureSet.frames?.FirstOrDefault();
				Debug.Log($"Sample texture_set: name={sampleTextureSet.name}, bAlphaTest={sampleTextureSet.bAlphaTest}, " +
						  $"frameName={sampleFrame?.name ?? "null"}, frameTexture={sampleFrame?.szTexture ?? "null"}, " +
						  $"frameDuration={sampleFrame?.fDuration}");
			}

			Debug.Log($"DatabaseSerializer: Verification complete: {data?.maps?.Length ?? 0} maps, {data?.themes?.Length ?? 0} themes, " +
					  $"{data?.tiledefs?.Length ?? 0} tiledefs, {data?.buttons?.Length ?? 0} buttons, {data?.texture_set?.Length ?? 0} texture sets");
		}
	}
}