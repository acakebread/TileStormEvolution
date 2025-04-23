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
		private GameObject spareTile;

		public DatabaseLoader.Map CurrentMap => currentMap;
		public string CurrentMapName => currentMap?.name;
		public int Width => currentMap?.tiles.nWidth ?? 0;
		public int Height => currentMap?.tiles.nHeight ?? 0;
		public GameObject MapRoot => mapRoot;
		public TileData[] Tiles => tiles;
		public IReadOnlyList<DatabaseLoader.Waypoint> Waypoints => waypoints?.AsReadOnly();

		private bool IsValidTileIndex(int tileIndex) => tileIndex >= 0 && tileIndex < tiles?.Length && Width > 0;

		public TileProperties GetTilePropertiesAt(int tileIndex) => IsValidTileIndex(tileIndex) ? tiles[tileIndex].Properties : null;

		public GridCoord GetTileCoordinates(int tileIndex) => new GridCoord(tileIndex % Width, tileIndex / Width);

		public Vector3 GetTilePosition(int tileIndex) => GetTileCoordinates(tileIndex).ToPosition();

		public int ToIndex(GridCoord coord) => coord.Z * Width + coord.X;

		public GridCoord FromIndex(int index) => new GridCoord(index % Width, index / Width);

		public Vector3 ScreenToWorld(Vector3 screenPos)
		{
			Ray ray = Camera.main.ScreenPointToRay(screenPos);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return Vector3.zero;
			return ray.GetPoint(distance);
		}

		public Vector3 GetCentre()
		{
			if (Width == 0 || Height == 0)
				return Vector3.zero;
			return new Vector3(Width / 2f, 0.5f, Height / 2f); // Approximate map center
		}

		public int GetAdjacentTile(int tileIndex, int dirBit)
		{
			var (dx, dz) = TileProperties.GetDirectionOffset(dirBit);
			var newCoord = GetTileCoordinates(tileIndex).Add(dx, dz);
			if (newCoord.X < 0 || newCoord.X >= Width || newCoord.Z < 0 || newCoord.Z >= Height) return -1;
			return ToIndex(newCoord);
		}

		public int SearchDirectionForLastType(int index, int dirBit, TileProperties.TileFlags flags)
		{
			var nextIndex = GetAdjacentTile(index, dirBit);
			while (-1 != nextIndex)
			{
				var props = GetTilePropertiesAt(nextIndex);
				if (props == null || (props.Flags & flags) != flags) break;
				index = nextIndex;
				nextIndex = GetAdjacentTile(index, dirBit);
			}
			return index;
		}

		public GridCoord GetTileCoordinatesForLastType(int index, int dirBit, TileProperties.TileFlags flags) => GetTileCoordinates(SearchDirectionForLastType(index, dirBit, flags));

		public void Reset()
		{
			if (mapRoot != null) Destroy(mapRoot);
			if (spareTile != null) Destroy(spareTile);
			mapRoot = null;
			spareTile = null;
			currentMap = null;
			waypoints = null;
			tiles = null;
		}

		private TileProperties CreateTileProperties(string szType, string szTheme)
		{
			var tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
			return tileDef != null ? new TileProperties(tileDef) : null;
		}

		public void Initialize(string mapName)
		{
			Reset();
			currentMap = string.IsNullOrEmpty(mapName) ? DatabaseLoader.instance.Maps.FirstOrDefault() : DatabaseLoader.instance.Maps.FirstOrDefault(m => m.name == mapName);

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
				waypoints = SetupWaypoints();

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
				waypoints = SetupWaypoints();

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

			for (var index = 0; index < tileMap.Length; index++)
			{
				var tileDefIndex = tileMap[index];
				if (tileDefIndex < 0 || tileDefIndex >= currentMap.defs.Length)
					Debug.LogWarning($"Invalid tileDefIndex={tileDefIndex}");

				var szTheme = currentMap.defs[tileDefIndex].szTheme;
				if (string.IsNullOrEmpty(szTheme))
					Debug.LogWarning($"Null szTheme at tileDefIndex {tileDefIndex}");

				var szType = currentMap.defs[tileDefIndex].szType;
				if (szType == "tile_empty")
					continue;

				this.tiles[index].Properties = CreateTileProperties(szType, szTheme);
				if (this.tiles[index].Properties == null)
					continue;

				var coord = GetTileCoordinates(index);
				var tileObj = new GameObject($"Tile_{index}_{coord.X}_{coord.Z}");
				this.tiles[index].GameObject = tileObj;
				tileObj.transform.SetParent(mapRoot.transform, false);
				tileObj.transform.position = coord.ToPosition();

				if (szType == "tile_invisible")
				{
					if (PreviewSettings.ShowHiddenTiles)
					{
						var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
						cube.transform.SetParent(tileObj.transform, false);
						cube.transform.localPosition = new Vector3(0f, -0.1f, 0f);
						cube.transform.localScale = new Vector3(1f, 0.1f, 1f);
						cube.name = "debug tile";
						var meshRenderer = cube.GetComponentInChildren<MeshRenderer>();
						if (meshRenderer != null) meshRenderer.material = new Material(meshRenderer.material) { color = Color.white * 0.2f };
					}
					continue;
				}

				if (PreviewSettings.FlipGeometry)
					tileObj.transform.rotation = Quaternion.AngleAxis(180, Vector3.up);

				if (this.tiles[index].Properties.IsSlide)
				{
					var collider = tileObj.AddComponent<BoxCollider>();
					collider.size = new Vector3(1f, 0.3f, 1f);
					collider.center = new Vector3(0f, -0.05f, 0f);
				}

				var geomPath = $"{PreviewSettings.GeometryPath}{this.tiles[index].Properties.Geom}".Replace(".x", "");
				var geomAsset = Resources.Load<GameObject>(geomPath);
				if (geomAsset != null)
				{
					var geomInstance = Instantiate(geomAsset, tileObj.transform);
					geomInstance.transform.localPosition = Vector3.zero;
					geomInstance.name = this.tiles[index].Properties.Geom;

					var textureSet = TileAnimator.GetTextureSetForTileDef(this.tiles[index].Properties.tileDef);
					if (textureSet?.frames?.Length > 0)
					{
						var animator = tileObj.AddComponent<TileAnimator>();
						animator.Initialize(textureSet);
					}
					else
					{
						Debug.LogWarning($"No texture set for {this.tiles[index].Properties.Type}, theme={this.tiles[index].Properties.Theme}");
					}
				}
				else
				{
					Debug.LogWarning($"Geometry not found: {geomPath}");
					var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
					cube.transform.SetParent(tileObj.transform, false);
					cube.transform.localPosition = Vector3.zero;
					cube.transform.localScale = Vector3.one * 0.1f;
					cube.name = "Fallback_Cube";
				}
			}
			SetCameraPosition();
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
			for (var index = 0; index < tiles.Length; index++)
			{
				var scrambledIndex = index + offsets[index];
				if (scrambledIndex >= 0 && scrambledIndex < tiles.Length)
					scrambledTiles[index] = tiles[scrambledIndex];
			}

			tiles = scrambledTiles;
			UpdateTileObjectNamesAndPositions();
		}

		private void UpdateTileObjectNamesAndPositions()
		{
			for (var index = 0; index < tiles.Length; index++)
			{
				if (null == tiles[index].GameObject) continue;
				var coord = GetTileCoordinates(index);
				tiles[index].GameObject.name = $"{tiles[index].Properties?.Type ?? "Empty"}_{coord.X}_{coord.Z}";
				tiles[index].GameObject.transform.position = coord.ToPosition();
			}
		}

		private void SetCameraPosition()
		{
			var mapMin = Vector3.one * 1000f;
			var mapMax = Vector3.zero;
			var activeTileCount = 0;
			for (var index = 0; index < tiles.Length; index++)
			{
				if (GetTilePropertiesAt(index) != null)
				{
					var pos = GetTileCoordinates(index).ToPosition();
					mapMin = Vector3.Min(mapMin, pos);
					mapMax = Vector3.Max(mapMax, pos);
					activeTileCount++;
				}
			}
			Camera.main.transform.position = activeTileCount > 0 ? (mapMin + mapMax) * 0.5f + Vector3.up * (mapMax.z - mapMin.z) : Vector3.up * 10f;
			Camera.main.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		}

		public int GetStartTile()
		{
			if (null != Waypoints && 0 != Waypoints.Count)
				return Waypoints[0].nTile;

			for (var i = 0; i < Width * Height; i++)
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
			if (null != Waypoints && 0 != Waypoints.Count)
				return Waypoints[Waypoints.Count - 1].nTile;

			for (var i = 0; i < Width * Height; i++)
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

			var startTile = GetStartTile();
			var endTile = GetEndTile();

			if (startTile == -1 || endTile == -1)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				return generatedWaypoints;
			}

			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });
			var path = FindPath(startTile, endTile);

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

			var currentProps = GetTilePropertiesAt(currentTile);
			if (currentProps == null)
			{
				path.RemoveAt(path.Count - 1);
				return null;
			}

			var tryDirections = GetTryDirections(currentProps.Nav, currentDirBit);
			for (int i = 0; i < tryDirections.Length; i++)
			{
				var dirBit = tryDirections[i];
				var nextTile = GetAdjacentTile(currentTile, dirBit);
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

			//local function
			static int[] GetTryDirections(int nav, int currentDirBit)
			{
				if ((TileProperties.GetOppositeDirection(nav) & nav) == nav)
					return new[] { currentDirBit };
				if (currentDirBit != 0)
					return new[] { nav & ~(currentDirBit | TileProperties.GetOppositeDirection(currentDirBit)) };
				return TileProperties.Directions;
			}
		}

		public bool CheckPathBetweenWaypoints(int currentWaypointIndex, out List<int> path)
		{
			path = null;
			if (Waypoints == null || currentWaypointIndex < 0 || currentWaypointIndex + 1 >= Waypoints.Count)
			{
				//Debug.Log($"No next waypoint (currentIndex={currentWaypointIndex}, waypoints.Count={Waypoints?.Count ?? 0})");
				return false;
			}

			var startTile = Waypoints[currentWaypointIndex].nTile;
			var targetTile = Waypoints[currentWaypointIndex + 1].nTile;
			path = FindPath(startTile, targetTile);
			if (path != null)
			{
				//Debug.Log($"Found path to waypoint {targetTile}: [{FormatPath(path)}]");
				return true;
			}
			return false;
		}

		public int FindAdjacentConsole(int nTile)
		{
			if (!IsValidTileIndex(nTile))
				return -1;

			foreach (var dirBit in TileProperties.Directions)
			{
				var consoleTile = GetAdjacentTile(nTile, dirBit);
				if (consoleTile == -1)
					continue;

				var consoleProps = GetTilePropertiesAt(consoleTile);
				if (consoleProps?.IsConsole != true)
					continue;

				var consoleNav = consoleProps.Nav;
				if (consoleNav == 0)
					continue;

				var navTile = GetAdjacentTile(consoleTile, consoleNav);
				if (navTile == nTile)
					return consoleTile;
			}

			return -1;
		}

		public bool RollStrip(TileStrip strip)
		{
			if (false == strip.LastIsDockOrRoll) return false;

			var tileData = new TileData[strip.TileIndices.Count];
			var positions = new Vector3[strip.TileIndices.Count];
			for (int i = 0; i < strip.TileIndices.Count; i++)
			{
				int index = strip.TileIndices[i];
				tileData[i] = Tiles[index];
				positions[i] = GetTilePosition(index);
			}

			for (int i = 0; i < strip.TileIndices.Count; i++)
			{
				int currentIndex = strip.TileIndices[i];
				if (i == 0)
				{
					Tiles[currentIndex] = tileData[strip.TileIndices.Count - 1];
					Tiles[currentIndex].GameObject.transform.position = positions[0];
				}
				else
				{
					Tiles[currentIndex] = tileData[i - 1];
					Tiles[currentIndex].GameObject.transform.position = positions[i];
				}

				var coord = GetTileCoordinates(currentIndex);
				Tiles[currentIndex].GameObject.name = $"{Tiles[currentIndex].Properties?.Type ?? "Empty"}_{coord.X}_{coord.Z}";
			}

			return true;
		}

		public struct TileStrip
		{
			public int FirstIndex;
			public int LastIndex;
			public bool LastIsDockOrRoll;
			public int Stride;

			public readonly int Count => (Stride == 0) ? 1 : Mathf.Abs((LastIndex - FirstIndex) / Stride) + 1;
			public readonly int First => FirstIndex;
			public readonly int Last => LastIndex;

			public readonly List<int> TileIndices
			{
				get
				{
					var indices = new List<int>();
					if (Stride == 0)
					{
						indices.Add(FirstIndex);
						return indices;
					}

					var currentIndex = FirstIndex;
					indices.Add(currentIndex);

					// Increment by Stride until LastIndex is reached
					while (currentIndex != LastIndex)
					{
						currentIndex += Stride;
						indices.Add(currentIndex);
					}

					return indices;
				}
			}
		}

		public TileStrip GetTileStrip(int startTileIndex, int dragDirectionBit)
		{
			var strip = new TileStrip
			{
				FirstIndex = startTileIndex,
				LastIndex = startTileIndex,
				LastIsDockOrRoll = false,
				Stride = 0
			};

			if (dragDirectionBit == 0)
				return strip;

			var startProps = GetTilePropertiesAt(startTileIndex);
			if (startProps == null || !startProps.Interactive)
				return strip;

			// Compute stride based on direction
			int stride = 0;
			var (dx, dz) = TileProperties.GetDirectionOffset(dragDirectionBit);
			if (dx != 0) stride = dx; // Horizontal: +1 (right) or -1 (left)
			else if (dz != 0) stride = dz * Width; // Vertical: +Width (down) or -Width (up)

			// Walk backward (opposite direction) to find FirstIndex
			int currentIndex = startTileIndex;
			var reverseDirection = TileProperties.GetOppositeDirection(dragDirectionBit);
			while (true)
			{
				var nextIndex = GetAdjacentTile(currentIndex, reverseDirection);
				var nextProps = GetTilePropertiesAt(nextIndex);
				if (nextProps == null || (!nextProps.IsDock && !nextProps.IsRoll)) break;
				currentIndex = nextIndex;
				strip.FirstIndex = currentIndex; // Update FirstIndex to the earliest index
			}

			// Walk forward to find LastIndex (interactive tiles)
			currentIndex = startTileIndex;
			while (true)
			{
				var nextIndex = GetAdjacentTile(currentIndex, dragDirectionBit);
				var nextProps = GetTilePropertiesAt(nextIndex);
				if (nextProps == null || !nextProps.Interactive) break;
				currentIndex = nextIndex;
				strip.LastIndex = currentIndex; // Update LastIndex
			}

			// Continue forward for roll or dock tiles
			while (true)
			{
				var nextIndex = GetAdjacentTile(currentIndex, dragDirectionBit);
				var nextProps = GetTilePropertiesAt(nextIndex);
				if (nextProps == null || (!nextProps.IsDock && !nextProps.IsRoll)) break;
				currentIndex = nextIndex;
				strip.LastIndex = currentIndex; // Update LastIndex
			}

			var finalProps = GetTilePropertiesAt(currentIndex);
			if (finalProps != null && (finalProps.IsDock || finalProps.IsRoll))
			{
				strip.LastIsDockOrRoll = true;
			}

			strip.Stride = stride;
			return strip;
		}

		public class OriginalMaterialHolder : MonoBehaviour { public Material originalMaterial; }

		public void HighlightStrip(TileStrip strip, bool highlight)
		{
			if (!PreviewSettings.ShowTileSelection) return;
			if (null == strip.TileIndices) return;
			foreach (var tileIndex in strip.TileIndices)
			{
				var tile = Tiles[tileIndex].GameObject;
				if (tile == null) continue;
				var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
				if (meshRenderer == null) continue;

				if (highlight)
				{
					if (!tile.TryGetComponent<OriginalMaterialHolder>(out var holder))
					{
						holder = tile.AddComponent<OriginalMaterialHolder>();
						holder.originalMaterial = meshRenderer.material;
					}
					meshRenderer.material = new Material(meshRenderer.material) { color = Color.red };
				}
				else
				{
					if (tile.TryGetComponent<OriginalMaterialHolder>(out var holder) && holder.originalMaterial != null)
					{
						meshRenderer.material = holder.originalMaterial;
					}
					else
					{
						var originalMaterial = GetTileMeshMaterial(tileIndex);
						if (originalMaterial != null)
							meshRenderer.material = originalMaterial;
					}
				}
			}
		}

		public Material GetTileMeshMaterial(int tileIndex)
		{
			if (tileIndex < 0 || tileIndex >= tiles.Length)
				return null;

			var meshRenderer = tiles[tileIndex].GameObject.GetComponentInChildren<MeshRenderer>();
			if (meshRenderer == null)
			{
				Debug.LogWarning($"No MeshRenderer found for tile at index {tileIndex}.");
				return null;
			}

			return meshRenderer.sharedMaterial;
		}

		public string FormatPath(List<int> path) => string.Join(" -> ", path.Select(t => GetTileCoordinates(t).ToString()));

		public void UpdateSpareTile(TileStrip strip, Vector3 delta, bool active)
		{
			if (!active)
			{
				if (spareTile != null)
					spareTile.SetActive(false);
				return;
			}

			if (false == strip.LastIsDockOrRoll)
				return;

			// Identify the leading tile (always the last tile in the strip, which wraps to the front in a roll)
			int leadingTileIndex = strip.TileIndices.Last(); // Always clone the last tile (e.g., S in [T,U,V,W,S])
			var leadingTile = Tiles[leadingTileIndex].GameObject;

			// Identify the trailing position (position of the tile adjacent to the strip in the opposite drag direction)
			var trailingTileIndex = strip.TileIndices.First() - strip.Stride;
			var trailingPosition = GetTilePosition(trailingTileIndex);

			// Initialize spare tile if it doesn't exist
			if (spareTile == null)
			{
				spareTile = new GameObject("SpareTile");
				spareTile.transform.SetParent(mapRoot.transform, false);
				spareTile.AddComponent<MeshFilter>();
				spareTile.AddComponent<MeshRenderer>();
			}

			// Update spare tile's mesh and material to match the leading tile
			var leadingRenderer = leadingTile.GetComponentInChildren<MeshRenderer>();
			var leadingFilter = leadingTile.GetComponentInChildren<MeshFilter>();
			var spareRenderer = spareTile.GetComponent<MeshRenderer>();
			var spareFilter = spareTile.GetComponent<MeshFilter>();

			if (leadingRenderer != null && leadingFilter != null && spareRenderer != null && spareFilter != null)
			{
				spareFilter.sharedMesh = leadingFilter.sharedMesh;
				spareRenderer.material = leadingRenderer.material; // Use material to preserve instance properties
				// Match local transform of the leading tile's mesh (if it's a child object)
				spareRenderer.transform.localPosition = leadingRenderer.transform.localPosition;
				spareRenderer.transform.rotation = leadingRenderer.transform.rotation;
				spareRenderer.transform.localScale = leadingRenderer.transform.localScale;
			}
			else
			{
				//Debug.LogWarning($"Failed to update spare tile: leading tile (index {leadingTileIndex}) missing MeshRenderer or MeshFilter.");
				spareTile.SetActive(false);
				return;
			}

			// Ensure no interactivity components
			foreach (var collider in spareTile.GetComponentsInChildren<Collider>())
				Destroy(collider);
			foreach (var animator in spareTile.GetComponentsInChildren<TileAnimator>())
				Destroy(animator);

			// Position the spare tile at the trailing edge, moving with the strip
			spareTile.transform.position += trailingPosition + delta;
			spareTile.SetActive(true);
		}
	}
}