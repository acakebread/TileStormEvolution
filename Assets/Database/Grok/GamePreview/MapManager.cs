using UnityEngine;
using GameDatabase;
using System.Linq;
using static GameDatabase.DatabaseLoader;

namespace GamePreviewNamespace
{
	public class MapManager : MonoBehaviour
	{
		private DatabaseLoader databaseLoader;
		private string mapName;
		private string geometryPath;
		private string texturePath;
		private bool flip;
		private Map currentMap;
		private int width, height;
		private int[] tileMap;
		private GameObject mapRoot;

		public Map CurrentMap => currentMap;
		public int Width => width;
		public int Height => height;
		public int[] TileMap => tileMap;
		public GameObject MapRoot => mapRoot;
		public string CurrentMapName => mapName;

		public void Initialize(DatabaseLoader loader, string name, string geomPath, string texPath, bool flipMeshes)
		{
			databaseLoader = loader;
			mapName = name;
			geometryPath = geomPath;
			texturePath = texPath;
			flip = flipMeshes;
			InitializeMap();
		}

		public void Reset()
		{
			if (mapRoot != null)
			{
				Destroy(mapRoot);
				mapRoot = null;
			}
			ResetTileMap();
		}

		public void ResetTileMap()
		{
			tileMap = null;
			width = 0;
			height = 0;
			currentMap = null;
		}

		public void UpdateTileMap(int[] newTileMap)
		{
			if (newTileMap == null || newTileMap.Length != tileMap.Length)
			{
				Debug.LogError($"UpdateTileMap: Invalid newTileMap, length={newTileMap?.Length}, expected={tileMap.Length}");
				return;
			}
			System.Array.Copy(newTileMap, tileMap, tileMap.Length);
			Debug.Log("TileMap updated");
		}

		public TileDef GetTileDefAt(int tileIndex)
		{
			if (tileIndex >= 0 && tileIndex < tileMap.Length)
			{
				int defIndex = tileMap[tileIndex];
				if (defIndex >= 0 && defIndex < currentMap.defs.Length)
				{
					string szType = currentMap.defs[defIndex].szType;
					string szTheme = currentMap.defs[defIndex].szTheme;
					if (!string.IsNullOrEmpty(szType))
					{
						TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
						if (tileDef != null)
							return tileDef;
						Debug.LogWarning($"No TileDef found for szType={szType}, szTheme={szTheme} at tileIndex={tileIndex}");
					}
					else
					{
						Debug.LogWarning($"Empty szType at defIndex={defIndex}, tileIndex={tileIndex}");
					}
				}
				else
				{
					Debug.LogWarning($"Invalid defIndex={defIndex} at tileIndex={tileIndex}, defs.Length={currentMap.defs.Length}");
				}
			}
			else
			{
				Debug.LogWarning($"Invalid tileIndex={tileIndex}, tileMap.Length={tileMap.Length}");
			}
			return null;
		}

		public void UpdateTilePositions()
		{
			if (mapRoot == null)
			{
				Debug.LogWarning("UpdateTilePositions: mapRoot is null");
				return;
			}

			foreach (Transform tile in mapRoot.transform)
			{
				if (tile.name.Contains("Eggbot"))
					continue;

				string[] parts = tile.name.Split('_');
				if (parts.Length >= 3 && int.TryParse(parts[parts.Length - 2], out int x) && int.TryParse(parts[parts.Length - 1], out int z))
				{
					int index = z * width + x;
					if (index >= 0 && index < tileMap.Length)
					{
						int tileIndex = tileMap[index];
						if (tileIndex >= 0 && tileIndex < currentMap.defs.Length)
						{
							tile.position = new Vector3(x, 0f, z);
						}
					}
				}
			}
			Debug.Log("Tile positions updated");
		}

		private void InitializeMap()
		{
			currentMap = string.IsNullOrEmpty(mapName)
				? databaseLoader.Maps.FirstOrDefault()
				: databaseLoader.Maps.FirstOrDefault(m => m.name == mapName);

			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", databaseLoader.Maps.Select(m => m.name))}");
				return;
			}

			width = currentMap.tiles.nWidth;
			height = currentMap.tiles.nHeight;
			Debug.Log($"Map {currentMap.name}: width={width}, height={height}, defs.Length={currentMap.defs?.Length}");

			tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
			if (tileMap == null || tileMap.Length != width * height)
			{
				Debug.LogError($"Invalid tiles data! tiles={currentMap.tiles != null}, nTileIndex={currentMap.tiles?.TileData != null}, length={(tileMap != null ? tileMap.Length : -1)}, expected={width * height}");
				return;
			}

			Debug.Log($"tileMap: [{string.Join(", ", tileMap.Take(10))}...] (first 10)");

			if (mapRoot != null)
			{
				Destroy(mapRoot);
			}
			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);

			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					int index = z * width + x;
					index += currentMap.mixed.TileData.unpacked_bytes[z * width + x];//apply 'mixed. offsets for pre-scarambled map
					int defIndex = tileMap[index];
					if (defIndex < 0 || defIndex >= currentMap.defs.Length)
					{
						Debug.LogWarning($"Invalid defIndex={defIndex} at ({x},{z}), defs.Length={currentMap.defs.Length}");
						continue;
					}

					string szType = currentMap.defs[defIndex].szType;
					string szTheme = currentMap.defs[defIndex].szTheme;
					if (string.IsNullOrEmpty(szType))
					{
						Debug.LogWarning($"Null or empty szType at defIndex {defIndex} in map {currentMap.name}");
						continue;
					}

					if (szType == "tile_empty")
						continue;

					TileDef tileDef = databaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
					if (tileDef == null)
					{
						Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {currentMap.name}");
						continue;
					}

					Debug.Log($"Tile at ({x},{z}): szType={szType}, bSlide={tileDef.bSlide}, bRoll={tileDef.bRoll}, bDock={tileDef.bDock}, bStart={tileDef.bStart}, bConsole={tileDef.bConsole}, bEnd={tileDef.bEnd}, bEast={tileDef.bEast}, bWest={tileDef.bWest}, bNorth={tileDef.bNorth}, bSouth={tileDef.bSouth}");

					GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}");
					tileObj.transform.SetParent(mapRoot.transform, false);
					tileObj.transform.position = new Vector3(x, 0f, z);
					if (flip)
					{
						tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);
					}

					if (szType != "tile_invisible")
					{
						BoxCollider collider = tileObj.AddComponent<BoxCollider>();
						collider.size = new Vector3(1f, 0.5f, 1f);
						collider.center = new Vector3(0f, 0.25f, 0f);
					}

					string geomPath = $"{geometryPath}{tileDef.szGeom}".Replace(".x", "");
					Debug.Log($"Loading geometry: {geomPath}");
					GameObject geomAsset = Resources.Load<GameObject>(geomPath);
					if (geomAsset != null)
					{
						GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
						geomInstance.transform.localPosition = Vector3.zero;
						geomInstance.name = tileDef.szGeom;
					}
					else
					{
						if (szType != "tile_invisible")
						{
							Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.szType}");
							GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
							cube.transform.SetParent(tileObj.transform, false);
							cube.transform.localPosition = Vector3.zero;
							cube.transform.localScale = Vector3.one * 0.1f;
							cube.name = "Fallback_Cube";
						}
					}

					TextureSet textureSet = GetTextureForTileDef(tileDef);
					if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
					{
						TileAnimator animator = tileObj.AddComponent<TileAnimator>();
						animator.Initialize(textureSet, texturePath);
					}
					else
					{
						Debug.LogWarning($"No valid texture set for TileDef {tileDef.szType}, szTheme={tileDef.szTheme}");
					}

					mapCentre += new Vector3(x, 0f, z);
					activeTileCount++;
				}
			}

			if (activeTileCount > 0)
			{
				mapCentre /= activeTileCount;
				Camera.main.transform.position = mapCentre + Vector3.up * 10f;
				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				Debug.Log($"Camera set to {Camera.main.transform.position}, mapCentre={mapCentre}, activeTileCount={activeTileCount}");
			}
			else
			{
				Debug.LogWarning("No active tiles found, camera at origin");
				Camera.main.transform.position = Vector3.up * 10f;
				Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			}
		}

		private TextureSet GetTextureForTileDef(TileDef tileDef)
		{
			Theme theme = databaseLoader.Themes.FirstOrDefault(t => t.name == tileDef.szTheme || t.szTileTextureSet == tileDef.szTheme);
			if (theme != null && !string.IsNullOrEmpty(theme.szTileTextureSet))
			{
				TextureSet texSet = databaseLoader.TextureSets.FirstOrDefault(ts => ts.name == theme.szTileTextureSet);
				if (texSet != null && texSet.frames != null && texSet.frames.Length > 0)
				{
					Debug.Log($"TextureSet found: {texSet.name}, frames={texSet.frames.Length}");
					return texSet;
				}
			}
			Debug.LogWarning($"No TextureSet for theme={tileDef.szTheme}");
			return null;
		}
	}
}