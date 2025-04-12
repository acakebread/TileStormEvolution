using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace GameDatabase
{
	public class DatabaseLoader : MonoBehaviour
	{
		[SerializeField] private TextAsset databaseJsonFile;

		private List<Map> maps = new List<Map>();
		private List<Theme> themes = new List<Theme>();
		private List<TileDef> tileDefs = new List<TileDef>();
		private List<Button> buttons = new List<Button>();
		private List<TextureSet> textureSets = new List<TextureSet>();

		internal IReadOnlyList<Map> Maps => maps.AsReadOnly();
		internal IReadOnlyList<Theme> Themes => themes.AsReadOnly();
		internal IReadOnlyList<TileDef> TileDefs => tileDefs.AsReadOnly();
		internal IReadOnlyList<Button> Buttons => buttons.AsReadOnly();
		internal IReadOnlyList<TextureSet> TextureSets => textureSets.AsReadOnly();

		// Event to notify when loading is complete
		public event Action OnDatabaseLoaded;

		void Start()
		{
			LoadDatabase();
		}

		void LoadDatabase()
		{
			if (databaseJsonFile == null)
			{
				Debug.LogError("databaseJsonFile is not assigned in the Inspector!");
				return;
			}

			try
			{
				DatabaseData data = JsonUtility.FromJson<DatabaseData>(databaseJsonFile.text);
				if (data == null)
				{
					Debug.LogError("Failed to parse JSON: DatabaseData is null!");
					return;
				}

				// Clear lists to ensure fresh data
				maps.Clear();
				themes.Clear();
				tileDefs.Clear();
				buttons.Clear();
				textureSets.Clear();

				// Load maps
				if (data.maps != null && data.maps.Length > 0)
				{
					maps.AddRange(data.maps);
					Debug.Log($"Loaded {maps.Count} maps: {string.Join(", ", maps.Select(m => m.name))}");
				}
				else
				{
					Debug.LogWarning("No maps found in JSON!");
				}

				// Load themes
				if (data.themes != null && data.themes.Length > 0)
				{
					themes.AddRange(data.themes);
					Debug.Log($"Loaded {themes.Count} themes: {string.Join(", ", themes.Select(t => t.name))}");
				}
				else
				{
					Debug.LogWarning("No themes found in JSON!");
				}

				// Load tiledefs
				if (data.tiledefs != null && data.tiledefs.Length > 0)
				{
					tileDefs.AddRange(data.tiledefs);
					Debug.Log($"Loaded {tileDefs.Count} tiledefs: {string.Join(", ", tileDefs.Take(5).Select(td => td.name))}");
				}
				else
				{
					Debug.LogWarning("No tiledefs found in JSON!");
				}

				// Load buttons
				if (data.buttons != null && data.buttons.Length > 0)
				{
					buttons.AddRange(data.buttons);
					Debug.Log($"Loaded {buttons.Count} buttons: {string.Join(", ", buttons.Select(b => b.name))}");
				}
				else
				{
					Debug.LogWarning("No buttons found in JSON!");
				}

				// Load texture_set
				if (data.texture_set != null && data.texture_set.Length > 0)
				{
					textureSets.AddRange(data.texture_set);
					Debug.Log($"Loaded {textureSets.Count} texture sets: {string.Join(", ", textureSets.Take(5).Select(ts => ts.name))}");
				}
				else
				{
					Debug.LogWarning("No texture_set found in JSON!");
				}

				// Verify data
				VerifyData();

				// Signal that loading is complete
				OnDatabaseLoaded?.Invoke();
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
			}
		}

		void VerifyData()
		{
			Debug.Log("Verifying data...");

			// Maps
			if (maps.Count > 0)
			{
				var sampleMap = maps[0];
				Debug.Log($"Sample map: name={sampleMap.name}, bLightTiles={sampleMap.bLightTiles}, defsCount={(sampleMap.defs?.Length ?? 0)}, " +
						  $"tiles={sampleMap.tiles?.nWidth}x{sampleMap.tiles?.nHeight}");
			}

			// Themes
			if (themes.Count > 0)
			{
				Debug.Log($"Sample theme: name={themes[0].name}, szTileTextureSet={themes[0].szTileTextureSet ?? "null"}");
			}

			// Tiledefs
			if (tileDefs.Count > 0)
			{
				var sampleTileDef = tileDefs[0];
				Debug.Log($"Sample tiledef: name={sampleTileDef.name}, szTheme={sampleTileDef.szTheme}, szType={sampleTileDef.szType}, " +
						  $"szGeom={sampleTileDef.szGeom}, bSlide={sampleTileDef.bSlide}, bNorth={sampleTileDef.bNorth}");
			}

			// Buttons
			if (buttons.Count > 0)
			{
				var sampleButton = buttons[0];
				Debug.Log($"Sample button: name={sampleButton.name}, szTexture={sampleButton.szTexture}, szButtonText={sampleButton.szButtonText}");
			}

			// TextureSets
			if (textureSets.Count > 0)
			{
				var sampleTextureSet = textureSets[0];
				var sampleFrame = sampleTextureSet.frames?.FirstOrDefault();
				Debug.Log($"Sample texture_set: name={sampleTextureSet.name}, bAlphaTest={sampleTextureSet.bAlphaTest}, " +
						  $"frameName={sampleFrame?.name ?? "null"}, frameTexture={sampleFrame?.szTexture ?? "null"}, " +
						  $"frameDuration={sampleFrame?.fDuration}");
			}

			Debug.Log($"Verification complete: {maps.Count} maps, {themes.Count} themes, {tileDefs.Count} tiledefs, " +
					  $"{buttons.Count} buttons, {textureSets.Count} texture sets");
		}

		// Existing data classes (unchanged)
		[System.Serializable]
		public class DatabaseData
		{
			public Map[] maps;
			public Theme[] themes;
			public TileDef[] tiledefs;
			public Button[] buttons;
			public TextureSet[] texture_set;
		}

		[System.Serializable]
		public class Map
		{
			public string name;
			public bool bLightTiles;
			public TileDef[] defs;
			public Tiles tiles;
			public Waypoints Waypoints;
			public Pickups Pickups;
			public string szButtonID;
			public string szMusic;
		}

		[System.Serializable]
		public class TileDef
		{
			public string name;
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

		[System.Serializable]
		public class Tiles
		{
			public int nWidth;
			public int nHeight;
			public TileIndex nTileIndex;
		}

		[System.Serializable]
		public class TileIndex
		{
			public int nReserved;
			public int nUncompressedLength;
			public int nCompression;
			public int nCompressedLength;
			public int nAdjust;
			public string[] bytes;
			public int[] unpacked_bytes;
		}

		[System.Serializable]
		public class Waypoints
		{
			public int nWaypointCount;
			public Waypoint WP0;
			public Waypoint WP1;
			public Waypoint WP2;
			public Waypoint WP3;
		}

		[System.Serializable]
		public class Waypoint
		{
			public int nTile;
			public bool bCamera;
			public VectorData vSrc;
			public VectorData vDst;
		}

		[System.Serializable]
		public class VectorData
		{
			public float fX;
			public float fY;
			public float fZ;
		}

		[System.Serializable]
		public class Pickups
		{
			public int nPickupCount;
		}

		[System.Serializable]
		public class Theme
		{
			public string name;
			public string szTileTextureSet;
		}

		[System.Serializable]
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

		[System.Serializable]
		public class TextureSet
		{
			public string name;
			public bool bAlphaTest;
			public TextureFrame[] frames;
		}

		[System.Serializable]
		public class TextureFrame
		{
			public string name;
			public string szTexture;
			public float fDuration;
		}
	}
}


//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;

//[System.Serializable]
//public class DatabaseData
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
//	public string name;
//	public string szTheme;
//	public string szType;
//	public string szGeom;
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
//	public string name;
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
//	public string name;
//	public bool bAlphaTest;
//	public TextureFrame[] frames;
//}

//[System.Serializable]
//public class TextureFrame
//{
//	public string name;
//	public string szTexture;
//	public float fDuration;
//}

//public class DatabaseLoader : MonoBehaviour
//{
//	[SerializeField] private TextAsset databaseJsonFile;

//	private List<Map> maps = new List<Map>();
//	private List<Theme> themes = new List<Theme>();
//	private List<TileDef> tileDefs = new List<TileDef>();
//	private List<Button> buttons = new List<Button>();
//	private List<TextureSet> textureSets = new List<TextureSet>();

//	public IReadOnlyList<Map> Maps => maps.AsReadOnly();
//	public IReadOnlyList<Theme> Themes => themes.AsReadOnly();
//	public IReadOnlyList<TileDef> TileDefs => tileDefs.AsReadOnly();
//	public IReadOnlyList<Button> Buttons => buttons.AsReadOnly();
//	public IReadOnlyList<TextureSet> TextureSets => textureSets.AsReadOnly();

//	void Start()
//	{
//		LoadDatabase();
//	}

//	void LoadDatabase()
//	{
//		if (databaseJsonFile == null)
//		{
//			Debug.LogError("databaseJsonFile is not assigned in the Inspector!");
//			return;
//		}

//		try
//		{
//			DatabaseData data = JsonUtility.FromJson<DatabaseData>(databaseJsonFile.text);
//			if (data == null)
//			{
//				Debug.LogError("Failed to parse JSON: DatabaseData is null!");
//				return;
//			}

//			// Clear lists to ensure fresh data
//			maps.Clear();
//			themes.Clear();
//			tileDefs.Clear();
//			buttons.Clear();
//			textureSets.Clear();

//			// Load maps
//			if (data.maps != null && data.maps.Length > 0)
//			{
//				maps.AddRange(data.maps);
//				Debug.Log($"Loaded {maps.Count} maps: {string.Join(", ", maps.Select(m => m.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No maps found in JSON!");
//			}

//			// Load themes
//			if (data.themes != null && data.themes.Length > 0)
//			{
//				themes.AddRange(data.themes);
//				Debug.Log($"Loaded {themes.Count} themes: {string.Join(", ", themes.Select(t => t.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No themes found in JSON!");
//			}

//			// Load tiledefs
//			if (data.tiledefs != null && data.tiledefs.Length > 0)
//			{
//				tileDefs.AddRange(data.tiledefs);
//				Debug.Log($"Loaded {tileDefs.Count} tiledefs: {string.Join(", ", tileDefs.Take(5).Select(td => td.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No tiledefs found in JSON!");
//			}

//			// Load buttons
//			if (data.buttons != null && data.buttons.Length > 0)
//			{
//				buttons.AddRange(data.buttons);
//				Debug.Log($"Loaded {buttons.Count} buttons: {string.Join(", ", buttons.Select(b => b.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No buttons found in JSON!");
//			}

//			// Load texture_set
//			if (data.texture_set != null && data.texture_set.Length > 0)
//			{
//				textureSets.AddRange(data.texture_set);
//				Debug.Log($"Loaded {textureSets.Count} texture sets: {string.Join(", ", textureSets.Take(5).Select(ts => ts.name))}");
//			}
//			else
//			{
//				Debug.LogWarning("No texture_set found in JSON!");
//			}

//			// Verify data
//			VerifyData();
//		}
//		catch (System.Exception ex)
//		{
//			Debug.LogError($"JSON deserialization failed: {ex.Message}\nStackTrace: {ex.StackTrace}");
//		}
//	}

//	void VerifyData()
//	{
//		Debug.Log("Verifying data...");

//		// Maps
//		if (maps.Count > 0)
//		{
//			var sampleMap = maps[0];
//			Debug.Log($"Sample map: name={sampleMap.name}, bLightTiles={sampleMap.bLightTiles}, defsCount={(sampleMap.defs?.Length ?? 0)}, " +
//					  $"tiles={sampleMap.tiles?.nWidth}x{sampleMap.tiles?.nHeight}");
//		}

//		// Themes
//		if (themes.Count > 0)
//		{
//			Debug.Log($"Sample theme: name={themes[0].name}, szTileTextureSet={themes[0].szTileTextureSet ?? "null"}");
//		}

//		// Tiledefs
//		if (tileDefs.Count > 0)
//		{
//			var sampleTileDef = tileDefs[0];
//			Debug.Log($"Sample tiledef: name={sampleTileDef.name}, szTheme={sampleTileDef.szTheme}, szType={sampleTileDef.szType}, " +
//					  $"szGeom={sampleTileDef.szGeom}, bSlide={sampleTileDef.bSlide}, bNorth={sampleTileDef.bNorth}");
//		}

//		// Buttons
//		if (buttons.Count > 0)
//		{
//			var sampleButton = buttons[0];
//			Debug.Log($"Sample button: name={sampleButton.name}, szTexture={sampleButton.szTexture}, szButtonText={sampleButton.szButtonText}");
//		}

//		// TextureSets
//		if (textureSets.Count > 0)
//		{
//			var sampleTextureSet = textureSets[0];
//			var sampleFrame = sampleTextureSet.frames?.FirstOrDefault();
//			Debug.Log($"Sample texture_set: name={sampleTextureSet.name}, bAlphaTest={sampleTextureSet.bAlphaTest}, " +
//					  $"frameName={sampleFrame?.name ?? "null"}, frameTexture={sampleFrame?.szTexture ?? "null"}, " +
//					  $"frameDuration={sampleFrame?.fDuration}");
//		}

//		Debug.Log($"Verification complete: {maps.Count} maps, {themes.Count} themes, {tileDefs.Count} tiledefs, " +
//				  $"{buttons.Count} buttons, {textureSets.Count} texture sets");
//	}
//}