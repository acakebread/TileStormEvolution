using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using GameDatabase;

namespace GamePreviewNamespace
{
	public class MapManager : MonoBehaviour
	{
		public struct TileData
		{
			public TileProperties Properties;
			public GameObject GameObject;
		}

		private DatabaseLoader.Map currentMap;
		private GameObject mapRoot;
		private TileData[] tiles;
		private List<DatabaseLoader.Waypoint> waypoints;

		public DatabaseLoader.Map CurrentMap => currentMap;
		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject MapRoot => mapRoot;
		public TileData[] Tiles => tiles;
		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();

		public void Reset()
		{
			if (mapRoot != null) Destroy(mapRoot);
			mapRoot = null;
			currentMap = null;
			waypoints = null;
			tiles = null;
		}

		private bool IsValidTileIndex(int tileIndex) => tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;

		public GridCoord GetTileCoordinates(int tileIndex) => IsValidTileIndex(tileIndex) ? new GridCoord(tileIndex % Width, tileIndex / Width) : new GridCoord(0, 0);

		public Vector3 GetTilePosition(int tileIndex) => GetTileCoordinates(tileIndex).ToPosition();

		public TileProperties GetTilePropertiesAt(int tileIndex)
		{
			if (!IsValidTileIndex(tileIndex))
			{
				Debug.LogWarning($"Invalid tile index: {tileIndex}");
				return null;
			}
			return tiles[tileIndex].Properties;
		}

		public int GetAdjacentTile(int tileIndex, int dirBit)
		{
			var coord = GetTileCoordinates(tileIndex);
			var (dx, dz) = TileProperties.GetDirectionOffset(dirBit);
			var newCoord = coord.Add(dx, dz);
			if (newCoord.X < 0 || newCoord.X >= Width || newCoord.Z < 0 || newCoord.Z >= Height) return -1;
			return newCoord.ToIndex(Width);
		}

		public GridCoord GetTileCoordinatesForLast(int index, int dirBit, TileProperties.TileFlags flags)
		{
			int lastIndex = SearchDirectionForLast(index, dirBit, flags);
			return GetTileCoordinates(lastIndex);
		}

		public int SearchDirectionForLast(int index, int dirBit, TileProperties.TileFlags flags)
		{
			var coord = GetTileCoordinates(index);
			var (dx, dz) = TileProperties.GetDirectionOffset(dirBit);
			var nextCoord = coord.Add(dx, dz);
			while (nextCoord.X >= 0 && nextCoord.X < Width && nextCoord.Z >= 0 && nextCoord.Z < Height)
			{
				var nextIndex = nextCoord.ToIndex(Width);
				var props = GetTilePropertiesAt(nextIndex);
				if (props == null || (props.Flags & flags) == 0) break;
				index = nextIndex;
				coord = nextCoord;
				nextCoord = coord.Add(dx, dz);
			}
			return index;
		}

		private TileData CreateTileData(int tileDefIndex, string szType, string szTheme)
		{
			if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length ||
				string.IsNullOrEmpty(szType) || szType == "tile_empty")
			{
				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
					Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");
				else if (string.IsNullOrEmpty(szType))
					Debug.LogWarning($"Null szType at tileDefIndex {tileDefIndex}");
				return default;
			}

			var tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
			if (tileDef == null)
				return default;

			return new TileData { Properties = new TileProperties(tileDef) };
		}

		public void Initialize(string mapName)
		{
			Reset();
			currentMap = string.IsNullOrEmpty(mapName)
				? DatabaseLoader.instance.Maps.FirstOrDefault()
				: DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

			if (currentMap == null)
			{
				Debug.LogError($"No map found for mapName={mapName}! Available maps: {string.Join(", ", DatabaseLoader.instance.Maps.Select(m => m.name))}");
				return;
			}

			mapRoot = new GameObject($"Map_{currentMap.name}");
			mapRoot.transform.SetParent(transform, false);
			LoadTileData(currentMap.tiles);

			waypoints = currentMap.waypoints?.Where(w => w != null).ToList();
			if (waypoints == null || waypoints.Count == 0)
			{
				waypoints = SetupWaypoints();
			}

			if (PreviewSettings.Scramble)
				Scramble();
		}

		public void Reload()
		{
			if (currentMap?.tiles == null || tiles == null)
			{
				Debug.LogWarning("Cannot reload: invalid map or tiles data");
				return;
			}

			LoadTileData(currentMap.tiles);
			waypoints = currentMap.waypoints?.Where(w => w != null).ToList();
			if (waypoints == null || waypoints.Count == 0)
			{
				waypoints = SetupWaypoints();
			}

			UpdateTileObjectNamesAndPositions();
		}

		private void LoadTileData(DatabaseLoader.Tiles tiles)
		{
			var tileMap = tiles.TileData.unpacked_bytes;
			if (tileMap == null || tileMap.Length != tiles.nWidth * tiles.nHeight)
			{
				Debug.LogError($"Invalid tiles data! length={(tileMap?.Length ?? -1)}, expected={tiles.nWidth * tiles.nHeight}");
				return;
			}

			this.tiles = new TileData[tiles.nWidth * tiles.nHeight];
			Vector3 mapCentre = Vector3.zero;
			int activeTileCount = 0;

			for (int index = 0; index < tileMap.Length; index++)
			{
				int tileDefIndex = tileMap[index];
				string szType = currentMap.defs[tileDefIndex].szType;
				string szTheme = currentMap.defs[tileDefIndex].szTheme;

				this.tiles[index] = CreateTileData(tileDefIndex, szType, szTheme);
				if (this.tiles[index].Properties == null)
					continue;

				if (szType == "tile_invisible")
					continue;

				var coord = GetTileCoordinates(index);
				GameObject tileObj = new GameObject($"{this.tiles[index].Properties.Type}_{coord.X}_{coord.Z}");
				this.tiles[index].GameObject = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = coord.ToPosition();
				if (PreviewSettings.FlipGeometry)
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

				if (this.tiles[index].Properties.Movable)
				{
					var collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.5f, 1f);
					collider.center = new Vector3(0f, 0.25f, 0f);
				}

				string geomPath = $"{PreviewSettings.GeometryPath}{this.tiles[index].Properties.Geom}".Replace(".x", "");
				GameObject geomAsset = Resources.Load<GameObject>(geomPath);
				if (geomAsset != null)
				{
					GameObject geomInstance = Object.Instantiate(geomAsset, tileObj.transform);
					geomInstance.transform.localPosition = Vector3.zero;
					geomInstance.name = this.tiles[index].Properties.Geom;
				}
				else
				{
					Debug.LogWarning($"Geometry not found: {geomPath}");
					GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.SetParent(tileObj.transform, false);
					cube.transform.localPosition = Vector3.zero;
					cube.transform.localScale = Vector3.one * 0.1f;
					cube.name = "Fallback_Cube";
				}

				DatabaseLoader.TextureSet textureSet = TileAnimator.GetTextureSetForTileDef(this.tiles[index].Properties.tileDef);
				if (textureSet?.frames?.Length > 0)
				{
					var animator = tileObj.AddComponent<TileAnimator>();
					animator.Initialize(textureSet);
				}
				else
				{
					Debug.LogWarning($"No texture set for {this.tiles[index].Properties.Type}, theme={this.tiles[index].Properties.Theme}");
				}

				mapCentre += coord.ToPosition();
				activeTileCount++;
			}

			SetCameraPosition(mapCentre, activeTileCount);
		}

		public void Scramble()
		{
			if (currentMap?.mixed?.TileData?.unpacked_bytes == null || tiles == null)
			{
				Debug.LogWarning("Cannot scramble: invalid map or tiles data");
				return;
			}

			var scrambledTiles = new TileData[tiles.Length];
			var offsets = currentMap.mixed.TileData.unpacked_bytes;
			for (int index = 0; index < tiles.Length; index++)
			{
				int scrambledIndex = index + offsets[index];
				if (scrambledIndex >= 0 && scrambledIndex < tiles.Length)
					scrambledTiles[index] = tiles[scrambledIndex];
			}

			tiles = scrambledTiles;
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			for (int index = 0; index < tiles.Length; index++)
			{
				if (tiles[index].GameObject == null) continue;
				var coord = GetTileCoordinates(index);
				tiles[index].GameObject.name = $"{tiles[index].Properties?.Type ?? "Empty"}_{coord.X}_{coord.Z}";
				tiles[index].GameObject.transform.position = coord.ToPosition();
			}
		}

		private void SetCameraPosition(Vector3 mapCentre, int activeTileCount)
		{
			Camera.main.transform.position = activeTileCount > 0 ? (mapCentre / activeTileCount) + Vector3.up * 10f : Vector3.up * 10f;
			Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		}

		public int GetStartTile()
		{
			for (int i = 0; i < Width * Height; i++)
			{
				var props = GetTilePropertiesAt(i);
				if (props != null && props.IsStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public int GetEndTile()
		{
			for (int i = 0; i < Width * Height; i++)
			{
				var props = GetTilePropertiesAt(i);
				if (props != null && props.IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		private List<DatabaseLoader.Waypoint> SetupWaypoints()
		{
			var generatedWaypoints = new List<DatabaseLoader.Waypoint>();
			if (tiles == null || tiles.Length != Width * Height)
			{
				Debug.LogWarning("Cannot setup waypoints: invalid tile data");
				return generatedWaypoints;
			}

			int startTile = GetStartTile();
			int endTile = GetEndTile();

			if (startTile == -1 || endTile == -1)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				return generatedWaypoints;
			}

			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });
			List<int> path = FindPath(startTile, endTile);

			if (path != null)
			{
				foreach (int tile in path)
				{
					if (tile == startTile)
						continue;
					if (FindAdjacentConsole(tile) != -1 || tile == endTile)
					{
						generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = tile });
					}
				}
			}
			else
			{
				Debug.LogWarning("Failed to find path from start to end for waypoint setup");
			}

			Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
			return generatedWaypoints;
		}

		private List<int> FindPath(int startTile, int targetTile)
		{
			var startProps = GetTilePropertiesAt(startTile);
			if (startProps == null)
				return null;

			foreach (int dirBit in TileProperties.Directions)
			{
				if ((startProps.Nav & dirBit) == 0)
					continue;
				var path = FindPathRecursive(startTile, targetTile, dirBit);
				if (path != null)
					return path;
			}
			return null;
		}

		private List<int> FindPathRecursive(int currentTile, int targetTile, int currentDirBit, List<int> path = null)
		{
			path ??= new List<int>();
			path.Add(currentTile);

			if (currentTile == targetTile)
				return path;

			var currentCoord = GetTileCoordinates(currentTile);
			var currentProps = GetTilePropertiesAt(currentTile);
			if (currentProps == null)
			{
				path.RemoveAt(path.Count - 1);
				return null;
			}

			int[] tryDirections = GetTryDirections(currentProps.Nav, currentDirBit);
			for (int i = 0; i < tryDirections.Length; i++)
			{
				int dirBit = tryDirections[i];
				int nextTile = GetAdjacentTile(currentTile, dirBit);
				if (nextTile == -1)
					continue;

				var nextProps = GetTilePropertiesAt(nextTile);
				if (!TileProperties.CanMoveBetweenTiles(currentProps, nextProps, dirBit))
					continue;

				var result = FindPathRecursive(nextTile, targetTile, dirBit, path);
				if (result != null)
					return result;
			}

			path.RemoveAt(path.Count - 1);
			return null;
		}

		private int[] GetTryDirections(int nav, int currentDirBit)
		{
			if ((TileProperties.GetOppositeDirection(nav) & nav) == nav)
				return new[] { currentDirBit };
			if (currentDirBit != 0)
				return new[] { nav & ~TileProperties.GetOppositeDirection(currentDirBit) };
			return TileProperties.Directions;
		}

		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
		{
			path = null;
			if (Waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex + 1 >= Waypoints.Count)
			{
				Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
				return false;
			}

			int startTile = Waypoints[currentWaypointIndex].nTile;
			int targetTile = Waypoints[currentWaypointIndex + 1].nTile;
			path = FindPath(startTile, targetTile);
			if (path != null)
			{
				Debug.Log($"Found path to waypoint {targetTile}: [{FormatPath(path)}]");
				return true;
			}
			return false;
		}

		public int FindAdjacentConsole(int nTile)
		{
			if (!IsValidTileIndex(nTile))
				return -1;

			foreach (int dirBit in TileProperties.Directions)
			{
				int consoleTile = GetAdjacentTile(nTile, dirBit);
				if (consoleTile == -1)
					continue;

				var consoleProps = GetTilePropertiesAt(consoleTile);
				if (consoleProps?.IsConsole != true)
					continue;

				int consoleNav = consoleProps.Nav;
				if (consoleNav == 0)
					continue;

				int navTile = GetAdjacentTile(consoleTile, consoleNav);
				if (navTile == nTile)
					return consoleTile;
			}

			return -1;
		}

		public string FormatPath(List<int> path) =>
			string.Join(" -> ", path.Select(t => GetTileCoordinates(t).ToString()));
	}
}
