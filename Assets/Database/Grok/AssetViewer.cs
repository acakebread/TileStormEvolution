using UnityEngine;
using GameDatabase;
using System.Linq;
using static GameDatabase.DatabaseLoader;

namespace AssetViewerNamespace
{
	public class AssetViewer : MonoBehaviour
	{
		[Header("woraround for inverted .obj meshes")]
		public bool flip = true;
		private Vector3 mapCentre = Vector3.zero;
		private int activeTileCount = 0;

		[SerializeField] private DatabaseLoader databaseLoader;
		[SerializeField] private string mapName = "";
		[SerializeField] private string geometryPath = "Geometry/fbx/";
		[SerializeField] private string texturePath = "Textures/";

		private bool isInitialized = false;

		void Start()
		{
			mapCentre = Vector3.zero;
			activeTileCount = 0;

			if (databaseLoader == null)
			{
				databaseLoader = FindAnyObjectByType<DatabaseLoader>();
				if (databaseLoader == null)
				{
					Debug.LogError("AssetViewer requires a DatabaseLoader!");
					return;
				}
			}

			Debug.Log($"AssetViewer Start: databaseLoader found, Maps.Count={databaseLoader.Maps.Count}");

			// Subscribe to event
			databaseLoader.OnDatabaseLoaded += Initialize;

			// Fallback: check if already loaded
			if (databaseLoader.Maps.Count > 0)
			{
				Initialize();
			}
		}

		void Initialize()
		{
			if (isInitialized)
				return;

			isInitialized = true;
			Debug.Log($"AssetViewer initialized: Maps.Count={databaseLoader.Maps.Count}");
			DisplayMap();
		}

		void OnDestroy()
		{
			if (databaseLoader != null)
			{
				databaseLoader.OnDatabaseLoaded -= Initialize;
			}
		}

		void DisplayMap()
		{
			Map map = string.IsNullOrEmpty(mapName)
				? databaseLoader.Maps.FirstOrDefault()
				: databaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

			if (map == null)
			{
				Debug.LogError("No map found!");
				return;
			}

			if (map.tiles == null || map.tiles.nTileIndex == null || map.tiles.nTileIndex.unpacked_bytes == null)
			{
				Debug.LogError($"Invalid tiles data for map {map.name}!");
				return;
			}

			int width = map.tiles.nWidth;
			int height = map.tiles.nHeight;
			int[] tileIndices = map.tiles.nTileIndex.unpacked_bytes;

			if (tileIndices.Length != width * height)
			{
				Debug.LogError($"Tile indices length ({tileIndices.Length}) does not match grid size ({width}x{height}) for map {map.name}!");
				return;
			}

			GameObject mapRoot = new GameObject($"Map_{map.name}");
			mapRoot.transform.SetParent(transform, false);

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					int index = z * width + x;
					int defIndex = tileIndices[index];

					if (defIndex < 0 || defIndex >= map.defs.Length)
					{
						Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {map.name}");
						continue;
					}

					string szType = map.defs[defIndex].szType;
					if ("tile_empty" == szType || "tile_invisible" == szType)
						continue;

					string szTheme = map.defs[defIndex].szTheme;
					if (string.IsNullOrEmpty(szType))
					{
						Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {map.name}");
						continue;
					}

					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					if (tileDef == null)
					{
						Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {map.name}");
						continue;
					}

					GameObject tileObj = new GameObject($"{tileDef.name}_{x}_{z}");
					tileObj.transform.SetParent(mapRoot.transform, false);
					tileObj.transform.position = new Vector3(x, 0f, z);
					if (flip)
						tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

					string geomPath = $"{geometryPath}{tileDef.szGeom}".Replace(".x", "");
					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
					if (geomAsset != null)
					{
						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
						geomInstance.transform.localPosition = Vector3.zero;
						geomInstance.name = tileDef.szGeom;

						mapCentre += tileObj.transform.position;
						activeTileCount++;
					}
					else
					{
						Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.name}");
						GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
						cube.transform.SetParent(tileObj.transform, false);
						cube.transform.localPosition = Vector3.zero;
						cube.transform.localScale = Vector3.one * 0.1f;
						cube.name = "Fallback_Cube";
						cube.SetActive(false);
					}

					// Apply texture or animation
					TextureSet textureSet = GetTextureForTileDef(tileDef, szTheme);
					if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
					{
						TileAnimator animator = tileObj.AddComponent<TileAnimator>();
						animator.Initialize(textureSet, texturePath);
					}
					else
					{
						Debug.LogWarning($"No valid texture set for TileDef {tileDef.name}, szTheme={szTheme}");
					}
				}
			}

			Debug.Log($"Displayed map {map.name} with {width}x{height} tiles");

			if (activeTileCount > 0)
				mapCentre /= activeTileCount;
			Camera.main.transform.position = mapCentre + (Vector3.up - Vector3.forward) * 8;
		}

		private TextureSet GetTextureForTileDef(TileDef tileDef, string szTheme)
		{
			Theme theme = databaseLoader.Themes.FirstOrDefault(t => t.name == szTheme || t.szTileTextureSet == szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				TextureSet texSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
				{
					Debug.Log($"TextureSet found: {texSet.name}, frames={texSet.frames.Length}");
					return texSet;
				}
			}

			TextureSet fallbackSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == szTheme);
			if (fallbackSet != null && fallbackSet.frames != null && fallbackSet.frames.Length > 0)
			{
				Debug.Log($"Fallback TextureSet: {fallbackSet.name}, frames={fallbackSet.frames.Length}");
				return fallbackSet;
			}

			return null;
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;
			if (GUI.Button(new Rect(10, 10, 100, 30), "reload"))
			{
				isInitialized = false;
				foreach (Transform child in transform)
				{
					Destroy(child.gameObject);
				}
				Initialize();
			}
		}
	}
}


//using UnityEngine;
//using GameDatabase;
//using System.Linq;
//using static GameDatabase.DatabaseLoader;

//namespace AssetViewerNamespace
//{
//	public class AssetViewer : MonoBehaviour
//	{
//		[Header("woraround for inverted .obj meshes")]
//		public bool flip = true;
//		private Vector3 mapCentre = Vector3.zero;
//		private int activeTileCount = 0;

//		[SerializeField] private DatabaseLoader databaseLoader;
//		[SerializeField] private string mapName = ""; // Optional: leave empty to use first map
//		[SerializeField] private string geometryPath = "Geometry/fbx/";
//		[SerializeField] private string texturePath = "Textures/";

//		void Start()
//		{
//			mapCentre = Vector3.zero;
//			activeTileCount = 0;

//			if (databaseLoader == null)
//			{
//				databaseLoader = FindAnyObjectByType<DatabaseLoader>();
//				if (databaseLoader == null)
//				{
//					Debug.LogError("AssetViewer requires a DatabaseLoader!");
//					return;
//				}
//			}

//			// Subscribe to DatabaseLoader's event
//			databaseLoader.OnDatabaseLoaded += DisplayMap;
//			DisplayMap();
//		}

//		void OnDestroy()
//		{
//			// Unsubscribe to prevent memory leaks
//			if (databaseLoader != null)
//			{
//				databaseLoader.OnDatabaseLoaded -= DisplayMap;
//			}
//		}

//		void DisplayMap()
//		{
//			// Get the map
//			Map map = string.IsNullOrEmpty(mapName)
//				? databaseLoader.Maps.FirstOrDefault()
//				: databaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

//			if (map == null)
//			{
//				Debug.LogError("No map found!");
//				return;
//			}

//			if (map.tiles == null || map.tiles.nTileIndex == null || map.tiles.nTileIndex.unpacked_bytes == null)
//			{
//				Debug.LogError($"Invalid tiles data for map {map.name}!");
//				return;
//			}

//			int width = map.tiles.nWidth;
//			int height = map.tiles.nHeight;
//			int[] tileIndices = map.tiles.nTileIndex.unpacked_bytes;

//			if (tileIndices.Length != width * height)
//			{
//				Debug.LogError($"Tile indices length ({tileIndices.Length}) does not match grid size ({width}x{height}) for map {map.name}!");
//				return;
//			}

//			// Create a parent GameObject for tiles
//			GameObject mapRoot = new GameObject($"Map_{map.name}");
//			mapRoot.transform.SetParent(transform, false);

//			// Load tile assets
//			for (int z = 0; z < height; z++)
//			{
//				for (int x = 0; x < width; x++)
//				{
//					int index = z * width + x;
//					int defIndex = tileIndices[index];

//					if (defIndex < 0 || defIndex >= map.defs.Length)
//					{
//						Debug.LogWarning($"Invalid defIndex {defIndex} at ({x}, {z}) in map {map.name}");
//						continue;
//					}

//					// Get szType from defs
//					string szType = map.defs[defIndex].szType;
//					if ("tile_empty" == szType) continue;//skip empty tiles
//					if ("tile_invisible" == szType) continue;//skip invisible tiles for now.. I think they are needed in map logic so will reinstate later
//					string szTheme = map.defs[defIndex].szTheme;
//					if (string.IsNullOrEmpty(szType))
//					{
//						Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {map.name}");
//						continue;
//					}

//					// Look up TileDef using szType and szTheme
//					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
//					if (tileDef == null)
//					{
//						Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {map.name}");
//						continue;
//					}

//					// Create tile GameObject
//					GameObject tileObj = new GameObject($"{tileDef.name}_{x}_{z}");
//					tileObj.transform.SetParent(mapRoot.transform, false);
//					//tileObj.transform.position = new Vector3(x, 0f, z);
//					//tileObj.transform.SetPositionAndRotation(new Vector3(x - width/2, 0f, z- height/2), Quaternion.Euler(0, 180, 0));//workaround for problem importing obj at correct orientation
//					tileObj.transform.position = new Vector3(x, 0f, z);
//					if (true == flip) tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

//					// Load geometry
//					string geomPath = $"{geometryPath}{tileDef.szGeom}".Replace(".x", "");
//					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
//					if (geomAsset != null)
//					{
//						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
//						geomInstance.transform.localPosition = Vector3.zero;
//						geomInstance.name = tileDef.szGeom;

//						//bodge to find map centre
//						mapCentre += tileObj.transform.position;
//						activeTileCount++;
//					}
//					else
//					{
//						Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.name}");
//						// Fallback: Add a cube
//						GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//						cube.transform.SetParent(tileObj.transform, false);
//						cube.transform.localPosition = Vector3.zero;
//						cube.transform.localScale = Vector3.one * 0.1f;
//						cube.name = "Fallback_Cube";
//						cube.SetActive(false);
//					}

//					// Load texture
//					string textureName = GetTextureForTileDef(tileDef, szTheme);
//					if (!string.IsNullOrEmpty(textureName))
//					{
//						string texPath = $"{texturePath}{textureName}".Replace(".tga", "");
//						Texture2D texture = Resources.Load<Texture2D>(texPath);
//						if (texture != null)
//						{
//							MeshRenderer renderer = tileObj.GetComponentInChildren<MeshRenderer>();
//							if (renderer != null)
//							{
//								Material mat = new Material(Shader.Find("Standard"));
//								mat.mainTexture = texture;
//								renderer.material = mat;
//							}
//						}
//						else
//						{
//							Debug.LogWarning($"Texture not found at {texPath} for TileDef {tileDef.name}");
//						}
//					}
//				}
//			}

//			Debug.Log($"Displayed map {map.name} with {width}x{height} tiles");

//			if (activeTileCount > 0) mapCentre /= activeTileCount;
//			Camera.main.transform.position = mapCentre + (Vector3.up - Vector3.forward) * 8;//bodge to move camera to map centre
//		}

//		private string GetTextureForTileDef(TileDef tileDef, string szTheme)
//		{
//			// Try to find texture via szTheme and themes
//			Theme theme = databaseLoader.Themes.FirstOrDefault(t => t.name == szTheme || t.szTileTextureSet == szTheme);
//			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
//			{
//				// Assume szTileTextureSet matches a texture_set.name or texture name
//				TextureSet texSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
//				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
//				{
//					return texSet.frames[0].szTexture; // Use first frame’s texture
//				}
//				return theme.szTileTextureSet; // Fallback to szTileTextureSet
//			}

//			// Fallback: Check texture_set directly
//			TextureSet fallbackSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == szTheme);
//			if (fallbackSet != null && fallbackSet.frames != null && fallbackSet.frames.Length > 0)
//			{
//				return fallbackSet.frames[0].szTexture;
//			}

//			return null;
//		}


//		void OnGUI()
//		{
//			GUI.skin.label.fontSize = 24;
//			GUI.color = Color.green;
//			if (true == GUI.Button(new Rect(10, 10, 100, 30), "reload"))
//			{
//				Start();
//			}
//		}
//	}
//}