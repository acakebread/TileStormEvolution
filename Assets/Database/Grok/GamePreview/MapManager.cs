using UnityEngine;
using GameDatabase;
using System.Linq;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class MapManager : MonoBehaviour
	{
		private DatabaseLoader.Map currentMap;
		private GameObject mapRoot;
		private GameObject[] tiles;

		public DatabaseLoader.Map CurrentMap => currentMap;
		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject MapRoot => mapRoot;
		public GameObject[] Tiles => tiles;

		private (int bit, int stride, int oppositeBit)[] _directions;
		public (int bit, int stride, int oppositeBit)[] Directions
		{
			get
			{
				if (_directions == null)
				{
					_directions = new[]
					{
						(1, Width, 2), // North
                        (2, -Width, 1), // South
                        (4, 1, 8), // East
                        (8, -1, 4) // West
                    };
				}
				return _directions;
			}
		}

		public void Initialize(string name)
		{
			InitializeMap(name);
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
			tiles = null;
			currentMap = null;
		}

		public TileProperties GetTileDefAt(int tileIndex)
		{
			return tileIndex >= 0 && tileIndex < tiles?.Length ? tiles[tileIndex]?.GetComponent<TileProperties>() : null;
		}

		public int GetStartTile()
		{
			for (int i = 0; i < Width * Height; i++)
			{
				var tileDef = GetTileDefAt(i);
				if (tileDef != null && tileDef.tileDef.bStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public bool FindPath(int startTile, int targetTile, int startDirBit, out List<int> path)
		{
			path = new List<int>();
			var visited = new List<int>();
			return FindPathRecursive(startTile, targetTile, startDirBit, visited, out path);
		}

		private bool FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> currentPath, out List<int> resultPath)
		{
			resultPath = null;
			currentPath.Add(currentTile);

			if (currentTile == targetTile)
			{
				resultPath = new List<int>(currentPath);
				return true;
			}

			var currentDef = GetTileDefAt(currentTile);
			if (currentDef == null)
				return false;

			int nav = currentDef.GetNav(false);
			int[] tryDirs = (nav == 3 || nav == 12)
				? new[] { currentDirBit }
				: currentDirBit != 0
					? new[] { currentDirBit, nav & ~(currentDirBit | Directions.FirstOrDefault(d => d.bit == currentDirBit).oppositeBit) }
					: new[] { 1, 2, 4, 8 };

			foreach (int dirBit in tryDirs)
			{
				if (dirBit == 0 || (nav & dirBit) == 0)
					continue;

				var dir = Directions.FirstOrDefault(d => d.bit == dirBit);
				int nextTile = currentTile + dir.stride;
				if (nextTile < 0 || nextTile >= Tiles?.Length)
					continue;

				var nextDef = GetTileDefAt(nextTile);
				if (!TileProperties.CanMoveBetweenTiles(currentDef, nextDef, dirBit, dir.oppositeBit))
					continue;

				if (nextDef?.tileDef.bConsole == true)
					continue;

				if (FindPathRecursive(nextTile, targetTile, dirBit, new List<int>(currentPath), out resultPath))
					return true;
			}

			return false;
		}

		private void InitializeMap(string mapName)
		{
			currentMap = string.IsNullOrEmpty(mapName)
				? DatabaseLoader.instance.Maps.FirstOrDefault()
				: DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.instance.Maps.Select(m => m.name))}");
				return;
			}

			Debug.Log($"Map {currentMap.name}: width={Width}, height={Height}, defs.Length={currentMap.defs?.Length}");

			tiles = new GameObject[Width * Height];
			var tileMap = currentMap.tiles?.TileData?.unpacked_bytes;
			if (tileMap == null || tileMap.Length != Width * Height)
			{
				Debug.LogError($"Invalid tiles data! tiles={currentMap.tiles != null}, nTileIndex={currentMap.tiles?.TileData != null}, length={(tileMap != null ? tileMap.Length : -1)}, expected={Width * Height}");
				return;
			}

			if (mapRoot != null)
			{
				Destroy(mapRoot);
			}
			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);

			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int index = 0; index < tileMap.Length; index++)
			{
				tiles[index] = null;
				int x = index % Width;
				int z = index / Width;
				int scrambledIndex = index;
				if (PreviewSettings.Scramble)
					scrambledIndex += currentMap.mixed.TileData.unpacked_bytes[index];

				int defIndex = tileMap[scrambledIndex];
				if (defIndex < 0 || defIndex >= currentMap.defs.Length)
				{
					Debug.LogWarning($"Invalid defIndex={defIndex} at index={index} ({x},{z}), defs.Length={currentMap.defs.Length}");
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

				DatabaseLoader.TileDef tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
				if (tileDef == null)
				{
					Debug.LogWarning($"TileDef not found for szType={szType}, szTheme={szTheme} at ({x}, {z}) in map {currentMap.name}");
					continue;
				}

				GameObject tileObj = new GameObject($"{tileDef.szType}_{x}_{z}", typeof(TileProperties));
				var properties = tileObj.GetComponent<TileProperties>();
				properties.tileDef = tileDef;
				tiles[index] = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = new Vector3(x, 0f, z);
				if (PreviewSettings.FlipGeometry)
				{
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);
				}

				if (properties.movable)
				{
					BoxCollider collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.5f, 1f);
					collider.center = new Vector3(0f, 0.25f, 0f);
				}

				string geomPath = $"{PreviewSettings.GeometryPath}{tileDef.szGeom}".Replace(".x", "");
				GameObject geomAsset = Resources.Load<GameObject>(geomPath);
				if (geomAsset != null)
				{
					GameObject geomInstance = Instantiate(geomAsset, tileObj.transform);
					geomInstance.transform.localPosition = Vector3.zero;
					geomInstance.name = tileDef.szGeom;
				}
				else if (szType != "tile_invisible")
				{
					Debug.LogWarning($"Geometry not found at {geomPath} for TileDef {tileDef.szType}");
					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.SetParent(tileObj.transform, false);
					cube.transform.localPosition = Vector3.zero;
					cube.transform.localScale = Vector3.one * 0.1f;
					cube.name = "Fallback_Cube";
				}

				DatabaseLoader.TextureSet textureSet = TileAnimator.GetTextureSetForTileDef(tileDef);
				if (textureSet != null && textureSet.frames != null && textureSet.frames.Length > 0)
				{
					TileAnimator animator = tileObj.AddComponent<TileAnimator>();
					animator.Initialize(textureSet);
				}
				else
				{
					Debug.LogWarning($"No valid texture set for TileDef {tileDef.szType}, szTheme={tileDef.szTheme}");
				}

				mapCentre += new Vector3(x, 0f, z);
				activeTileCount++;
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
	}
}
