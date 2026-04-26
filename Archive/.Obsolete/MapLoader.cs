using UnityEngine;

public class MapLoader : MonoBehaviour
{
	[SerializeField] private TextAsset mapJsonFile;
}

//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//[System.Serializable]
//public class MapData
//{
//	public Map[] maps;
//	public Theme[] themes;
//	public TileDef[] tiledefs;
//	public Button[] buttons;
//	public TextureSet[] texture_set;
//}

//[System.Serializable]
//public class Map
//{
//	public string name;
//	public bool bLightTiles;
//	public TileDef[] defs;
//	public Tiles tiles;
//	public Waypoints Waypoints;
//	public Pickups Pickups;
//	public string szButtonID;
//	public string szMusic;
//}

//[System.Serializable]
//public class TileDef
//{
//	public string szTheme;
//	public string szType;
//	public string szGeom; // Added to match JSON
//	public bool bSlide;
//	public bool bRoll;
//	public bool bDock;
//	public bool bConsole;
//	public bool bDoor;
//	public bool bStart;
//	public bool bEnd;
//	public int nPickup;
//	public bool bPuzzleBlock;
//	public bool bNorth;
//	public bool bSouth;
//	public bool bEast;
//	public bool bWest;
//}

//[System.Serializable]
//public class Tiles
//{
//	public int nWidth;
//	public int nHeight;
//	public TileIndex nTileIndex;
//}

//[System.Serializable]
//public class TileIndex
//{
//	public int nReserved;
//	public int nUncompressedLength;
//	public int nCompression;
//	public int nCompressedLength;
//	public int nAdjust;
//	public string[] bytes;
//	public int[] unpacked_bytes;
//}

//[System.Serializable]
//public class Waypoints
//{
//	public int nWaypointCount;
//	public Waypoint WP0;
//	public Waypoint WP1;
//	public Waypoint WP2;
//	public Waypoint WP3;
//}

//[System.Serializable]
//public class Waypoint
//{
//	public int nTile;
//	public bool bCamera;
//	public VectorData vSrc;
//	public VectorData vDst;
//}

//[System.Serializable]
//public class VectorData
//{
//	public float fX;
//	public float fY;
//	public float fZ;
//}

//[System.Serializable]
//public class Pickups
//{
//	public int nPickupCount;
//}

//[System.Serializable]
//public class Theme
//{
//	//public string szTheme;
//	public string szTileTextureSet;
//}

//[System.Serializable]
//public class Button
//{
//	public string name;
//	public string szTexture;
//	public string szButtonText;
//	public float fWidth;
//	public float fHeight;
//	public float fUVupX;
//	public float fUVupY;
//	public float fUVupW;
//	public float fUVupH;
//	public float fUVdownX;
//	public float fUVdownY;
//	public float fUVdownW;
//	public float fUVdownH;
//}

//[System.Serializable]
//public class TextureSet
//{
//	public string szTexture; // Verify this matches JSON (e.g., might be "texture")
//}

//public class Tile : MonoBehaviour
//{
//	public TileDef tileDef;
//	public int gridX, gridZ;

//	public bool GetSlide() => tileDef.bSlide;
//	public bool GetRoll() => tileDef.bRoll;
//	public bool GetDock() => tileDef.bDock;
//	public bool GetConsole() => tileDef.bConsole;
//	public bool GetDoor() => tileDef.bDoor;
//	public bool GetStart() => tileDef.bStart;
//	public bool GetEnd() => tileDef.bEnd;
//	public int GetPickup() => tileDef.nPickup;
//	public bool GetPuzzleBlock() => tileDef.bPuzzleBlock;
//	public int GetNav()
//	{
//		int nav = 0;
//		if (tileDef.bNorth) nav |= 1;
//		if (tileDef.bSouth) nav |= 2;
//		if (tileDef.bEast) nav |= 4;
//		if (tileDef.bWest) nav |= 8;
//		return nav;
//	}
//}

//public class MapLoader : MonoBehaviour
//{
//	[SerializeField] private TextAsset mapJsonFile;
//	[SerializeField] private bool instantiatePrefabs = false;
//	[SerializeField] private Dictionary<string, GameObject> prefabLibrary;

//	private Dictionary<string, Tile[,]> mapGrids = new Dictionary<string, Tile[,]>();
//	private Dictionary<string, Map> loadedMaps = new Dictionary<string, Map>();
//	private List<Theme> themes = new List<Theme>();
//	private List<TileDef> tileDefs = new List<TileDef>();
//	private List<Button> buttons = new List<Button>();
//	private List<TextureSet> textureSets = new List<TextureSet>();

//	public Dictionary<string, Tile[,]> TileGrids => mapGrids;
//	public IReadOnlyList<Theme> Themes => themes.AsReadOnly();
//	public IReadOnlyList<TileDef> TileDefs => tileDefs.AsReadOnly();
//	public IReadOnlyList<Button> Buttons => buttons.AsReadOnly();
//	public IReadOnlyList<TextureSet> TextureSets => textureSets.AsReadOnly();

//	void Start()
//	{
//		LoadAllMaps();
//	}

//	void LoadAllMaps()
//	{
//		if (mapJsonFile == null)
//		{
//			Debug.LogError("mapJsonFile is not assigned in the Inspector!");
//			return;
//		}

//		try
//		{
//			// Log raw JSON for inspection
//			Debug.Log($"Raw JSON: {mapJsonFile.text.Substring(0, Mathf.Min(2000, mapJsonFile.text.Length))}...");

//			MapData mapData = JsonUtility.FromJson<MapData>(mapJsonFile.text);
//			if (mapData == null)
//			{
//				Debug.LogError("Failed to parse map JSON: MapData is null!");
//				return;
//			}

//			// Detailed debugging of deserialized data
//			Debug.Log($"Maps count: {(mapData.maps?.Length ?? 0)}");
//			if (mapData.themes?.Length > 0)
//			{
//				Debug.Log($"Themes count: {mapData.themes.Length}, Sample: szTheme={mapData.themes[0].szTileTextureSet}");
//			}
//			else
//			{
//				Debug.LogWarning("Themes array is null or empty!");
//			}

//			if (mapData.tiledefs?.Length > 0)
//			{
//				var sampleTileDef = mapData.tiledefs[0];
//				Debug.Log($"Tiledefs count: {mapData.tiledefs.Length}, Sample: szTheme={sampleTileDef.szTheme}, szType={sampleTileDef.szType}, " +
//						  $"szGeom={sampleTileDef.szGeom}, bSlide={sampleTileDef.bSlide}, bNorth={sampleTileDef.bNorth}, nPickup={sampleTileDef.nPickup}");
//			}
//			else
//			{
//				Debug.LogWarning("Tiledefs array is null or empty!");
//			}

//			if (mapData.buttons?.Length > 0)
//			{
//				Debug.Log($"Buttons count: {mapData.buttons.Length}, Sample: name={mapData.buttons[0].name}, szTexture={mapData.buttons[0].szTexture}");
//			}
//			else
//			{
//				Debug.LogWarning("Buttons array is null or empty!");
//			}

//			if (mapData.texture_set?.Length > 0)
//			{
//				Debug.Log($"TextureSet count: {mapData.texture_set.Length}, Sample: szTexture={mapData.texture_set[0].szTexture}");
//			}
//			else
//			{
//				Debug.LogWarning("TextureSet array is null or empty!");
//			}

//			// Process maps
//			if (mapData.maps == null || mapData.maps.Length == 0)
//			{
//				Debug.LogError("No maps found in JSON!");
//				return;
//			}
//			Dictionary<string, Map> mapsDict = new Dictionary<string, Map>();
//			foreach (var mapEntry in mapData.maps)
//			{
//				if (!string.IsNullOrEmpty(mapEntry.name) && mapEntry != null)
//				{
//					mapsDict[mapEntry.name] = mapEntry;
//				}
//				else
//				{
//					Debug.LogWarning($"Map entry with invalid name or null map found!");
//				}
//			}
//			if (mapsDict.Count == 0)
//			{
//				Debug.LogError("No valid maps loaded into dictionary!");
//				return;
//			}

//			// Process themes
//			if (mapData.themes != null && mapData.themes.Length > 0)
//			{
//				themes.AddRange(mapData.themes);
//				Debug.Log($"Loaded {themes.Count} themes: {string.Join(", ", themes.Select(t => t.szTileTextureSet))}");
//			}
//			else
//			{
//				Debug.LogWarning("No themes found in JSON!");
//			}

//			// Process tiledefs
//			if (mapData.tiledefs != null && mapData.tiledefs.Length > 0)
//			{
//				tileDefs.AddRange(mapData.tiledefs);
//				Debug.Log($"Loaded {tileDefs.Count} tiledefs: {string.Join(", ", tileDefs.Select(td => $"{td.szTheme}_{td.szType}, bSlide={td.bSlide}"))}");
//			}
//			else
//			{
//				Debug.LogWarning("No tiledefs found in JSON!");
//			}

//			// Process buttons
//			if (mapData.buttons != null && mapData.buttons.Length > 0)
//			{
//				buttons.AddRange(mapData.buttons);
//				Debug.Log($"Loaded {buttons.Count} buttons: {string.Join(", ", buttons.Select(b => b.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No buttons found in JSON!");
//			}

//			// Process texture_set
//			if (mapData.texture_set != null && mapData.texture_set.Length > 0)
//			{
//				textureSets.AddRange(mapData.texture_set);
//				Debug.Log($"Loaded {textureSets.Count} texture sets: {string.Join(", ", textureSets.Select(ts => ts.szTexture ?? "null"))}");
//			}
//			else
//			{
//				Debug.LogWarning("No texture_set found in JSON!");
//			}

//			// Process maps into grids
//			foreach (var mapEntry in mapsDict)
//			{
//				string mapName = mapEntry.Key;
//				Map map = mapEntry.Value;

//				if (map.tiles == null || map.tiles.nTileIndex == null || map.tiles.nTileIndex.unpacked_bytes == null)
//				{
//					Debug.LogError($"Invalid tiles data for map {mapName}!");
//					continue;
//				}

//				loadedMaps[mapName] = map;

//				int width = map.tiles.nWidth;
//				int height = map.tiles.nHeight;
//				Tile[,] tileGrid = new Tile[width, height];

//				int[] tileIndices = map.tiles.nTileIndex.unpacked_bytes;

//				if (tileIndices.Length != width * height)
//				{
//					Debug.LogError($"Tile indices length does not match grid size for map {mapName}!");
//					continue;
//				}

//				for (int z = 0; z < height; z++)
//				{
//					for (int x = 0; x < width; x++)
//					{
//						int index = z * width + x;
//						int defIndex = tileIndices[index];

//						if (defIndex >= 0 && defIndex < map.defs.Length)
//						{
//							GameObject tileObj = new GameObject($"{mapName}_Tile_{x}_{z}");
//							tileObj.transform.parent = transform;
//							tileObj.transform.position = new Vector3(x, 0, z);

//							Tile tile = tileObj.AddComponent<Tile>();
//							tile.tileDef = map.defs[defIndex];
//							tile.gridX = x;
//							tile.gridZ = z;
//							tileGrid[x, z] = tile;

//							if (instantiatePrefabs)
//							{
//								string prefabKey = $"{tile.tileDef.szTheme}_{tile.tileDef.szType}";
//								if (prefabLibrary != null && prefabLibrary.TryGetValue(prefabKey, out GameObject prefab))
//								{
//									GameObject prefabInstance = Instantiate(prefab, tileObj.transform.position, Quaternion.identity, tileObj.transform);
//									prefabInstance.name = $"{prefabKey}";
//								}
//								else
//								{
//									Debug.LogWarning($"Prefab not found for {prefabKey} in map {mapName} at ({x}, {z})");
//								}
//							}
//						}
//						else
//						{
//							Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {mapName}");
//						}
//					}
//				}

//				mapGrids[mapName] = tileGrid;

//				if (instantiatePrefabs)
//				{
//					RenderSettings.ambientIntensity = map.bLightTiles ? 1.0f : 0.5f;
//				}
//			}

//			Debug.Log($"Loaded {mapGrids.Count} maps: {string.Join(", ", mapGrids.Keys)}");
//			Debug.Log($"Summary: {themes.Count} themes, {tileDefs.Count} tiledefs, {buttons.Count} buttons, {textureSets.Count} texture sets");
//		}
//		catch (System.Exception ex)
//		{
//			Debug.LogError($"JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
//		}
//	}

//	public Tile[,] GetMapGrid(string mapName)
//	{
//		return mapGrids.TryGetValue(mapName, out Tile[,] grid) ? grid : null;
//	}

//	public void ValidateMap(string mapName)
//	{
//		if (!mapGrids.TryGetValue(mapName, out Tile[,] grid))
//		{
//			Debug.LogError($"Map {mapName} not found!");
//			return;
//		}

//		Debug.Log($"Validating map {mapName}: {grid.GetLength(0)}x{grid.GetLength(1)}");
//		for (int z = 0; z < grid.GetLength(1); z++)
//		{
//			for (int x = 0; x < grid.GetLength(0); x++)
//			{
//				Tile tile = grid[x, z];
//				if (tile != null)
//				{
//					Debug.Log($"Tile at ({x}, {z}): {tile.tileDef.szTheme}_{tile.tileDef.szType}, " +
//							  $"Slide: {tile.GetSlide()}, Nav: {tile.GetNav()}, Position: {tile.transform.position}");
//				}
//			}
//		}
//	}
//}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using Unity.VisualScripting;

//[System.Serializable]
//public class MapData
//{
//	public Map[] maps;
//	public Theme[] themes;
//	public TileDef[] tiledefs;
//	public Button[] buttons; // Also supports "level_select" as fallback
//	public TextureSet[] texture_set;
//}

//[System.Serializable]
//public class Map
//{
//	public string name;
//	public bool bLightTiles;
//	public TileDef[] defs;
//	public Tiles tiles;
//	public Waypoints Waypoints;
//	public Pickups Pickups;
//	public string szButtonID;
//	public string szMusic;
//	public string szEggbotCostume; // Kept from original, though not in your update
//}

//[System.Serializable]
//public class TileDef
//{
//	public string szTheme;
//	public string szType;
//	public bool bSlide;
//	public bool bRoll;
//	public bool bDock;
//	public bool bConsole;
//	public bool bDoor;
//	public bool bStart;
//	public bool bEnd;
//	public int nPickup;
//	public bool bPuzzleBlock;
//	public bool bNorth;
//	public bool bSouth;
//	public bool bEast;
//	public bool bWest;
//}

//[System.Serializable]
//public class Tiles
//{
//	public int nWidth;
//	public int nHeight;
//	public TileIndex nTileIndex;
//}

//[System.Serializable]
//public class TileIndex
//{
//	public int nReserved;
//	public int nUncompressedLength;
//	public int nCompression;
//	public int nCompressedLength;
//	public int nAdjust;
//	public string[] bytes;
//	public int[] unpacked_bytes;
//}

//[System.Serializable]
//public class Waypoints
//{
//	public int nWaypointCount;
//	public Waypoint WP0;
//	public Waypoint WP1;
//	public Waypoint WP2;
//	public Waypoint WP3;
//}

//[System.Serializable]
//public class Waypoint
//{
//	public int nTile;
//	public bool bCamera;
//	public VectorData vSrc;
//	public VectorData vDst;
//}

//[System.Serializable]
//public class VectorData
//{
//	public float fX;
//	public float fY;
//	public float fZ;
//}

//[System.Serializable]
//public class Pickups
//{
//	public int nPickupCount;
//}

//// New classes for additional sections
//[System.Serializable]
//public class Theme
//{
//	public string szTheme;
//	// Add more fields if parser exports them (e.g., szMusic, colors)
//}

//[System.Serializable]
//public class Button
//{
//	public string name;
//	public string szTexture;
//	public string szButtonText;
//	public float fWidth;
//	public float fHeight;
//	public float fUVupX;
//	public float fUVupY;
//	public float fUVupW;
//	public float fUVupH;
//	public float fUVdownX;
//	public float fUVdownY;
//	public float fUVdownW;
//	public float fUVdownH;
//}

//[System.Serializable]
//public class TextureSet
//{
//	public string szTexture;
//	// Add more fields if parser exports them (e.g., path, format)
//}

//public class Tile : MonoBehaviour
//{
//	public TileDef tileDef;
//	public int gridX, gridZ;

//	public bool GetSlide() => tileDef.bSlide;
//	public bool GetRoll() => tileDef.bRoll;
//	public bool GetDock() => tileDef.bDock;
//	public bool GetConsole() => tileDef.bConsole;
//	public bool GetDoor() => tileDef.bDoor;
//	public bool GetStart() => tileDef.bStart;
//	public bool GetEnd() => tileDef.bEnd;
//	public int GetPickup() => tileDef.nPickup;
//	public bool GetPuzzleBlock() => tileDef.bPuzzleBlock;
//	public int GetNav()
//	{
//		int nav = 0;
//		if (tileDef.bNorth) nav |= 1;
//		if (tileDef.bSouth) nav |= 2;
//		if (tileDef.bEast) nav |= 4;
//		if (tileDef.bWest) nav |= 8;
//		return nav;
//	}
//}

//public class MapLoader : MonoBehaviour
//{
//	[SerializeField] private TextAsset mapJsonFile;
//	[SerializeField] private bool instantiatePrefabs = false;
//	[SerializeField] private Dictionary<string, GameObject> prefabLibrary;

//	private Dictionary<string, Tile[,]> mapGrids = new Dictionary<string, Tile[,]>();
//	private Dictionary<string, Map> loadedMaps = new Dictionary<string, Map>();
//	private List<Theme> themes = new List<Theme>();
//	private List<TileDef> tileDefs = new List<TileDef>();
//	private List<Button> buttons = new List<Button>();
//	private List<TextureSet> textureSets = new List<TextureSet>();

//	public Dictionary<string, Tile[,]> TileGrids => mapGrids;
//	public List<Theme> Themes => themes;
//	public List<TileDef> TileDefs => tileDefs;
//	public List<Button> Buttons => buttons;
//	public List<TextureSet> TextureSets => textureSets;

//	void Start()
//	{
//		LoadAllMaps();
//	}

//	void LoadAllMaps()
//	{
//		if (mapJsonFile == null)
//		{
//			Debug.LogError("mapJsonFile is not assigned in the Inspector!");
//			return;
//		}

//		try
//		{
//			MapData mapData = JsonUtility.FromJson<MapData>(mapJsonFile.text);
//			if (mapData == null || mapData.maps == null || mapData.maps.Length == 0)
//			{
//				Debug.LogError("Failed to parse map JSON or no maps found!");
//				return;
//			}

//			// Process maps
//			Dictionary<string, Map> mapsDict = new Dictionary<string, Map>();
//			foreach (var map in mapData.maps)
//			{
//				if (!string.IsNullOrEmpty(map.name) && map != null)
//				{
//					mapsDict[map.name] = map;
//				}
//				else
//				{
//					Debug.LogWarning($"Map entry with invalid name or null map found!");
//				}
//			}

//			if (mapsDict.Count == 0)
//			{
//				Debug.LogError("No valid maps loaded into dictionary!");
//				return;
//			}

//			// Process themes
//			if (mapData.themes != null && mapData.themes.Length > 0)
//			{
//				themes.AddRange(mapData.themes);
//				Debug.Log($"Loaded {themes.Count} themes: {string.Join(", ", themes.ConvertAll(t => t.szTheme))}");
//			}
//			else
//			{
//				Debug.LogWarning("No themes array found! Extracting from defs.");
//				foreach (var map in mapsDict.Values)
//				{
//					if (map.defs != null)
//					{
//						var mapThemes = map.defs
//							.Where(d => !string.IsNullOrEmpty(d.szTheme))
//							.Select(d => new Theme { szTheme = d.szTheme })
//							.DistinctBy(t => t.szTheme);
//						themes.AddRange(mapThemes);
//					}
//				}
//				Debug.Log($"Extracted {themes.Count} themes: {string.Join(", ", themes.ConvertAll(t => t.szTheme))}");
//			}

//			// Process tiledefs
//			if (mapData.tiledefs != null && mapData.tiledefs.Length > 0)
//			{
//				tileDefs.AddRange(mapData.tiledefs);
//				Debug.Log($"Loaded {tileDefs.Count} tiledefs.");
//			}
//			else
//			{
//				Debug.LogWarning("No tiledefs array found! Using map.defs.");
//				foreach (var map in mapsDict.Values)
//				{
//					if (map.defs != null)
//					{
//						tileDefs.AddRange(map.defs);
//					}
//				}
//				tileDefs = tileDefs
//					.DistinctBy(td => $"{td.szTheme}_{td.szType}")
//					.ToList();
//				Debug.Log($"Collected {tileDefs.Count} tiledefs from map.defs.");
//			}

//			// Process buttons
//			if (mapData.buttons != null && mapData.buttons.Length > 0)
//			{
//				buttons.AddRange(mapData.buttons);
//				Debug.Log($"Loaded {buttons.Count} buttons: {string.Join(", ", buttons.ConvertAll(b => b.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No buttons array found! Extracting from szButtonID.");
//				foreach (var map in mapsDict.Values)
//				{
//					if (!string.IsNullOrEmpty(map.szButtonID))
//					{
//						buttons.Add(new Button
//						{
//							name = map.szButtonID,
//							szTexture = $"button{map.szButtonID.Replace("ls ", "")}",
//							szButtonText = ""
//						});
//					}
//				}
//				Debug.Log($"Extracted {buttons.Count} buttons from szButtonID.");
//			}

//			// Process texture_set
//			if (mapData.texture_set != null && mapData.texture_set.Length > 0)
//			{
//				textureSets.AddRange(mapData.texture_set);
//				Debug.Log($"Loaded {textureSets.Count} texture sets.");
//			}
//			else
//			{
//				Debug.LogWarning("No texture_set array found! Extracting from buttons.");
//				foreach (var button in buttons)
//				{
//					if (!string.IsNullOrEmpty(button.szTexture))
//					{
//						textureSets.Add(new TextureSet { szTexture = button.szTexture });
//					}
//				}
//				textureSets = textureSets
//					.DistinctBy(ts => ts.szTexture)
//					.ToList();
//				Debug.Log($"Collected {textureSets.Count} texture sets from buttons.");
//			}

//			// Process maps into grids
//			foreach (var mapEntry in mapsDict)
//			{
//				string mapName = mapEntry.Key;
//				Map map = mapEntry.Value;

//				if (map.tiles == null || map.tiles.nTileIndex == null || map.tiles.nTileIndex.unpacked_bytes == null)
//				{
//					Debug.LogError($"Invalid tiles data for map {mapName}!");
//					continue;
//				}

//				loadedMaps[mapName] = map;

//				int width = map.tiles.nWidth;
//				int height = map.tiles.nHeight;
//				Tile[,] tileGrid = new Tile[width, height];

//				int[] tileIndices = map.tiles.nTileIndex.unpacked_bytes;

//				if (tileIndices.Length != width * height)
//				{
//					Debug.LogError($"Tile indices length does not match grid size for map {mapName}!");
//					continue;
//				}

//				for (int z = 0; z < height; z++)
//				{
//					for (int x = 0; x < width; x++)
//					{
//						int index = z * width + x;
//						int defIndex = tileIndices[index];

//						if (defIndex >= 0 && defIndex < map.defs.Length)
//						{
//							GameObject tileObj = new GameObject($"{mapName}_Tile_{x}_{z}");
//							tileObj.transform.parent = transform;
//							tileObj.transform.position = new Vector3(x, 0, z);

//							Tile tile = tileObj.AddComponent<Tile>();
//							tile.tileDef = map.defs[defIndex];
//							tile.gridX = x;
//							tile.gridZ = z;
//							tileGrid[x, z] = tile;

//							if (instantiatePrefabs)
//							{
//								string prefabKey = $"{tile.tileDef.szTheme}_{tile.tileDef.szType}";
//								if (prefabLibrary != null && prefabLibrary.TryGetValue(prefabKey, out GameObject prefab))
//								{
//									GameObject prefabInstance = Instantiate(prefab, tileObj.transform.position, Quaternion.identity, tileObj.transform);
//									prefabInstance.name = $"{prefabKey}";
//								}
//								else
//								{
//									Debug.LogWarning($"Prefab not found for {prefabKey} in map {mapName} at ({x}, {z})");
//								}
//							}
//						}
//						else
//						{
//							Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {mapName}");
//						}
//					}
//				}

//				mapGrids[mapName] = tileGrid;

//				if (instantiatePrefabs)
//				{
//					RenderSettings.ambientIntensity = map.bLightTiles ? 1.0f : 0.5f;
//				}
//			}

//			Debug.Log($"Loaded {mapGrids.Count} maps: {string.Join(", ", mapGrids.Keys)}");
//			Debug.Log($"Exported: {themes.Count} themes, {tileDefs.Count} tiledefs, {buttons.Count} buttons, {textureSets.Count} texture sets");
//		}
//		catch (System.Exception ex)
//		{
//			Debug.LogError($"JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
//		}
//	}

//	public Tile[,] GetMapGrid(string mapName)
//	{
//		return mapGrids.TryGetValue(mapName, out Tile[,] grid) ? grid : null;
//	}

//	public void ValidateMap(string mapName)
//	{
//		if (!mapGrids.TryGetValue(mapName, out Tile[,] grid))
//		{
//			Debug.LogError($"Map {mapName} not found!");
//			return;
//		}

//		Debug.Log($"Validating map {mapName}: {grid.GetLength(0)}x{grid.GetLength(1)}");
//		for (int z = 0; z < grid.GetLength(1); z++)
//		{
//			for (int x = 0; x < grid.GetLength(0); x++)
//			{
//				Tile tile = grid[x, z];
//				if (tile != null)
//				{
//					Debug.Log($"Tile at ({x}, {z}): {tile.tileDef.szTheme}_{tile.tileDef.szType}, " +
//							  $"Slide: {tile.GetSlide()}, Nav: {tile.GetNav()}, Position: {tile.transform.position}");
//				}
//			}
//		}
//	}
//}

//using UnityEngine;
//using System.Collections.Generic;

//[System.Serializable]
//public class MapData
//{
//	//public MapEntry[] maps;
//	public Map[] maps;
//}

////[System.Serializable]
////public class MapEntry
////{
////	public string name;
////	public Map map;
////}

//[System.Serializable]
//public class Map
//{
//	public string name;
//	public bool bLightTiles;
//	public TileDef[] defs;
//	public Tiles tiles;
//	public Waypoints Waypoints; // Case-sensitive to match JSON
//	public Pickups Pickups; // Case-sensitive to match JSON
//	public string szButtonID;
//	public string szMusic;
//}

//[System.Serializable]
//public class TileDef
//{
//	public string szTheme;
//	public string szType;
//	public bool bSlide;
//	public bool bRoll;
//	public bool bDock;
//	public bool bConsole;
//	public bool bDoor;
//	public bool bStart;
//	public bool bEnd;
//	public int nPickup;
//	public bool bPuzzleBlock;
//	public bool bNorth;
//	public bool bSouth;
//	public bool bEast;
//	public bool bWest;
//}

//[System.Serializable]
//public class Tiles
//{
//	public int nWidth;
//	public int nHeight;
//	public TileIndex nTileIndex;
//}

//[System.Serializable]
//public class TileIndex
//{
//	public int nReserved;
//	public int nUncompressedLength;
//	public int nCompression;
//	public int nCompressedLength;
//	public int nAdjust;
//	public string[] bytes;
//	public int[] unpacked_bytes;
//}

//[System.Serializable]
//public class Waypoints
//{
//	public int nWaypointCount;
//	public Waypoint WP0;
//	public Waypoint WP1;
//	public Waypoint WP2;
//	public Waypoint WP3;
//}

//[System.Serializable]
//public class Waypoint
//{
//	public int nTile;
//	public bool bCamera;
//	public VectorData vSrc;
//	public VectorData vDst;
//}

//[System.Serializable]
//public class VectorData
//{
//	public float fX;
//	public float fY;
//	public float fZ;
//}

//[System.Serializable]
//public class Pickups
//{
//	public int nPickupCount;
//}

//public class Tile : MonoBehaviour
//{
//	public TileDef tileDef;
//	public int gridX, gridZ;

//	public bool GetSlide() => tileDef.bSlide;
//	public bool GetRoll() => tileDef.bRoll;
//	public bool GetDock() => tileDef.bDock;
//	public bool GetConsole() => tileDef.bConsole;
//	public bool GetDoor() => tileDef.bDoor;
//	public bool GetStart() => tileDef.bStart;
//	public bool GetEnd() => tileDef.bEnd;
//	public int GetPickup() => tileDef.nPickup;
//	public bool GetPuzzleBlock() => tileDef.bPuzzleBlock;
//	public int GetNav()
//	{
//		int nav = 0;
//		if (tileDef.bNorth) nav |= 1;
//		if (tileDef.bSouth) nav |= 2;
//		if (tileDef.bEast) nav |= 4;
//		if (tileDef.bWest) nav |= 8;
//		return nav;
//	}
//}

//public class MapLoader : MonoBehaviour
//{
//	[SerializeField] private TextAsset mapJsonFile;
//	[SerializeField] private bool instantiatePrefabs = false;
//	[SerializeField] private Dictionary<string, GameObject> prefabLibrary;

//	private Dictionary<string, Tile[,]> mapGrids = new Dictionary<string, Tile[,]>();
//	private Dictionary<string, Map> loadedMaps = new Dictionary<string, Map>();

//	public Dictionary<string, Tile[,]> TileGrids => mapGrids;

//	void Start()
//	{
//		LoadAllMaps();
//	}

//	void LoadAllMaps()
//	{
//		MapData mapData = JsonUtility.FromJson<MapData>(mapJsonFile.text);
//		if (mapData == null || mapData.maps == null || mapData.maps.Length == 0)
//		{
//			Debug.LogError("Failed to parse map JSON or no maps found!");
//			return;
//		}

//		// Convert array to dictionary
//		Dictionary<string, Map> mapsDict = new Dictionary<string, Map>();
//		foreach (var mapEntry in mapData.maps)
//		{
//			//if (!string.IsNullOrEmpty(mapEntry.name) && mapEntry.map != null)
//			//{
//			//	mapsDict[mapEntry.name] = mapEntry.map;
//			//}
//			if (!string.IsNullOrEmpty(mapEntry.name) && mapEntry != null)
//			{
//				mapsDict[mapEntry.name] = mapEntry;
//			}
//			else
//			{
//				Debug.LogWarning($"Map entry with invalid name or null map found!");
//			}
//		}

//		if (mapsDict.Count == 0)
//		{
//			Debug.LogError("No valid maps loaded into dictionary!");
//			return;
//		}

//		foreach (var mapEntry in mapsDict)
//		{
//			string mapName = mapEntry.Key;
//			Map map = mapEntry.Value;

//			loadedMaps[mapName] = map;

//			int width = map.tiles.nWidth;
//			int height = map.tiles.nHeight;
//			Tile[,] tileGrid = new Tile[width, height];

//			int[] tileIndices = map.tiles.nTileIndex.unpacked_bytes;

//			if (tileIndices.Length != width * height)
//			{
//				Debug.LogError($"Tile indices length does not match grid size for map {mapName}!");
//				continue;
//			}

//			for (int z = 0; z < height; z++)
//			{
//				for (int x = 0; x < width; x++)
//				{
//					int index = z * width + x;
//					int defIndex = tileIndices[index];

//					if (defIndex >= 0 && defIndex < map.defs.Length)
//					{
//						GameObject tileObj = new GameObject($"{mapName}_Tile_{x}_{z}");
//						tileObj.transform.parent = transform;
//						tileObj.transform.position = new Vector3(x, 0, z);

//						Tile tile = tileObj.AddComponent<Tile>();
//						tile.tileDef = map.defs[defIndex];
//						tile.gridX = x;
//						tile.gridZ = z;
//						tileGrid[x, z] = tile;

//						if (instantiatePrefabs)
//						{
//							string prefabKey = $"{tile.tileDef.szTheme}_{tile.tileDef.szType}";
//							if (prefabLibrary != null && prefabLibrary.TryGetValue(prefabKey, out GameObject prefab))
//							{
//								GameObject prefabInstance = Instantiate(prefab, tileObj.transform.position, Quaternion.identity, tileObj.transform);
//								prefabInstance.name = $"{prefabKey}";
//							}
//							else
//							{
//								Debug.LogWarning($"Prefab not found for {prefabKey} in map {mapName} at ({x}, {z})");
//							}
//						}
//					}
//					else
//					{
//						Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {mapName}");
//					}
//				}
//			}

//			mapGrids[mapName] = tileGrid;

//			if (instantiatePrefabs)
//			{
//				RenderSettings.ambientIntensity = map.bLightTiles ? 1.0f : 0.5f;
//			}
//		}

//		Debug.Log($"Loaded {mapGrids.Count} maps: {string.Join(", ", mapGrids.Keys)}");
//	}

//	public Tile[,] GetMapGrid(string mapName)
//	{
//		return mapGrids.TryGetValue(mapName, out Tile[,] grid) ? grid : null;
//	}

//	public void ValidateMap(string mapName)
//	{
//		if (!mapGrids.TryGetValue(mapName, out Tile[,] grid))
//		{
//			Debug.LogError($"Map {mapName} not found!");
//			return;
//		}

//		Debug.Log($"Validating map {mapName}: {grid.GetLength(0)}x{grid.GetLength(1)}");
//		for (int z = 0; z < grid.GetLength(1); z++)
//		{
//			for (int x = 0; x < grid.GetLength(0); x++)
//			{
//				Tile tile = grid[x, z];
//				if (tile != null)
//				{
//					Debug.Log($"Tile at ({x}, {z}): {tile.tileDef.szTheme}_{tile.tileDef.szType}, " +
//							  $"Slide: {tile.GetSlide()}, Nav: {tile.GetNav()}, Position: {tile.transform.position}");
//				}
//			}
//		}
//	}
//}

//using UnityEngine;
//using System.Collections.Generic;

//public class MapLoader : MonoBehaviour
//{
//	public TextAsset mapJsonFile;

//	[System.Serializable]
//	public class MapData
//	{
//		public List<MapEntry> maps;
//	}

//	[System.Serializable]
//	public class MapEntry
//	{
//		public string name;
//		public bool bLightTiles;
//		public List<DefEntry> defs;
//		public TileData tiles;
//		public TileData mixed;
//	}

//	[System.Serializable]
//	public class DefEntry
//	{
//		public string szTheme;
//		public string szType;
//	}

//	[System.Serializable]
//	public class TileData
//	{
//		public int nWidth;
//		public int nHeight;
//		public TileIndex nTileIndex;
//	}

//	[System.Serializable]
//	public class TileIndex
//	{
//		public int nCompression;
//		public int nCompressedLength;
//		public int nUncompressedLength;
//		public int nAdjust;
//		public string[] bytes;
//		public int[] unpacked_bytes;
//	}

//	void Start()
//	{
//		LoadAllMaps();
//	}

//	void LoadAllMaps()
//	{
//		if (mapJsonFile == null)
//		{
//			Debug.LogError("No JSON file assigned to MapLoader!");
//			return;
//		}

//		string jsonText = mapJsonFile.text;
//		Debug.Log($"Loading JSON: {mapJsonFile.name}, Length: {jsonText.Length}");

//		try
//		{
//			MapData mapData = JsonUtility.FromJson<MapData>(jsonText);
//			if (mapData != null && mapData.maps != null)
//			{
//				string mapNames = string.Join(", ", mapData.maps.ConvertAll(m => m.name));
//				Debug.Log($"Loaded {mapData.maps.Count} maps: {mapNames}");
//			}
//			else
//			{
//				Debug.LogError("Failed to parse map data or no maps found!");
//			}
//		}
//		catch (System.Exception ex)
//		{
//			Debug.LogError($"JSON parsing failed: {ex.Message}");
//		}
//	}
//}



//using UnityEngine;
//using System.Collections.Generic;

//[System.Serializable]
//public class MapData
//{
//	public List<MapEntry> maps; // Changed to List
//}

//[System.Serializable]
//public class MapEntry // New class for list items
//{
//	public string name;
//	public bool bLightTiles;
//	public TileDef[] defs;
//	public Tiles tiles;
//}

//[System.Serializable]
//public class TileDef
//{
//	public string szTheme;
//	public string szType;
//	public bool bSlide = false;
//	public bool bRoll = false;
//	public bool bDock = false;
//	public bool bConsole = false;
//	public bool bDoor = false;
//	public bool bStart = false;
//	public bool bEnd = false;
//	public int nPickup = 0;
//	public bool bPuzzleBlock = false;
//	public bool bNorth = false;
//	public bool bSouth = false;
//	public bool bEast = false;
//	public bool bWest = false;
//}

//[System.Serializable]
//public class Tiles
//{
//	public int nWidth;
//	public int nHeight;
//	public TileIndex nTileIndex;
//}

//[System.Serializable]
//public class TileIndex
//{
//	public int nReserved;
//	public int nUncompressedLength;
//	public int nCompression;
//	public int nCompressedLength;
//	public int nAdjust;
//	public string[] bytes;
//	public int[] unpacked_bytes;
//}

//public class Tile : MonoBehaviour
//{
//	public TileDef tileDef;
//	public int gridX, gridZ;

//	public bool GetSlide() => tileDef.bSlide;
//	public bool GetRoll() => tileDef.bRoll;
//	public bool GetDock() => tileDef.bDock;
//	public bool GetConsole() => tileDef.bConsole;
//	public bool GetDoor() => tileDef.bDoor;
//	public bool GetStart() => tileDef.bStart;
//	public bool GetEnd() => tileDef.bEnd;
//	public int GetPickup() => tileDef.nPickup;
//	public bool GetPuzzleBlock() => tileDef.bPuzzleBlock;
//	public int GetNav()
//	{
//		int nav = 0;
//		if (tileDef.bNorth) nav |= 1;
//		if (tileDef.bSouth) nav |= 2;
//		if (tileDef.bEast) nav |= 4;
//		if (tileDef.bWest) nav |= 8;
//		return nav;
//	}
//}

//public class MapLoader : MonoBehaviour
//{
//	[SerializeField] private TextAsset mapJsonFile;
//	[SerializeField] private bool instantiatePrefabs = false;
//	[SerializeField] private Dictionary<string, GameObject> prefabLibrary;

//	private Dictionary<string, Tile[,]> mapGrids = new Dictionary<string, Tile[,]>();
//	private Dictionary<string, MapEntry> loadedMaps = new Dictionary<string, MapEntry>(); // Adjusted type

//	public Dictionary<string, Tile[,]> TileGrids => mapGrids;

//	void Start()
//	{
//		LoadAllMaps();
//	}

//	void LoadAllMaps()
//	{
//		if (mapJsonFile == null)
//		{
//			Debug.LogError("mapJsonFile is not assigned in the Inspector!");
//			return;
//		}

//		Debug.Log($"Loading JSON: {mapJsonFile.name}, Length: {mapJsonFile.text.Length}");
//		Debug.Log($"JSON Content: {mapJsonFile.text}");

//		MapData mapData = JsonUtility.FromJson<MapData>(mapJsonFile.text);
//		if (mapData == null)
//		{
//			Debug.LogError("mapData is null after parsing!");
//			return;
//		}
//		if (mapData.maps == null)
//		{
//			Debug.LogError("mapData.maps is null!");
//			return;
//		}

//		foreach (var mapEntry in mapData.maps)
//		{
//			string mapName = mapEntry.name;
//			loadedMaps[mapName] = mapEntry;

//			int width = mapEntry.tiles.nWidth;
//			int height = mapEntry.tiles.nHeight;
//			Tile[,] tileGrid = new Tile[width, height];

//			int[] tileIndices = mapEntry.tiles.nTileIndex.unpacked_bytes;

//			if (tileIndices.Length != width * height)
//			{
//				Debug.LogError($"Tile indices length ({tileIndices.Length}) does not match grid size ({width}x{height}) for map {mapName}!");
//				continue;
//			}

//			for (int z = 0; z < height; z++)
//			{
//				for (int x = 0; x < width; x++)
//				{
//					int index = z * width + x;
//					int defIndex = tileIndices[index];

//					if (defIndex >= 0 && defIndex < mapEntry.defs.Length)
//					{
//						GameObject tileObj = new GameObject($"{mapName}_Tile_{x}_{z}");
//						tileObj.transform.parent = transform;
//						tileObj.transform.position = new Vector3(x, 0, z);

//						Tile tile = tileObj.AddComponent<Tile>();
//						tile.tileDef = mapEntry.defs[defIndex];
//						tile.gridX = x;
//						tile.gridZ = z;
//						tileGrid[x, z] = tile;

//						if (instantiatePrefabs)
//						{
//							string prefabKey = $"{tile.tileDef.szTheme}_{tile.tileDef.szType}";
//							if (prefabLibrary != null && prefabLibrary.TryGetValue(prefabKey, out GameObject prefab))
//							{
//								GameObject prefabInstance = Instantiate(prefab, tileObj.transform.position, Quaternion.identity, tileObj.transform);
//								prefabInstance.name = $"{prefabKey}";
//							}
//							else
//							{
//								Debug.LogWarning($"Prefab not found for {prefabKey} in map {mapName} at ({x}, {z})");
//							}
//						}
//					}
//					else
//					{
//						Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {mapName}");
//					}
//				}
//			}

//			mapGrids[mapName] = tileGrid;

//			if (instantiatePrefabs)
//			{
//				RenderSettings.ambientIntensity = mapEntry.bLightTiles ? 1.0f : 0.5f;
//			}
//		}

//		Debug.Log($"Loaded {mapGrids.Count} maps: {string.Join(", ", mapGrids.Keys)}");
//	}

//	public Tile[,] GetMapGrid(string mapName)
//	{
//		return mapGrids.TryGetValue(mapName, out Tile[,] grid) ? grid : null;
//	}

//	public void ValidateMap(string mapName)
//	{
//		if (!mapGrids.TryGetValue(mapName, out Tile[,] grid))
//		{
//			Debug.LogError($"Map {mapName} not found!");
//			return;
//		}

//		Debug.Log($"Validating map {mapName}: {grid.GetLength(0)}x{grid.GetLength(1)}");
//		for (int z = 0; z < grid.GetLength(1); z++)
//		{
//			for (int x = 0; x < grid.GetLength(0); x++)
//			{
//				Tile tile = grid[x, z];
//				if (tile != null)
//				{
//					Debug.Log($"Tile at ({x}, {z}): {tile.tileDef.szTheme}_{tile.tileDef.szType}, " +
//							  $"Slide: {tile.GetSlide()}, Nav: {tile.GetNav()}, Position: {tile.transform.position}");
//				}
//			}
//		}
//	}
//}