using UnityEngine;
using GameDatabase;
using System.Linq;
using static GameDatabase.DatabaseLoader;

namespace AssetViewerNamespace
{
	public class AssetViewer : MonoBehaviour
	{
		[SerializeField] private DatabaseLoader databaseLoader;
		[SerializeField] private string mapName = ""; // Optional: leave empty to use first map
		[SerializeField] private string geometryPath = "Geometry/fbx/";
		[SerializeField] private string texturePath = "Textures/";

		void Start()
		{
			if (databaseLoader == null)
			{
				databaseLoader = FindAnyObjectByType<DatabaseLoader>();
				if (databaseLoader == null)
				{
					Debug.LogError("AssetViewer requires a DatabaseLoader!");
					return;
				}
			}

			// Subscribe to DatabaseLoader's event
			databaseLoader.OnDatabaseLoaded += DisplayMap;
			DisplayMap();
		}

		void OnDestroy()
		{
			// Unsubscribe to prevent memory leaks
			if (databaseLoader != null)
			{
				databaseLoader.OnDatabaseLoaded -= DisplayMap;
			}
		}

		void DisplayMap()
		{
			// Get the map
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

			// Create a parent GameObject for tiles
			GameObject mapRoot = new GameObject($"Map_{map.name}");
			mapRoot.transform.SetParent(transform, false);

			// Load tile assets
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

					// Get szType from defs
					string szType = map.defs[defIndex].szType;
					string szTheme = map.defs[defIndex].szTheme;
					if (string.IsNullOrEmpty(szType))
					{
						Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {map.name}");
						continue;
					}

					// Look up TileDef using szType and szTheme
					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					if (tileDef == null)
					{
						Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {map.name}");
						continue;
					}

					// Create tile GameObject
					GameObject tileObj = new GameObject($"{tileDef.name}_{x}_{z}");
					tileObj.transform.SetParent(mapRoot.transform, false);
					//tileObj.transform.position = new Vector3(x, 0f, z);
					tileObj.transform.SetPositionAndRotation(new Vector3(x - width/2, 0f, z- height/2), Quaternion.Euler(0, 180, 0));//workaround for problem importing obj at correct orientation

					// Load geometry
					string geomPath = $"{geometryPath}{tileDef.szGeom}".Replace(".x", "");
					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
					if (geomAsset != null)
					{
						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
						geomInstance.transform.localPosition = Vector3.zero;
						geomInstance.name = tileDef.szGeom;
					}
					else
					{
						Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.name}");
						// Fallback: Add a cube
						GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
						cube.transform.SetParent(tileObj.transform, false);
						cube.transform.localPosition = Vector3.zero;
						cube.transform.localScale = Vector3.one * 0.1f;
						cube.name = "Fallback_Cube";
						cube.SetActive(false);
					}

					// Load texture
					string textureName = GetTextureForTileDef(tileDef, szTheme);
					if (!string.IsNullOrEmpty(textureName))
					{
						string texPath = $"{texturePath}{textureName}".Replace(".tga", "");
						Texture2D texture = Resources.Load<Texture2D>(texPath);
						if (texture != null)
						{
							MeshRenderer renderer = tileObj.GetComponentInChildren<MeshRenderer>();
							if (renderer != null)
							{
								Material mat = new Material(Shader.Find("Standard"));
								mat.mainTexture = texture;
								renderer.material = mat;
							}
						}
						else
						{
							Debug.LogWarning($"Texture not found at {texPath} for TileDef {tileDef.name}");
						}
					}
				}
			}

			Debug.Log($"Displayed map {map.name} with {width}x{height} tiles");
		}

		private string GetTextureForTileDef(TileDef tileDef, string szTheme)
		{
			// Try to find texture via szTheme and themes
			Theme theme = databaseLoader.Themes.FirstOrDefault(t => t.name == szTheme || t.szTileTextureSet == szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				// Assume szTileTextureSet matches a texture_set.name or texture name
				TextureSet texSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
				{
					return texSet.frames[0].szTexture; // Use first frame’s texture
				}
				return theme.szTileTextureSet; // Fallback to szTileTextureSet
			}

			// Fallback: Check texture_set directly
			TextureSet fallbackSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == szTheme);
			if (fallbackSet != null && fallbackSet.frames != null && fallbackSet.frames.Length > 0)
			{
				return fallbackSet.frames[0].szTexture;
			}

			return null;
		}


		void OnGUI()
		{
			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;
			if (true == GUI.Button(new Rect(10, 10, 100, 30), "reload"))
			{
				Start();
			}
		}
	}
}